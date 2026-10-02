using System.Security.Cryptography;
using System.Text;
using PhotoArchive.Spikes.AppleDevice.Analysis;
using PhotoArchive.Spikes.AppleDevice.Output;
using PhotoArchive.Spikes.AppleDevice.Wpd;

namespace PhotoArchive.Spikes.AppleDevice.Probes;

/// <summary>
/// Source-identifier stability experiment: capture a snapshot, disconnect/reconnect (and optionally restart the
/// iPhone, take a photo, delete a photo on the phone), capture again, then compare.
/// </summary>
internal static class IdentityCommands
{
    internal static string Snapshot(RunContext ctx, WpdDeviceInfo info, WpdDevice device, string? firmware, string? outPath)
    {
        var objects = EnumerateCommand.Run(ctx, device, new EnumerateCommand.Options(0, 128, 0, 8, IdsOnly: false));
        var snapshot = new IdentitySnapshot(
            ProbeVersion: typeof(IdentityCommands).Assembly.GetName().Version?.ToString() ?? "0",
            CapturedAtUtc: DateTimeOffset.UtcNow,
            DeviceFingerprint: Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(info.PnpId.ToUpperInvariant())))[..16],
            FirmwareVersion: firmware,
            Entries: objects.Where(o => !o.IsContainer).Select(o => new SnapshotEntry(
                o.ObjectId,
                o.PersistentUniqueId,
                o.ParentId,
                o.Name,
                o.OriginalFileName,
                o.Size,
                o.DateCreated,
                o.DateModified,
                WpdNames.Guid(o.ContentType),
                WpdNames.Guid(o.Format))).ToList());

        var path = string.IsNullOrWhiteSpace(outPath) ? Path.Combine(ctx.RawDirectory, "identity-snapshot.json") : Path.GetFullPath(outPath);
        snapshot.Save(path);
        ctx.Report.Section("Identity snapshot")
            .Fact("Entries", snapshot.Entries.Count)
            .Fact("Snapshot file (local only; contains personal metadata)", "raw/identity-snapshot.json")
            .Line("Next: disconnect the iPhone, reconnect/unlock it, run `identity-snapshot` again, then `identity-compare <first> <second>`.");
        ctx.Good($"Snapshot with {snapshot.Entries.Count} entries written to {path}");
        ctx.Warn("The snapshot contains personal metadata (names, dates, sizes). Do not commit it.");
        return path;
    }

    internal static void Compare(RunContext ctx, string beforePath, string afterPath)
    {
        var comparison = IdentityComparer.Compare(IdentitySnapshot.Load(beforePath), IdentitySnapshot.Load(afterPath));
        var section = ctx.Report.Section("Identifier stability across connection sessions");
        section.Fact("Verdict", comparison.Verdict)
            .Fact("Same device fingerprint", comparison.SameDeviceFingerprint)
            .Fact("Firmware before / after", $"{comparison.FirmwareBefore} / {comparison.FirmwareAfter}")
            .Fact("Entries before / after", $"{comparison.CountBefore} / {comparison.CountAfter}")
            .Fact("PUID present before / after", $"{comparison.PuidPresentBefore} / {comparison.PuidPresentAfter}")
            .Fact("Duplicate PUIDs before / after", $"{comparison.DuplicatePuidsBefore} / {comparison.DuplicatePuidsAfter}")
            .Fact("Ambiguous metadata keys before / after", $"{comparison.DuplicateMetadataKeysBefore} / {comparison.DuplicateMetadataKeysAfter}")
            .Fact("Entries matched by metadata key (test oracle only)", comparison.MetadataKeyMatched)
            .Fact("...of which WPD_OBJECT_ID unchanged", comparison.ObjectIdUnchanged)
            .Fact("...of which PUID unchanged", comparison.PuidUnchanged)
            .Fact("Metadata keys only before / only after", $"{comparison.MetadataKeyOnlyBefore} / {comparison.MetadataKeyOnlyAfter}")
            .Fact("PUIDs matched across sessions", comparison.PuidMatched)
            .Fact("PUID matched but metadata differs (PUID reuse!)", comparison.PuidMatchedButMetadataDiffers)
            .Fact("PUIDs only before / only after", $"{comparison.PuidOnlyBefore} / {comparison.PuidOnlyAfter}")
            .Line("Even a stable PUID is only a same-device rematching accelerator. It is not archive identity and never authorizes deletion (ADR-0003, SAFETY_AND_INTEGRITY.md).");
        foreach (var (key, value) in section.Facts)
        {
            ctx.Info($"{key}: {ReportDocument.Format(value)}");
        }
    }
}
