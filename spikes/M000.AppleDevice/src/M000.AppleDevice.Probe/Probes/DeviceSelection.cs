using System.Globalization;
using PhotoArchive.Spikes.AppleDevice.Cli;
using PhotoArchive.Spikes.AppleDevice.Output;
using PhotoArchive.Spikes.AppleDevice.Wpd;

namespace PhotoArchive.Spikes.AppleDevice.Probes;

internal static class DeviceSelection
{
    internal const string NoDeviceHelp =
        "No Apple device is visible through Windows Portable Devices. Check: (1) the iPhone is connected by a data-capable USB cable, " +
        "(2) it is UNLOCKED, (3) you tapped Trust on the 'Trust This Computer?' prompt, (4) Apple Devices (Microsoft Store) is installed, " +
        "(5) 'Apple iPhone' appears under Portable Devices in Device Manager. Run `devices` to list what Windows sees.";

    /// <summary>
    /// Resolves <c>--device</c>: omitted/"auto" = the single Apple-looking device; a number = index from the <c>devices</c> list.
    /// </summary>
    internal static WpdDeviceInfo Select(RunContext ctx, CommandLine cli)
    {
        var devices = WpdDevice.ListDevices(includePrivate: true);
        var selector = cli.Get("device");
        if (!string.IsNullOrEmpty(selector) && !selector.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(selector, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) || index < 0 || index >= devices.Count)
            {
                throw new ProbeUsageException($"--device {selector} is not a valid index (0..{devices.Count - 1}). Run `devices` first.");
            }

            return devices[index];
        }

        var apple = devices.Where(d => d.LooksLikeApple).ToList();
        if (apple.Count == 1)
        {
            ctx.Info($"Selected Apple device: manufacturer='{apple[0].Manufacturer}', description='{apple[0].Description}', id={ctx.Sanitizer.PnpId(apple[0].PnpId)}");
            return apple[0];
        }

        if (apple.Count > 1)
        {
            throw new ProbeUsageException("More than one Apple device is connected; pass --device <index> (see `devices`).");
        }

        throw new ProbeAbortedException(NoDeviceHelp);
    }

    /// <summary>
    /// Refuses to place device media (copies, thumbnails) inside a git working tree, so personal media cannot be
    /// committed to the public repository by accident.
    /// </summary>
    internal static void EnsureOutsideGitWorkingTree(RunContext ctx)
    {
        for (var dir = new DirectoryInfo(ctx.RunDirectory); dir is not null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")) || File.Exists(Path.Combine(dir.FullName, ".git")))
            {
                throw new ProbeAbortedException(
                    $"Refusing to write device media under a git working tree ({dir.FullName}). " +
                    "Use the default output location or pass --out <folder outside any repository>.");
            }
        }
    }
}
