namespace PhotoArchive.Spikes.AppleDevice.Analysis;

/// <summary>Reads pixel dimensions from a JPEG SOF marker (used to characterize device-provided thumbnails).</summary>
internal static class JpegInfo
{
    internal static (int Width, int Height)? TryReadDimensions(ReadOnlySpan<byte> jpeg)
    {
        if (jpeg.Length < 4 || jpeg[0] != 0xFF || jpeg[1] != 0xD8)
        {
            return null;
        }

        var i = 2;
        while (i + 4 <= jpeg.Length)
        {
            if (jpeg[i] != 0xFF)
            {
                i++;
                continue;
            }

            var marker = jpeg[i + 1];
            if (marker == 0xFF)
            {
                i++;
                continue;
            }

            if (marker is 0xD8 or 0x01 || (marker >= 0xD0 && marker <= 0xD7))
            {
                i += 2;
                continue;
            }

            var length = (jpeg[i + 2] << 8) | jpeg[i + 3];
            var isSof = marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC;
            if (isSof && i + 9 <= jpeg.Length)
            {
                var height = (jpeg[i + 5] << 8) | jpeg[i + 6];
                var width = (jpeg[i + 7] << 8) | jpeg[i + 8];
                return (width, height);
            }

            if (marker == 0xD9 || length < 2)
            {
                return null;
            }

            i += 2 + length;
        }

        return null;
    }
}
