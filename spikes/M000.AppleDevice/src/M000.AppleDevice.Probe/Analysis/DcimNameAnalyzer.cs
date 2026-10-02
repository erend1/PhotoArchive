using System.Text.RegularExpressions;

namespace PhotoArchive.Spikes.AppleDevice.Analysis;

/// <summary>
/// Classifies DCIM-style file names and groups them into *candidate* relationships
/// (Live Photo still+motion pairs, edited renders, adjustment sidecars).
/// </summary>
/// <remarks>
/// SAFETY: every relationship produced here is a filename-convention heuristic. Apple does not document
/// the PTP/DCIM naming scheme. These candidates are evidence about what the interface exposes; they are
/// never proof of asset identity, of resource completeness, or of archive state.
/// </remarks>
internal static partial class DcimNameAnalyzer
{
    [GeneratedRegex(@"^(?<stem>[A-Za-z]+_)(?<marker>[A-Z]?)(?<num>\d{4,})(?<suffix>[^.]*)\.(?<ext>[A-Za-z0-9]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex DcimName();

    private static readonly HashSet<string> StillExtensions =
        new(StringComparer.OrdinalIgnoreCase) { "heic", "heif", "hif", "jpg", "jpeg", "png", "dng", "gif", "tif", "tiff", "webp", "avif" };

    private static readonly HashSet<string> VideoExtensions =
        new(StringComparer.OrdinalIgnoreCase) { "mov", "mp4", "m4v", "3gp" };

    internal static ParsedDcimName? Parse(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var match = DcimName().Match(fileName);
        if (!match.Success)
        {
            return null;
        }

        var ext = match.Groups["ext"].Value;
        var kind = StillExtensions.Contains(ext) ? DcimResourceKind.Still
            : VideoExtensions.Contains(ext) ? DcimResourceKind.Video
            : ext.Equals("aae", StringComparison.OrdinalIgnoreCase) ? DcimResourceKind.AdjustmentSidecar
            : DcimResourceKind.Other;

        return new ParsedDcimName(
            Stem: match.Groups["stem"].Value,
            Marker: match.Groups["marker"].Value,
            Number: match.Groups["num"].Value,
            Suffix: match.Groups["suffix"].Value,
            Extension: ext.ToUpperInvariant(),
            Kind: kind);
    }

    /// <summary>Groups names by (folder, stem, number) and derives candidate relationships.</summary>
    internal static DcimGroupingSummary Summarize(IEnumerable<(string? Folder, string? FileName)> files)
    {
        var groups = new Dictionary<(string Folder, string Stem, string Number), List<ParsedDcimName>>();
        var unparsed = 0;
        var total = 0;

        foreach (var (folder, fileName) in files)
        {
            total++;
            var parsed = Parse(fileName);
            if (parsed is null)
            {
                unparsed++;
                continue;
            }

            var key = (folder ?? string.Empty, parsed.Stem.ToUpperInvariant(), parsed.Number);
            if (!groups.TryGetValue(key, out var list))
            {
                list = [];
                groups[key] = list;
            }

            list.Add(parsed);
        }

        var summary = new DcimGroupingSummary { TotalFiles = total, UnparsedFiles = unparsed, Groups = groups.Count };
        foreach (var members in groups.Values)
        {
            var originalStill = members.Any(m => m.Marker.Length == 0 && m.Kind == DcimResourceKind.Still);
            var originalVideo = members.Any(m => m.Marker.Length == 0 && m.Kind == DcimResourceKind.Video);
            var editedStill = members.Any(m => m.Marker == "E" && m.Kind == DcimResourceKind.Still);
            var editedVideo = members.Any(m => m.Marker == "E" && m.Kind == DcimResourceKind.Video);
            var adjustment = members.Any(m => m.Kind == DcimResourceKind.AdjustmentSidecar);
            var otherMarkers = members.Where(m => m.Marker.Length > 0 && m.Marker != "E")
                .Select(m => m.Marker + ":" + m.Extension);

            foreach (var marker in otherMarkers)
            {
                summary.OtherMarkerCounts[marker] = summary.OtherMarkerCounts.GetValueOrDefault(marker) + 1;
            }

            foreach (var ext in members.Select(m => (m.Marker.Length == 0 ? "" : m.Marker + ":") + m.Extension))
            {
                summary.ExtensionCounts[ext] = summary.ExtensionCounts.GetValueOrDefault(ext) + 1;
            }

            if (originalStill && originalVideo)
            {
                summary.LivePhotoCandidates++;
            }
            else if (originalStill)
            {
                summary.StillOnlyGroups++;
            }
            else if (originalVideo)
            {
                summary.VideoOnlyGroups++;
            }

            if (editedStill || editedVideo)
            {
                summary.EditedRenderGroups++;
                if (adjustment)
                {
                    summary.EditedRenderWithAdjustmentGroups++;
                }

                if (!originalStill && !originalVideo)
                {
                    // An edited render without an exposed original is a completeness red flag.
                    summary.EditedRenderWithoutOriginalGroups++;
                }
            }

            if (adjustment)
            {
                summary.AdjustmentSidecarGroups++;
                if (!originalStill && !originalVideo && !editedStill && !editedVideo)
                {
                    summary.OrphanAdjustmentGroups++;
                }
            }

            if (members.Count(m => m.Marker.Length == 0 && m.Kind == DcimResourceKind.Still) > 1)
            {
                // e.g. IMG_1234.HEIC + IMG_1234.JPG in the same folder (RAW+JPEG-like alternates or collisions).
                summary.MultipleOriginalStillGroups++;
            }
        }

        return summary;
    }

    /// <summary>Returns the character-class shape of a name, e.g. "202409__" -> "DDDDDD__", "100APPLE" -> "DDDAAAAA".</summary>
    internal static string Shape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return string.Create(value.Length, value, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                var c = source[i];
                span[i] = char.IsAsciiDigit(c) ? 'D' : char.IsAsciiLetter(c) ? 'A' : c;
            }
        });
    }
}

internal enum DcimResourceKind
{
    Still,
    Video,
    AdjustmentSidecar,
    Other,
}

internal sealed record ParsedDcimName(string Stem, string Marker, string Number, string Suffix, string Extension, DcimResourceKind Kind);

internal sealed class DcimGroupingSummary
{
    public int TotalFiles { get; init; }
    public int UnparsedFiles { get; init; }
    public int Groups { get; init; }
    public int StillOnlyGroups { get; set; }
    public int VideoOnlyGroups { get; set; }
    public int LivePhotoCandidates { get; set; }
    public int EditedRenderGroups { get; set; }
    public int EditedRenderWithAdjustmentGroups { get; set; }
    public int EditedRenderWithoutOriginalGroups { get; set; }
    public int AdjustmentSidecarGroups { get; set; }
    public int OrphanAdjustmentGroups { get; set; }
    public int MultipleOriginalStillGroups { get; set; }
    public SortedDictionary<string, int> ExtensionCounts { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, int> OtherMarkerCounts { get; } = new(StringComparer.Ordinal);
}
