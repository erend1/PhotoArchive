using System.Text;
using PhotoArchive.Spikes.AppleDevice.Analysis;

namespace PhotoArchive.Spikes.AppleDevice.Tests;

public sealed class MediaSignatureTests
{
    public static TheoryData<byte[], string> Headers => new()
    {
        { [0xFF, 0xD8, 0xFF, 0xE1, 0, 0, 0, 0], nameof(SniffedFormat.Jpeg) },
        { [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0], nameof(SniffedFormat.Png) },
        { Encoding.ASCII.GetBytes("GIF89a......"), nameof(SniffedFormat.Gif) },
        { [(byte)'I', (byte)'I', 0x2A, 0x00, 8, 0, 0, 0], nameof(SniffedFormat.Tiff) },
        { [(byte)'M', (byte)'M', 0x00, 0x2A, 0, 0, 0, 8], nameof(SniffedFormat.Tiff) },
        { Ftyp("heic"), nameof(SniffedFormat.Heif) },
        { Ftyp("mif1"), nameof(SniffedFormat.Heif) },
        { Ftyp("avif"), nameof(SniffedFormat.Avif) },
        { Ftyp("qt  "), nameof(SniffedFormat.QuickTime) },
        { Ftyp("isom"), nameof(SniffedFormat.Mp4) },
        { Ftyp("3gp5"), nameof(SniffedFormat.ThreeGp) },
        { Ftyp("zzzz"), nameof(SniffedFormat.IsoBmffOther) },
        { Atom("wide"), nameof(SniffedFormat.QuickTime) },
        { Atom("moov"), nameof(SniffedFormat.QuickTime) },
        { Encoding.ASCII.GetBytes("<?xml version=\"1.0\"?>"), nameof(SniffedFormat.Xml) },
        { Encoding.ASCII.GetBytes("bplist00........"), nameof(SniffedFormat.BinaryPlist) },
        { [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12], nameof(SniffedFormat.Unknown) },
        { [], nameof(SniffedFormat.Unknown) },
    };

    [Theory]
    [MemberData(nameof(Headers))]
    public void Sniff_identifies_containers(byte[] header, string expected) => Assert.Equal(Enum.Parse<SniffedFormat>(expected), MediaSignature.Sniff(header));

    [Theory]
    [InlineData("HEIC", nameof(SniffedFormat.Heif), true)]
    [InlineData(".heic", nameof(SniffedFormat.Jpeg), false)] // converted on the fly -> must be flagged
    [InlineData("JPG", nameof(SniffedFormat.Jpeg), true)]
    [InlineData("MOV", nameof(SniffedFormat.QuickTime), true)]
    [InlineData("MOV", nameof(SniffedFormat.Mp4), true)]
    [InlineData("MOV", nameof(SniffedFormat.Jpeg), false)]
    [InlineData("DNG", nameof(SniffedFormat.Tiff), true)]
    [InlineData("AAE", nameof(SniffedFormat.Xml), true)]
    [InlineData("PNG", nameof(SniffedFormat.Heif), false)]
    public void MatchesExtension_flags_contradictions(string extension, string format, bool expected) =>
        Assert.Equal(expected, MediaSignature.MatchesExtension(extension, Enum.Parse<SniffedFormat>(format)));

    [Fact]
    public void MatchesExtension_is_unknown_for_unrecognized_extensions() => Assert.Null(MediaSignature.MatchesExtension("XYZ", SniffedFormat.Jpeg));

    private static byte[] Ftyp(string brand) => [0, 0, 0, 0x18, .. Encoding.ASCII.GetBytes("ftyp"), .. Encoding.ASCII.GetBytes(brand), 0, 0, 0, 0];

    private static byte[] Atom(string type) => [0, 0, 0, 8, .. Encoding.ASCII.GetBytes(type), 0, 0, 0, 0];
}
