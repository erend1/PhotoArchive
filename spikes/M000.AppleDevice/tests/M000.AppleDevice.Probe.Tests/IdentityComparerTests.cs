using PhotoArchive.Spikes.AppleDevice.Analysis;

namespace PhotoArchive.Spikes.AppleDevice.Tests;

public sealed class IdentityComparerTests
{
    private static readonly DateTime Created = new(2026, 9, 1, 10, 0, 0);

    [Fact]
    public void Stable_puids_are_reported_as_accelerator_candidates_only()
    {
        var before = Snapshot(Entry("o1", "P1", "IMG_0001.HEIC", 100), Entry("o2", "P2", "IMG_0001.MOV", 200));
        var after = Snapshot(Entry("o7", "P1", "IMG_0001.HEIC", 100), Entry("o8", "P2", "IMG_0001.MOV", 200));

        var result = IdentityComparer.Compare(before, after);

        Assert.Equal(2, result.MetadataKeyMatched);
        Assert.Equal(0, result.ObjectIdUnchanged);
        Assert.Equal(2, result.PuidUnchanged);
        Assert.Equal(0, result.PuidMatchedButMetadataDiffers);
        Assert.StartsWith("PUID STABLE", result.Verdict, StringComparison.Ordinal);
        Assert.Contains("never identity proof", result.Verdict, StringComparison.Ordinal);
    }

    [Fact]
    public void Changed_puids_are_not_stable()
    {
        var before = Snapshot(Entry("o1", "P1", "IMG_0001.HEIC", 100), Entry("o2", "P2", "IMG_0002.HEIC", 200));
        var after = Snapshot(Entry("o1", "Q1", "IMG_0001.HEIC", 100), Entry("o2", "Q2", "IMG_0002.HEIC", 200));

        var result = IdentityComparer.Compare(before, after);

        Assert.Equal(2, result.ObjectIdUnchanged);
        Assert.Equal(0, result.PuidUnchanged);
        Assert.StartsWith("PUID NOT STABLE", result.Verdict, StringComparison.Ordinal);
    }

    [Fact]
    public void Puid_reuse_for_different_content_is_detected()
    {
        // After a delete + new capture, the same PUID now points at a different file: unusable even as an accelerator.
        var before = Snapshot(Entry("o1", "P1", "IMG_0001.HEIC", 100), Entry("o2", "P2", "IMG_0002.HEIC", 200));
        var after = Snapshot(Entry("o1", "P1", "IMG_0003.HEIC", 300), Entry("o2", "P2", "IMG_0002.HEIC", 200));

        var result = IdentityComparer.Compare(before, after);

        Assert.Equal(1, result.PuidMatchedButMetadataDiffers);
        Assert.StartsWith("PUID NOT STABLE", result.Verdict, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_puids_are_reported()
    {
        var before = Snapshot(Entry("o1", null, "IMG_0001.HEIC", 100));
        var after = Snapshot(Entry("o1", null, "IMG_0001.HEIC", 100));

        Assert.Equal("PUID NOT EXPOSED", IdentityComparer.Compare(before, after).Verdict);
    }

    [Fact]
    public void Duplicate_puids_make_the_identifier_unusable()
    {
        var before = Snapshot(Entry("o1", "P1", "IMG_0001.HEIC", 100), Entry("o2", "P1", "IMG_0002.HEIC", 200));
        var after = Snapshot(Entry("o1", "P1", "IMG_0001.HEIC", 100), Entry("o2", "P1", "IMG_0002.HEIC", 200));

        var result = IdentityComparer.Compare(before, after);

        Assert.Equal(2, result.DuplicatePuidsBefore);
        Assert.StartsWith("PUID NOT STABLE", result.Verdict, StringComparison.Ordinal);
    }

    [Fact]
    public void Ambiguous_metadata_keys_are_excluded_from_matching()
    {
        var before = Snapshot(Entry("o1", "P1", "IMG_0001.HEIC", 100), Entry("o2", "P2", "IMG_0001.HEIC", 100));
        var after = Snapshot(Entry("o1", "P1", "IMG_0001.HEIC", 100), Entry("o2", "P2", "IMG_0001.HEIC", 100));

        var result = IdentityComparer.Compare(before, after);

        Assert.Equal(2, result.DuplicateMetadataKeysBefore);
        Assert.Equal(0, result.MetadataKeyMatched);
        Assert.StartsWith("INCONCLUSIVE", result.Verdict, StringComparison.Ordinal);
    }

    [Fact]
    public void Snapshot_round_trips_through_json()
    {
        var path = Path.Combine(Path.GetTempPath(), $"m000-snapshot-{Guid.NewGuid():N}.json");
        try
        {
            var snapshot = Snapshot(Entry("o1", "P1", "IMG_0001.HEIC", 100));
            snapshot.Save(path);

            var loaded = IdentitySnapshot.Load(path);

            Assert.Equal(snapshot.DeviceFingerprint, loaded.DeviceFingerprint);
            Assert.Equal(snapshot.Entries, loaded.Entries);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static IdentitySnapshot Snapshot(params SnapshotEntry[] entries) =>
        new("test", DateTimeOffset.UnixEpoch, "fingerprint", "27.0", [.. entries]);

    private static SnapshotEntry Entry(string objectId, string? puid, string name, ulong size) =>
        new(objectId, puid, "parent", name, name, size, Created, Created, "image", "format");
}
