using System.Buffers.Binary;
using System.Text;
using PhotoArchive.Spikes.AppleDevice.Analysis;

namespace PhotoArchive.Spikes.AppleDevice.Tests;

public sealed class EmbeddedMetadataScannerTests
{
    private const string Uuid = "3F2D9B8E-1C4A-4E6B-9F00-0123456789AB";

    [Fact]
    public void Detects_exif_gps_datetime_and_apple_makernote_presence()
    {
        var jpeg = SyntheticJpegWithExif(includeGps: true);

        var result = EmbeddedMetadataScanner.Scan(jpeg);

        Assert.True(result.ExifFound);
        Assert.True(result.GpsIfdPresent);
        Assert.True(result.GpsLatitudePresent);
        Assert.True(result.DateTimeOriginalPresent);
        Assert.True(result.AppleMakerNotePresent);
        Assert.Contains(Uuid, result.UuidTokens);
    }

    [Fact]
    public void Absent_gps_is_reported_as_absent()
    {
        var result = EmbeddedMetadataScanner.Scan(SyntheticJpegWithExif(includeGps: false));

        Assert.True(result.ExifFound);
        Assert.False(result.GpsIfdPresent);
        Assert.False(result.GpsLatitudePresent);
    }

    [Fact]
    public void Detects_quicktime_location_and_live_photo_content_identifier_key()
    {
        var mov = Encoding.ASCII.GetBytes(
            "....ftypqt  ....moov....keys....mdtacom.apple.quicktime.content.identifier" +
            "....mdtacom.apple.quicktime.location.ISO6709....ilst....data" + Uuid + "....+41.0000+029.0000/");

        var result = EmbeddedMetadataScanner.Scan(mov);

        Assert.True(result.ContentIdentifierKeyPresent);
        Assert.True(result.QuickTimeLocationPresent);
        Assert.False(result.ExifFound);
        Assert.Contains(Uuid, result.UuidTokens);
    }

    [Fact]
    public void Uuid_tokens_require_exact_shape_regardless_of_neighbouring_bytes()
    {
        const string other = "00000000-1111-2222-3333-444444444444";
        var data = Encoding.ASCII.GetBytes($"x{Uuid}y a{other}f {Uuid.Replace('-', '_')} {Uuid[..35]}");

        var tokens = EmbeddedMetadataScanner.FindUuidTokens(data);

        // Both well-formed tokens are found even when adjacent bytes are hex-like; underscores and truncation are not.
        Assert.Equal(new HashSet<string> { Uuid, other }, tokens);
    }

    [Fact]
    public void Garbage_does_not_throw()
    {
        var random = new byte[4096];
        new Random(7).NextBytes(random);
        var withMarker = Encoding.ASCII.GetBytes("Exif\0\0MM\0*\0\0\0ÿ").Concat(random).ToArray();

        var result = EmbeddedMetadataScanner.Scan(withMarker);

        Assert.NotNull(result);
    }

    /// <summary>Builds a JPEG whose APP1 contains a big-endian TIFF: IFD0 -> Exif IFD (DateTimeOriginal, MakerNote) and optional GPS IFD.</summary>
    internal static byte[] SyntheticJpegWithExif(bool includeGps)
    {
        var tiff = new byte[512];
        var span = tiff.AsSpan();
        "MM"u8.CopyTo(span);
        BinaryPrimitives.WriteUInt16BigEndian(span[2..], 0x2A);
        BinaryPrimitives.WriteUInt32BigEndian(span[4..], 8);

        const int exifIfd = 64, gpsIfd = 128, dateAt = 192, makerAt = 224, latAt = 320;
        var ifd0Entries = new List<(ushort Tag, ushort Type, uint Count, uint Value)> { (0x8769, 4, 1, exifIfd) };
        if (includeGps)
        {
            ifd0Entries.Add((0x8825, 4, 1, gpsIfd));
        }

        WriteIfd(span, 8, ifd0Entries);
        WriteIfd(span, exifIfd, [(0x9003, 2, 20, dateAt), (0x927C, 7, 64, makerAt)]);
        Encoding.ASCII.GetBytes("2026:09:14 17:45:03\0").CopyTo(span[dateAt..]);
        Encoding.ASCII.GetBytes("Apple iOS\0\0\u0001MM" + Uuid).CopyTo(span[makerAt..]);
        if (includeGps)
        {
            WriteIfd(span, gpsIfd, [(0x0002, 5, 3, latAt)]);
        }

        var app1 = Encoding.ASCII.GetBytes("Exif\0\0").Concat(tiff).ToArray();
        var segmentLength = (ushort)(app1.Length + 2);
        return [0xFF, 0xD8, 0xFF, 0xE1, (byte)(segmentLength >> 8), (byte)segmentLength, .. app1, 0xFF, 0xD9];
    }

    private static void WriteIfd(Span<byte> tiff, int offset, List<(ushort Tag, ushort Type, uint Count, uint Value)> entries)
    {
        BinaryPrimitives.WriteUInt16BigEndian(tiff[offset..], (ushort)entries.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            var at = offset + 2 + (i * 12);
            BinaryPrimitives.WriteUInt16BigEndian(tiff[at..], entries[i].Tag);
            BinaryPrimitives.WriteUInt16BigEndian(tiff[(at + 2)..], entries[i].Type);
            BinaryPrimitives.WriteUInt32BigEndian(tiff[(at + 4)..], entries[i].Count);
            BinaryPrimitives.WriteUInt32BigEndian(tiff[(at + 8)..], entries[i].Value);
        }
    }
}
