using System.Diagnostics;
using System.Globalization;
using PhotoArchive.Spikes.AppleDevice.Analysis;
using PhotoArchive.Spikes.AppleDevice.Output;
using PhotoArchive.Spikes.AppleDevice.Wpd;
using Windows.Win32;

namespace PhotoArchive.Spikes.AppleDevice.Probes;

/// <summary>
/// Tests on-demand device thumbnails: IPortableDeviceResources::GetStream(WPD_RESOURCE_THUMBNAIL), which maps to
/// PTP GetThumb for PTP devices. Thumbnail bytes are only saved (outside the repository) with --save.
/// </summary>
internal static class ThumbnailCommand
{
    internal static void Run(RunContext ctx, WpdDevice device, IReadOnlyList<EnumeratedObject> objects, int count, bool save)
    {
        var section = ctx.Report.Section("Thumbnails (WPD_RESOURCE_THUMBNAIL)");
        section.Fact("API", "IPortableDeviceResources::GetSupportedResources / GetResourceAttributes / GetStream(WPD_RESOURCE_THUMBNAIL)");
        if (save)
        {
            DeviceSelection.EnsureOutsideGitWorkingTree(ctx);
        }

        var sample = SampleSelector.Representative(objects, count, includeGroups: false);
        if (sample.Count < count)
        {
            sample.AddRange(objects.Where(o => o.IsMediaLike && !sample.Contains(o)).Take(count - sample.Count));
        }

        var rows = new List<IReadOnlyList<string>>();
        var latencies = new List<double>();
        var available = 0;
        foreach (var obj in sample)
        {
            var name = ctx.Sanitizer.FileName(obj.FileName);
            try
            {
                var resources = device.GetSupportedResources(obj.ObjectId).Select(WpdNames.Key).ToList();
                var hasThumb = resources.Contains(nameof(PInvoke.WPD_RESOURCE_THUMBNAIL));
                if (!hasThumb)
                {
                    rows.Add([name, obj.Extension, string.Join(" ", resources), "no", "", "", "", ""]);
                    continue;
                }

                var attributes = device.GetResourceAttributes(obj.ObjectId, PInvoke.WPD_RESOURCE_THUMBNAIL);
                var declaredFormat = attributes.FirstOrDefault(a => WpdNames.Key(a.Key) == nameof(PInvoke.WPD_RESOURCE_ATTRIBUTE_FORMAT)).Value;
                using var buffer = new MemoryStream();
                var sw = Stopwatch.StartNew();
                var read = device.ReadResource(obj.ObjectId, PInvoke.WPD_RESOURCE_THUMBNAIL, buffer, CancellationToken.None);
                sw.Stop();
                available++;
                latencies.Add(sw.Elapsed.TotalMilliseconds);
                var bytes = buffer.ToArray();
                var sniffed = MediaSignature.Sniff(bytes);
                var dims = sniffed == SniffedFormat.Jpeg ? JpegInfo.TryReadDimensions(bytes) : null;
                rows.Add([
                    name,
                    obj.Extension,
                    string.Join(" ", resources),
                    "yes",
                    read.BytesRead.ToString(CultureInfo.InvariantCulture),
                    $"{sniffed} (declared {(declaredFormat is Guid g ? WpdNames.Guid(g) : "n/a")})",
                    dims is { } d ? $"{d.Width}x{d.Height}" : "n/a",
                    sw.Elapsed.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture),
                ]);

                if (save)
                {
                    var dir = Path.Combine(ctx.RawDirectory, "thumbnails");
                    Directory.CreateDirectory(dir);
                    File.WriteAllBytes(Path.Combine(dir, $"{rows.Count:D3}.{(sniffed == SniffedFormat.Jpeg ? "jpg" : "bin")}"), bytes);
                }
            }
            catch (Exception ex)
            {
                rows.Add([name, obj.Extension, "", "error", "", WpdInterop.Describe(ex), "", ""]);
            }
        }

        latencies.Sort();
        section.Fact("Objects sampled", sample.Count)
            .Fact("Thumbnails retrieved", available)
            .Fact("Median thumbnail latency (ms)", latencies.Count == 0 ? null : latencies[latencies.Count / 2].ToString("F0", CultureInfo.InvariantCulture))
            .Fact("Max thumbnail latency (ms)", latencies.Count == 0 ? null : latencies[^1].ToString("F0", CultureInfo.InvariantCulture));
        section.Table(["Object (sanitized)", "Ext", "Supported resources", "Thumb", "Bytes", "Format", "Pixels", "ms"], rows);
        ctx.Good($"Thumbnails: {available}/{sample.Count} retrieved.");
    }
}
