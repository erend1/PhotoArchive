using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Windows.Win32;
using Windows.Win32.Devices.PortableDevices;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.System.Com.StructuredStorage;

namespace PhotoArchive.Spikes.AppleDevice.Wpd;

internal sealed record WpdDeviceInfo(string PnpId, string? FriendlyName, string? Manufacturer, string? Description)
{
    public bool LooksLikeApple =>
        (Manufacturer?.Contains("Apple", StringComparison.OrdinalIgnoreCase) ?? false)
        || PnpId.Contains("vid_05ac", StringComparison.OrdinalIgnoreCase);
}

internal enum WpdAccess
{
    /// <summary>GENERIC_READ only. The default for every non-destructive probe command.</summary>
    ReadOnly,

    /// <summary>GENERIC_READ | GENERIC_WRITE. Used only by the explicitly guarded delete/write experiments.</summary>
    ReadWriteForGuardedExperiment,
}

/// <summary>
/// A WPD session to one device through the documented Windows Portable Devices COM API.
/// For an iPhone this talks to the in-box WPD MTP/PTP class driver, i.e. the iPhone's PTP camera
/// interface (DCIM view). It is NOT an Apple Photos-library API.
/// </summary>
internal sealed unsafe class WpdDevice : IDisposable
{
    private WpdDevice(string pnpId, WpdAccess access, IPortableDevice device)
    {
        PnpId = pnpId;
        Access = access;
        Device = device;
        device.Content(out var content);
        Content = content;
        content.Properties(out var properties);
        Properties = properties;
        content.Transfer(out var resources);
        Resources = resources;
        device.Capabilities(out var capabilities);
        Capabilities = capabilities;
    }

    public string PnpId { get; }

    public WpdAccess Access { get; }

    public IPortableDevice Device { get; }

    public IPortableDeviceContent Content { get; }

    public IPortableDeviceProperties Properties { get; }

    public IPortableDeviceResources Resources { get; }

    public IPortableDeviceCapabilities Capabilities { get; }

    internal static List<WpdDeviceInfo> ListDevices(bool includePrivate = false)
    {
        var manager = (IPortableDeviceManager)new PortableDeviceManager();
        var result = new List<WpdDeviceInfo>();
        foreach (var id in GetDeviceIds(manager, includePrivate))
        {
            result.Add(new WpdDeviceInfo(
                id,
                ManagerString(manager, id, ManagerField.FriendlyName),
                ManagerString(manager, id, ManagerField.Manufacturer),
                ManagerString(manager, id, ManagerField.Description)));
        }

        return result;
    }

    internal static WpdDevice Open(string pnpId, WpdAccess access)
    {
        var device = (IPortableDevice)new PortableDeviceFTM();
        var client = WpdInterop.NewValues();
        client.SetStringValue(PInvoke.WPD_CLIENT_NAME, "PhotoArchive M000 Apple device capability probe");
        client.SetUnsignedIntegerValue(PInvoke.WPD_CLIENT_MAJOR_VERSION, 0);
        client.SetUnsignedIntegerValue(PInvoke.WPD_CLIENT_MINOR_VERSION, 1);
        client.SetUnsignedIntegerValue(PInvoke.WPD_CLIENT_REVISION, 0);
        client.SetUnsignedIntegerValue(PInvoke.WPD_CLIENT_SECURITY_QUALITY_OF_SERVICE, WpdInterop.SecurityImpersonation);
        client.SetUnsignedIntegerValue(
            PInvoke.WPD_CLIENT_DESIRED_ACCESS,
            access == WpdAccess.ReadOnly ? WpdInterop.GenericRead : WpdInterop.GenericRead | WpdInterop.GenericWrite);

        fixed (char* id = pnpId)
        {
            device.Open(new PCWSTR(id), client);
        }

        return new WpdDevice(pnpId, access, device);
    }

    /// <summary>All properties the driver reports for an object (GetValues with a NULL key collection = all).</summary>
    internal List<(PROPERTYKEY Key, object? Value)> GetAllProperties(string objectId)
    {
        Properties.GetValues(objectId, null!, out var values);
        return WpdInterop.ReadAll(values);
    }

    internal IPortableDeviceValues GetValues(string objectId, params PROPERTYKEY[] keys)
    {
        var collection = WpdInterop.NewKeyCollection();
        foreach (var key in keys)
        {
            collection.Add(key);
        }

        Properties.GetValues(objectId, collection, out var values);
        return values;
    }

    internal List<PROPERTYKEY> GetSupportedProperties(string objectId)
    {
        Properties.GetSupportedProperties(objectId, out var keys);
        return WpdInterop.ReadKeys(keys);
    }

    /// <summary>Enumerates direct children in batches; each batch is yielded as soon as the driver returns it.</summary>
    internal IEnumerable<string> EnumerateChildren(string parentId, uint batchSize, CancellationToken cancellationToken)
    {
        Content.EnumObjects(0, parentId, null!, out var enumerator);
        var batch = new PWSTR[batchSize];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ids = NextBatch(enumerator, batch, out var fetched, out var done);
            foreach (var id in ids)
            {
                yield return id;
            }

            // Completion is signalled by S_FALSE (or an empty batch); a short batch with S_OK is not the end.
            if (done || fetched == 0)
            {
                yield break;
            }
        }
    }

    private static List<string> NextBatch(IEnumPortableDeviceObjectIDs enumerator, PWSTR[] batch, out uint fetched, out bool done)
    {
        fetched = 0;
        HRESULT hr;
        fixed (PWSTR* p = batch)
        {
            hr = enumerator.Next((uint)batch.Length, p, ref fetched);
        }

        hr.ThrowOnFailure();
        done = hr == HRESULT.S_FALSE;
        var ids = new List<string>((int)fetched);
        for (var i = 0; i < fetched; i++)
        {
            var id = WpdInterop.TakeString(batch[i]);
            batch[i] = default;
            if (id is not null)
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    internal List<PROPERTYKEY> GetSupportedResources(string objectId)
    {
        Resources.GetSupportedResources(objectId, out var keys);
        return WpdInterop.ReadKeys(keys);
    }

    internal List<(PROPERTYKEY Key, object? Value)> GetResourceAttributes(string objectId, in PROPERTYKEY resource)
    {
        Resources.GetResourceAttributes(objectId, resource, out var attributes);
        return WpdInterop.ReadAll(attributes);
    }

    /// <summary>
    /// Streams a resource (WPD_RESOURCE_DEFAULT = the object's bytes, WPD_RESOURCE_THUMBNAIL = device thumbnail)
    /// into <paramref name="destination"/>, hashing as it goes.
    /// </summary>
    internal ResourceReadResult ReadResource(string objectId, in PROPERTYKEY resource, Stream destination, CancellationToken cancellationToken)
    {
        uint optimal = 0;
        Resources.GetStream(objectId, resource, WpdInterop.StgmRead, ref optimal, out IStream stream);
        try
        {
            var buffer = new byte[Math.Clamp((int)optimal, 64 * 1024, 4 * 1024 * 1024)];
            var header = new byte[64];
            var headerLength = 0;
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long total = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                uint read = 0;
                HRESULT hr;
                fixed (byte* p = buffer)
                {
                    hr = stream.Read(p, (uint)buffer.Length, &read);
                }

                hr.ThrowOnFailure();
                if (read == 0)
                {
                    break;
                }

                if (headerLength < header.Length)
                {
                    var take = Math.Min(header.Length - headerLength, (int)read);
                    Array.Copy(buffer, 0, header, headerLength, take);
                    headerLength += take;
                }

                destination.Write(buffer, 0, (int)read);
                sha.AppendData(buffer, 0, (int)read);
                total += read;
            }

            return new ResourceReadResult(total, optimal, header[..headerLength], Convert.ToHexStringLower(sha.GetHashAndReset()));
        }
        finally
        {
            Marshal.ReleaseComObject(stream);
        }
    }

    internal List<PROPERTYKEY> GetSupportedCommands()
    {
        Capabilities.GetSupportedCommands(out var commands);
        return WpdInterop.ReadKeys(commands);
    }

    internal List<(PROPERTYKEY Key, object? Value)> GetCommandOptions(in PROPERTYKEY command)
    {
        Capabilities.GetCommandOptions(command, out var options);
        return WpdInterop.ReadAll(options);
    }

    internal List<Guid> GetFunctionalCategories()
    {
        Capabilities.GetFunctionalCategories(out var categories);
        return WpdInterop.ReadCollection(categories).OfType<Guid>().ToList();
    }

    internal List<string> GetFunctionalObjects(Guid category)
    {
        Capabilities.GetFunctionalObjects(category, out var objects);
        return WpdInterop.ReadCollection(objects).OfType<string>().ToList();
    }

    internal List<Guid> GetSupportedContentTypes(Guid category)
    {
        Capabilities.GetSupportedContentTypes(category, out var types);
        return WpdInterop.ReadCollection(types).OfType<Guid>().ToList();
    }

    internal List<Guid> GetSupportedFormats(Guid contentType)
    {
        Capabilities.GetSupportedFormats(contentType, out var formats);
        return WpdInterop.ReadCollection(formats).OfType<Guid>().ToList();
    }

    internal List<Guid> GetSupportedEvents()
    {
        Capabilities.GetSupportedEvents(out var events);
        return WpdInterop.ReadCollection(events).OfType<Guid>().ToList();
    }

    /// <summary>
    /// DESTRUCTIVE. Deletes exactly one object without recursion. Only reachable from the guarded
    /// <c>delete-experiment</c> command, which requires write access, a completed verified copy, and typed confirmation.
    /// </summary>
    internal WpdHResult DeleteSingleObject(string objectId)
    {
        if (Access != WpdAccess.ReadWriteForGuardedExperiment)
        {
            throw new InvalidOperationException("Delete is only permitted on a session opened for the guarded experiment.");
        }

        var ids = WpdInterop.NewPropVariantCollection();
        var pv = WpdInterop.StringVariant(objectId);
        try
        {
            ids.Add(in pv);
        }
        finally
        {
            WpdInterop.Clear(ref pv);
        }

        IPortableDevicePropVariantCollection results = null!;
        try
        {
            Content.Delete((uint)DELETE_OBJECT_OPTIONS.PORTABLE_DEVICE_DELETE_NO_RECURSION, ids, ref results);
        }
        catch (COMException ex)
        {
            return new WpdHResult(ex.HResult);
        }

        if (results is not null && WpdInterop.ReadCollection(results).FirstOrDefault() is WpdHResult perObject)
        {
            return perObject;
        }

        return new WpdHResult(0);
    }

    /// <summary>
    /// Attempts to create one object with data (PTP SendObjectInfo/SendObject underneath). Only reachable from the
    /// guarded <c>write-experiment</c> command. Returns the created object ID, or throws with the driver's HRESULT.
    /// </summary>
    internal string? CreateObjectWithData(string parentId, string fileName, Guid contentType, Guid format, ReadOnlySpan<byte> data)
    {
        if (Access != WpdAccess.ReadWriteForGuardedExperiment)
        {
            throw new InvalidOperationException("Object creation is only permitted on a session opened for the guarded experiment.");
        }

        var values = WpdInterop.NewValues();
        values.SetStringValue(PInvoke.WPD_OBJECT_PARENT_ID, parentId);
        values.SetStringValue(PInvoke.WPD_OBJECT_NAME, fileName);
        values.SetStringValue(PInvoke.WPD_OBJECT_ORIGINAL_FILE_NAME, fileName);
        values.SetUnsignedLargeIntegerValue(PInvoke.WPD_OBJECT_SIZE, (ulong)data.Length);
        var contentTypeKey = PInvoke.WPD_OBJECT_CONTENT_TYPE;
        var formatKey = PInvoke.WPD_OBJECT_FORMAT;
        values.SetGuidValue(&contentTypeKey, &contentType);
        values.SetGuidValue(&formatKey, &format);

        uint optimal = 0;
        PWSTR cookie = default;
        Content.CreateObjectWithPropertiesAndData(values, out IStream stream, ref optimal, ref cookie);
        WpdInterop.TakeString(cookie);
        try
        {
            fixed (byte* p = data)
            {
                uint written = 0;
                stream.Write(p, (uint)data.Length, &written).ThrowOnFailure();
            }

            stream.Commit(STGC.STGC_DEFAULT);
            if (stream is IPortableDeviceDataStream dataStream)
            {
                dataStream.GetObjectID(out PWSTR id);
                return WpdInterop.TakeString(id);
            }

            return null;
        }
        finally
        {
            Marshal.ReleaseComObject(stream);
        }
    }

    internal string Advise(IPortableDeviceEventCallback callback)
    {
        PWSTR cookie = default;
        Device.Advise(0, callback, null!, &cookie);
        return WpdInterop.TakeString(cookie) ?? string.Empty;
    }

    internal void Unadvise(string cookie)
    {
        fixed (char* c = cookie)
        {
            Device.Unadvise(new PCWSTR(c));
        }
    }

    public void Dispose()
    {
        try
        {
            Device.Close();
        }
        catch (COMException)
        {
            // Device may already be gone (e.g. unplugged mid-session); nothing else to release safely.
        }
    }

    private enum ManagerField
    {
        FriendlyName,
        Manufacturer,
        Description,
    }

    private static List<string> GetDeviceIds(IPortableDeviceManager manager, bool includePrivate)
    {
        var ids = new List<string>();
        uint count = 0;
        manager.GetDevices(null, ref count);
        if (count > 0)
        {
            var buffer = new PWSTR[count];
            fixed (PWSTR* p = buffer)
            {
                manager.GetDevices(p, ref count);
            }

            ids.AddRange(buffer.Take((int)count).Select(WpdInterop.TakeString).OfType<string>());
        }

        if (includePrivate)
        {
            uint privateCount = 0;
            manager.GetPrivateDevices(null, ref privateCount);
            if (privateCount > 0)
            {
                var buffer = new PWSTR[privateCount];
                fixed (PWSTR* p = buffer)
                {
                    manager.GetPrivateDevices(p, ref privateCount);
                }

                ids.AddRange(buffer.Take((int)privateCount).Select(WpdInterop.TakeString).OfType<string>());
            }
        }

        return ids;
    }

    private static string? ManagerString(IPortableDeviceManager manager, string id, ManagerField field)
    {
        try
        {
            fixed (char* pid = id)
            {
                uint length = 0;
                Call(manager, field, new PCWSTR(pid), default, ref length);
                if (length == 0)
                {
                    return null;
                }

                var buffer = new char[length];
                fixed (char* b = buffer)
                {
                    Call(manager, field, new PCWSTR(pid), new PWSTR(b), ref length);
                }

                return new string(buffer).TrimEnd('\0');
            }
        }
        catch (COMException)
        {
            return null;
        }

        static void Call(IPortableDeviceManager m, ManagerField f, PCWSTR id, PWSTR buffer, ref uint length)
        {
            switch (f)
            {
                case ManagerField.FriendlyName:
                    m.GetDeviceFriendlyName(id, buffer, ref length);
                    break;
                case ManagerField.Manufacturer:
                    m.GetDeviceManufacturer(id, buffer, ref length);
                    break;
                default:
                    m.GetDeviceDescription(id, buffer, ref length);
                    break;
            }
        }
    }
}

internal sealed record ResourceReadResult(long BytesRead, uint OptimalBufferSize, byte[] Header, string Sha256);
