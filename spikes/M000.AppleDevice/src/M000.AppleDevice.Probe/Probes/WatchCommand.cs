using System.Collections.Concurrent;
using System.Globalization;
using PhotoArchive.Spikes.AppleDevice.Output;
using PhotoArchive.Spikes.AppleDevice.Wpd;
using Windows.Win32;

namespace PhotoArchive.Spikes.AppleDevice.Probes;

/// <summary>
/// Listens for WPD events for a fixed period. Suggested manual actions while it runs: take a photo on the iPhone,
/// delete a photo on the iPhone, lock the iPhone, then unplug the cable. Records which of these are observable.
/// </summary>
internal static class WatchCommand
{
    internal static void Run(RunContext ctx, WpdDevice device, int seconds)
    {
        var section = ctx.Report.Section("In-session change notifications (WPD events)");
        section.Fact("API", "IPortableDevice::Advise(IPortableDeviceEventCallback)").Fact("Listen duration (s)", seconds);
        var events = new ConcurrentQueue<(DateTime At, string Name, string Details)>();
        var sink = new WpdEventSink(parameters =>
        {
            var id = parameters.FirstOrDefault(p => WpdNames.Key(p.Key) == nameof(PInvoke.WPD_EVENT_PARAMETER_EVENT_ID)).Value;
            var name = id is Guid g ? WpdNames.Guid(g) : "(unknown event)";
            var details = string.Join(", ", parameters
                .Where(p => WpdNames.Key(p.Key) != nameof(PInvoke.WPD_EVENT_PARAMETER_EVENT_ID))
                .Select(p => $"{WpdNames.Key(p.Key)}={EnumerateCommand.SanitizedValue(ctx, p.Key, p.Value)}"));
            events.Enqueue((DateTime.Now, name, details));
            ctx.Info($"  event {name}: {details}");
        });

        string cookie;
        try
        {
            cookie = device.Advise(sink);
        }
        catch (Exception ex)
        {
            section.Fact("Advise", "FAILED: " + WpdInterop.Describe(ex));
            return;
        }

        ctx.Info($"Listening for {seconds}s. Now: take a photo, delete a photo, lock the phone, and finally unplug the cable.");
        Thread.Sleep(TimeSpan.FromSeconds(seconds));
        try
        {
            device.Unadvise(cookie);
        }
        catch (Exception ex)
        {
            section.Fact("Unadvise", WpdInterop.Describe(ex));
        }

        section.Fact("Events received", events.Count);
        section.Table(
            ["Local time", "Event", "Parameters (sanitized)"],
            events.Select(e => (IReadOnlyList<string>)[e.At.ToString("HH:mm:ss", CultureInfo.InvariantCulture), e.Name, e.Details]));
        section.Line("WPD events exist only while connected. They are not a persistent change history; PhotoKit's persistent change tokens are the documented equivalent on iOS.");
    }
}
