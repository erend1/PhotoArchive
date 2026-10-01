using PhotoArchive.Spikes.AppleDevice.Wpd;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace PhotoArchive.Spikes.AppleDevice.Tests;

public sealed class WpdNamesTests
{
    [Fact]
    public void Known_property_keys_resolve_to_header_names()
    {
        Assert.Equal("WPD_OBJECT_NAME", WpdNames.Key(PInvoke.WPD_OBJECT_NAME));
        Assert.Equal("WPD_OBJECT_PERSISTENT_UNIQUE_ID", WpdNames.Key(PInvoke.WPD_OBJECT_PERSISTENT_UNIQUE_ID));
        Assert.Equal("WPD_RESOURCE_THUMBNAIL", WpdNames.Key(PInvoke.WPD_RESOURCE_THUMBNAIL));
        Assert.Equal("WPD_COMMAND_OBJECT_MANAGEMENT_DELETE_OBJECTS", WpdNames.Key(PInvoke.WPD_COMMAND_OBJECT_MANAGEMENT_DELETE_OBJECTS));
    }

    [Fact]
    public void Unknown_property_keys_are_rendered_raw()
    {
        var key = new PROPERTYKEY { fmtid = new Guid("11111111-2222-3333-4444-555555555555"), pid = 42 };

        Assert.Equal("{11111111-2222-3333-4444-555555555555},42", WpdNames.Key(key));
    }

    [Fact]
    public void Ptp_format_codes_are_decoded_from_wpd_format_guids()
    {
        Assert.Equal((ushort)0x3801, WpdNames.PtpFormatCode(PInvoke.WPD_OBJECT_FORMAT_EXIF));
        Assert.Equal((ushort)0x3001, WpdNames.PtpFormatCode(PInvoke.WPD_OBJECT_FORMAT_PROPERTIES_ONLY));
        Assert.Null(WpdNames.PtpFormatCode(PInvoke.WPD_CONTENT_TYPE_IMAGE));
    }

    [Fact]
    public void Unnamed_ptp_formats_are_described_by_code()
    {
        var heif = new Guid(0x38120000, 0xAE6C, 0x4804, 0x98, 0xBA, 0xC5, 0x7B, 0x46, 0x96, 0x5F, 0xE7);
        var vendor = new Guid(0xB9990000, 0xAE6C, 0x4804, 0x98, 0xBA, 0xC5, 0x7B, 0x46, 0x96, 0x5F, 0xE7);

        Assert.Equal("PTP/MTP format 0x3812 (HEIF)", WpdNames.Guid(heif));
        Assert.Equal("PTP/MTP format 0xB999 (vendor/unknown)", WpdNames.Guid(vendor));
        Assert.StartsWith("WPD_OBJECT_FORMAT_EXIF", WpdNames.Guid(PInvoke.WPD_OBJECT_FORMAT_EXIF), StringComparison.Ordinal);
        Assert.Equal("WPD_CONTENT_TYPE_IMAGE", WpdNames.Guid(PInvoke.WPD_CONTENT_TYPE_IMAGE));
        Assert.Equal("(none)", WpdNames.Guid(null));
    }

    [Fact]
    public void Hresults_have_readable_names()
    {
        Assert.Equal("0x80070005 E_ACCESSDENIED", WpdHResult.Describe(unchecked((int)0x80070005)));
        Assert.Equal("0x802A0006 E_WPD_DEVICE_IS_HUNG", WpdHResult.Describe(unchecked((int)0x802A0006)));
        Assert.Equal("0x12345678", WpdHResult.Describe(0x12345678));
    }
}
