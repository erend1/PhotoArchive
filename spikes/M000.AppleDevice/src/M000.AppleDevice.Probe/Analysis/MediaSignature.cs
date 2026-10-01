using System.Text;

namespace PhotoArchive.Spikes.AppleDevice.Analysis;

/// <summary>
/// Identifies a container format from leading bytes so the probe can detect when the bytes delivered
/// by the device do not match the advertised name (e.g. an ".HEIC" object delivering JPEG bytes because
/// the iPhone "Transfer to Mac or PC: Automatic" setting converted it on the fly).
/// </summary>
internal static class MediaSignature
{
    internal const int RequiredHeaderLength = 16;

    private static readonly HashSet<string> HeifBrands = new(StringComparer.Ordinal)
        { "heic", "heix", "hevc", "hevx", "heim", "heis", "hevm", "hevs", "mif1", "msf1" };

    private static readonly HashSet<string> Mp4Brands = new(StringComparer.Ordinal)
        { "isom", "iso2", "iso4", "iso5", "iso6", "mp41", "mp42", "avc1", "dash", "M4V ", "M4VH", "M4VP", "M4A ", "f4v " };

    private static readonly HashSet<string> ThreeGpBrands = new(StringComparer.Ordinal)
        { "3gp4", "3gp5", "3gp6", "3g2a", "3g2b", "3g2c" };

    private static readonly HashSet<string> BareQuickTimeAtoms = new(StringComparer.Ordinal)
        { "moov", "mdat", "wide", "free", "skip", "pnot" };

    internal static SniffedFormat Sniff(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return SniffedFormat.Jpeg;
        }

        if (header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return SniffedFormat.Png;
        }

        if (header.Length >= 6 && (Ascii(header[..6]) is "GIF87a" or "GIF89a"))
        {
            return SniffedFormat.Gif;
        }

        if (header.Length >= 4 && ((header[0] == (byte)'I' && header[1] == (byte)'I' && header[2] == 0x2A && header[3] == 0x00)
                                   || (header[0] == (byte)'M' && header[1] == (byte)'M' && header[2] == 0x00 && header[3] == 0x2A)))
        {
            // TIFF container; DNG/ProRAW files are TIFF-based.
            return SniffedFormat.Tiff;
        }

        if (header.Length >= 8 && Ascii(header[..8]) == "bplist00")
        {
            return SniffedFormat.BinaryPlist;
        }

        if (header.Length >= 5 && Ascii(header[..5]) == "<?xml")
        {
            return SniffedFormat.Xml;
        }

        if (header.Length >= 12 && Ascii(header.Slice(4, 4)) == "ftyp")
        {
            var brand = Ascii(header.Slice(8, 4));
            if (HeifBrands.Contains(brand))
            {
                return SniffedFormat.Heif;
            }

            if (brand == "avif" || brand == "avis")
            {
                return SniffedFormat.Avif;
            }

            if (brand == "qt  ")
            {
                return SniffedFormat.QuickTime;
            }

            if (Mp4Brands.Contains(brand))
            {
                return SniffedFormat.Mp4;
            }

            if (ThreeGpBrands.Contains(brand))
            {
                return SniffedFormat.ThreeGp;
            }

            return SniffedFormat.IsoBmffOther;
        }

        if (header.Length >= 8 && BareQuickTimeAtoms.Contains(Ascii(header.Slice(4, 4))))
        {
            return SniffedFormat.QuickTime;
        }

        return SniffedFormat.Unknown;
    }

    /// <summary>
    /// Returns true/false when the extension has a known expected container; null when the extension is unknown.
    /// </summary>
    internal static bool? MatchesExtension(string? extension, SniffedFormat format)
    {
        var ext = (extension ?? string.Empty).TrimStart('.').ToLowerInvariant();
        SniffedFormat[]? expected = ext switch
        {
            "heic" or "heif" or "hif" => [SniffedFormat.Heif],
            "jpg" or "jpeg" => [SniffedFormat.Jpeg],
            "png" => [SniffedFormat.Png],
            "gif" => [SniffedFormat.Gif],
            "dng" or "tif" or "tiff" => [SniffedFormat.Tiff],
            "mov" => [SniffedFormat.QuickTime, SniffedFormat.Mp4],
            "mp4" or "m4v" => [SniffedFormat.Mp4, SniffedFormat.QuickTime],
            "3gp" => [SniffedFormat.ThreeGp, SniffedFormat.Mp4],
            "avif" => [SniffedFormat.Avif],
            "aae" => [SniffedFormat.Xml, SniffedFormat.BinaryPlist],
            _ => null,
        };

        return expected is null ? null : expected.Contains(format);
    }

    private static string Ascii(ReadOnlySpan<byte> bytes)
    {
        foreach (var b in bytes)
        {
            if (b < 0x20 || b > 0x7E)
            {
                return string.Empty;
            }
        }

        return Encoding.ASCII.GetString(bytes);
    }
}

internal enum SniffedFormat
{
    Unknown,
    Jpeg,
    Png,
    Gif,
    Tiff,
    Heif,
    Avif,
    QuickTime,
    Mp4,
    ThreeGp,
    IsoBmffOther,
    Xml,
    BinaryPlist,
}
