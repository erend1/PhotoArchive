using System.Security.Cryptography;
using Xunit;

namespace M000.MediaCompatibility;

public sealed class MediaCompatibilityCoreTests
{
    [Theory]
    [InlineData("jpeg")]
    [InlineData("png")]
    [InlineData("heic")]
    [InlineData("mov-h264")]
    [InlineData("mov-hevc")]
    [InlineData("mp4-h264")]
    [InlineData("mp4-hevc")]
    [InlineData("dng")]
    public async Task Preservation_copy_is_byte_identical_for_every_fixture(string key)
    {
        var fixture = FixtureCatalog.Get(key);
        var originalHash = Convert.ToHexString(SHA256.HashData(fixture.Bytes));
        var root = Path.Combine(Path.GetTempPath(), "PhotoArchive-M000-Media", Guid.NewGuid().ToString("N"));
        var destination = Path.Combine(root, fixture.FileName);

        try
        {
            var result = await BytePreservingArchiver.CopyAndVerifyAsync(fixture.Bytes, destination);
            Assert.Equal(originalHash, result.SourceSha256);
            Assert.Equal(originalHash, result.DestinationSha256);
            Assert.Equal(fixture.Bytes.LongLength, result.ByteLength);
            Assert.Equal(fixture.Bytes, await File.ReadAllBytesAsync(destination));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Jpeg_metadata_fixture_extracts_dimensions_orientation_date_and_location()
    {
        var result = MediaProbe.Probe(FixtureCatalog.Get("jpeg").Bytes);

        Assert.Equal("JPEG", result.Container);
        Assert.Equal(4032, result.Width);
        Assert.Equal(3024, result.Height);
        Assert.Equal(6, result.Orientation);
        Assert.Equal("2026:09:30 10:15:00", result.CaptureDate);
        Assert.Equal(41d, result.Latitude);
        Assert.Equal(29d, result.Longitude);
    }

    [Fact]
    public void Png_fixture_extracts_dimensions()
    {
        var result = MediaProbe.Probe(FixtureCatalog.Get("png").Bytes);
        Assert.Equal("PNG", result.Container);
        Assert.Equal(640, result.Width);
        Assert.Equal(480, result.Height);
    }

    [Fact]
    public void Heic_structure_fixture_extracts_heif_dimensions_and_codec_family()
    {
        var result = MediaProbe.Probe(FixtureCatalog.Get("heic").Bytes);
        Assert.Equal("HEIF", result.Container);
        Assert.Equal("HEVC/H.265", result.Codec);
        Assert.Equal(3024, result.Width);
        Assert.Equal(4032, result.Height);
    }

    [Theory]
    [InlineData("mov-h264", "QuickTime/MOV", "H.264/AVC")]
    [InlineData("mov-hevc", "QuickTime/MOV", "HEVC/H.265")]
    [InlineData("mp4-h264", "MP4", "H.264/AVC")]
    [InlineData("mp4-hevc", "MP4", "HEVC/H.265")]
    public void Video_structure_fixtures_extract_container_codec_dimensions_duration_and_capture_date(
        string key,
        string expectedContainer,
        string expectedCodec)
    {
        var result = MediaProbe.Probe(FixtureCatalog.Get(key).Bytes);

        Assert.Equal(expectedContainer, result.Container);
        Assert.Equal(expectedCodec, result.Codec);
        Assert.Equal(1920, result.Width);
        Assert.Equal(1080, result.Height);
        Assert.Equal(TimeSpan.FromSeconds(5), result.Duration);
        Assert.StartsWith("2026-09-30T10:15:00", result.CaptureDate, StringComparison.Ordinal);
    }

    [Fact]
    public void Dng_structure_fixture_extracts_core_tiff_dng_metadata_without_rendering()
    {
        var result = MediaProbe.Probe(FixtureCatalog.Get("dng").Bytes);

        Assert.Equal("DNG", result.Container);
        Assert.Equal(4032, result.Width);
        Assert.Equal(3024, result.Height);
        Assert.Equal(1, result.Orientation);
        Assert.Equal("2026:09:30 10:15:00", result.CaptureDate);
        Assert.Equal("1.6.0.0", result.DngVersion);
    }

    [Fact]
    public void Live_photo_manifest_keeps_still_and_motion_as_one_logical_asset_with_two_resources()
    {
        var (still, motion) = FixtureCatalog.CreateLivePhotoResources();
        var manifest = LivePhotoManifestFactory.Create(still, motion);
        var restored = LivePhotoManifestFactory.RoundTripJson(manifest);

        Assert.Equal("live-photo-fixture-001", restored.AssetId);
        Assert.Collection(
            restored.Resources,
            resource =>
            {
                Assert.Equal("OriginalPhoto", resource.Role);
                Assert.EndsWith("IMG_0001.HEIC", resource.RelativePath, StringComparison.Ordinal);
                Assert.Equal(still.Bytes.LongLength, resource.ByteLength);
                Assert.Equal(Convert.ToHexString(SHA256.HashData(still.Bytes)), resource.Sha256);
            },
            resource =>
            {
                Assert.Equal("LivePhotoMotion", resource.Role);
                Assert.EndsWith("IMG_0001.MOV", resource.RelativePath, StringComparison.Ordinal);
                Assert.Equal(motion.Bytes.LongLength, resource.ByteLength);
                Assert.Equal(Convert.ToHexString(SHA256.HashData(motion.Bytes)), resource.Sha256);
            });
    }

    [Fact]
    public void Unsupported_heic_preview_never_blocks_preservation()
    {
        var decision = ViewingPolicy.Evaluate(
            FixtureCatalog.Get("heic"),
            new DecoderInventory(
                JpegPngWic: true,
                HeifWic: false,
                DngWic: false,
                H264MediaFoundation: true,
                HevcMediaFoundation: false));

        Assert.True(decision.PreservationAllowed);
        Assert.Equal(CapabilityStatus.Limited, decision.Thumbnail);
        Assert.Equal(CapabilityStatus.Limited, decision.Preview);
        Assert.Equal(CapabilityStatus.NotApplicable, decision.Playback);
    }

    [Fact]
    public void Unsupported_hevc_playback_never_blocks_preservation()
    {
        var decision = ViewingPolicy.Evaluate(
            FixtureCatalog.Get("mov-hevc"),
            new DecoderInventory(
                JpegPngWic: true,
                HeifWic: false,
                DngWic: false,
                H264MediaFoundation: true,
                HevcMediaFoundation: false));

        Assert.True(decision.PreservationAllowed);
        Assert.Equal(CapabilityStatus.Limited, decision.Thumbnail);
        Assert.Equal(CapabilityStatus.Limited, decision.Preview);
        Assert.Equal(CapabilityStatus.Limited, decision.Playback);
    }
}
