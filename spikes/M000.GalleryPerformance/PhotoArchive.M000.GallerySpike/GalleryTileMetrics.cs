using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;

namespace PhotoArchive.M000.GallerySpike;

public static class GalleryTileMetrics
{
    private sealed class Marker { }

    private static readonly ConditionalWeakTable<FrameworkElement, Marker> Seen = new();
    private static int _active;
    private static int _created;
    private static int _peakActive;

    public static int Active => Volatile.Read(ref _active);
    public static int Created => Volatile.Read(ref _created);
    public static int PeakActive => Volatile.Read(ref _peakActive);

    public static void OnLoaded(FrameworkElement element)
    {
        if (!Seen.TryGetValue(element, out _))
        {
            Seen.Add(element, new Marker());
            Interlocked.Increment(ref _created);
        }

        var active = Interlocked.Increment(ref _active);
        while (true)
        {
            var current = Volatile.Read(ref _peakActive);
            if (active <= current || Interlocked.CompareExchange(ref _peakActive, active, current) == current)
            {
                break;
            }
        }
    }

    public static void OnUnloaded() => Interlocked.Decrement(ref _active);
}
