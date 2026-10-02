using PhotoArchive.Spikes.AppleDevice.Cli;
using PhotoArchive.Spikes.AppleDevice.Output;
using PhotoArchive.Spikes.AppleDevice.Probes;
using PhotoArchive.Spikes.AppleDevice.Wpd;

// M000 Apple device capability probe (spike-only; see spikes/M000.AppleDevice/README.md).
// Every command is read-only unless it is one of the two explicitly guarded *-experiment commands.
var cli = CommandLine.Parse(args);
if (cli.Command is "help" or "-h" or "/?")
{
    Console.WriteLine(Help.Text);
    return 0;
}

var sanitize = !cli.Has("no-sanitize");
using var ctx = RunContext.Create(cli.Get("out"), cli.Command, sanitize, writeRaw: cli.Has("raw"));
ctx.Info($"PhotoArchive M000 Apple device probe — command '{cli.Command}', run folder: {ctx.RunDirectory}");
if (!sanitize)
{
    ctx.Warn("--no-sanitize: output contains personal metadata. Do not commit or post it.");
}

try
{
    switch (cli.Command)
    {
        case "env":
            await EnvironmentCommand.RunAsync(ctx);
            break;
        case "devices":
            DevicesCommand.Run(ctx, cli.GetInt("watch", 0));
            break;
        case "inspect":
            WithDevice(ctx, cli, (info, device, latency) => InspectCommand.Run(ctx, info, device, latency));
            break;
        case "enumerate":
            WithDevice(ctx, cli, (_, device, _) => EnumerateCommand.Run(ctx, device, EnumerateCommand.Options.From(cli)));
            break;
        case "thumbs":
            WithDevice(ctx, cli, (_, device, _) =>
            {
                var objects = EnumerateCommand.Run(ctx, device, EnumerateCommand.Options.From(cli));
                ThumbnailCommand.Run(ctx, device, objects, Math.Clamp(cli.GetInt("count", 20), 1, 500), cli.Has("save"));
            });
            break;
        case "copy":
            WithDevice(ctx, cli, (_, device, _) =>
            {
                var objects = EnumerateCommand.Run(ctx, device, EnumerateCommand.Options.From(cli));
                CopyCommand.Run(ctx, device, objects, CopyCommand.Options.From(cli));
            });
            break;
        case "identity-snapshot":
            WithDevice(ctx, cli, (info, device, latency) =>
            {
                var inspect = InspectCommand.Run(ctx, info, device, latency);
                IdentityCommands.Snapshot(ctx, info, device, inspect.Firmware, cli.Get("snapshot-out"));
            });
            break;
        case "identity-compare":
            if (cli.Positionals.Count != 2)
            {
                throw new ProbeUsageException("identity-compare needs two snapshot paths: identity-compare <before.json> <after.json>");
            }

            IdentityCommands.Compare(ctx, cli.Positionals[0], cli.Positionals[1]);
            break;
        case "watch":
            WithDevice(ctx, cli, (_, device, _) => WatchCommand.Run(ctx, device, Math.Clamp(cli.GetInt("seconds", 120), 5, 3600)));
            break;
        case "wmi-sources":
            await MediaImportCommands.SourcesAsync(ctx);
            break;
        case "wmi-find":
            await MediaImportCommands.FindAsync(ctx, cli, await MediaImportCommands.SourcesAsync(ctx));
            break;
        case "report":
            await ReportCommand.RunAsync(ctx, cli);
            break;
        case "delete-experiment":
            GuardedExperiments.Delete(ctx, cli, DeviceSelection.Select(ctx, cli));
            break;
        case "write-experiment":
            await GuardedExperiments.WriteAsync(ctx, cli, DeviceSelection.Select(ctx, cli));
            break;
        default:
            throw new ProbeUsageException($"Unknown command '{cli.Command}'. Run with 'help'.");
    }

    var report = ctx.SaveReport($"M000 Apple device probe — {cli.Command}");
    ctx.Good($"Sanitized report: {report}");
    if (Directory.Exists(Path.Combine(ctx.RunDirectory, "raw")))
    {
        ctx.Warn($"Local-only raw data (personal; never commit): {Path.Combine(ctx.RunDirectory, "raw")}");
    }

    return 0;
}
catch (ProbeUsageException ex)
{
    ctx.Error(ex.Message);
    return 2;
}
catch (ProbeAbortedException ex)
{
    ctx.Error(ex.Message);
    ctx.SaveReport($"M000 Apple device probe — {cli.Command} (aborted)");
    return 3;
}
catch (Exception ex)
{
    ctx.Error($"Unexpected failure: {WpdInterop.Describe(ex)}");
    ctx.Error(ex.ToString());
    ctx.SaveReport($"M000 Apple device probe — {cli.Command} (failed)");
    return 4;
}

static void WithDevice(RunContext ctx, CommandLine cli, Action<WpdDeviceInfo, WpdDevice, TimeSpan> action)
{
    var info = DeviceSelection.Select(ctx, cli);
    var (device, latency) = InspectCommand.OpenTimed(info, WpdAccess.ReadOnly);
    using (device)
    {
        action(info, device, latency);
    }
}

internal static class Help
{
    internal const string Text = """
        PhotoArchive M000 Apple device capability probe (spike-only; documented Windows APIs only)

        Read-only commands (the device is opened with GENERIC_READ):
          env                         Windows/.NET/WPD/Apple-component inventory (no device needed)
          devices [--watch SECONDS]   List WPD devices; optionally watch arrivals/removals
          inspect                     Device properties, supported WPD commands, storage access capability, events
          enumerate [--max N] [--batch N] [--cancel-after N] [--ids-only] [--max-depth N]
                                      Progressive enumeration with per-object properties and timing
          thumbs [--count N] [--save] Device thumbnails via WPD_RESOURCE_THUMBNAIL
          copy [--count N | --newest N | --object ID] [--keep] [--no-reread] [--max-total-mb N]
                                      Original bytes via staging + size/hash/signature verification
                                      (--newest N = the N most recent captures; hashes go to raw\copies.jsonl)
          identity-snapshot [--snapshot-out PATH]
          identity-compare BEFORE.json AFTER.json
                                      Object ID / PUID stability across reconnects
          watch [--seconds N]         WPD events (photo taken/deleted on phone, lock, unplug)
          wmi-sources                 Windows.Media.Import sources and storage access modes
          wmi-find [--source N] [--cancel-after-ms N] [--thumbs N] [--import N]
                                      Windows.Media.Import FindItemsAsync (+ optional bounded import)
          report [--max N] [--thumbs N] [--count N] [--skip-thumbs] [--skip-copy] [--skip-wmi]
                                      All read-only experiments -> report.md / report.json

        Guarded experiments (opt-in; use a throwaway photo; see README before running):
          delete-experiment --i-understand-this-deletes-from-my-iphone [--object ID]
          write-experiment  --i-understand-this-may-add-a-test-image-to-my-iphone

        Common options:
          --device N | auto   Select device by index from `devices` (default: the single Apple device)
          --out DIR           Run folder root (default %LOCALAPPDATA%\PhotoArchive\M000-AppleDevice\runs)
          --raw               Also write unsanitized per-object data under <run>\raw (never commit)
          --no-sanitize       Do not redact names/dates/serials in report.md (never post publicly)
        """;
}
