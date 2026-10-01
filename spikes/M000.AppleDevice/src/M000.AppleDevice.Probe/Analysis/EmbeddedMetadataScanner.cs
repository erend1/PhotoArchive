using System.Buffers.Binary;
using System.Text;

namespace PhotoArchive.Spikes.AppleDevice.Analysis;

/// <summary>
/// Best-effort, presence-only scan of metadata embedded in copied media bytes. Reports whether EXIF GPS /
/// DateTimeOriginal / an Apple MakerNote / QuickTime location / the Live Photo content-identifier key exist,
/// and collects UUID-shaped tokens so a still and a motion resource can be compared for a shared identifier.
/// Values (coordinates, dates, identifiers) are never written to sanitized reports.
/// </summary>
internal static class EmbeddedMetadataScanner
{
    private static readonly byte[] ExifMarker = "Exif\0\0"u8.ToArray();
    private static readonly byte[] AppleMakerNotePrefix = "Apple iOS"u8.ToArray();
    private static readonly byte[] QuickTimeLocationKey = "com.apple.quicktime.location.ISO6709"u8.ToArray();
    private static readonly byte[] QuickTimeXyzAtom = [0xA9, (byte)'x', (byte)'y', (byte)'z'];
    private static readonly byte[] ContentIdentifierKey = "com.apple.quicktime.content.identifier"u8.ToArray();

    internal static EmbeddedMetadata Scan(ReadOnlySpan<byte> data)
    {
        var result = new EmbeddedMetadata
        {
            QuickTimeLocationPresent = data.IndexOf(QuickTimeLocationKey) >= 0 || data.IndexOf(QuickTimeXyzAtom) >= 0,
            ContentIdentifierKeyPresent = data.IndexOf(ContentIdentifierKey) >= 0,
            UuidTokens = FindUuidTokens(data),
        };

        var exif = data.IndexOf(ExifMarker);
        while (exif >= 0)
        {
            var tiffStart = exif + ExifMarker.Length;
            if (TryParseTiff(data, tiffStart, result))
            {
                result.ExifFound = true;
                break;
            }

            var next = data[(exif + 1)..].IndexOf(ExifMarker);
            exif = next < 0 ? -1 : exif + 1 + next;
        }

        return result;
    }

    internal static HashSet<string> FindUuidTokens(ReadOnlySpan<byte> data)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i + 36 <= data.Length; i++)
        {
            if (data[i + 8] != '-' || data[i + 13] != '-' || data[i + 18] != '-' || data[i + 23] != '-')
            {
                continue;
            }

            var ok = true;
            for (var j = 0; j < 36 && ok; j++)
            {
                if (j is 8 or 13 or 18 or 23)
                {
                    continue;
                }

                ok = char.IsAsciiHexDigit((char)data[i + j]);
            }

            // No boundary check: the byte before a stored identifier is arbitrary (binary atom headers, other EXIF
            // values) and may itself look like a hex digit. A spurious match only matters if it occurs in BOTH files.
            if (ok)
            {
                tokens.Add(Encoding.ASCII.GetString(data.Slice(i, 36)).ToUpperInvariant());
                i += 35;
            }
        }

        return tokens;
    }

    private static bool TryParseTiff(ReadOnlySpan<byte> data, int start, EmbeddedMetadata result)
    {
        if (start + 8 > data.Length)
        {
            return false;
        }

        var tiff = data[start..];
        bool little;
        if (tiff[0] == 'I' && tiff[1] == 'I' && tiff[2] == 0x2A && tiff[3] == 0)
        {
            little = true;
        }
        else if (tiff[0] == 'M' && tiff[1] == 'M' && tiff[2] == 0 && tiff[3] == 0x2A)
        {
            little = false;
        }
        else
        {
            return false;
        }

        var ifd0 = ReadU32(tiff, 4, little);
        foreach (var (tag, _, _, value) in ReadIfd(tiff, ifd0, little))
        {
            if (tag == 0x8825)
            {
                // GPS IFD pointer. Count GPS entries to distinguish "pointer present" from "coordinates present".
                var gps = ReadIfd(tiff, value, little).ToList();
                result.GpsIfdPresent = gps.Count > 0;
                result.GpsLatitudePresent = gps.Any(e => e.Tag == 0x0002);
            }
            else if (tag == 0x8769)
            {
                foreach (var (exifTag, _, count, exifValue) in ReadIfd(tiff, value, little))
                {
                    if (exifTag == 0x9003)
                    {
                        result.DateTimeOriginalPresent = true;
                    }
                    else if (exifTag == 0x9011)
                    {
                        result.OffsetTimeOriginalPresent = true;
                    }
                    else if (exifTag == 0x927C && exifValue + AppleMakerNotePrefix.Length <= tiff.Length && count >= AppleMakerNotePrefix.Length)
                    {
                        result.AppleMakerNotePresent = tiff.Slice((int)exifValue, AppleMakerNotePrefix.Length).SequenceEqual(AppleMakerNotePrefix);
                    }
                }
            }
        }

        return true;
    }

    private static IEnumerable<(ushort Tag, ushort Type, uint Count, uint Value)> ReadIfd(ReadOnlySpan<byte> tiff, uint offset, bool little)
    {
        var entries = new List<(ushort, ushort, uint, uint)>();
        if (offset == 0 || offset + 2 > tiff.Length)
        {
            return entries;
        }

        var count = ReadU16(tiff, (int)offset, little);
        for (var i = 0; i < count && i < 512; i++)
        {
            var at = (int)offset + 2 + (i * 12);
            if (at + 12 > tiff.Length)
            {
                break;
            }

            entries.Add((ReadU16(tiff, at, little), ReadU16(tiff, at + 2, little), ReadU32(tiff, at + 4, little), ReadU32(tiff, at + 8, little)));
        }

        return entries;
    }

    private static ushort ReadU16(ReadOnlySpan<byte> s, int at, bool little) =>
        little ? BinaryPrimitives.ReadUInt16LittleEndian(s[at..]) : BinaryPrimitives.ReadUInt16BigEndian(s[at..]);

    private static uint ReadU32(ReadOnlySpan<byte> s, int at, bool little) =>
        little ? BinaryPrimitives.ReadUInt32LittleEndian(s[at..]) : BinaryPrimitives.ReadUInt32BigEndian(s[at..]);
}

internal sealed class EmbeddedMetadata
{
    public bool ExifFound { get; set; }

    public bool GpsIfdPresent { get; set; }

    public bool GpsLatitudePresent { get; set; }

    public bool DateTimeOriginalPresent { get; set; }

    public bool OffsetTimeOriginalPresent { get; set; }

    public bool AppleMakerNotePresent { get; set; }

    public bool QuickTimeLocationPresent { get; init; }

    public bool ContentIdentifierKeyPresent { get; init; }

    public HashSet<string> UuidTokens { get; init; } = [];
}
