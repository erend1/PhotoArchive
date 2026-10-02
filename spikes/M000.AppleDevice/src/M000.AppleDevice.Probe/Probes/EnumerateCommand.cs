using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using PhotoArchive.Spikes.AppleDevice.Analysis;
using PhotoArchive.Spikes.AppleDevice.Cli;
using PhotoArchive.Spikes.AppleDevice.Output;
using PhotoArchive.Spikes.AppleDevice.Wpd;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace PhotoArchive.Spikes.AppleDevice.Probes;

/// <summary>
/// Progressive, cancellable enumeration of everything the device exposes through WPD, with per-object
/// property capture. Answers: what metadata is available without reading media bytes, how fast, how
/// identifiers look, and what DCIM naming patterns (Live Photo / edit candidates) are exposed.
/// </summary>
internal static class EnumerateCommand
{
    internal sealed record Options(int MaxObjects, uint BatchSize, int CancelAfter, int MaxDepth, bool IdsOnly)
    {
        internal static Options From(CommandLine cli) => new(
            MaxObjects: cli.GetInt("max", 0),
            BatchSize: (uint)Math.Clamp(cli.GetInt("batch", 64), 1, 1024),
            CancelAfter: cli.GetInt("cancel-after", 0),
            MaxDepth: Math.Clamp(cli.GetInt("max-depth", 8), 1, 32),
            IdsOnly: cli.Has("ids-only"));
    }

    internal static List<EnumeratedObject> Run(RunContext ctx, WpdDevice device, Options options)
    {
        var section = ctx.Report.Section(options.IdsOnly ? "Enumeration (WPD, names/types only)" : "Enumeration (WPD, all properties per object)");
        section.Fact("API", options.IdsOnly
            ? "IPortableDeviceContent::EnumObjects + IEnumPortableDeviceObjectIDs::Next(batch) + IPortableDeviceProperties::GetValues(name, content type)"
            : "IPortableDeviceContent::EnumObjects + IEnumPortableDeviceObjectIDs::Next(batch) + IPortableDeviceProperties::GetValues(NULL = all)");
        section.Fact("Batch size", options.BatchSize).Fact("Max objects (0 = all)", options.MaxObjects).Fact("Max depth", options.MaxDepth);

        using var cts = new CancellationTokenSource();
        var objects = new List<EnumeratedObject>();
        var errors = new Dictionary<string, int>(StringComparer.Ordinal);
        TimeSpan? firstObject = null;
        TimeSpan? firstMedia = null;
        TimeSpan? cancelRequestedAt = null;
        var cancelled = false;
        var stopwatch = Stopwatch.StartNew();
        Console.CancelKeyPress += OnCancel;
        StreamWriter? raw = ctx.WriteRaw ? new StreamWriter(Path.Combine(ctx.RawDirectory, "objects.jsonl")) : null;

        try
        {
            foreach (var obj in Walk(device, options, cts.Token, errors))
            {
                objects.Add(obj);
                firstObject ??= stopwatch.Elapsed;
                if (obj.IsMediaLike)
                {
                    firstMedia ??= stopwatch.Elapsed;
                }

                raw?.WriteLine(JsonSerializer.Serialize(new
                {
                    obj.ObjectId,
                    obj.ParentId,
                    obj.Depth,
                    Properties = obj.Properties.ToDictionary(p => WpdNames.Key(p.Key), p => ReportDocument.Format(p.Value is Guid g ? WpdNames.Guid(g) : p.Value)),
                }));

                if (objects.Count % 500 == 0)
                {
                    ctx.Info($"  ... {objects.Count} objects in {stopwatch.Elapsed.TotalSeconds:F1}s");
                }

                if (options.CancelAfter > 0 && objects.Count == options.CancelAfter && !cts.IsCancellationRequested)
                {
                    cancelRequestedAt = stopwatch.Elapsed;
                    cts.Cancel();
                }

                if (options.MaxObjects > 0 && objects.Count >= options.MaxObjects)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Typical cause: device unplugged or locked mid-enumeration. This is evidence (M000 capability 18), not a crash.
            section.Fact("Enumeration aborted by device error", WpdInterop.Describe(ex))
                .Fact("Objects received before abort", objects.Count);
            ctx.Error($"Enumeration aborted after {objects.Count} objects: {WpdInterop.Describe(ex)}");
        }
        finally
        {
            Console.CancelKeyPress -= OnCancel;
            raw?.Dispose();
        }

        stopwatch.Stop();
        var media = objects.Where(o => o.IsMediaLike).ToList();
        section.Fact("Objects enumerated", objects.Count)
            .Fact("Containers (functional objects/folders)", objects.Count(o => o.IsContainer))
            .Fact("Media-like objects", media.Count)
            .Fact("Time to first object (ms)", firstObject?.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture))
            .Fact("Time to first media object (ms)", firstMedia?.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture))
            .Fact("Total enumeration time (s)", stopwatch.Elapsed.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture))
            .Fact("Objects per second", objects.Count == 0 ? 0 : Math.Round(objects.Count / stopwatch.Elapsed.TotalSeconds, 1))
            .Fact("Enumeration was cancelled", cancelled)
            .Fact("Cancellation latency (ms)", cancelRequestedAt is null ? null
                : (stopwatch.Elapsed - cancelRequestedAt.Value).TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture))
            .Fact("Property read errors", errors.Values.Sum());

        foreach (var (error, count) in errors)
        {
            section.Line($"- Property read error `{error}` x{count}");
        }

        if (!options.IdsOnly)
        {
            DescribeMetadata(ctx, section, objects, media);
        }

        DescribeNaming(ctx, section, objects, media);
        ctx.Good($"Enumerated {objects.Count} objects ({media.Count} media-like) in {stopwatch.Elapsed.TotalSeconds:F1}s{(cancelled ? " (cancelled)" : string.Empty)}.");
        return objects;

        void OnCancel(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            cancelRequestedAt ??= stopwatch.Elapsed;
            cts.Cancel();
        }
    }

    private static IEnumerable<EnumeratedObject> Walk(WpdDevice device, Options options, CancellationToken token, Dictionary<string, int> errors)
    {
        var stack = new Stack<(string Id, string? Name, int Depth)>();
        stack.Push((PInvoke.WPD_DEVICE_OBJECT_ID, null, 0));
        while (stack.Count > 0)
        {
            var (parentId, parentName, depth) = stack.Pop();
            var children = new List<EnumeratedObject>();
            foreach (var childId in device.EnumerateChildren(parentId, options.BatchSize, token))
            {
                var obj = ReadObject(device, childId, parentId, parentName, depth + 1, options.IdsOnly, errors);
                if (obj.IsContainer && depth + 1 < options.MaxDepth)
                {
                    children.Add(obj);
                }

                yield return obj;
            }

            // Push in reverse so folders are visited in device order (depth-first).
            for (var i = children.Count - 1; i >= 0; i--)
            {
                stack.Push((children[i].ObjectId, children[i].FileName, children[i].Depth));
            }
        }
    }

    private static EnumeratedObject ReadObject(WpdDevice device, string id, string parentId, string? parentName, int depth, bool idsOnly, Dictionary<string, int> errors)
    {
        try
        {
            var properties = idsOnly
                ? WpdInterop.ReadAll(device.GetValues(id, PInvoke.WPD_OBJECT_NAME, PInvoke.WPD_OBJECT_CONTENT_TYPE, PInvoke.WPD_OBJECT_ORIGINAL_FILE_NAME))
                : device.GetAllProperties(id);
            return new EnumeratedObject { ObjectId = id, ParentId = parentId, ParentName = parentName, Depth = depth, Properties = properties };
        }
        catch (Exception ex)
        {
            var key = WpdHResult.Describe(ex.HResult);
            errors[key] = errors.GetValueOrDefault(key) + 1;
            return new EnumeratedObject { ObjectId = id, ParentId = parentId, ParentName = parentName, Depth = depth, PropertyError = key };
        }
    }

    private static void DescribeMetadata(RunContext ctx, ReportSection section, List<EnumeratedObject> all, List<EnumeratedObject> media)
    {
        var images = all.Where(o => o.IsImage).ToList();
        var videos = all.Where(o => o.IsVideo).ToList();
        var others = all.Where(o => !o.IsContainer && !o.IsImage && !o.IsVideo).ToList();

        section.Fact("Content type = WPD_CONTENT_TYPE_IMAGE", images.Count)
            .Fact("Content type = WPD_CONTENT_TYPE_VIDEO", videos.Count)
            .Fact("Other non-container objects", others.Count)
            .Fact("Objects with WPD_OBJECT_SIZE = 0xFFFFFFFF (PTP 32-bit sentinel)", all.Count(o => o.SizeIsPtp32BitSentinel))
            .Fact("Media objects with size reported", media.Count(o => o.Size is not null))
            .Fact("Media objects with DATE_CREATED", media.Count(o => o.DateCreated is not null))
            .Fact("Media objects with DATE_MODIFIED", media.Count(o => o.DateModified is not null))
            .Fact("Media objects where DATE_CREATED == DATE_MODIFIED", media.Count(o => o.DateCreated is not null && o.DateCreated == o.DateModified))
            .Fact("Media objects with DATE_CREATED having sub-second precision", media.Count(o => o.DateCreated is { } d && d.Millisecond != 0))
            .Fact("Images with WPD_MEDIA_WIDTH/HEIGHT", images.Count(o => o.Width is > 0 && o.Height is > 0))
            .Fact("Videos with WPD_MEDIA_WIDTH/HEIGHT", videos.Count(o => o.Width is > 0 && o.Height is > 0))
            .Fact("Videos with WPD_MEDIA_DURATION", videos.Count(o => o.Duration is > 0))
            .Fact("Objects with PERSISTENT_UNIQUE_ID", all.Count(o => !string.IsNullOrEmpty(o.PersistentUniqueId)))
            .Fact("Objects where PUID == OBJECT_ID", all.Count(o => o.PersistentUniqueId == o.ObjectId))
            .Fact("Duplicate PUIDs within session", all.Where(o => !string.IsNullOrEmpty(o.PersistentUniqueId)).GroupBy(o => o.PersistentUniqueId).Count(g => g.Count() > 1))
            .Fact("WPD_OBJECT_CAN_DELETE = true (media)", media.Count(o => o.CanDelete == true))
            .Fact("WPD_OBJECT_CAN_DELETE = false (media)", media.Count(o => o.CanDelete == false))
            .Fact("WPD_OBJECT_CAN_DELETE absent (media)", media.Count(o => o.CanDelete is null))
            .Fact("WPD_OBJECT_ISHIDDEN = true", all.Count(o => o.Properties.Any(p => Is(p.Key, PInvoke.WPD_OBJECT_ISHIDDEN) && p.Value is true)));

        var idShapes = all.Take(2000).Select(o => Sanitizer.ShapeOf(o.ObjectId)).GroupBy(s => s).OrderByDescending(g => g.Count()).Take(3);
        var puidShapes = all.Where(o => o.PersistentUniqueId is not null).Take(2000)
            .Select(o => Sanitizer.ShapeOf(o.PersistentUniqueId!)).GroupBy(s => s).OrderByDescending(g => g.Count()).Take(3);
        section.Line("Object ID shapes (9=digit, A/a=letter): " + string.Join(", ", idShapes.Select(g => $"`{g.Key}` x{g.Count()}")));
        section.Line("PUID shapes: " + string.Join(", ", puidShapes.Select(g => $"`{g.Key}` x{g.Count()}")));
        section.Line(string.Empty);

        // Which properties does the driver expose, per content type? (Answers "metadata without reading bytes".)
        var groups = new (string Label, List<EnumeratedObject> Items)[] { ("images", images), ("videos", videos), ("other files", others) };
        var keys = all.SelectMany(o => o.Properties.Select(p => WpdNames.Key(p.Key))).Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToList();
        section.Line("Property presence by content type (objects exposing the property / objects of that type):");
        section.Line(string.Empty);
        section.Table(
            ["Property", .. groups.Select(g => g.Label)],
            keys.Select(k => (IReadOnlyList<string>)[k, .. groups.Select(g => $"{g.Items.Count(o => o.Properties.Any(p => WpdNames.Key(p.Key) == k))}/{g.Items.Count}")]));

        section.Line("Extension x WPD format (x content type) for non-container objects:");
        section.Line(string.Empty);
        section.Table(
            ["Extension", "WPD_OBJECT_FORMAT", "WPD_OBJECT_CONTENT_TYPE", "Count"],
            all.Where(o => !o.IsContainer)
                .GroupBy(o => (o.Extension, Format: WpdNames.Guid(o.Format), Type: WpdNames.Guid(o.ContentType)))
                .OrderByDescending(g => g.Count())
                .Select(g => (IReadOnlyList<string>)[g.Key.Extension, g.Key.Format, g.Key.Type, g.Count().ToString(CultureInfo.InvariantCulture)]));

        var sample = media.FirstOrDefault(o => o.IsImage) ?? media.FirstOrDefault();
        if (sample is not null)
        {
            section.Line("Sanitized property dump of one media object (values that could be personal are redacted):");
            section.Line(string.Empty);
            section.Table(["Property", "Value"], sample.Properties.Select(p => (IReadOnlyList<string>)[WpdNames.Key(p.Key), SanitizedValue(ctx, p.Key, p.Value)]));
        }
    }

    private static void DescribeNaming(RunContext ctx, ReportSection section, List<EnumeratedObject> all, List<EnumeratedObject> media)
    {
        var folders = all.Where(o => o.IsContainer).ToList();
        section.Line("Container names (sanitized) and DCIM sub-folder name shapes (D=digit, A=letter):");
        section.Line(string.Empty);
        section.Table(
            ["Depth", "Container (sanitized)", "Shape", "Children"],
            folders.Take(60).Select(f => (IReadOnlyList<string>)[
                f.Depth.ToString(CultureInfo.InvariantCulture),
                ctx.Sanitizer.FileName(f.FileName),
                DcimNameAnalyzer.Shape(f.FileName),
                all.Count(o => o.ParentId == f.ObjectId).ToString(CultureInfo.InvariantCulture)]));
        if (folders.Count > 60)
        {
            section.Line($"(… {folders.Count - 60} more containers omitted)");
        }

        var grouping = DcimNameAnalyzer.Summarize(media.Select(o => ((string?)o.ParentId, (string?)o.FileName)));
        section.Fact("DCIM naming: files", grouping.TotalFiles)
            .Fact("DCIM naming: unparsed names", grouping.UnparsedFiles)
            .Fact("DCIM naming: (folder, number) groups", grouping.Groups)
            .Fact("Candidate Live Photo groups (still + video, same number) [filename heuristic]", grouping.LivePhotoCandidates)
            .Fact("Groups with an edited render (IMG_E*) [filename heuristic]", grouping.EditedRenderGroups)
            .Fact("Edited-render groups that also expose .AAE", grouping.EditedRenderWithAdjustmentGroups)
            .Fact("Edited-render groups WITHOUT an exposed original (completeness red flag)", grouping.EditedRenderWithoutOriginalGroups)
            .Fact("Groups with .AAE adjustment sidecar", grouping.AdjustmentSidecarGroups)
            .Fact("Orphan .AAE groups (no media exposed)", grouping.OrphanAdjustmentGroups)
            .Fact("Groups with more than one original still (e.g. HEIC+JPG)", grouping.MultipleOriginalStillGroups);
        section.Line("Extension (with marker) counts: " + string.Join(", ", grouping.ExtensionCounts.Select(kv => $"`{kv.Key}` x{kv.Value}")));
        if (grouping.OtherMarkerCounts.Count > 0)
        {
            section.Line("Other name markers: " + string.Join(", ", grouping.OtherMarkerCounts.Select(kv => $"`{kv.Key}` x{kv.Value}")));
        }

        section.Line("Relationships above are inferred from DCIM file-name conventions only. They are candidates, never identity or completeness proof.");
    }

    // String properties that describe the device model/protocol rather than the owner or their content.
    private static readonly HashSet<string> NonPersonalStringKeys = new(StringComparer.Ordinal)
    {
        "WPD_DEVICE_MANUFACTURER",
        "WPD_DEVICE_MODEL",
        "WPD_DEVICE_FIRMWARE_VERSION",
        "WPD_DEVICE_PROTOCOL",
        "WPD_STORAGE_DESCRIPTION",
        "WPD_STORAGE_FILE_SYSTEM_TYPE",
    };

    /// <summary>
    /// Renders a property value for sanitized output. Fail-closed: a string from a key that is not explicitly
    /// classified is redacted (length only), so new or unexpected properties cannot leak personal values.
    /// </summary>
    internal static string SanitizedValue(RunContext ctx, PROPERTYKEY key, object? value)
    {
        var name = WpdNames.Key(key);
        return value switch
        {
            Guid g => WpdNames.Guid(g),
            DateTime d => ctx.Sanitizer.Date(d),
            string s when !ctx.Sanitizer.Enabled => s,
            string s when name is "WPD_OBJECT_NAME" or "WPD_OBJECT_ORIGINAL_FILE_NAME" => ctx.Sanitizer.FileName(s),
            string s when name is "WPD_OBJECT_ID" or "WPD_OBJECT_PERSISTENT_UNIQUE_ID" or "WPD_OBJECT_PARENT_ID" or "WPD_OBJECT_CONTAINER_FUNCTIONAL_OBJECT_ID"
                => ctx.Sanitizer.Opaque(s, name.Replace("WPD_OBJECT_", string.Empty, StringComparison.Ordinal).ToLowerInvariant()),
            string s when name.Contains("PNP_DEVICE_ID", StringComparison.Ordinal) || s.StartsWith(@"\\?\", StringComparison.Ordinal) || s.Contains("#{", StringComparison.Ordinal)
                => ctx.Sanitizer.PnpId(s),
            string s when name.Contains("SERIAL", StringComparison.Ordinal) => ctx.Sanitizer.Serial(s),
            string s when name is "WPD_DEVICE_FRIENDLY_NAME" => ctx.Sanitizer.DeviceName(s),
            string s when NonPersonalStringKeys.Contains(name) => s,
            string s => ctx.Sanitizer.FreeText(s),
            _ => ReportDocument.Format(value),
        };
    }

    private static bool Is(PROPERTYKEY a, PROPERTYKEY b) => a.fmtid == b.fmtid && a.pid == b.pid;
}
