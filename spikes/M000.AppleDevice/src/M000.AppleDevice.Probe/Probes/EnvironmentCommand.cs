using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using PhotoArchive.Spikes.AppleDevice.Output;
using PhotoArchive.Spikes.AppleDevice.Wpd;
using Windows.Management.Deployment;
using Windows.Media.Import;

namespace PhotoArchive.Spikes.AppleDevice.Probes;

/// <summary>Records the Windows-side environment. Needs no device.</summary>
internal static class EnvironmentCommand
{
    internal static async Task RunAsync(RunContext ctx)
    {
        var section = ctx.Report.Section("Environment (Windows side)");
        var cv = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        var build = int.TryParse(cv?.GetValue("CurrentBuildNumber") as string, out var b) ? b : 0;
        section.Fact("Windows edition (derived from build; registry ProductName still says 'Windows 10' on Windows 11)",
                build >= 22000 ? "Windows 11" : build > 0 ? "Windows 10" : "unknown")
            .Fact("Windows product (registry ProductName)", cv?.GetValue("ProductName") as string)
            .Fact("Windows display version", cv?.GetValue("DisplayVersion") as string)
            .Fact("Windows build", $"{cv?.GetValue("CurrentBuildNumber")}.{cv?.GetValue("UBR")}")
            .Fact("OS description", RuntimeInformation.OSDescription)
            .Fact("Process architecture", RuntimeInformation.ProcessArchitecture.ToString())
            .Fact(".NET runtime", RuntimeInformation.FrameworkDescription)
            .Fact("Probe version", typeof(EnvironmentCommand).Assembly.GetName().Version?.ToString());

        var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        foreach (var dll in new[] { "PortableDeviceApi.dll", "PortableDeviceTypes.dll", "WpdShext.dll", "wpdmtp.dll", "wpdmtpdr.dll" })
        {
            var path = Path.Combine(system32, dll);
            section.Fact($"{dll} version", File.Exists(path) ? FileVersionInfo.GetVersionInfo(path).FileVersion : "(not present)");
        }

        section.Fact("WPD name table size (keys/GUIDs)", $"{WpdNames.KnownKeyCount}/{WpdNames.KnownGuidCount}");

        try
        {
            section.Fact("Windows.Media.Import PhotoImportManager.IsSupportedAsync()", await PhotoImportManager.IsSupportedAsync());
        }
        catch (Exception ex)
        {
            section.Fact("Windows.Media.Import PhotoImportManager.IsSupportedAsync()", "FAILED: " + WpdInterop.Describe(ex));
        }

        // Apple components. Apple Devices / iTunes / iCloud are MSIX/Store packages; Apple Mobile Device Support is classic MSI.
        section.Fact("Apple Store packages (current user)", DescribeApplePackages());
        section.Fact("Apple classic installs (uninstall registry)", DescribeAppleUninstallEntries());
        section.Fact("Apple USB driver package (appleusb.inf) in driver store", AppleDriverInStore());

        section.Line("Evidence level for this section: EXECUTED on the machine that produced this report.");
        foreach (var (key, value) in section.Facts)
        {
            ctx.Info($"{key}: {ReportDocument.Format(value)}");
        }
    }

    private static string DescribeApplePackages()
    {
        try
        {
            var packages = new PackageManager().FindPackagesForUser(string.Empty)
                .Where(p => p.Id.Publisher.Contains("Apple", StringComparison.OrdinalIgnoreCase)
                            || p.Id.Name.StartsWith("AppleInc.", StringComparison.OrdinalIgnoreCase))
                .Select(p => $"{p.Id.Name} {p.Id.Version.Major}.{p.Id.Version.Minor}.{p.Id.Version.Build}.{p.Id.Version.Revision}")
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToList();
            return packages.Count == 0 ? "(none)" : string.Join("; ", packages);
        }
        catch (Exception ex)
        {
            return "(query failed: " + ex.GetType().Name + ")";
        }
    }

    private static string DescribeAppleUninstallEntries()
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (hive, path) in new[]
                 {
                     (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                     (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
                     (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                 })
        {
            using var root = hive.OpenSubKey(path);
            if (root is null)
            {
                continue;
            }

            foreach (var name in root.GetSubKeyNames())
            {
                using var key = root.OpenSubKey(name);
                var publisher = key?.GetValue("Publisher") as string;
                var display = key?.GetValue("DisplayName") as string;
                if (display is not null && ((publisher?.Contains("Apple", StringComparison.OrdinalIgnoreCase) ?? false)
                                            || display.Contains("Apple", StringComparison.OrdinalIgnoreCase)
                                            || display.Contains("iTunes", StringComparison.OrdinalIgnoreCase)
                                            || display.Contains("Bonjour", StringComparison.OrdinalIgnoreCase)))
                {
                    found.Add($"{display} {key?.GetValue("DisplayVersion")}".Trim());
                }
            }
        }

        return found.Count == 0 ? "(none)" : string.Join("; ", found);
    }

    private static string AppleDriverInStore()
    {
        var repository = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "DriverStore", "FileRepository");
        try
        {
            var dirs = Directory.GetDirectories(repository, "appleusb.inf_*");
            return dirs.Length == 0 ? "no" : $"yes ({dirs.Length} package folder(s))";
        }
        catch (Exception ex)
        {
            return "(query failed: " + ex.GetType().Name + ")";
        }
    }
}
