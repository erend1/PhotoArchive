using System.Globalization;
using System.Text.RegularExpressions;

namespace PhotoArchive.Spikes.AppleDevice.Output;

/// <summary>
/// Removes personal data from probe output intended for the public repository / GitHub issue.
/// Enabled by default; <c>--no-sanitize</c> disables it for local-only diagnostics.
/// </summary>
/// <remarks>
/// - File-name digit runs are replaced with stable per-run pseudonyms, preserving the naming *pattern*
///   (e.g. IMG_1234.HEIC / IMG_E1234.HEIC / IMG_1234.AAE / IMG_1234.MOV stay visibly related).
/// - User-assigned device names, serial numbers and PnP instance identifiers (which embed the device UDID)
///   are redacted. Manufacturer, model, firmware version and USB VID/PID are kept (not personal).
/// - Dates are reduced to year-month.
/// </remarks>
internal sealed partial class Sanitizer
{
    private readonly Dictionary<string, string> _numberPseudonyms = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _opaquePseudonyms = new(StringComparer.Ordinal);

    internal Sanitizer(bool enabled) => Enabled = enabled;

    internal bool Enabled { get; }

    [GeneratedRegex(@"\d+", RegexOptions.CultureInvariant)]
    private static partial Regex DigitRun();

    [GeneratedRegex(@"^(?<head>.*?(vid_[0-9a-f]{4}&pid_[0-9a-f]{4}(&mi_[0-9a-f]{2})?)?)#(?<instance>[^#{}]+)(?<tail>#\{[0-9a-f-]{36}\}.*)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PnpInstance();

    /// <summary>Pseudonymizes digit runs in a file or folder name while keeping letters, markers and extension.</summary>
    internal string FileName(string? name)
    {
        if (!Enabled || string.IsNullOrEmpty(name))
        {
            return name ?? string.Empty;
        }

        return DigitRun().Replace(name, m =>
        {
            if (!_numberPseudonyms.TryGetValue(m.Value, out var pseudonym))
            {
                pseudonym = "#" + (_numberPseudonyms.Count + 1).ToString("D4", CultureInfo.InvariantCulture);
                _numberPseudonyms[m.Value] = pseudonym;
            }

            return pseudonym;
        });
    }

    /// <summary>Replaces an opaque identifier (object ID, PUID, item key) with a stable per-run token plus its shape.</summary>
    internal string Opaque(string? value, string kind)
    {
        if (!Enabled || string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        var key = kind + ":" + value;
        if (!_opaquePseudonyms.TryGetValue(key, out var pseudonym))
        {
            pseudonym = $"<{kind}#{_opaquePseudonyms.Count + 1:D4} len={value.Length} shape={ShapeOf(value)}>";
            _opaquePseudonyms[key] = pseudonym;
        }

        return pseudonym;
    }

    internal string DeviceName(string? name) =>
        !Enabled || string.IsNullOrEmpty(name) ? name ?? string.Empty : "<user-assigned device name redacted>";

    internal string Serial(string? serial) =>
        !Enabled || string.IsNullOrEmpty(serial) ? serial ?? string.Empty : $"<serial redacted len={serial.Length}>";

    /// <summary>Keeps bus, VID/PID and interface-class GUID; redacts the instance segment that embeds the device serial/UDID.</summary>
    internal string PnpId(string? pnpId)
    {
        if (!Enabled || string.IsNullOrEmpty(pnpId))
        {
            return pnpId ?? string.Empty;
        }

        var match = PnpInstance().Match(pnpId);
        return match.Success
            ? match.Groups["head"].Value + "#<instance redacted>" + match.Groups["tail"].Value
            : "<pnp id redacted>";
    }

    internal string Date(DateTime? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return Enabled
            ? value.Value.ToString("yyyy-MM", CultureInfo.InvariantCulture) + " (day/time redacted)"
            : value.Value.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }

    /// <summary>Free text that may contain personal values (e.g. a raw property dump) is suppressed entirely.</summary>
    internal string FreeText(string? value) =>
        !Enabled || string.IsNullOrEmpty(value) ? value ?? string.Empty : $"<text redacted len={value.Length}>";

    internal static string ShapeOf(string value)
    {
        var chars = new char[value.Length];
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            chars[i] = char.IsAsciiDigit(c) ? '9' : char.IsAsciiLetterUpper(c) ? 'A' : char.IsAsciiLetterLower(c) ? 'a' : c;
        }

        return new string(chars);
    }
}
