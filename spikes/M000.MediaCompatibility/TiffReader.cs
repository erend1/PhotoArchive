using System.Buffers.Binary;
using System.Text;

namespace M000.MediaCompatibility;

internal sealed record TiffMetadata(
    int? Width,
    int? Height,
    string? CaptureDate,
    int? Orientation,
    double? Latitude,
    double? Longitude,
    string? DngVersion);

internal sealed class TiffReader
{
    private readonly byte[] _data;
    private readonly bool _littleEndian;

    private TiffReader(byte[] data)
    {
        _data = data;
        if (data.Length < 8)
        {
            throw new InvalidDataException("TIFF data is too short.");
        }

        _littleEndian = data[0] == (byte)'I' && data[1] == (byte)'I';
        var bigEndian = data[0] == (byte)'M' && data[1] == (byte)'M';
        if (!_littleEndian && !bigEndian)
        {
            throw new InvalidDataException("TIFF byte order is invalid.");
        }

        if (ReadUInt16(2) != 42)
        {
            throw new InvalidDataException("TIFF magic value is invalid.");
        }
    }

    public static TiffMetadata Read(byte[] data) => new TiffReader(data).ReadMetadata();

    private TiffMetadata ReadMetadata()
    {
        int? width = null;
        int? height = null;
        int? orientation = null;
        string? captureDate = null;
        string? dngVersion = null;
        double? latitude = null;
        double? longitude = null;
        uint? exifIfdOffset = null;
        uint? gpsIfdOffset = null;

        var ifd0 = checked((int)ReadUInt32(4));
        foreach (var entry in EnumerateIfd(ifd0))
        {
            switch (entry.Tag)
            {
                case 0x0100:
                    width = checked((int)ReadUnsignedScalar(entry));
                    break;
                case 0x0101:
                    height = checked((int)ReadUnsignedScalar(entry));
                    break;
                case 0x0112:
                    orientation = checked((int)ReadUnsignedScalar(entry));
                    break;
                case 0x0132:
                    captureDate = ReadAscii(entry);
                    break;
                case 0x8769:
                    exifIfdOffset = ReadUnsignedScalar(entry);
                    break;
                case 0x8825:
                    gpsIfdOffset = ReadUnsignedScalar(entry);
                    break;
                case 0xC612:
                    dngVersion = ReadDngVersion(entry);
                    break;
            }
        }

        if (exifIfdOffset is not null)
        {
            foreach (var entry in EnumerateIfd(checked((int)exifIfdOffset.Value)))
            {
                if (entry.Tag is 0x9003 or 0x9004)
                {
                    captureDate = ReadAscii(entry);
                    break;
                }
            }
        }

        if (gpsIfdOffset is not null)
        {
            string? latitudeRef = null;
            string? longitudeRef = null;
            double? latitudeValue = null;
            double? longitudeValue = null;

            foreach (var entry in EnumerateIfd(checked((int)gpsIfdOffset.Value)))
            {
                switch (entry.Tag)
                {
                    case 0x0001:
                        latitudeRef = ReadAscii(entry);
                        break;
                    case 0x0002:
                        latitudeValue = ReadGpsCoordinate(entry);
                        break;
                    case 0x0003:
                        longitudeRef = ReadAscii(entry);
                        break;
                    case 0x0004:
                        longitudeValue = ReadGpsCoordinate(entry);
                        break;
                }
            }

            if (latitudeValue is not null)
            {
                latitude = string.Equals(latitudeRef, "S", StringComparison.OrdinalIgnoreCase)
                    ? -latitudeValue.Value
                    : latitudeValue.Value;
            }

            if (longitudeValue is not null)
            {
                longitude = string.Equals(longitudeRef, "W", StringComparison.OrdinalIgnoreCase)
                    ? -longitudeValue.Value
                    : longitudeValue.Value;
            }
        }

        return new(width, height, captureDate, orientation, latitude, longitude, dngVersion);
    }

    private IEnumerable<TiffEntry> EnumerateIfd(int offset)
    {
        EnsureRange(offset, 2);
        var count = ReadUInt16(offset);
        var entriesStart = offset + 2;
        EnsureRange(entriesStart, count * 12 + 4);

        for (var index = 0; index < count; index++)
        {
            var entryOffset = entriesStart + index * 12;
            yield return new TiffEntry(
                ReadUInt16(entryOffset),
                ReadUInt16(entryOffset + 2),
                ReadUInt32(entryOffset + 4),
                entryOffset);
        }
    }

    private uint ReadUnsignedScalar(TiffEntry entry)
    {
        if (entry.Count != 1)
        {
            throw new InvalidDataException($"Tag 0x{entry.Tag:X4} is not scalar.");
        }

        return entry.Type switch
        {
            3 => ReadUInt16(entry.EntryOffset + 8),
            4 => ReadUInt32(entry.EntryOffset + 8),
            _ => throw new InvalidDataException($"Unsupported scalar TIFF type {entry.Type}.")
        };
    }

    private string ReadAscii(TiffEntry entry)
    {
        if (entry.Type != 2 || entry.Count == 0 || entry.Count > int.MaxValue)
        {
            throw new InvalidDataException("Invalid TIFF ASCII entry.");
        }

        var length = checked((int)entry.Count);
        var start = length <= 4 ? entry.EntryOffset + 8 : checked((int)ReadUInt32(entry.EntryOffset + 8));
        EnsureRange(start, length);
        return Encoding.ASCII.GetString(_data, start, length).TrimEnd('\0').Trim();
    }

    private string ReadDngVersion(TiffEntry entry)
    {
        if (entry.Type != 1 || entry.Count != 4)
        {
            throw new InvalidDataException("Invalid DNGVersion entry.");
        }

        EnsureRange(entry.EntryOffset + 8, 4);
        return string.Join('.', _data.AsSpan(entry.EntryOffset + 8, 4).ToArray());
    }

    private double ReadGpsCoordinate(TiffEntry entry)
    {
        if (entry.Type != 5 || entry.Count != 3)
        {
            throw new InvalidDataException("Invalid GPS coordinate entry.");
        }

        var offset = checked((int)ReadUInt32(entry.EntryOffset + 8));
        EnsureRange(offset, 24);
        var degrees = ReadRational(offset);
        var minutes = ReadRational(offset + 8);
        var seconds = ReadRational(offset + 16);
        return degrees + minutes / 60d + seconds / 3600d;
    }

    private double ReadRational(int offset)
    {
        var numerator = ReadUInt32(offset);
        var denominator = ReadUInt32(offset + 4);
        if (denominator == 0)
        {
            throw new InvalidDataException("TIFF rational denominator is zero.");
        }

        return numerator / (double)denominator;
    }

    private ushort ReadUInt16(int offset)
    {
        EnsureRange(offset, 2);
        return _littleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(offset, 2))
            : BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(offset, 2));
    }

    private uint ReadUInt32(int offset)
    {
        EnsureRange(offset, 4);
        return _littleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(offset, 4))
            : BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(offset, 4));
    }

    private void EnsureRange(int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > _data.Length - count)
        {
            throw new InvalidDataException("TIFF offset is outside the available bytes.");
        }
    }

    private sealed record TiffEntry(ushort Tag, ushort Type, uint Count, int EntryOffset);
}
