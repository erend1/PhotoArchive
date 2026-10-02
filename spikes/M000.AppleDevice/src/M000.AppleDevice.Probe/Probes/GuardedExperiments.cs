using System.Globalization;
using PhotoArchive.Spikes.AppleDevice.Analysis;
using PhotoArchive.Spikes.AppleDevice.Cli;
using PhotoArchive.Spikes.AppleDevice.Output;
using PhotoArchive.Spikes.AppleDevice.Wpd;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.Win32;

namespace PhotoArchive.Spikes.AppleDevice.Probes;

/// <summary>
/// OPT-IN destructive experiments. They exist only to replace assumptions about delete/write-back with evidence.
/// Guards: explicit flag, interactive console, typed confirmation of the exact target, write access requested only
/// here, a single object at a time, and (for delete) a completed and verified copy before anything is removed.
/// Use a throwaway photo taken for this purpose. These are NOT a product cleanup design.
/// </summary>
internal static class GuardedExperiments
{
    internal const string DeleteFlag = "i-understand-this-deletes-from-my-iphone";
    internal const string WriteFlag = "i-understand-this-may-add-a-test-image-to-my-iphone";

    internal static void Delete(RunContext ctx, CommandLine cli, WpdDeviceInfo info)
    {
        if (!cli.Has(DeleteFlag))
        {
            throw new ProbeUsageException($"delete-experiment requires --{DeleteFlag}. Read spikes/M000.AppleDevice/README.md first.");
        }

        RequireInteractive();
        DeviceSelection.EnsureOutsideGitWorkingTree(ctx);
        var section = ctx.Report.Section("GUARDED EXPERIMENT: delete one object through WPD (PTP DeleteObject)");
        using var device = WpdDevice.Open(info.PnpId, WpdAccess.ReadWriteForGuardedExperiment);
        section.Fact("Session access", "GENERIC_READ|GENERIC_WRITE (granted)");

        var objects = EnumerateCommand.Run(ctx, device, new EnumerateCommand.Options(0, 128, 0, 8, IdsOnly: false));
        var target = cli.Get("object") is { Length: > 0 } id
            ? objects.FirstOrDefault(o => o.ObjectId == id)
            : objects.Where(o => o.IsImage && o.DateCreated is not null).MaxBy(o => o.DateCreated);
        if (target is null || target.IsContainer)
        {
            throw new ProbeAbortedException("No suitable target object found (need an image/video object; default is the newest image).");
        }

        var siblings = GroupMembers(objects, target);
        ctx.Warn("TARGET (unsanitized, shown locally only):");
        ctx.Warn($"  name={target.FileName}  created={target.DateCreated:yyyy-MM-dd HH:mm:ss}  size={target.Size}  canDelete={Fmt(target.CanDelete)}");
        ctx.Warn($"  related-by-name objects in the same folder: {string.Join(", ", siblings.Select(s => s.FileName))}");
        ctx.Warn("Confirm on the iPhone (Photos > item > Info) that this is the throwaway photo you just took.");

        // Verify-before-delete: a complete, size-checked, hashed copy must exist first.
        var copy = CopyCommand.Run(ctx, device, objects, new CopyCommand.Options(1, [target.ObjectId], Reread: true, Keep: true, long.MaxValue)).Single();
        if (!copy.Succeeded || copy.SizeMatches == false || copy.RereadMatches == false || copy.ExtensionMatches == false)
        {
            // ExtensionMatches == false means the device delivered converted bytes: the original would NOT be preserved.
            section.Fact("Aborted", "verified copy failed (transfer, size, re-read or format check); nothing was deleted");
            throw new ProbeAbortedException("Verified copy failed (transfer, size, re-read or format check); refusing to delete.");
        }

        var expected = $"DELETE {target.FileName}";
        Console.Write($"Type exactly \"{expected}\" to delete it from the iPhone (anything else aborts): ");
        if (!string.Equals(Console.ReadLine()?.Trim(), expected, StringComparison.Ordinal))
        {
            section.Fact("Aborted", "confirmation text did not match; nothing was deleted");
            ctx.Warn("Aborted. Nothing was deleted.");
            return;
        }

        var before = siblings.Select(s => s.FileName).ToList();
        var hr = device.DeleteSingleObject(target.ObjectId);
        section.Fact("Target (sanitized)", ctx.Sanitizer.FileName(target.FileName))
            .Fact("WPD_OBJECT_CAN_DELETE before delete", target.CanDelete)
            .Fact("Verified copy before delete (bytes / size match / re-read match)", $"{copy.BytesRead} / {Fmt(copy.SizeMatches)} / {Fmt(copy.RereadMatches)}")
            .Fact("IPortableDeviceContent::Delete result", hr.ToString());

        // Post-conditions: is the object gone, and did related resources disappear with it (asset-level delete)?
        Thread.Sleep(1500);
        string stillReadable;
        try
        {
            device.GetAllProperties(target.ObjectId);
            stillReadable = "yes (object still exists)";
        }
        catch (Exception ex)
        {
            stillReadable = "no: " + WpdInterop.Describe(ex);
        }

        var parentChildren = device.EnumerateChildren(target.ParentId, 256, CancellationToken.None)
            .Select(childId =>
            {
                try
                {
                    var values = device.GetValues(childId, PInvoke.WPD_OBJECT_ORIGINAL_FILE_NAME, PInvoke.WPD_OBJECT_NAME);
                    return WpdInterop.GetString(values, PInvoke.WPD_OBJECT_ORIGINAL_FILE_NAME) ?? WpdInterop.GetString(values, PInvoke.WPD_OBJECT_NAME);
                }
                catch (Exception)
                {
                    return null;
                }
            })
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var after = before.Where(parentChildren.Contains).ToList();
        section.Fact("Target object still readable after delete", stillReadable)
            .Fact("Related-by-name objects before", string.Join(", ", before.Select(ctx.Sanitizer.FileName)))
            .Fact("Related-by-name objects still listed after", after.Count == 0 ? "(none)" : string.Join(", ", after.Select(ctx.Sanitizer.FileName)));
        section.Line("Manual follow-up (record answers in the issue): (1) Is the item in Photos > Recently Deleted on the iPhone? " +
                     "(2) If it was a Live Photo/edited photo, did the whole asset disappear or only one resource? " +
                     "(3) With iCloud Photos ON: is it deleted/in Recently Deleted on iCloud.com and other devices? " +
                     "(4) Did iOS show any confirmation prompt on the phone?");
        ctx.Good($"Delete returned {hr}. Target still readable: {stillReadable}. Related objects still listed: {after.Count}/{before.Count}.");
    }

    internal static async Task WriteAsync(RunContext ctx, CommandLine cli, WpdDeviceInfo info)
    {
        if (!cli.Has(WriteFlag))
        {
            throw new ProbeUsageException($"write-experiment requires --{WriteFlag}. Read spikes/M000.AppleDevice/README.md first.");
        }

        RequireInteractive();
        var section = ctx.Report.Section("GUARDED EXPERIMENT: create one synthetic image through WPD (PTP SendObjectInfo/SendObject)");
        Console.Write("Type exactly \"WRITE TEST\" to attempt to create a synthetic 64x64 grey JPEG on the iPhone: ");
        if (!string.Equals(Console.ReadLine()?.Trim(), "WRITE TEST", StringComparison.Ordinal))
        {
            section.Fact("Aborted", "confirmation text did not match");
            return;
        }

        var jpeg = await CreateSyntheticJpegAsync();
        using var device = WpdDevice.Open(info.PnpId, WpdAccess.ReadWriteForGuardedExperiment);
        var containers = EnumerateCommand.Run(ctx, device, new EnumerateCommand.Options(0, 128, 0, 3, IdsOnly: true)).Where(o => o.IsContainer).ToList();
        var targets = containers.Where(c => string.Equals(c.FileName, "DCIM", StringComparison.OrdinalIgnoreCase)).Concat(containers.Where(c => c.Depth == 1)).Take(2).ToList();
        section.Fact("Synthetic payload", $"{jpeg.Length} bytes, 64x64 uniform grey JPEG generated by Windows.Graphics.Imaging (no personal content)");
        foreach (var parent in targets)
        {
            var label = $"parent '{ctx.Sanitizer.FileName(parent.FileName)}' (depth {parent.Depth})";
            try
            {
                var created = device.CreateObjectWithData(parent.ObjectId, "PAPROBE1.JPG", PInvoke.WPD_CONTENT_TYPE_IMAGE, PInvoke.WPD_OBJECT_FORMAT_EXIF, jpeg);
                section.Fact($"Create under {label}", $"SUCCEEDED (object {ctx.Sanitizer.Opaque(created, "created")}) — check the Photos app and delete the grey test image");
                ctx.Warn("Object creation SUCCEEDED. Check the iPhone Photos app for a grey 64x64 image and delete it manually.");
            }
            catch (Exception ex)
            {
                section.Fact($"Create under {label}", "FAILED: " + WpdInterop.Describe(ex));
            }
        }

        foreach (var (key, value) in section.Facts)
        {
            ctx.Info($"{key}: {ReportDocument.Format(value)}");
        }
    }

    private static List<EnumeratedObject> GroupMembers(List<EnumeratedObject> objects, EnumeratedObject target)
    {
        var parsed = DcimNameAnalyzer.Parse(target.FileName);
        if (parsed is null)
        {
            return [target];
        }

        return objects.Where(o => o.ParentId == target.ParentId
                                  && DcimNameAnalyzer.Parse(o.FileName) is { } p
                                  && p.Number == parsed.Number
                                  && p.Stem.Equals(parsed.Stem, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static async Task<byte[]> CreateSyntheticJpegAsync()
    {
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
        var pixels = new byte[64 * 64 * 4];
        Array.Fill(pixels, (byte)0x80);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, 64, 64, 96, 96, pixels);
        await encoder.FlushAsync();
        stream.Seek(0);
        var bytes = new byte[stream.Size];
        await stream.AsStreamForRead().ReadExactlyAsync(bytes);
        return bytes;
    }

    private static void RequireInteractive()
    {
        if (Console.IsInputRedirected)
        {
            throw new ProbeAbortedException("Guarded experiments require an interactive console (stdin must not be redirected).");
        }
    }

    private static string Fmt(bool? value) => value switch { true => "yes", false => "no", null => "n/a" };
}
