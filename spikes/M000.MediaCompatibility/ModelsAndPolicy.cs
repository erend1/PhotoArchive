using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace M000.MediaCompatibility;

public enum CapabilityStatus
{
    Pass,
    Limited,
    Fail,
    NotApplicable
}

public sealed record MediaFixture(
    string Key,
    string FileName,
    string Description,
    byte[] Bytes);

public sealed record MediaProbeResult(
    string Container,
    string? Codec,
    int? Width,
    int? Height,
    TimeSpan? Duration,
    string? CaptureDate,
    int? Orientation,
    double? Latitude,
    double? Longitude,
    string? DngVersion);

public sealed record CopyVerification(
    string SourceSha256,
    string DestinationSha256,
    long ByteLength);

public sealed record ManifestResource(
    string Role,
    string RelativePath,
    string Sha256,
    long ByteLength);

public sealed record AssetManifest(
    string AssetId,
    List<ManifestResource> Resources);

public sealed record DecoderInventory(
    bool JpegPngWic,
    bool HeifWic,
    bool DngWic,
    bool H264MediaFoundation,
    bool HevcMediaFoundation);

public sealed record ViewCapabilityDecision(
    bool PreservationAllowed,
    CapabilityStatus Thumbnail,
    CapabilityStatus Preview,
    CapabilityStatus Playback,
    string Reason);

public static class FixtureCatalog
{
    private static readonly IReadOnlyDictionary<string, MediaFixture> Fixtures =
        new Dictionary<string, MediaFixture>(StringComparer.OrdinalIgnoreCase)
        {
            ["jpeg"] = new(
                "jpeg",
                "fixture.jpg",
                "Metadata-focused JPEG marker stream with EXIF orientation/date/GPS and SOF dimensions.",
                SyntheticMediaFactory.CreateJpegMetadataFixture(4032, 3024)),
            ["png"] = new(
                "png",
                "fixture.png",
                "Valid generated RGB PNG with deterministic 640x480 pixels.",
                SyntheticMediaFactory.CreatePng(640, 480)),
            ["heic"] = new(
                "heic",
                "fixture.heic",
                "HEIF/HEIC ISO-BMFF structural fixture containing ftyp/meta/iprp/ipco/ispe boxes; no coded image payload.",
                SyntheticMediaFactory.CreateHeicStructureFixture(3024, 4032)),
            ["mov-h264"] = new(
                "mov-h264",
                "fixture-h264.mov",
                "QuickTime ISO-BMFF structural fixture with mvhd and avc1 video sample entry; no coded video samples.",
                SyntheticMediaFactory.CreateVideoStructureFixture("qt  ", "avc1", 1920, 1080, TimeSpan.FromSeconds(5))),
            ["mov-hevc"] = new(
                "mov-hevc",
                "fixture-hevc.mov",
                "QuickTime ISO-BMFF structural fixture with mvhd and hvc1 video sample entry; no coded video samples.",
                SyntheticMediaFactory.CreateVideoStructureFixture("qt  ", "hvc1", 1920, 1080, TimeSpan.FromSeconds(5))),
            ["mp4-h264"] = new(
                "mp4-h264",
                "fixture-h264.mp4",
                "MP4 ISO-BMFF structural fixture with mvhd and avc1 video sample entry; no coded video samples.",
                SyntheticMediaFactory.CreateVideoStructureFixture("isom", "avc1", 1920, 1080, TimeSpan.FromSeconds(5))),
            ["mp4-hevc"] = new(
                "mp4-hevc",
                "fixture-hevc.mp4",
                "MP4 ISO-BMFF structural fixture with mvhd and hvc1 video sample entry; no coded video samples.",
                SyntheticMediaFactory.CreateVideoStructureFixture("isom", "hvc1", 1920, 1080, TimeSpan.FromSeconds(5))),
            ["dng"] = new(
                "dng",
                "fixture.dng",
                "TIFF/DNG structural fixture with dimensions, orientation, capture date, and DNGVersion tags; no RAW mosaic payload.",
                SyntheticMediaFactory.CreateDngStructureFixture(4032, 3024))
        };

    public static IEnumerable<string> Keys => Fixtures.Keys;

    public static MediaFixture Get(string key) => Fixtures[key];

    public static (MediaFixture Still, MediaFixture Motion) CreateLivePhotoResources() =>
        (Get("heic") with { FileName = "IMG_0001.HEIC" }, Get("mov-h264") with { FileName = "IMG_0001.MOV" });
}

public static class BytePreservingArchiver
{
    public static async Task<CopyVerification> CopyAndVerifyAsync(
        byte[] sourceBytes,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        var parent = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        var sourceHash = Convert.ToHexString(SHA256.HashData(sourceBytes));
        await File.WriteAllBytesAsync(destinationPath, sourceBytes, cancellationToken);
        var destinationBytes = await File.ReadAllBytesAsync(destinationPath, cancellationToken);
        var destinationHash = Convert.ToHexString(SHA256.HashData(destinationBytes));

        if (!string.Equals(sourceHash, destinationHash, StringComparison.Ordinal) ||
            sourceBytes.LongLength != destinationBytes.LongLength)
        {
            throw new InvalidDataException("Byte-preserving copy verification failed.");
        }

        return new(sourceHash, destinationHash, destinationBytes.LongLength);
    }
}

public static class LivePhotoManifestFactory
{
    public static AssetManifest Create(MediaFixture still, MediaFixture motion)
    {
        return new AssetManifest(
            "live-photo-fixture-001",
            new List<ManifestResource>
            {
                new(
                    "OriginalPhoto",
                    $"Media/2026/09/{still.FileName}",
                    Convert.ToHexString(SHA256.HashData(still.Bytes)),
                    still.Bytes.LongLength),
                new(
                    "LivePhotoMotion",
                    $"Media/2026/09/{motion.FileName}",
                    Convert.ToHexString(SHA256.HashData(motion.Bytes)),
                    motion.Bytes.LongLength)
            });
    }

    public static AssetManifest RoundTripJson(AssetManifest manifest)
    {
        var json = JsonSerializer.Serialize(manifest);
        return JsonSerializer.Deserialize<AssetManifest>(json)
            ?? throw new InvalidDataException("Manifest JSON round-trip returned null.");
    }
}

public static class ViewingPolicy
{
    public static ViewCapabilityDecision Evaluate(MediaFixture fixture, DecoderInventory inventory)
    {
        var key = fixture.Key.ToLowerInvariant();

        if (key is "jpeg" or "png")
        {
            var status = inventory.JpegPngWic ? CapabilityStatus.Pass : CapabilityStatus.Limited;
            return new(true, status, status, CapabilityStatus.NotApplicable,
                inventory.JpegPngWic ? "WIC still-image decoder available." : "Still image is preserved but native WIC decode is unavailable.");
        }

        if (key == "heic")
        {
            var status = inventory.HeifWic ? CapabilityStatus.Pass : CapabilityStatus.Limited;
            return new(true, status, status, CapabilityStatus.NotApplicable,
                inventory.HeifWic ? "HEIF decoder available." : "HEIF decoder unavailable; keep original and use a placeholder derivative state.");
        }

        if (key == "dng")
        {
            var status = inventory.DngWic ? CapabilityStatus.Pass : CapabilityStatus.Limited;
            return new(true, status, status, CapabilityStatus.NotApplicable,
                inventory.DngWic ? "DNG/RAW decoder available." : "DNG/RAW decoder unavailable; preservation remains allowed.");
        }

        if (key.Contains("h264", StringComparison.Ordinal))
        {
            var status = inventory.H264MediaFoundation ? CapabilityStatus.Pass : CapabilityStatus.Limited;
            return new(true, status, status, status,
                inventory.H264MediaFoundation ? "H.264 Media Foundation decode available." : "H.264 playback unavailable; preservation remains allowed.");
        }

        if (key.Contains("hevc", StringComparison.Ordinal))
        {
            var status = inventory.HevcMediaFoundation ? CapabilityStatus.Pass : CapabilityStatus.Limited;
            return new(true, status, status, status,
                inventory.HevcMediaFoundation ? "HEVC Media Foundation decode available." : "HEVC codec unavailable; preservation remains allowed.");
        }

        return new(true, CapabilityStatus.Limited, CapabilityStatus.Limited, CapabilityStatus.NotApplicable,
            "Unknown viewing capability; preserve bytes and surface an unsupported-view placeholder.");
    }
}
