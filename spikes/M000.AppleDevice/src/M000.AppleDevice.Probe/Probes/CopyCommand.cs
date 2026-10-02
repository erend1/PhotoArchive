using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using PhotoArchive.Spikes.AppleDevice.Analysis;
using PhotoArchive.Spikes.AppleDevice.Cli;
using PhotoArchive.Spikes.AppleDevice.Output;
using PhotoArchive.Spikes.AppleDevice.Wpd;
using Windows.Win32;

namespace PhotoArchive.Spikes.AppleDevice.Probes;

/// <summary>
/// Reads original resource bytes (WPD_RESOURCE_DEFAULT) through a staging file, then verifies before promoting:
/// byte count vs. size reported before transfer, SHA-256, container signature vs. file name, optional re-read.
/// Demonstrates the read side of ADR-0005 (transactional import); it is not the production import pipeline.
/// </summary>
internal static partial class CopyCommand
{
    private const long RereadLimitBytes = 64L * 1024 * 1024;
    private const int ScanWindowBytes = 8 * 1024 * 1024;

    internal sealed record Options(int Count, IReadOnlyList<string> ObjectIds, bool Reread, bool Keep, long MaxTotalBytes, int Newest = 0)
    {
        internal static Options From(CommandLine cli) => new(
            Count: Math.Clamp(cli.GetInt("count", 8), 1, 200),
            ObjectIds: cli.Get("object") is { Length: > 0 } id ? [id] : [],
            Reread: !cli.Has("no-reread"),
            Keep: cli.Has("keep"),
            MaxTotalBytes: Math.Max(1, cli.GetInt("max-total-mb", 2048)) * 1024L * 1024L,
            Newest: Math.Clamp(cli.GetInt("newest", 0), 0, 50));
    }

    internal sealed record Outcome(
        EnumeratedObject Object,
        bool Succeeded,
        long BytesRead,
        string? Sha256,
        SniffedFormat Sniffed,
        bool? ExtensionMatches,
        bool? SizeMatches,
        double MegabytesPerSecond,
        bool? RereadMatches,
        EmbeddedMetadata? Metadata,
        string? AdjustmentSummary,
        string? Error,
        string? HeaderHex = null,
        string? RereadHeaderHex = null);

    [GeneratedRegex(@"<key>([^<]{1,64})</key>", RegexOptions.CultureInvariant)]
    private static partial Regex PlistKey();

    [GeneratedRegex(@"<key>adjustmentFormatIdentifier</key>\s*<string>([A-Za-z0-9.\-]{1,80})</string>", RegexOptions.CultureInvariant)]
    private static partial Regex AdjustmentFormat();

    internal static List<Outcome> Run(RunContext ctx, WpdDevice device, IReadOnlyList<EnumeratedObject> objects, Options options)
    {
        DeviceSelection.EnsureOutsideGitWorkingTree(ctx);
        var section = ctx.Report.Section("Original bytes (WPD_RESOURCE_DEFAULT) through staging + verification");
        section.Fact("API", "IPortableDeviceResources::GetStream(WPD_RESOURCE_DEFAULT, STGM_READ) -> IStream::Read")
            .Fact("Transaction model", "stream to staging/*.partial -> verify -> File.Move to verified/ (never promoted on failure)")
            .Fact("Copies retained after run", options.Keep ? "yes (--keep; they are personal media, do not commit)" : "no (deleted after verification)");

        var targets = SelectTargets(objects, options);
        section.Fact("Selection", options.ObjectIds.Count > 0 ? "explicit --object"
            : options.Newest > 0 ? $"--newest {options.Newest} (most recent DATE_CREATED)"
            : $"representative kinds, filled up to --count {options.Count}");
        using var hashLog = new StreamWriter(Path.Combine(ctx.RawDirectory, "copies.jsonl"), append: true);

        var staging = Path.Combine(ctx.RawDirectory, "staging");
        var verified = Path.Combine(ctx.RawDirectory, "verified");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(verified);
        var outcomes = new List<Outcome>();
        long total = 0;
        var index = 0;
        foreach (var obj in targets)
        {
            if (total + (long)Math.Min(obj.Size ?? 0, int.MaxValue) > options.MaxTotalBytes)
            {
                ctx.Warn($"Skipping {ctx.Sanitizer.FileName(obj.FileName)}: would exceed --max-total-mb.");
                continue;
            }

            index++;
            var outcome = CopyOne(ctx, device, obj, index, staging, verified, options);
            total += outcome.BytesRead;
            outcomes.Add(outcome);
            hashLog.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
            {
                CapturedAtUtc = DateTime.UtcNow,
                obj.ObjectId,
                obj.PersistentUniqueId,
                obj.FileName,
                obj.DateCreated,
                ReportedSize = obj.Size,
                outcome.BytesRead,
                outcome.Sha256,
                Sniffed = outcome.Sniffed.ToString(),
                HeaderHex = outcome.HeaderHex,
                outcome.RereadMatches,
                outcome.Error,
            }));
            var label = ctx.Sanitizer.FileName(obj.FileName);
            if (outcome.Succeeded)
            {
                ctx.Good($"{label}: {outcome.BytesRead} bytes, {outcome.Sniffed}, size match={Fmt(outcome.SizeMatches)}, ext match={Fmt(outcome.ExtensionMatches)}, reread={Fmt(outcome.RereadMatches)}");
            }
            else
            {
                ctx.Error($"{label}: {outcome.Error}");
            }
        }

        var leftovers = Directory.EnumerateFiles(staging, "*.partial").Count();
        section.Fact("Objects attempted", outcomes.Count)
            .Fact("Succeeded", outcomes.Count(o => o.Succeeded))
            .Fact("Byte count == size reported before transfer", outcomes.Count(o => o.SizeMatches == true))
            .Fact("Byte count != size reported before transfer", outcomes.Count(o => o.SizeMatches == false))
            .Fact("Content signature matches file extension", outcomes.Count(o => o.ExtensionMatches == true))
            .Fact("Content signature CONTRADICTS file extension (possible on-the-fly conversion)", outcomes.Count(o => o.ExtensionMatches == false))
            .Fact("Re-read produced identical SHA-256", outcomes.Count(o => o.RereadMatches == true))
            .Fact("Re-read produced DIFFERENT SHA-256", outcomes.Count(o => o.RereadMatches == false))
            .Fact("Partial staging files left behind", leftovers)
            .Fact("Total bytes read", total);

        section.Table(
            ["#", "Object (sanitized)", "Ext", "Reported size", "Bytes read", "Signature", "Ext match", "Size match", "Re-read", "MB/s", "EXIF / GPS / DTO / Apple MakerNote", "QT location / content-id key", "Error"],
            outcomes.Select((o, i) => (IReadOnlyList<string>)[
                (i + 1).ToString(CultureInfo.InvariantCulture),
                ctx.Sanitizer.FileName(o.Object.FileName),
                o.Object.Extension,
                o.Object.SizeIsPtp32BitSentinel ? "0xFFFFFFFF (sentinel)" : o.Object.Size?.ToString(CultureInfo.InvariantCulture) ?? "n/a",
                o.BytesRead.ToString(CultureInfo.InvariantCulture),
                o.Sniffed.ToString(),
                Fmt(o.ExtensionMatches),
                Fmt(o.SizeMatches),
                Fmt(o.RereadMatches),
                o.MegabytesPerSecond.ToString("F1", CultureInfo.InvariantCulture),
                o.Metadata is null ? "" : $"{Fmt(o.Metadata.ExifFound)} / {Fmt(o.Metadata.GpsLatitudePresent)} / {Fmt(o.Metadata.DateTimeOriginalPresent)} / {Fmt(o.Metadata.AppleMakerNotePresent)}",
                o.Metadata is null ? "" : $"{Fmt(o.Metadata.QuickTimeLocationPresent)} / {Fmt(o.Metadata.ContentIdentifierKeyPresent)}",
                o.Error ?? o.AdjustmentSummary ?? (o.BytesRead is > 0 and <= 64 ? $"tiny payload hex {o.HeaderHex}; re-read {o.RereadHeaderHex}" : ""),
            ]));

        DescribeRelationships(ctx, section, outcomes);

        if (!options.Keep)
        {
            foreach (var file in Directory.EnumerateFiles(verified))
            {
                File.Delete(file);
            }
        }

        return outcomes;
    }

    private static List<EnumeratedObject> SelectTargets(IReadOnlyList<EnumeratedObject> objects, Options options)
    {
        if (options.ObjectIds.Count > 0)
        {
            return objects.Where(o => options.ObjectIds.Contains(o.ObjectId)).ToList();
        }

        var media = objects.Where(o => o.IsMediaLike && o.PropertyError is null).ToList();
        if (options.Newest > 0)
        {
            return media.Where(o => o.DateCreated is not null)
                .OrderByDescending(o => o.DateCreated)
                .ThenBy(o => o.FileName, StringComparer.Ordinal)
                .Take(options.Newest)
                .ToList();
        }

        var picked = SampleSelector.Representative(objects, options.Count, includeGroups: true);
        picked.AddRange(media.Where(o => !picked.Contains(o)).OrderBy(o => o.Size ?? ulong.MaxValue).Take(Math.Max(0, options.Count - picked.Count)));
        return picked;
    }

    private static Outcome CopyOne(RunContext ctx, WpdDevice device, EnumeratedObject obj, int index, string staging, string verified, Options options)
    {
        var partial = Path.Combine(staging, $"{index:D3}.partial");
        var sw = Stopwatch.StartNew();
        try
        {
            ResourceReadResult read;
            using (var file = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024))
            {
                read = device.ReadResource(obj.ObjectId, PInvoke.WPD_RESOURCE_DEFAULT, file, CancellationToken.None);
                file.Flush(flushToDisk: true);
            }

            sw.Stop();
            var sniffed = MediaSignature.Sniff(read.Header);
            bool? sizeMatches = obj.Size is null || obj.SizeIsPtp32BitSentinel ? null : (ulong)read.BytesRead == obj.Size;
            var metadata = ScanEmbedded(partial);
            var adjustment = obj.Extension == "AAE" ? SummarizeAdjustment(partial) : null;

            bool? reread = null;
            string? rereadHeader = null;
            if (options.Reread && read.BytesRead <= RereadLimitBytes)
            {
                using var sink = Stream.Null;
                var second = device.ReadResource(obj.ObjectId, PInvoke.WPD_RESOURCE_DEFAULT, sink, CancellationToken.None);
                reread = second.Sha256 == read.Sha256;
                rereadHeader = Convert.ToHexString(second.Header.AsSpan(0, Math.Min(second.Header.Length, 16)));
            }

            var final = Path.Combine(verified, $"{index:D3}.{(obj.Extension.Length > 0 ? obj.Extension.ToLowerInvariant() : "bin")}");
            File.Move(partial, final);
            return new Outcome(obj, true, read.BytesRead, read.Sha256, sniffed, MediaSignature.MatchesExtension(obj.Extension, sniffed), sizeMatches,
                read.BytesRead / 1048576.0 / Math.Max(sw.Elapsed.TotalSeconds, 0.001), reread, metadata, adjustment, null,
                Convert.ToHexString(read.Header.AsSpan(0, Math.Min(read.Header.Length, 16))), rereadHeader);
        }
        catch (Exception ex)
        {
            // A failed transfer must never look like a completed one: remove the partial file and record the error.
            TryDelete(partial);
            return new Outcome(obj, false, 0, null, SniffedFormat.Unknown, null, null, 0, null, null, null, WpdInterop.Describe(ex));
        }
    }

    private static EmbeddedMetadata ScanEmbedded(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length <= ScanWindowBytes * 2L)
        {
            var all = new byte[stream.Length];
            stream.ReadExactly(all);
            return EmbeddedMetadataScanner.Scan(all);
        }

        // Large videos: metadata lives in the moov atom at the start or the end.
        var window = new byte[ScanWindowBytes * 2];
        stream.ReadExactly(window, 0, ScanWindowBytes);
        stream.Seek(-ScanWindowBytes, SeekOrigin.End);
        stream.ReadExactly(window, ScanWindowBytes, ScanWindowBytes);
        return EmbeddedMetadataScanner.Scan(window);
    }

    private static string SummarizeAdjustment(string path)
    {
        var text = File.ReadAllText(path);
        var keys = PlistKey().Matches(text).Select(m => m.Groups[1].Value).Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal);
        var format = AdjustmentFormat().Match(text);
        return $"AAE plist keys: {string.Join(",", keys)}" + (format.Success ? $"; adjustmentFormatIdentifier={format.Groups[1].Value}" : string.Empty);
    }

    private static void DescribeRelationships(RunContext ctx, ReportSection section, List<Outcome> outcomes)
    {
        var groups = outcomes.Where(o => o.Succeeded)
            .GroupBy(o => (o.Object.ParentId, Key: DcimNameAnalyzer.Parse(o.Object.FileName) is { } p ? p.Stem.ToUpperInvariant() + p.Number : null))
            .Where(g => g.Key.Key is not null && g.Count() > 1);
        foreach (var group in groups)
        {
            var still = group.FirstOrDefault(o => DcimNameAnalyzer.Parse(o.Object.FileName) is { Marker: "", Kind: DcimResourceKind.Still });
            var video = group.FirstOrDefault(o => DcimNameAnalyzer.Parse(o.Object.FileName) is { Marker: "", Kind: DcimResourceKind.Video });
            var names = string.Join(" + ", group.Select(o => ctx.Sanitizer.FileName(o.Object.FileName)));
            if (still?.Metadata is not null && video?.Metadata is not null)
            {
                var shared = still.Metadata.UuidTokens.Intersect(video.Metadata.UuidTokens, StringComparer.OrdinalIgnoreCase).Any();
                section.Line($"- Live Photo candidate `{names}`: motion file has `com.apple.quicktime.content.identifier` key = {Fmt(video.Metadata.ContentIdentifierKeyPresent)}; " +
                             $"still has Apple MakerNote = {Fmt(still.Metadata.AppleMakerNotePresent)}; **still and motion bytes share a UUID token = {Fmt(shared)}** " +
                             "(byte-level pairing evidence; values not shown).");
            }
            else
            {
                section.Line($"- Related-by-name group `{names}` (filename heuristic only).");
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Reported via "Partial staging files left behind".
        }
    }

    private static string Fmt(bool? value) => value switch { true => "yes", false => "NO", null => "n/a" };
}
