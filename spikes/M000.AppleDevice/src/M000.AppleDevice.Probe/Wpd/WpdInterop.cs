using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Devices.PortableDevices;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com.StructuredStorage;
using Windows.Win32.System.Variant;

namespace PhotoArchive.Spikes.AppleDevice.Wpd;

/// <summary>Low-level helpers around the documented WPD COM API (PortableDeviceApi.h / PortableDevice.h).</summary>
internal static unsafe class WpdInterop
{
    internal const uint GenericRead = 0x80000000;
    internal const uint GenericWrite = 0x40000000;
    internal const uint SecurityImpersonation = 0x00020000; // SECURITY_IMPERSONATION (SecurityImpersonation << 16)
    internal const uint StgmRead = 0x00000000;

    internal static IPortableDeviceValues NewValues() => (IPortableDeviceValues)new PortableDeviceValues();

    internal static IPortableDeviceKeyCollection NewKeyCollection() => (IPortableDeviceKeyCollection)new PortableDeviceKeyCollection();

    internal static IPortableDevicePropVariantCollection NewPropVariantCollection() =>
        (IPortableDevicePropVariantCollection)new PortableDevicePropVariantCollection();

    /// <summary>Converts a CoTaskMem-allocated WPD string to managed and frees it.</summary>
    internal static string? TakeString(PWSTR value)
    {
        if (value.Value == null)
        {
            return null;
        }

        try
        {
            return new string(value.Value);
        }
        finally
        {
            Marshal.FreeCoTaskMem((nint)value.Value);
        }
    }

    internal static void Clear(ref PROPVARIANT value) => PInvoke.PropVariantClear(ref value);

    /// <summary>Converts a PROPVARIANT to a managed value for reporting. Does not clear it.</summary>
    internal static object? ToManaged(in PROPVARIANT value)
    {
        var inner = value.Anonymous.Anonymous;
        var u = inner.Anonymous;
        return inner.vt switch
        {
            VARENUM.VT_EMPTY => null,
            VARENUM.VT_LPWSTR => u.pwszVal.Value == null ? null : new string(u.pwszVal.Value),
            VARENUM.VT_BSTR => u.bstrVal.ToString(),
            VARENUM.VT_UI1 => u.bVal,
            VARENUM.VT_I2 => u.iVal,
            VARENUM.VT_UI2 => u.uiVal,
            VARENUM.VT_I4 => u.lVal,
            VARENUM.VT_UI4 => u.ulVal,
            VARENUM.VT_INT => u.intVal,
            VARENUM.VT_UINT => u.uintVal,
            VARENUM.VT_I8 => u.hVal,
            VARENUM.VT_UI8 => u.uhVal,
            VARENUM.VT_R4 => u.fltVal,
            VARENUM.VT_R8 => u.dblVal,
            VARENUM.VT_BOOL => (bool)u.boolVal,
            VARENUM.VT_ERROR => new WpdHResult(u.scode),
            VARENUM.VT_DATE => DateTime.FromOADate(u.date),
            VARENUM.VT_FILETIME => DateTime.FromFileTimeUtc(((long)u.filetime.dwHighDateTime << 32) | (uint)u.filetime.dwLowDateTime),
            VARENUM.VT_CLSID => u.puuid == null ? null : *u.puuid,
            VARENUM.VT_UNKNOWN => "(IUnknown value)",
            VARENUM.VT_BLOB => $"(blob, {u.blob.cbSize} bytes)",
            VARENUM.VT_VECTOR | VARENUM.VT_UI1 => $"(byte vector, {u.caub.cElems} bytes)",
            _ => $"(unsupported VARTYPE 0x{(ushort)inner.vt:X4})",
        };
    }

    internal static PROPVARIANT StringVariant(string text)
    {
        var pv = default(PROPVARIANT);
        pv.Anonymous.Anonymous.vt = VARENUM.VT_LPWSTR;
        pv.Anonymous.Anonymous.Anonymous.pwszVal = new PWSTR((char*)Marshal.StringToCoTaskMemUni(text));
        return pv; // caller must PropVariantClear (frees the CoTaskMem string)
    }

    internal static List<object?> ReadCollection(IPortableDevicePropVariantCollection collection)
    {
        var items = new List<object?>();
        uint count = 0;
        collection.GetCount(in count);
        for (uint i = 0; i < count; i++)
        {
            var pv = default(PROPVARIANT);
            collection.GetAt(i, in pv);
            try
            {
                items.Add(ToManaged(pv));
            }
            finally
            {
                Clear(ref pv);
            }
        }

        return items;
    }

    internal static List<PROPERTYKEY> ReadKeys(IPortableDeviceKeyCollection keys)
    {
        var list = new List<PROPERTYKEY>();
        uint count = 0;
        keys.GetCount(in count);
        for (uint i = 0; i < count; i++)
        {
            var key = default(PROPERTYKEY);
            keys.GetAt(i, &key);
            list.Add(key);
        }

        return list;
    }

    /// <summary>Reads every (key, value) pair from an IPortableDeviceValues bag.</summary>
    internal static List<(PROPERTYKEY Key, object? Value)> ReadAll(IPortableDeviceValues values)
    {
        var list = new List<(PROPERTYKEY, object?)>();
        uint count = 0;
        values.GetCount(in count);
        for (uint i = 0; i < count; i++)
        {
            var key = default(PROPERTYKEY);
            var pv = default(PROPVARIANT);
            values.GetAt(i, ref key, ref pv);
            try
            {
                list.Add((key, ToManaged(pv)));
            }
            finally
            {
                Clear(ref pv);
            }
        }

        return list;
    }

    internal static string? GetString(IPortableDeviceValues values, in PROPERTYKEY key)
    {
        try
        {
            values.GetStringValue(key, out PWSTR value);
            return TakeString(value);
        }
        catch (COMException)
        {
            return null;
        }
    }

    internal static ulong? GetULong(IPortableDeviceValues values, in PROPERTYKEY key)
    {
        try
        {
            values.GetUnsignedLargeIntegerValue(key, out ulong value);
            return value;
        }
        catch (COMException)
        {
            try
            {
                values.GetUnsignedIntegerValue(key, out uint small);
                return small;
            }
            catch (COMException)
            {
                return null;
            }
        }
    }

    internal static uint? GetUInt(IPortableDeviceValues values, in PROPERTYKEY key)
    {
        try
        {
            values.GetUnsignedIntegerValue(key, out uint value);
            return value;
        }
        catch (COMException)
        {
            return null;
        }
    }

    internal static bool? GetBool(IPortableDeviceValues values, in PROPERTYKEY key)
    {
        try
        {
            BOOL value;
            var k = key;
            values.GetBoolValue(&k, &value);
            return value != 0;
        }
        catch (COMException)
        {
            return null;
        }
    }

    internal static Guid? GetGuid(IPortableDeviceValues values, in PROPERTYKEY key)
    {
        try
        {
            Guid value;
            var k = key;
            values.GetGuidValue(&k, &value);
            return value;
        }
        catch (COMException)
        {
            return null;
        }
    }

    internal static object? GetAny(IPortableDeviceValues values, in PROPERTYKEY key)
    {
        try
        {
            values.GetValue(key, out PROPVARIANT pv);
            try
            {
                return ToManaged(pv);
            }
            finally
            {
                Clear(ref pv);
            }
        }
        catch (COMException)
        {
            return null;
        }
    }

    internal static string Describe(Exception ex) => ex switch
    {
        COMException com => $"{WpdHResult.Describe(com.HResult)} ({com.Message.Trim()})",
        UnauthorizedAccessException ua => $"{WpdHResult.Describe(ua.HResult)} (access denied: {ua.Message.Trim()})",
        _ => $"{WpdHResult.Describe(ex.HResult)} {ex.GetType().Name}: {ex.Message.Trim()}",
    };
}

/// <summary>An HRESULT value surfaced in reports with a well-known name where one exists.</summary>
internal readonly record struct WpdHResult(int Value)
{
    private static readonly Dictionary<int, string> Known = new()
    {
        [unchecked((int)0x80070005)] = "E_ACCESSDENIED",
        [unchecked((int)0x80070057)] = "E_INVALIDARG",
        [unchecked((int)0x80004001)] = "E_NOTIMPL",
        [unchecked((int)0x80004005)] = "E_FAIL",
        [unchecked((int)0x8007000E)] = "E_OUTOFMEMORY",
        [unchecked((int)0x80070032)] = "HRESULT_FROM_WIN32(ERROR_NOT_SUPPORTED)",
        [unchecked((int)0x80070490)] = "HRESULT_FROM_WIN32(ERROR_NOT_FOUND)",
        [unchecked((int)0x8007001F)] = "HRESULT_FROM_WIN32(ERROR_GEN_FAILURE) 'A device attached to the system is not functioning'",
        [unchecked((int)0x8007048F)] = "HRESULT_FROM_WIN32(ERROR_DEVICE_NOT_CONNECTED)",
        [unchecked((int)0x800704C7)] = "HRESULT_FROM_WIN32(ERROR_CANCELLED)",
        [unchecked((int)0x800710DF)] = "HRESULT_FROM_WIN32(ERROR_DEVICE_NOT_AVAILABLE)",
        [unchecked((int)0x80070091)] = "HRESULT_FROM_WIN32(ERROR_DIR_NOT_EMPTY)",
        [unchecked((int)0x800710DD)] = "HRESULT_FROM_WIN32(ERROR_INVALID_OPERATION)",
        [unchecked((int)0x80070079)] = "HRESULT_FROM_WIN32(ERROR_SEM_TIMEOUT)",
        [unchecked((int)0x800705B4)] = "HRESULT_FROM_WIN32(ERROR_TIMEOUT)",
        [unchecked((int)0x80070013)] = "HRESULT_FROM_WIN32(ERROR_WRITE_PROTECT)",
        [HRESULT.E_WPD_DEVICE_ALREADY_OPENED.Value] = nameof(HRESULT.E_WPD_DEVICE_ALREADY_OPENED),
        [HRESULT.E_WPD_DEVICE_NOT_OPEN.Value] = nameof(HRESULT.E_WPD_DEVICE_NOT_OPEN),
        [HRESULT.E_WPD_OBJECT_ALREADY_ATTACHED_TO_DEVICE.Value] = nameof(HRESULT.E_WPD_OBJECT_ALREADY_ATTACHED_TO_DEVICE),
        [HRESULT.E_WPD_OBJECT_NOT_ATTACHED_TO_DEVICE.Value] = nameof(HRESULT.E_WPD_OBJECT_NOT_ATTACHED_TO_DEVICE),
        [HRESULT.E_WPD_OBJECT_NOT_COMMITED.Value] = nameof(HRESULT.E_WPD_OBJECT_NOT_COMMITED),
        [HRESULT.E_WPD_DEVICE_IS_HUNG.Value] = nameof(HRESULT.E_WPD_DEVICE_IS_HUNG),
        [HRESULT.E_WPD_SMS_INVALID_RECIPIENT.Value] = nameof(HRESULT.E_WPD_SMS_INVALID_RECIPIENT),
        [HRESULT.E_WPD_SERVICE_BAD_PARAMETER_ORDER.Value] = nameof(HRESULT.E_WPD_SERVICE_BAD_PARAMETER_ORDER),
    };

    public static string Describe(int hr) =>
        Known.TryGetValue(hr, out var name) ? $"0x{hr:X8} {name}" : $"0x{hr:X8}";

    public override string ToString() => Describe(Value);
}
