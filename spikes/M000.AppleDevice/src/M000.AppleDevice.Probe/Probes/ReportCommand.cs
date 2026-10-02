using PhotoArchive.Spikes.AppleDevice.Cli;
using PhotoArchive.Spikes.AppleDevice.Output;
using PhotoArchive.Spikes.AppleDevice.Wpd;

namespace PhotoArchive.Spikes.AppleDevice.Probes;

/// <summary>
/// Runs every NON-destructive experiment in sequence and writes one sanitized report (report.md / report.json).
/// </summary>
internal static class ReportCommand
{
    internal static async Task RunAsync(RunContext ctx, CommandLine cli)
    {
        var header = ctx.Report.Section("Run");
        header.Fact("Command line (options only)", string.Join(" ", cli.OptionNames.Select(n => "--" + n)))
            .Fact("Sanitized", ctx.Sanitizer.Enabled)
            .Fact("Test conditions (fill in manually)", "iPhone model/iOS: ? | iCloud Photos: on/off | Optimize iPhone Storage: on/off | Transfer to Mac or PC: Automatic/Keep Originals | cable/port: ?");

        await EnvironmentCommand.RunAsync(ctx);
        DevicesCommand.Run(ctx, watchSeconds: 0);

        WpdDeviceInfo info;
        try
        {
            info = DeviceSelection.Select(ctx, cli);
        }
        catch (ProbeAbortedException ex)
        {
            ctx.Report.Section("Apple device").Fact("Result", "No Apple device visible through WPD on this run").Line(ex.Message);
            ctx.Warn(ex.Message);
            await TryMediaImportAsync(ctx, cli);
            return;
        }

        var (device, latency) = InspectCommand.OpenTimed(info, WpdAccess.ReadOnly);
        using (device)
        {
            InspectCommand.Run(ctx, info, device, latency);
            var objects = EnumerateCommand.Run(ctx, device, EnumerateCommand.Options.From(cli));
            if (!cli.Has("skip-thumbs"))
            {
                ThumbnailCommand.Run(ctx, device, objects, Math.Clamp(cli.GetInt("thumbs", 20), 1, 500), save: false);
            }

            if (!cli.Has("skip-copy"))
            {
                CopyCommand.Run(ctx, device, objects, CopyCommand.Options.From(cli));
            }
        }

        await TryMediaImportAsync(ctx, cli);
    }

    private static async Task TryMediaImportAsync(RunContext ctx, CommandLine cli)
    {
        if (cli.Has("skip-wmi"))
        {
            return;
        }

        try
        {
            var sources = await MediaImportCommands.SourcesAsync(ctx);
            if (sources.Count > 0)
            {
                await MediaImportCommands.FindAsync(ctx, cli, sources);
            }
        }
        catch (Exception ex)
        {
            ctx.Report.Section("Windows.Media.Import").Fact("Failed", WpdInterop.Describe(ex));
            ctx.Error("Windows.Media.Import probe failed: " + WpdInterop.Describe(ex));
        }
    }
}

/// <summary>Lists WPD devices; optionally polls for arrivals/removals (detection latency, disconnect visibility).</summary>
internal static class DevicesCommand
{
    internal static void Run(RunContext ctx, int watchSeconds)
    {
        var section = ctx.Report.Section("WPD devices");
        section.Fact("API", "IPortableDeviceManager::GetDevices / GetPrivateDevices / GetDeviceFriendlyName / GetDeviceManufacturer / GetDeviceDescription");
        var devices = WpdDevice.ListDevices(includePrivate: true);
        section.Fact("Devices visible", devices.Count).Fact("Apple-looking devices", devices.Count(d => d.LooksLikeApple));
        section.Table(
            ["#", "Friendly name", "Manufacturer", "Description", "Apple?", "PnP id (sanitized)"],
            devices.Select((d, i) => (IReadOnlyList<string>)[
                i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ctx.Sanitizer.DeviceName(d.FriendlyName),
                d.Manufacturer ?? "",
                d.Description ?? "",
                d.LooksLikeApple ? "yes" : "no",
                ctx.Sanitizer.PnpId(d.PnpId),
            ]));
        ctx.Info($"{devices.Count} WPD device(s) visible; {devices.Count(d => d.LooksLikeApple)} look like Apple devices.");
        for (var i = 0; i < devices.Count; i++)
        {
            ctx.Info($"  [{i}] {devices[i].Manufacturer} | {devices[i].Description} | apple={devices[i].LooksLikeApple} | {ctx.Sanitizer.PnpId(devices[i].PnpId)}");
        }

        if (watchSeconds <= 0)
        {
            return;
        }

        ctx.Info($"Watching for {watchSeconds}s: connect/unlock/trust or unplug the iPhone now.");
        var known = devices.Select(d => d.PnpId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var start = DateTime.UtcNow;
        while ((DateTime.UtcNow - start).TotalSeconds < watchSeconds)
        {
            Thread.Sleep(1000);
            var now = WpdDevice.ListDevices(includePrivate: true);
            var current = now.Select(d => d.PnpId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var added in now.Where(d => !known.Contains(d.PnpId)))
            {
                var line = $"+{(DateTime.UtcNow - start).TotalSeconds:F0}s ARRIVED {added.Manufacturer} | {added.Description} | {ctx.Sanitizer.PnpId(added.PnpId)}";
                section.Line("- " + line);
                ctx.Good(line);
            }

            foreach (var removed in known.Where(id => !current.Contains(id)))
            {
                var line = $"+{(DateTime.UtcNow - start).TotalSeconds:F0}s REMOVED {ctx.Sanitizer.PnpId(removed)}";
                section.Line("- " + line);
                ctx.Warn(line);
            }

            known = current;
        }
    }
}
