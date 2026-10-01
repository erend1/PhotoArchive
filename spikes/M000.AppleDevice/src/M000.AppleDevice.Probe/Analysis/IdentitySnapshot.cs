using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhotoArchive.Spikes.AppleDevice.Analysis;

/// <summary>
/// One row per device object, captured by <c>identity-snapshot</c>. Raw snapshots contain personal metadata
/// (file names, dates, sizes) and are written only to the local run folder, never to the repository.
/// </summary>
internal sealed record SnapshotEntry(
    string ObjectId,
    string? PersistentUniqueId,
    string? ParentId,
    string? Name,
    string? OriginalFileName,
    ulong? Size,
    DateTime? DateCreated,
    DateTime? DateModified,
    string? ContentType,
    string? Format);

internal sealed record IdentitySnapshot(
    string ProbeVersion,
    DateTimeOffset CapturedAtUtc,
    string DeviceFingerprint,
    string? FirmwareVersion,
    List<SnapshotEntry> Entries)
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    internal void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));

    internal static IdentitySnapshot Load(string path) =>
        JsonSerializer.Deserialize<IdentitySnapshot>(File.ReadAllText(path), JsonOptions)
        ?? throw new InvalidDataException($"Snapshot '{path}' is empty or invalid.");
}

/// <summary>
/// Compares two snapshots of the same device taken in different connection sessions to measure whether
/// WPD object IDs / persistent unique IDs (PUIDs) are stable enough to serve as same-device rematching
/// accelerators. Stability here never replaces SHA-256 content identity (ADR-0003).
/// </summary>
internal static class IdentityComparer
{
    internal static IdentityComparison Compare(IdentitySnapshot before, IdentitySnapshot after)
    {
        var result = new IdentityComparison
        {
            CountBefore = before.Entries.Count,
            CountAfter = after.Entries.Count,
            SameDeviceFingerprint = string.Equals(before.DeviceFingerprint, after.DeviceFingerprint, StringComparison.Ordinal),
            FirmwareBefore = before.FirmwareVersion,
            FirmwareAfter = after.FirmwareVersion,
            PuidPresentBefore = before.Entries.Count(e => !string.IsNullOrEmpty(e.PersistentUniqueId)),
            PuidPresentAfter = after.Entries.Count(e => !string.IsNullOrEmpty(e.PersistentUniqueId)),
            DuplicatePuidsBefore = CountDuplicates(before.Entries.Select(e => e.PersistentUniqueId)),
            DuplicatePuidsAfter = CountDuplicates(after.Entries.Select(e => e.PersistentUniqueId)),
            DuplicateMetadataKeysBefore = CountDuplicates(before.Entries.Select(MetadataKey)),
            DuplicateMetadataKeysAfter = CountDuplicates(after.Entries.Select(MetadataKey)),
        };

        // 1) Match by metadata key (name + original name + size + created date). This is a *test oracle only*:
        //    it lets us observe whether IDs changed for what is very likely the same file. It is never used
        //    as identity proof by the product.
        var beforeByKey = UniqueIndex(before.Entries, MetadataKey);
        var afterByKey = UniqueIndex(after.Entries, MetadataKey);
        foreach (var entry in beforeByKey)
        {
            if (!afterByKey.TryGetValue(entry.Key, out var match))
            {
                result.MetadataKeyOnlyBefore++;
                continue;
            }

            result.MetadataKeyMatched++;
            if (string.Equals(entry.Value.ObjectId, match.ObjectId, StringComparison.Ordinal))
            {
                result.ObjectIdUnchanged++;
            }

            if (!string.IsNullOrEmpty(entry.Value.PersistentUniqueId)
                && string.Equals(entry.Value.PersistentUniqueId, match.PersistentUniqueId, StringComparison.Ordinal))
            {
                result.PuidUnchanged++;
            }
        }

        result.MetadataKeyOnlyAfter = afterByKey.Keys.Count(k => !beforeByKey.ContainsKey(k));

        // 2) Match by PUID and check that the metadata still agrees (a PUID pointing at different content
        //    after reconnection would make it unusable even as an accelerator).
        var afterByPuid = UniqueIndex(after.Entries.Where(e => !string.IsNullOrEmpty(e.PersistentUniqueId)), e => e.PersistentUniqueId!);
        foreach (var entry in UniqueIndex(before.Entries.Where(e => !string.IsNullOrEmpty(e.PersistentUniqueId)), e => e.PersistentUniqueId!))
        {
            if (!afterByPuid.TryGetValue(entry.Key, out var match))
            {
                result.PuidOnlyBefore++;
                continue;
            }

            result.PuidMatched++;
            if (MetadataKey(entry.Value) != MetadataKey(match))
            {
                result.PuidMatchedButMetadataDiffers++;
            }
        }

        result.PuidOnlyAfter = afterByPuid.Count - result.PuidMatched;
        return result;
    }

    internal static string MetadataKey(SnapshotEntry e) =>
        string.Join('|',
            e.Name ?? string.Empty,
            e.OriginalFileName ?? string.Empty,
            e.Size?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            e.DateCreated?.ToString("O", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);

    private static int CountDuplicates(IEnumerable<string?> values) =>
        values.Where(v => !string.IsNullOrEmpty(v)).GroupBy(v => v, StringComparer.Ordinal).Sum(g => g.Count() > 1 ? g.Count() : 0);

    private static Dictionary<string, SnapshotEntry> UniqueIndex(IEnumerable<SnapshotEntry> entries, Func<SnapshotEntry, string> key)
    {
        // Keys that occur more than once are ambiguous and excluded from matching.
        return entries.GroupBy(key, StringComparer.Ordinal)
            .Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
    }
}

internal sealed class IdentityComparison
{
    public int CountBefore { get; init; }
    public int CountAfter { get; init; }
    public bool SameDeviceFingerprint { get; init; }
    public string? FirmwareBefore { get; init; }
    public string? FirmwareAfter { get; init; }
    public int PuidPresentBefore { get; init; }
    public int PuidPresentAfter { get; init; }
    public int DuplicatePuidsBefore { get; init; }
    public int DuplicatePuidsAfter { get; init; }
    public int DuplicateMetadataKeysBefore { get; init; }
    public int DuplicateMetadataKeysAfter { get; init; }
    public int MetadataKeyMatched { get; set; }
    public int MetadataKeyOnlyBefore { get; set; }
    public int MetadataKeyOnlyAfter { get; set; }
    public int ObjectIdUnchanged { get; set; }
    public int PuidUnchanged { get; set; }
    public int PuidMatched { get; set; }
    public int PuidOnlyBefore { get; set; }
    public int PuidOnlyAfter { get; set; }
    public int PuidMatchedButMetadataDiffers { get; set; }

    public string Verdict =>
        MetadataKeyMatched == 0 ? "INCONCLUSIVE (no comparable entries)"
        : PuidPresentBefore == 0 || PuidPresentAfter == 0 ? "PUID NOT EXPOSED"
        : PuidUnchanged == MetadataKeyMatched && PuidMatchedButMetadataDiffers == 0 && DuplicatePuidsBefore == 0 && DuplicatePuidsAfter == 0
            ? "PUID STABLE FOR ALL COMPARABLE ENTRIES (accelerator candidate only; never identity proof)"
            : "PUID NOT STABLE OR NOT UNIQUE (unusable as a rematching accelerator)";
}
