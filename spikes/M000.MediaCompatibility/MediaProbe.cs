using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace M000.MediaCompatibility;

public static class MediaProbe
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static MediaProbeResult Probe(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        if (bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(PngSignature))
        {
            return ProbePng(bytes);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8)
        {
            return ProbeJpeg(bytes);
        }

        if (LooksLikeTiff(bytes))
        {
            return ProbeTiff(bytes, "DNG");
        }

        if (bytes.Length >= 12 && Encoding.ASCII.GetString(bytes, 4, 4) == "ftyp")
        {
            return ProbeIsoBmff(bytes);
        }

        throw new NotSupportedException("The spike probe does not recognize this fixture container.");
    }

    private static MediaProbeResult ProbePng(byte[] bytes)
    {
        if (bytes.Length < 24 || Encoding.ASCII.GetString(bytes, 12, 4) != "IHDR")
        {
            throw new InvalidDataException("PNG fixture is missing IHDR.");
        }

        var width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4)));
        var height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4)));
        return new("PNG", null, width, height, null, null, null, null, null, null);
    }

    private static MediaProbeResult ProbeJpeg(byte[] bytes)
    {
        int? width = null;
        int? height = null;
        TiffMetadata? exif = null;
        var position = 2;

        while (position + 4 <= bytes.Length)
        {
            if (bytes[position] != 0xFF)
            {
                break;
            }

            var marker = bytes[position + 1];
            if (marker == 0xD9)
            {
                break;
            }

            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(position + 2, 2));
            if (segmentLength < 2 || position + 2 + segmentLength > bytes.Length)
            {
                throw new InvalidDataException("JPEG segment length is invalid.");
            }

            var payloadStart = position + 4;
            var payloadLength = segmentLength - 2;

            if (marker == 0xE1 && payloadLength >= 6 &&
                bytes.AsSpan(payloadStart, 6).SequenceEqual("Exif\0\0"u8))
            {
                exif = TiffReader.Read(bytes.AsSpan(payloadStart + 6, payloadLength - 6).ToArray());
            }
            else if (marker is 0xC0 or 0xC2 && payloadLength >= 6)
            {
                height = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(payloadStart + 1, 2));
                width = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(payloadStart + 3, 2));
            }

            position += 2 + segmentLength;
        }

        return new(
            "JPEG",
            null,
            width,
            height,
            null,
            exif?.CaptureDate,
            exif?.Orientation,
            exif?.Latitude,
            exif?.Longitude,
            null);
    }

    private static MediaProbeResult ProbeTiff(byte[] bytes, string defaultContainer)
    {
        var metadata = TiffReader.Read(bytes);
        var container = metadata.DngVersion is null ? defaultContainer : "DNG";
        return new(
            container,
            null,
            metadata.Width,
            metadata.Height,
            null,
            metadata.CaptureDate,
            metadata.Orientation,
            metadata.Latitude,
            metadata.Longitude,
            metadata.DngVersion);
    }

    private static MediaProbeResult ProbeIsoBmff(byte[] bytes)
    {
        var state = new IsoBmffState();
        ParseBoxes(bytes, 0, bytes.Length, state);

        var heif = state.MajorBrand is "heic" or "heix" or "hevc" or "heim" or "mif1";
        var container = heif ? "HEIF" : state.MajorBrand == "qt  " ? "QuickTime/MOV" : "MP4";
        var codec = state.Codec;
        if (heif && codec is null && (state.MajorBrand is "heic" or "heix" or "hevc" or "heim"))
        {
            codec = "HEVC/H.265";
        }

        return new(
            container,
            codec,
            state.Width,
            state.Height,
            state.Duration,
            state.CaptureDate,
            null,
            null,
            null,
            null);
    }

    private static void ParseBoxes(byte[] bytes, int start, int end, IsoBmffState state)
    {
        var position = start;
        while (position + 8 <= end)
        {
            var size = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(position, 4));
            if (size < 8 || size > int.MaxValue)
            {
                break;
            }

            var boxEnd = position + (int)size;
            if (boxEnd > end)
            {
                break;
            }

            var type = Encoding.ASCII.GetString(bytes, position + 4, 4);
            var payloadStart = position + 8;

            switch (type)
            {
                case "ftyp":
                    if (payloadStart + 4 <= boxEnd)
                    {
                        state.MajorBrand = Encoding.ASCII.GetString(bytes, payloadStart, 4);
                    }
                    break;

                case "mvhd":
                    ParseMovieHeader(bytes, payloadStart, boxEnd, state);
                    break;

                case "stsd":
                    ParseSampleDescription(bytes, payloadStart, boxEnd, state);
                    break;

                case "ispe":
                    if (payloadStart + 12 <= boxEnd)
                    {
                        state.Width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(payloadStart + 4, 4)));
                        state.Height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(payloadStart + 8, 4)));
                    }
                    break;

                case "moov":
                case "trak":
                case "mdia":
                case "minf":
                case "stbl":
                case "iprp":
                case "ipco":
                    ParseBoxes(bytes, payloadStart, boxEnd, state);
                    break;

                case "meta":
                    if (payloadStart + 4 <= boxEnd)
                    {
                        ParseBoxes(bytes, payloadStart + 4, boxEnd, state);
                    }
                    break;
            }

            position = boxEnd;
        }
    }

    private static void ParseMovieHeader(byte[] bytes, int payloadStart, int boxEnd, IsoBmffState state)
    {
        if (payloadStart + 20 > boxEnd || bytes[payloadStart] != 0)
        {
            return;
        }

        var creationSeconds = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(payloadStart + 4, 4));
        var timescale = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(payloadStart + 12, 4));
        var duration = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(payloadStart + 16, 4));

        if (timescale > 0)
        {
            state.Duration = TimeSpan.FromSeconds(duration / (double)timescale);
        }

        var epoch = new DateTimeOffset(1904, 1, 1, 0, 0, 0, TimeSpan.Zero);
        state.CaptureDate = epoch.AddSeconds(creationSeconds).ToString("O", CultureInfo.InvariantCulture);
    }

    private static void ParseSampleDescription(byte[] bytes, int payloadStart, int boxEnd, IsoBmffState state)
    {
        if (payloadStart + 16 > boxEnd)
        {
            return;
        }

        var entryCount = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(payloadStart + 4, 4));
        if (entryCount == 0)
        {
            return;
        }

        var entryStart = payloadStart + 8;
        var entrySize = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(entryStart, 4));
        if (entrySize < 36 || entrySize > int.MaxValue || entryStart + entrySize > boxEnd)
        {
            return;
        }

        var sampleType = Encoding.ASCII.GetString(bytes, entryStart + 4, 4);
        state.Codec = sampleType switch
        {
            "avc1" => "H.264/AVC",
            "hvc1" or "hev1" => "HEVC/H.265",
            _ => sampleType
        };

        var bodyStart = entryStart + 8;
        state.Width = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(bodyStart + 24, 2));
        state.Height = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(bodyStart + 26, 2));
    }

    private static bool LooksLikeTiff(byte[] bytes) =>
        bytes.Length >= 8 &&
        ((bytes[0] == (byte)'I' && bytes[1] == (byte)'I' && bytes[2] == 42 && bytes[3] == 0) ||
         (bytes[0] == (byte)'M' && bytes[1] == (byte)'M' && bytes[2] == 0 && bytes[3] == 42));

    private sealed class IsoBmffState
    {
        public string? MajorBrand { get; set; }
        public string? Codec { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public TimeSpan? Duration { get; set; }
        public string? CaptureDate { get; set; }
    }
}
