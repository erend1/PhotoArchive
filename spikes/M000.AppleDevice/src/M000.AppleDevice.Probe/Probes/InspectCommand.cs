using System.Diagnostics;
using System.Globalization;
using PhotoArchive.Spikes.AppleDevice.Output;
using PhotoArchive.Spikes.AppleDevice.Wpd;
using Windows.Win32;
using Windows.Win32.Devices.PortableDevices;
using Windows.Win32.Foundation;

namespace PhotoArchive.Spikes.AppleDevice.Probes;

/// <summary>
/// Non-destructive capability discovery: device properties, supported WPD commands (does the driver/device offer
/// delete or create-with-data?), functional objects, content types/formats, events, and storage access capability.
/// </summary>
internal static class InspectCommand
{
    internal static InspectResult Run(RunContext ctx, WpdDeviceInfo info, WpdDevice device, TimeSpan openLatency)
    {
        var result = new InspectResult();
        var section = ctx.Report.Section("Device and driver capabilities (WPD, non-destructive)");
        section.Fact("Device selected (sanitized PnP id)", ctx.Sanitizer.PnpId(info.PnpId))
            .Fact("Manager manufacturer", info.Manufacturer)
            .Fact("Manager description", info.Description)
            .Fact("Manager friendly name", ctx.Sanitizer.DeviceName(info.FriendlyName))
            .Fact("Session access requested", device.Access == WpdAccess.ReadOnly ? "GENERIC_READ (read-only)" : "GENERIC_READ|GENERIC_WRITE (guarded experiment)")
            .Fact("IPortableDevice::Open latency (ms)", openLatency.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture));

        // Device object properties.
        try
        {
            var props = device.GetAllProperties(PInvoke.WPD_DEVICE_OBJECT_ID);
            section.Line("Device object (`DEVICE`) properties:");
            section.Line(string.Empty);
            section.Table(["Property", "Value"], props.Select(p => (IReadOnlyList<string>)[WpdNames.Key(p.Key), EnumerateCommand.SanitizedValue(ctx, p.Key, p.Value)]));
            result.Firmware = props.FirstOrDefault(p => Is(p.Key, PInvoke.WPD_DEVICE_FIRMWARE_VERSION)).Value as string;
            result.Model = props.FirstOrDefault(p => Is(p.Key, PInvoke.WPD_DEVICE_MODEL)).Value as string;
            result.Protocol = props.FirstOrDefault(p => Is(p.Key, PInvoke.WPD_DEVICE_PROTOCOL)).Value as string;
            section.Fact("Device model", result.Model).Fact("Device firmware (iOS version as reported)", result.Firmware).Fact("Device protocol", result.Protocol);
        }
        catch (Exception ex)
        {
            section.Fact("Device properties", "FAILED: " + WpdInterop.Describe(ex));
        }

        // Supported commands: the device/driver's own declaration of what can be done.
        try
        {
            var commands = device.GetSupportedCommands().Select(WpdNames.Key).OrderBy(s => s, StringComparer.Ordinal).ToList();
            result.SupportsDelete = commands.Contains(nameof(PInvoke.WPD_COMMAND_OBJECT_MANAGEMENT_DELETE_OBJECTS));
            result.SupportsCreateWithData = commands.Contains(nameof(PInvoke.WPD_COMMAND_OBJECT_MANAGEMENT_CREATE_OBJECT_WITH_PROPERTIES_AND_DATA));
            section.Fact("Driver advertises WPD_COMMAND_OBJECT_MANAGEMENT_DELETE_OBJECTS", result.SupportsDelete)
                .Fact("Driver advertises ..._CREATE_OBJECT_WITH_PROPERTIES_AND_DATA", result.SupportsCreateWithData)
                .Fact("Driver advertises WPD_COMMAND_OBJECT_PROPERTIES_SET", commands.Contains(nameof(PInvoke.WPD_COMMAND_OBJECT_PROPERTIES_SET)))
                .Fact("Driver advertises bulk property retrieval", commands.Contains(nameof(PInvoke.WPD_COMMAND_OBJECT_PROPERTIES_BULK_GET_VALUES_BY_OBJECT_LIST_START)));
            section.Line("Supported WPD commands: " + string.Join(", ", commands.Select(c => $"`{c}`")));
            section.Line(string.Empty);
            if (result.SupportsDelete)
            {
                try
                {
                    var options = device.GetCommandOptions(PInvoke.WPD_COMMAND_OBJECT_MANAGEMENT_DELETE_OBJECTS);
                    section.Line("Delete command options: " + string.Join(", ", options.Select(o => $"`{WpdNames.Key(o.Key)}={ReportDocument.Format(o.Value)}`")));
                }
                catch (Exception ex)
                {
                    section.Line("Delete command options: FAILED " + WpdInterop.Describe(ex));
                }
            }
        }
        catch (Exception ex)
        {
            section.Fact("Supported commands", "FAILED: " + WpdInterop.Describe(ex));
        }

        // Functional categories / objects, content types and formats.
        try
        {
            foreach (var category in device.GetFunctionalCategories())
            {
                var objects = SafeList(() => device.GetFunctionalObjects(category));
                var types = SafeList(() => device.GetSupportedContentTypes(category));
                section.Line($"- Functional category `{WpdNames.Guid(category)}`: objects={objects.Count}, content types: "
                             + string.Join(", ", types.Select(t => $"`{WpdNames.Guid(t)}`")));
                foreach (var type in types)
                {
                    var formats = SafeList(() => device.GetSupportedFormats(type));
                    section.Line($"  - `{WpdNames.Guid(type)}` formats: " + string.Join(", ", formats.Select(f => $"`{WpdNames.Guid(f)}`")));
                }

                if (category == PInvoke.WPD_FUNCTIONAL_CATEGORY_STORAGE)
                {
                    result.StorageObjectIds.AddRange(objects);
                }
            }

            section.Line(string.Empty);
        }
        catch (Exception ex)
        {
            section.Fact("Functional categories", "FAILED: " + WpdInterop.Describe(ex));
        }

        try
        {
            var events = device.GetSupportedEvents().Select(g => WpdNames.Guid(g)).ToList();
            section.Fact("Supported events", events.Count == 0 ? "(none)" : string.Join(", ", events));
        }
        catch (Exception ex)
        {
            section.Fact("Supported events", "FAILED: " + WpdInterop.Describe(ex));
        }

        // Storage objects: WPD_STORAGE_ACCESS_CAPABILITY maps PTP StorageInfo.AccessCapability.
        foreach (var storageId in result.StorageObjectIds)
        {
            try
            {
                var props = device.GetAllProperties(storageId);
                var access = props.FirstOrDefault(p => Is(p.Key, PInvoke.WPD_STORAGE_ACCESS_CAPABILITY)).Value;
                var accessText = access switch
                {
                    uint v => DescribeAccess(v),
                    _ => "(not reported)",
                };
                result.StorageAccess.Add(accessText);
                section.Line($"Storage object {ctx.Sanitizer.Opaque(storageId, "storage")} — WPD_STORAGE_ACCESS_CAPABILITY: **{accessText}**");
                section.Line(string.Empty);
                section.Table(["Property", "Value"], props.Select(p => (IReadOnlyList<string>)[WpdNames.Key(p.Key), EnumerateCommand.SanitizedValue(ctx, p.Key, p.Value)]));
            }
            catch (Exception ex)
            {
                section.Line($"Storage object properties FAILED: {WpdInterop.Describe(ex)}");
            }
        }

        section.Fact("Storage access capability (all storages)", result.StorageAccess.Count == 0 ? "(no storage object found)" : string.Join("; ", result.StorageAccess));
        foreach (var (key, value) in section.Facts)
        {
            ctx.Info($"{key}: {ReportDocument.Format(value)}");
        }

        return result;
    }

    internal static (WpdDevice Device, TimeSpan Latency) OpenTimed(WpdDeviceInfo info, WpdAccess access)
    {
        var sw = Stopwatch.StartNew();
        var device = WpdDevice.Open(info.PnpId, access);
        return (device, sw.Elapsed);
    }

    internal static string DescribeAccess(uint value) => value switch
    {
        (uint)WPD_STORAGE_ACCESS_CAPABILITY_VALUES.WPD_STORAGE_ACCESS_CAPABILITY_READWRITE => "READWRITE (0)",
        (uint)WPD_STORAGE_ACCESS_CAPABILITY_VALUES.WPD_STORAGE_ACCESS_CAPABILITY_READ_ONLY_WITHOUT_OBJECT_DELETION => "READ_ONLY_WITHOUT_OBJECT_DELETION (1)",
        (uint)WPD_STORAGE_ACCESS_CAPABILITY_VALUES.WPD_STORAGE_ACCESS_CAPABILITY_READ_ONLY_WITH_OBJECT_DELETION => "READ_ONLY_WITH_OBJECT_DELETION (2)",
        _ => $"unknown ({value})",
    };

    private static List<T> SafeList<T>(Func<List<T>> query)
    {
        try
        {
            return query();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static bool Is(PROPERTYKEY a, PROPERTYKEY b) => a.fmtid == b.fmtid && a.pid == b.pid;
}

internal sealed class InspectResult
{
    public string? Firmware { get; set; }

    public string? Model { get; set; }

    public string? Protocol { get; set; }

    public bool SupportsDelete { get; set; }

    public bool SupportsCreateWithData { get; set; }

    public List<string> StorageObjectIds { get; } = [];

    public List<string> StorageAccess { get; } = [];
}
