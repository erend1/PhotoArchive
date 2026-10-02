using PhotoArchive.Spikes.AppleDevice.Output;
using PhotoArchive.Spikes.AppleDevice.Probes;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace PhotoArchive.Spikes.AppleDevice.Tests;

public sealed class SanitizedValueTests
{
    [Fact]
    public void Property_values_are_sanitized_fail_closed()
    {
        var root = Path.Combine(Path.GetTempPath(), "m000-sanitized-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var ctx = RunContext.Create(root, "test", sanitize: true, writeRaw: false))
            {
                // Regression: WPD event parameters carry the PnP interface path, whose instance segment must be redacted.
                var pnp = EnumerateCommand.SanitizedValue(
                    ctx,
                    PInvoke.WPD_EVENT_PARAMETER_PNP_DEVICE_ID,
                    @"\\?\usb#vid_05ac&pid_12a8&mi_00#6&1a2b3c4d&1&0000#{6ac27878-a6fa-4155-ba85-f98f491d4f33}");
                Assert.DoesNotContain("1a2b3c4d", pnp, StringComparison.Ordinal);
                Assert.Contains("vid_05ac&pid_12a8", pnp, StringComparison.Ordinal);

                // Unknown string properties are redacted rather than passed through.
                var unknownKey = new PROPERTYKEY { fmtid = new Guid("0F0E0D0C-0B0A-0908-0706-050403020100"), pid = 9 };
                Assert.DoesNotContain("Holiday", EnumerateCommand.SanitizedValue(ctx, unknownKey, "Holiday in Ankara"), StringComparison.Ordinal);

                // Non-personal descriptive values stay readable; file names keep their pattern only.
                Assert.Equal("Apple Inc.", EnumerateCommand.SanitizedValue(ctx, PInvoke.WPD_DEVICE_MANUFACTURER, "Apple Inc."));
                Assert.Equal("27.0", EnumerateCommand.SanitizedValue(ctx, PInvoke.WPD_DEVICE_FIRMWARE_VERSION, "27.0"));
                Assert.Equal("IMG_#0001.JPG", EnumerateCommand.SanitizedValue(ctx, PInvoke.WPD_OBJECT_ORIGINAL_FILE_NAME, "IMG_4821.JPG"));
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
