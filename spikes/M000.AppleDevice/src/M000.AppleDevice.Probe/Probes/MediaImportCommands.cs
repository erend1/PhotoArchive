using System.Diagnostics;
using System.Globalization;
using PhotoArchive.Spikes.AppleDevice.Analysis;
using PhotoArchive.Spikes.AppleDevice.Cli;
using PhotoArchive.Spikes.AppleDevice.Output;
using PhotoArchive.Spikes.AppleDevice.Wpd;
using Windows.Media.Import;
using Windows.Storage;

namespace PhotoArchive.Spikes.AppleDevice.Probes;

/// <summary>
/// Probes the documented WinRT photo-import API (Windows.Media.Import), which Windows' own import experiences build on.
/// It adds Windows' grouping of siblings/sidecars and a storage-medium access-mode flag on top of the same PTP transport.
/// </summary>
internal static class MediaImportCommands
{
    internal static async Task<List<PhotoImportSource>> SourcesAsync(RunContext ctx)
    {
        var section = ctx.Report.Section("Windows.Media.Import sources");
        section.Fact("API", "PhotoImportManager.IsSupportedAsync / FindAllSourcesAsync; PhotoImportSource; PhotoImportStorageMedium.SupportedAccessMode");
        var supported = await PhotoImportManager.IsSupportedAsync();
        section.Fact("PhotoImportManager.IsSupportedAsync()", supported);
        var sources = (await PhotoImportManager.FindAllSourcesAsync()).ToList();
        section.Fact("Sources found", sources.Count);
        ctx.Info($"Windows.Media.Import: IsSupported={supported}, sources found={sources.Count}");
        var rows = new List<IReadOnlyList<string>>();
        for (var i = 0; i < sources.Count; i++)
        {
            var s = sources[i];
            var media = s.StorageMedia.Select(m =>
                $"{m.StorageMediumType}/{m.SupportedAccessMode} cap={m.CapacityInBytes} free={m.AvailableSpaceInBytes} serial={ctx.Sanitizer.Serial(m.SerialNumber)}");
            rows.Add([
                i.ToString(CultureInfo.InvariantCulture),
                ctx.Sanitizer.DeviceName(s.DisplayName),
                s.Manufacturer ?? "",
                s.Model ?? "",
                s.Type.ToString(),
                s.ConnectionProtocol ?? "",
                s.ConnectionTransport.ToString(),
                s.IsMassStorage.ToString(),
                s.IsLocked?.ToString() ?? "n/a",
                string.Join("; ", media),
            ]);
        }

        section.Table(["#", "Display name", "Manufacturer", "Model", "Type", "Protocol", "Transport", "Mass storage", "Locked", "Storage media (type/access mode)"], rows);
        foreach (var row in rows)
        {
            ctx.Info("  source " + string.Join(" | ", row));
        }

        return sources;
    }

    internal static async Task FindAsync(RunContext ctx, CommandLine cli, List<PhotoImportSource> sources)
    {
        var section = ctx.Report.Section("Windows.Media.Import FindItemsAsync");
        var source = PickSource(cli, sources);
        if (source is null)
        {
            section.Fact("Result", "No Apple import source found (see sources table).");
            return;
        }

        var cancelAfterMs = cli.GetInt("cancel-after-ms", 0);
        using var session = source.CreateImportSession();
        using var cts = new CancellationTokenSource();
        long progressCallbacks = 0;
        uint lastProgress = 0;
        TimeSpan? firstProgress = null;
        var sw = Stopwatch.StartNew();
        var progress = new Progress<uint>(count =>
        {
            progressCallbacks++;
            lastProgress = count;
            firstProgress ??= sw.Elapsed;
        });
        if (cancelAfterMs > 0)
        {
            cts.CancelAfter(cancelAfterMs);
        }

        PhotoImportFindItemsResult? result = null;
        try
        {
            result = await session.FindItemsAsync(PhotoImportContentTypeFilter.ImagesAndVideos, PhotoImportItemSelectionMode.SelectNone)
                .AsTask(cts.Token, progress);
        }
        catch (OperationCanceledException)
        {
            section.Fact("Cancelled after (ms)", sw.Elapsed.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture))
                .Fact("Items reported by progress before cancel", lastProgress);
        }
        catch (Exception ex)
        {
            section.Fact("FindItemsAsync failed", WpdInterop.Describe(ex));
        }

        sw.Stop();
        section.Fact("Source", $"{source.Manufacturer} {source.Model} ({source.ConnectionProtocol})")
            .Fact("Progress callbacks", progressCallbacks)
            .Fact("Time to first progress callback (ms)", firstProgress?.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture))
            .Fact("Elapsed (s)", sw.Elapsed.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture))
            .Fact("Items usable before completion", "no — FoundItems is only available after the operation completes (progress reports counts only)");
        if (result is null)
        {
            return;
        }

        var items = result.FoundItems.ToList();
        section.Fact("HasSucceeded", result.HasSucceeded)
            .Fact("TotalCount / PhotosCount / VideosCount", $"{result.TotalCount} / {result.PhotosCount} / {result.VideosCount}")
            .Fact("SiblingsCount / SidecarsCount", $"{result.SiblingsCount} / {result.SidecarsCount}")
            .Fact("Items with Sibling", items.Count(i => i.Sibling is not null))
            .Fact("Items with Sidecars", items.Count(i => i.Sidecars.Count > 0))
            .Fact("Items with VideoSegments", items.Count(i => i.VideoSegments.Count > 0))
            .Fact("ItemKey unique within session", items.Select(i => i.ItemKey).Distinct().Count() == items.Count);

        var thumbCount = Math.Min(cli.GetInt("thumbs", 10), items.Count);
        var thumbsOk = 0;
        foreach (var item in items.Take(thumbCount))
        {
            try
            {
                using var thumb = await item.Thumbnail.OpenReadAsync();
                if (thumb.Size > 0)
                {
                    thumbsOk++;
                }
            }
            catch (Exception)
            {
                // Counted as unavailable.
            }
        }

        section.Fact("PhotoImportItem.Thumbnail readable (sampled)", $"{thumbsOk}/{thumbCount}");
        section.Line("Sample of items (sanitized). Sibling/sidecar pairing is Windows' own file-name based grouping:");
        section.Line(string.Empty);
        section.Table(
            ["Name", "ContentType", "Size", "Date", "Sibling", "Sidecars", "VideoSegments"],
            items.Where(i => i.Sibling is not null || i.Sidecars.Count > 0).Take(15)
                .Concat(items.Take(10))
                .Distinct()
                .Select(i => (IReadOnlyList<string>)[
                    ctx.Sanitizer.FileName(i.Name),
                    i.ContentType.ToString(),
                    i.SizeInBytes.ToString(CultureInfo.InvariantCulture),
                    ctx.Sanitizer.Date(i.Date.DateTime),
                    i.Sibling is null ? "" : ctx.Sanitizer.FileName(i.Sibling.Name),
                    string.Join(" ", i.Sidecars.Select(s => ctx.Sanitizer.FileName(s.Name))),
                    i.VideoSegments.Count.ToString(CultureInfo.InvariantCulture),
                ]));

        var grouping = DcimNameAnalyzer.Summarize(items.Select(i => ((string?)null, (string?)i.Name)));
        section.Fact("Names as seen by Windows.Media.Import: live-photo candidates / edited renders / .AAE groups",
            $"{grouping.LivePhotoCandidates} / {grouping.EditedRenderGroups} / {grouping.AdjustmentSidecarGroups}");

        if (cli.GetInt("import", 0) is > 0 and var importCount)
        {
            await ImportAsync(ctx, section, result, items, importCount);
        }
    }

    private static async Task ImportAsync(RunContext ctx, ReportSection section, PhotoImportFindItemsResult result, List<PhotoImportItem> items, int count)
    {
        DeviceSelection.EnsureOutsideGitWorkingTree(ctx);
        var destination = Path.Combine(ctx.RawDirectory, "wmi-import");
        Directory.CreateDirectory(destination);
        result.Session.DestinationFolder = await StorageFolder.GetFolderFromPathAsync(destination);
        result.SelectNone();
        foreach (var item in items.Where(i => i.Sibling is not null || i.Sidecars.Count > 0).Concat(items).Distinct().Take(count))
        {
            item.IsSelected = true;
        }

        var sw = Stopwatch.StartNew();
        var imported = await result.ImportItemsAsync();
        sw.Stop();
        section.Fact("ImportItemsAsync HasSucceeded", imported.HasSucceeded)
            .Fact("Imported total / photos / videos / siblings / sidecars",
                $"{imported.TotalCount} / {imported.PhotosCount} / {imported.VideosCount} / {imported.SiblingsCount} / {imported.SidecarsCount}")
            .Fact("Import elapsed (s)", sw.Elapsed.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture))
            .Fact("Files written per item (sanitized)", string.Join("; ", imported.ImportedItems.Select(i =>
                ctx.Sanitizer.FileName(i.Name) + " -> [" + string.Join(", ", i.ImportedFileNames.Select(ctx.Sanitizer.FileName)) + "]")));
        section.Line("DeleteImportedItemsFromSourceAsync was deliberately NOT called: it deletes every item imported in this session.");
    }

    private static PhotoImportSource? PickSource(CommandLine cli, List<PhotoImportSource> sources)
    {
        var index = cli.GetInt("source", -1);
        if (index >= 0 && index < sources.Count)
        {
            return sources[index];
        }

        return sources.FirstOrDefault(s => (s.Manufacturer ?? string.Empty).Contains("Apple", StringComparison.OrdinalIgnoreCase));
    }
}
