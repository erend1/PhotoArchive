using System.Runtime.InteropServices;
using Windows.Win32.Devices.PortableDevices;
using Windows.Win32.Foundation;

namespace PhotoArchive.Spikes.AppleDevice.Wpd;

/// <summary>
/// Receives WPD events (IPortableDevice::Advise) such as WPD_EVENT_OBJECT_ADDED / _REMOVED / WPD_EVENT_DEVICE_REMOVED.
/// Used to test whether the connection exposes in-session change notifications (M000 capability 16) and how a
/// disconnect is surfaced (capability 18). Events only exist while connected; they are not a persistent change log.
/// </summary>
[ComVisible(true)]
internal sealed class WpdEventSink(Action<List<(PROPERTYKEY Key, object? Value)>> onEvent) : IPortableDeviceEventCallback
{
    public void OnEvent(IPortableDeviceValues pEventParameters)
    {
        try
        {
            onEvent(WpdInterop.ReadAll(pEventParameters));
        }
        catch (Exception ex)
        {
            // Never let an exception cross the COM boundary into the WPD runtime.
            Console.Error.WriteLine($"[event sink] {ex.GetType().Name}: {ex.Message}");
        }
    }
}
