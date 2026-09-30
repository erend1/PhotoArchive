using System.Security.Cryptography;
using Windows.Graphics.Imaging;
using Windows.Media.Core;
using Windows.Media.Editing;
using Windows.Storage;
using Xunit;

namespace M000.MediaCompatibility;

public sealed class MediaCompatibilityWindowsTests
{
    [Theory]
    [InlineData("fixture.jpg", 64, 48)]
    [InlineData("fixture.png", 64, 48)]
    public async Task Windows_bitmap_decoder_can_decode_generated_jpeg_and_png_and_produce_scaled_pixels(
        string fileName,
        uint expectedWidth,
        uint expectedHeight)
    {
        Assert.True(OperatingSystem.IsWindows());
        var path = GeneratedFixturePath(fileName);
        var file = await StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(stream);

        Assert.Equal(expectedWidth, decoder.PixelWidth);
        Assert.Equal(expectedHeight, decoder.PixelHeight);

        var transform = new BitmapTransform
        {
            ScaledWidth = 32,
            ScaledHeight = 24,
            InterpolationMode = BitmapInterpolationMode.Fant
        };
        var pixelData = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            transform,
            ExifOrientationMode.RespectExifOrientation,
            ColorManagementMode.DoNotColorManage);

        Assert.Equal(32 * 24 * 4, pixelData.DetachPixelData().Length);
    }

    [Theory]
    [InlineData("fixture-h264.mov")]
    [InlineData("fixture-h264.mp4")]
    public async Task Windows_media_stack_can_open_generated_h264_video(string fileName)
    {
        Assert.True(OperatingSystem.IsWindows());
        var file = await StorageFile.GetFileFromPathAsync(GeneratedFixturePath(fileName));
        var clip = await MediaClip.CreateFromFileAsync(file);

        Assert.True(clip.OriginalDuration > TimeSpan.Zero);
        var composition = new MediaComposition();
        composition.Clips.Add(clip);
        using var thumbnail = await composition.GetThumbnailAsync(
            TimeSpan.FromMilliseconds(200),
            32,
            24,
            VideoFramePrecision.NearestFrame);
        Assert.True(thumbnail.Size > 0);
        Console.WriteLine($"EVIDENCE H264_OPEN PASS file={fileName} duration={clip.OriginalDuration} thumbnailBytes={thumbnail.Size}");
    }

    [Fact]
    public async Task Windows_codec_inventory_reports_h264_and_records_hevc_without_assuming_it_exists()
    {
        Assert.True(OperatingSystem.IsWindows());
        var query = new CodecQuery();
        var h264 = await query.FindAllAsync(CodecKind.Video, CodecCategory.Decoder, CodecSubtypes.VideoFormatH264);
        var hevc = await query.FindAllAsync(CodecKind.Video, CodecCategory.Decoder, CodecSubtypes.VideoFormatH265);

        Assert.NotEmpty(h264);
        Console.WriteLine($"EVIDENCE CODEC_INVENTORY H264_DECODERS={h264.Count} HEVC_DECODERS={hevc.Count}");
        foreach (var codec in hevc)
        {
            Console.WriteLine($"EVIDENCE HEVC_DECODER name={codec.DisplayName} trusted={codec.IsTrusted}");
        }
    }

    [Fact]
    public void Windows_wic_inventory_records_optional_heif_and_dng_decoders()
    {
        Assert.True(OperatingSystem.IsWindows());
        var decoders = BitmapDecoder.GetDecoderInformationEnumerator().ToArray();
        var heif = decoders.Where(codec => codec.FileExtensions.Any(extension =>
            extension.Equals(".heic", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".heif", StringComparison.OrdinalIgnoreCase))).ToArray();
        var dng = decoders.Where(codec => codec.FileExtensions.Any(extension =>
            extension.Equals(".dng", StringComparison.OrdinalIgnoreCase))).ToArray();

        Console.WriteLine($"EVIDENCE WIC_INVENTORY DECODERS={decoders.Length} HEIF_DECODERS={heif.Length} DNG_DECODERS={dng.Length}");
        foreach (var codec in heif.Concat(dng).DistinctBy(codec => codec.CodecId))
        {
            Console.WriteLine($"EVIDENCE WIC_DECODER name={codec.FriendlyName} extensions={string.Join(',', codec.FileExtensions)}");
        }
    }

    [Theory]
    [InlineData("fixture-hevc.mov")]
    [InlineData("fixture-hevc.mp4")]
    public async Task Generated_hevc_video_is_preserved_even_if_windows_cannot_open_it(string fileName)
    {
        Assert.True(OperatingSystem.IsWindows());
        var sourcePath = GeneratedFixturePath(fileName);
        var sourceBytes = await File.ReadAllBytesAsync(sourcePath);
        var destination = Path.Combine(Path.GetTempPath(), "PhotoArchive-M000-Media", Guid.NewGuid().ToString("N"), fileName);

        try
        {
            var verification = await BytePreservingArchiver.CopyAndVerifyAsync(sourceBytes, destination);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(sourceBytes)), verification.DestinationSha256);

            try
            {
                var file = await StorageFile.GetFileFromPathAsync(sourcePath);
                var clip = await MediaClip.CreateFromFileAsync(file);
                Assert.True(clip.OriginalDuration > TimeSpan.Zero);
                var composition = new MediaComposition();
                composition.Clips.Add(clip);
                using var thumbnail = await composition.GetThumbnailAsync(
                    TimeSpan.FromMilliseconds(200),
                    32,
                    24,
                    VideoFramePrecision.NearestFrame);
                Assert.True(thumbnail.Size > 0);
                Console.WriteLine($"EVIDENCE HEVC_OPEN PASS file={fileName} duration={clip.OriginalDuration} thumbnailBytes={thumbnail.Size}");
            }
            catch (Exception exception)
            {
                Console.WriteLine($"EVIDENCE HEVC_OPEN LIMITED file={fileName} exception={exception.GetType().Name} hresult=0x{exception.HResult:X8}");
                var decision = ViewingPolicy.Evaluate(
                    FixtureCatalog.Get(fileName.EndsWith(".mov", StringComparison.OrdinalIgnoreCase) ? "mov-hevc" : "mp4-hevc"),
                    new DecoderInventory(true, false, false, true, false));
                Assert.True(decision.PreservationAllowed);
                Assert.Equal(CapabilityStatus.Limited, decision.Playback);
            }
        }
        finally
        {
            var directory = Path.GetDirectoryName(destination);
            if (directory is not null && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("PHOTOARCHIVE_HEIC_FIXTURE", "HEIC")]
    [InlineData("PHOTOARCHIVE_DNG_FIXTURE", "DNG/RAW")]
    public async Task Optional_local_still_fixture_exercises_installed_windows_decoder_when_supplied(
        string environmentVariable,
        string label)
    {
        Assert.True(OperatingSystem.IsWindows());
        var path = Environment.GetEnvironmentVariable(environmentVariable);
        if (string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine($"EVIDENCE {label}_LOCAL_DECODE NOT_RUN env={environmentVariable}");
            return;
        }

        Assert.True(File.Exists(path), $"{environmentVariable} points to a missing file: {path}");
        var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(path));
        using var stream = await file.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var targetWidth = Math.Max(1u, Math.Min(64u, decoder.PixelWidth));
        var targetHeight = Math.Max(1u, Math.Min(64u, decoder.PixelHeight));
        var transform = new BitmapTransform { ScaledWidth = targetWidth, ScaledHeight = targetHeight };
        var pixels = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            transform,
            ExifOrientationMode.RespectExifOrientation,
            ColorManagementMode.DoNotColorManage);

        Assert.NotEmpty(pixels.DetachPixelData());
        Console.WriteLine($"EVIDENCE {label}_LOCAL_DECODE PASS width={decoder.PixelWidth} height={decoder.PixelHeight}");
    }

    private static string GeneratedFixturePath(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "generated", fileName);
        Assert.True(File.Exists(path), $"Generated fixture was not copied to test output: {path}");
        return path;
    }
}
