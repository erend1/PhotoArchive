using PhotoArchive.Spikes.AppleDevice.Output;

namespace PhotoArchive.Spikes.AppleDevice.Tests;

public sealed class SanitizerTests
{
    [Fact]
    public void FileName_pseudonymizes_digits_but_keeps_relationships_visible()
    {
        var sanitizer = new Sanitizer(enabled: true);

        var still = sanitizer.FileName("IMG_4821.HEIC");
        var edited = sanitizer.FileName("IMG_E4821.HEIC");
        var sidecar = sanitizer.FileName("IMG_4821.AAE");
        var motion = sanitizer.FileName("IMG_4821.MOV");
        var other = sanitizer.FileName("IMG_4822.HEIC");

        Assert.Equal("IMG_#0001.HEIC", still);
        Assert.Equal("IMG_E#0001.HEIC", edited);
        Assert.Equal("IMG_#0001.AAE", sidecar);
        Assert.Equal("IMG_#0001.MOV", motion);
        Assert.Equal("IMG_#0002.HEIC", other);
        Assert.DoesNotContain("4821", still + edited + sidecar + motion + other, StringComparison.Ordinal);
    }

    [Fact]
    public void Disabled_sanitizer_passes_values_through()
    {
        var sanitizer = new Sanitizer(enabled: false);

        Assert.Equal("IMG_4821.HEIC", sanitizer.FileName("IMG_4821.HEIC"));
        Assert.Equal("My iPhone", sanitizer.DeviceName("My iPhone"));
        Assert.Equal("ABC123", sanitizer.Serial("ABC123"));
    }

    [Fact]
    public void PnpId_keeps_vid_pid_and_interface_class_but_redacts_instance()
    {
        var sanitizer = new Sanitizer(enabled: true);
        const string id = @"\\?\usb#vid_05ac&pid_12a8&mi_00#6&1a2b3c4d&0&0000#{6ac27878-a6fa-4155-ba85-f98f491d4f33}";

        var sanitized = sanitizer.PnpId(id);

        Assert.Contains("vid_05ac&pid_12a8&mi_00", sanitized, StringComparison.Ordinal);
        Assert.Contains("{6ac27878-a6fa-4155-ba85-f98f491d4f33}", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("1a2b3c4d", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public void PnpId_without_recognizable_structure_is_fully_redacted()
    {
        var sanitizer = new Sanitizer(enabled: true);

        Assert.Equal("<pnp id redacted>", sanitizer.PnpId("SOMETHING-UNEXPECTED"));
    }

    [Fact]
    public void Personal_values_are_redacted()
    {
        var sanitizer = new Sanitizer(enabled: true);

        Assert.DoesNotContain("Eren", sanitizer.DeviceName("Eren's iPhone"), StringComparison.Ordinal);
        Assert.DoesNotContain("F2LXJ", sanitizer.Serial("F2LXJ0000000"), StringComparison.Ordinal);
        Assert.Equal("2026-09 (day/time redacted)", sanitizer.Date(new DateTime(2026, 9, 14, 17, 45, 3)));
        Assert.DoesNotContain("secret", sanitizer.FreeText("secret keyword"), StringComparison.Ordinal);
    }

    [Fact]
    public void Opaque_tokens_are_stable_per_value_and_expose_only_shape()
    {
        var sanitizer = new Sanitizer(enabled: true);

        var first = sanitizer.Opaque("o1A2B", "puid");
        var again = sanitizer.Opaque("o1A2B", "puid");
        var different = sanitizer.Opaque("o1A2C", "puid");

        Assert.Equal(first, again);
        Assert.NotEqual(first, different);
        Assert.Contains("shape=a9A9A", first, StringComparison.Ordinal);
        Assert.DoesNotContain("o1A2B", first, StringComparison.Ordinal);
    }
}
