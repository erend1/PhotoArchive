using System.Globalization;
using System.Reflection;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace PhotoArchive.Spikes.AppleDevice.Wpd;

/// <summary>
/// Human-readable names for WPD PROPERTYKEYs and GUIDs, built by reflecting over the CsWin32-generated
/// constants (so names match Microsoft's PortableDevice.h exactly), plus PTP/MTP format-code decoding.
/// </summary>
internal static class WpdNames
{
    // WPD object-format GUIDs for PTP/MTP formats follow {XXXX0000-AE6C-4804-98BA-C57B46965FE7}
    // where XXXX is the PTP/MTP ObjectFormatCode (e.g. WPD_OBJECT_FORMAT_EXIF = {38010000-...}).
    private static readonly Guid PtpFormatTemplate = new(0x00000000, 0xAE6C, 0x4804, 0x98, 0xBA, 0xC5, 0x7B, 0x46, 0x96, 0x5F, 0xE7);

    private static readonly Dictionary<ushort, string> PtpFormatCodes = new()
    {
        [0x3000] = "Undefined (non-image)",
        [0x3001] = "Association (folder)",
        [0x3002] = "Script",
        [0x3004] = "Text",
        [0x3006] = "DPOF",
        [0x3007] = "AIFF",
        [0x3008] = "WAV",
        [0x3009] = "MP3",
        [0x300A] = "AVI",
        [0x300B] = "MPEG",
        [0x300C] = "ASF",
        [0x300D] = "QuickTime",
        [0x3800] = "Undefined image",
        [0x3801] = "EXIF/JPEG",
        [0x3802] = "TIFF/EP",
        [0x3804] = "BMP",
        [0x3807] = "GIF",
        [0x3808] = "JFIF",
        [0x380A] = "PICT",
        [0x380B] = "PNG",
        [0x380D] = "TIFF",
        [0x380F] = "JP2",
        [0x3810] = "JPX",
        [0x3811] = "DNG",
        [0x3812] = "HEIF",
        [0xB981] = "WMV (MTP)",
        [0xB982] = "MP4 container (MTP)",
        [0xB984] = "3GP container (MTP)",
        [0xB985] = "3G2 container (MTP)",
    };

    private static readonly Dictionary<ushort, string> PtpEventCodes = new()
    {
        [0x4001] = "CancelTransaction",
        [0x4002] = "ObjectAdded",
        [0x4003] = "ObjectRemoved",
        [0x4004] = "StoreAdded",
        [0x4005] = "StoreRemoved",
        [0x4006] = "DevicePropChanged",
        [0x4007] = "ObjectInfoChanged",
        [0x4008] = "DeviceInfoChanged",
        [0x4009] = "RequestObjectTransfer",
        [0x400A] = "StoreFull",
        [0x400B] = "DeviceReset",
        [0x400C] = "StorageInfoChanged",
        [0x400D] = "CaptureComplete",
        [0x400E] = "UnreportedStatus",
        [0xC801] = "ObjectPropChanged (MTP)",
        [0xC802] = "ObjectPropDescChanged (MTP)",
        [0xC803] = "ObjectReferencesChanged (MTP)",
    };

    private static readonly Dictionary<(Guid, uint), string> KeyNames = new();
    private static readonly Dictionary<Guid, string> GuidNames = new();

    static WpdNames()
    {
        foreach (var field in typeof(PInvoke).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (!field.Name.StartsWith("WPD_", StringComparison.Ordinal))
            {
                continue;
            }

            if (field.FieldType == typeof(PROPERTYKEY) && field.GetValue(null) is PROPERTYKEY key)
            {
                KeyNames.TryAdd((key.fmtid, key.pid), field.Name);
            }
            else if (field.FieldType == typeof(Guid) && field.GetValue(null) is Guid guid)
            {
                GuidNames.TryAdd(guid, field.Name);
            }
        }
    }

    internal static int KnownKeyCount => KeyNames.Count;

    internal static int KnownGuidCount => GuidNames.Count;

    internal static string Key(PROPERTYKEY key) =>
        KeyNames.TryGetValue((key.fmtid, key.pid), out var name)
            ? name
            : $"{{{key.fmtid.ToString("D", CultureInfo.InvariantCulture).ToUpperInvariant()}}},{key.pid}";

    internal static string Guid(Guid? value)
    {
        if (value is null)
        {
            return "(none)";
        }

        if (GuidNames.TryGetValue(value.Value, out var name))
        {
            var code = PtpFormatCode(value.Value);
            return code is null ? name : $"{name} [PTP 0x{code:X4}]";
        }

        var eventCode = MtpEventCode(value.Value);
        if (eventCode is not null)
        {
            return PtpEventCodes.TryGetValue(eventCode.Value, out var eventName)
                ? $"PTP/MTP event 0x{eventCode:X4} ({eventName})"
                : $"PTP/MTP event 0x{eventCode:X4} (vendor/unknown)";
        }

        var ptp = PtpFormatCode(value.Value);
        if (ptp is not null)
        {
            return PtpFormatCodes.TryGetValue(ptp.Value, out var known)
                ? $"PTP/MTP format 0x{ptp:X4} ({known})"
                : $"PTP/MTP format 0x{ptp:X4} (vendor/unknown)";
        }

        return "{" + value.Value.ToString("D", CultureInfo.InvariantCulture).ToUpperInvariant() + "}";
    }

    /// <summary>Extracts the PTP/MTP ObjectFormatCode embedded in a WPD format GUID, if the GUID follows the PTP pattern.</summary>
    internal static ushort? PtpFormatCode(Guid format) => EmbeddedCode(format, PtpFormatTemplate);

    private static ushort? EmbeddedCode(Guid format, Guid family)
    {
        Span<byte> actual = stackalloc byte[16];
        Span<byte> template = stackalloc byte[16];
        format.TryWriteBytes(actual);
        family.TryWriteBytes(template);

        // Bytes 0..1 are the low word of Data1 (must be zero); bytes 2..3 hold the format code (little-endian).
        if (actual[0] != 0 || actual[1] != 0 || !actual[4..].SequenceEqual(template[4..]))
        {
            return null;
        }

        return (ushort)(actual[2] | (actual[3] << 8));
    }

    /// <summary>
    /// Extracts the PTP/MTP event code from a WPD event GUID of the WPD_EVENT_MTP_VENDOR_EXTENDED_EVENTS family
    /// ({XXXX0000-5738-4FF2-8445-BE3126691059}, event code in the high word of Data1).
    /// </summary>
    internal static ushort? MtpEventCode(Guid value) => EmbeddedCode(value, PInvoke.WPD_EVENT_MTP_VENDOR_EXTENDED_EVENTS);

    internal static string PtpFormatName(ushort code) =>
        PtpFormatCodes.TryGetValue(code, out var name) ? name : "vendor/unknown";
}
