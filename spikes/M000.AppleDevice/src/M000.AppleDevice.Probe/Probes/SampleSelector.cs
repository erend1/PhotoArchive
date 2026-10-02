using PhotoArchive.Spikes.AppleDevice.Analysis;

namespace PhotoArchive.Spikes.AppleDevice.Probes;

/// <summary>Picks a small, representative set of objects so a bounded run still covers every exposed resource kind.</summary>
internal static class SampleSelector
{
    internal static List<EnumeratedObject> Representative(IReadOnlyList<EnumeratedObject> objects, int maxCount, bool includeGroups)
    {
        var media = objects.Where(o => o.IsMediaLike && o.PropertyError is null).ToList();
        var picked = new List<EnumeratedObject>();

        void Add(EnumeratedObject? o)
        {
            if (o is not null && !picked.Contains(o) && picked.Count < maxCount)
            {
                picked.Add(o);
            }
        }

        if (includeGroups)
        {
            // A Live Photo candidate (still + video with the same DCIM number) and an edited group (IMG_E* + original + .AAE).
            var groups = media.GroupBy(o => (o.ParentId, Key: GroupKey(o))).Where(g => g.Key.Key is not null).ToList();
            var live = groups.FirstOrDefault(g =>
                g.Any(o => Parsed(o) is { Marker: "", Kind: DcimResourceKind.Still })
                && g.Any(o => Parsed(o) is { Marker: "", Kind: DcimResourceKind.Video }));
            foreach (var o in live?.Where(o => Parsed(o)?.Marker == "") ?? [])
            {
                Add(o);
            }

            var edited = groups.FirstOrDefault(g => g.Any(o => Parsed(o)?.Marker == "E"));
            foreach (var o in edited?.OrderBy(o => Parsed(o)?.Marker).ThenBy(o => o.Extension) ?? Enumerable.Empty<EnumeratedObject>())
            {
                Add(o);
            }
        }

        // One of each (marker, extension) kind, smallest first to keep transfers short.
        foreach (var kind in media.GroupBy(o => (Parsed(o)?.Marker ?? "?") + ":" + o.Extension).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            Add(kind.OrderBy(o => o.Size ?? ulong.MaxValue).First());
        }

        return picked;
    }

    private static ParsedDcimName? Parsed(EnumeratedObject o) => DcimNameAnalyzer.Parse(o.FileName);

    private static string? GroupKey(EnumeratedObject o) => Parsed(o) is { } p ? p.Stem.ToUpperInvariant() + p.Number : null;
}
