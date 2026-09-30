using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace M000.MediaCompatibility;

internal static class SyntheticMediaFactory
{
    private static readonly DateTimeOffset CaptureUtc = new(2026, 9, 30, 10, 15, 0, TimeSpan.Zero);

    public static byte[] CreateJpegMetadataFixture(int width, int height)
    {
        var tiff = CreateExifTiff();
        using var stream = new MemoryStream();
        stream.WriteByte(0xFF);
        stream.WriteByte(0xD8);

        stream.WriteByte(0xFF);
        stream.WriteByte(0xE1);
        WriteUInt16BigEndian(stream, checked((ushort)(2 + 6 + tiff.Length)));
        stream.Write("Exif\0\0"u8);
        stream.Write(tiff);

        stream.WriteByte(0xFF);
        stream.WriteByte(0xC0);
        WriteUInt16BigEndian(stream, 17);
        stream.WriteByte(8);
        WriteUInt16BigEndian(stream, checked((ushort)height));
        WriteUInt16BigEndian(stream, checked((ushort)width));
        stream.WriteByte(3);
        stream.Write([1, 0x11, 0, 2, 0x11, 0, 3, 0x11, 0]);

        stream.WriteByte(0xFF);
        stream.WriteByte(0xD9);
        return stream.ToArray();
    }

    public static byte[] CreatePng(int width, int height)
    {
        using var stream = new MemoryStream();
        stream.Write([137, 80, 78, 71, 13, 10, 26, 10]);

        var ihdr = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(0, 4), checked((uint)width));
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4, 4), checked((uint)height));
        ihdr[8] = 8;
        ihdr[9] = 2;
        WritePngChunk(stream, "IHDR", ihdr);

        var raw = new byte[height * (1 + width * 3)];
        for (var row = 0; row < height; row++)
        {
            raw[row * (1 + width * 3)] = 0;
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        WritePngChunk(stream, "IDAT", compressed.ToArray());
        WritePngChunk(stream, "IEND", []);
        return stream.ToArray();
    }

    public static byte[] CreateHeicStructureFixture(int width, int height)
    {
        var ftyp = CreateFileTypeBox("heic", "mif1");
        var ispePayload = new byte[12];
        BinaryPrimitives.WriteUInt32BigEndian(ispePayload.AsSpan(4, 4), checked((uint)width));
        BinaryPrimitives.WriteUInt32BigEndian(ispePayload.AsSpan(8, 4), checked((uint)height));
        var ispe = Box("ispe", ispePayload);
        var ipco = Box("ipco", ispe);
        var iprp = Box("iprp", ipco);
        var metaPayload = Combine(new byte[4], iprp);
        var meta = Box("meta", metaPayload);
        return Combine(ftyp, meta);
    }

    public static byte[] CreateVideoStructureFixture(
        string majorBrand,
        string sampleEntry,
        int width,
        int height,
        TimeSpan duration)
    {
        var ftyp = CreateFileTypeBox(majorBrand, majorBrand == "qt  " ? "qt  " : "mp42");

        var mvhdPayload = new byte[20];
        var epoch = new DateTimeOffset(1904, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var creationSeconds = checked((uint)(CaptureUtc - epoch).TotalSeconds);
        BinaryPrimitives.WriteUInt32BigEndian(mvhdPayload.AsSpan(4, 4), creationSeconds);
        BinaryPrimitives.WriteUInt32BigEndian(mvhdPayload.AsSpan(8, 4), creationSeconds);
        BinaryPrimitives.WriteUInt32BigEndian(mvhdPayload.AsSpan(12, 4), 1000);
        BinaryPrimitives.WriteUInt32BigEndian(mvhdPayload.AsSpan(16, 4), checked((uint)duration.TotalMilliseconds));
        var mvhd = Box("mvhd", mvhdPayload);

        var sampleBody = new byte[78];
        BinaryPrimitives.WriteUInt16BigEndian(sampleBody.AsSpan(6, 2), 1);
        BinaryPrimitives.WriteUInt16BigEndian(sampleBody.AsSpan(24, 2), checked((ushort)width));
        BinaryPrimitives.WriteUInt16BigEndian(sampleBody.AsSpan(26, 2), checked((ushort)height));
        BinaryPrimitives.WriteUInt32BigEndian(sampleBody.AsSpan(28, 4), 0x00480000);
        BinaryPrimitives.WriteUInt32BigEndian(sampleBody.AsSpan(32, 4), 0x00480000);
        BinaryPrimitives.WriteUInt16BigEndian(sampleBody.AsSpan(40, 2), 1);
        BinaryPrimitives.WriteUInt16BigEndian(sampleBody.AsSpan(74, 2), 0x0018);
        BinaryPrimitives.WriteUInt16BigEndian(sampleBody.AsSpan(76, 2), 0xFFFF);
        var sample = Box(sampleEntry, sampleBody);

        var stsdPayload = Combine(new byte[4], UInt32BigEndianBytes(1), sample);
        var stsd = Box("stsd", stsdPayload);
        var stbl = Box("stbl", stsd);
        var minf = Box("minf", stbl);
        var mdia = Box("mdia", minf);
        var trak = Box("trak", mdia);
        var moov = Box("moov", Combine(mvhd, trak));
        return Combine(ftyp, moov);
    }

    public static byte[] CreateDngStructureFixture(int width, int height)
    {
        const int ifdOffset = 8;
        const ushort entryCount = 5;
        var ifdSize = 2 + entryCount * 12 + 4;
        var dateOffset = ifdOffset + ifdSize;
        var dateBytes = Encoding.ASCII.GetBytes("2026:09:30 10:15:00\0");
        var data = new byte[dateOffset + dateBytes.Length];

        data[0] = (byte)'I';
        data[1] = (byte)'I';
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(2, 2), 42);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4, 4), ifdOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(ifdOffset, 2), entryCount);

        var entry = ifdOffset + 2;
        WriteIfdEntryLittleEndian(data, entry, 0x0100, 4, 1, checked((uint)width));
        entry += 12;
        WriteIfdEntryLittleEndian(data, entry, 0x0101, 4, 1, checked((uint)height));
        entry += 12;
        WriteIfdEntryLittleEndian(data, entry, 0x0112, 3, 1, 1);
        entry += 12;
        WriteIfdEntryLittleEndian(data, entry, 0x0132, 2, checked((uint)dateBytes.Length), checked((uint)dateOffset));
        entry += 12;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(entry, 2), 0xC612);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(entry + 2, 2), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(entry + 4, 4), 4);
        data[entry + 8] = 1;
        data[entry + 9] = 6;
        data[entry + 10] = 0;
        data[entry + 11] = 0;

        dateBytes.CopyTo(data.AsSpan(dateOffset));
        return data;
    }

    private static byte[] CreateExifTiff()
    {
        var data = new byte[190];
        data[0] = (byte)'I';
        data[1] = (byte)'I';
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(2, 2), 42);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4, 4), 8);

        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(8, 2), 3);
        WriteIfdEntryLittleEndian(data, 10, 0x0112, 3, 1, 6);
        WriteIfdEntryLittleEndian(data, 22, 0x8769, 4, 1, 50);
        WriteIfdEntryLittleEndian(data, 34, 0x8825, 4, 1, 68);

        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(50, 2), 1);
        WriteIfdEntryLittleEndian(data, 52, 0x9003, 2, 20, 122);

        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(68, 2), 4);
        WriteIfdAsciiInline(data, 70, 0x0001, "N");
        WriteIfdEntryLittleEndian(data, 82, 0x0002, 5, 3, 142);
        WriteIfdAsciiInline(data, 94, 0x0003, "E");
        WriteIfdEntryLittleEndian(data, 106, 0x0004, 5, 3, 166);

        Encoding.ASCII.GetBytes("2026:09:30 10:15:00\0").CopyTo(data.AsSpan(122));
        WriteRationalLittleEndian(data, 142, 41, 1);
        WriteRationalLittleEndian(data, 150, 0, 1);
        WriteRationalLittleEndian(data, 158, 0, 1);
        WriteRationalLittleEndian(data, 166, 29, 1);
        WriteRationalLittleEndian(data, 174, 0, 1);
        WriteRationalLittleEndian(data, 182, 0, 1);
        return data;
    }

    private static void WriteIfdEntryLittleEndian(
        byte[] data,
        int offset,
        ushort tag,
        ushort type,
        uint count,
        uint valueOrOffset)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset, 2), tag);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + 2, 2), type);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + 4, 4), count);

        if (type == 3 && count == 1)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + 8, 2), checked((ushort)valueOrOffset));
        }
        else
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + 8, 4), valueOrOffset);
        }
    }

    private static void WriteIfdAsciiInline(byte[] data, int offset, ushort tag, string value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset, 2), tag);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + 2, 2), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + 4, 4), 2);
        data[offset + 8] = checked((byte)value[0]);
        data[offset + 9] = 0;
    }

    private static void WriteRationalLittleEndian(byte[] data, int offset, uint numerator, uint denominator)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, 4), numerator);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + 4, 4), denominator);
    }

    private static byte[] CreateFileTypeBox(string majorBrand, string compatibleBrand)
    {
        var payload = Combine(
            Encoding.ASCII.GetBytes(majorBrand),
            UInt32BigEndianBytes(0),
            Encoding.ASCII.GetBytes(compatibleBrand));
        return Box("ftyp", payload);
    }

    private static byte[] Box(string type, params byte[][] payloadParts)
    {
        if (type.Length != 4)
        {
            throw new ArgumentException("ISO-BMFF box type must contain exactly four ASCII characters.", nameof(type));
        }

        var payload = Combine(payloadParts);
        var box = new byte[8 + payload.Length];
        BinaryPrimitives.WriteUInt32BigEndian(box.AsSpan(0, 4), checked((uint)box.Length));
        Encoding.ASCII.GetBytes(type).CopyTo(box.AsSpan(4, 4));
        payload.CopyTo(box.AsSpan(8));
        return box;
    }

    private static byte[] Combine(params byte[][] parts)
    {
        var length = parts.Sum(part => part.Length);
        var result = new byte[length];
        var offset = 0;
        foreach (var part in parts)
        {
            part.CopyTo(result.AsSpan(offset));
            offset += part.Length;
        }
        return result;
    }

    private static byte[] UInt32BigEndianBytes(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }

    private static void WriteUInt16BigEndian(Stream stream, ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WritePngChunk(Stream stream, string type, byte[] data)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)data.Length));
        stream.Write(length);
        stream.Write(typeBytes);
        stream.Write(data);

        var crcInput = Combine(typeBytes, data);
        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, ComputeCrc32(crcInput));
        stream.Write(crc);
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                var mask = (uint)-(int)(crc & 1u);
                crc = (crc >> 1) ^ (0xEDB88320u & mask);
            }
        }
        return ~crc;
    }
}
