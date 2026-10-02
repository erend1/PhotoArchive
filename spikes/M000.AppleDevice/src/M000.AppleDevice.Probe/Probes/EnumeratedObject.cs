using Windows.Win32;
using Windows.Win32.Foundation;

namespace PhotoArchive.Spikes.AppleDevice.Probes;

/// <summary>One object seen through WPD, with every property the driver reported for it.</summary>
internal sealed class EnumeratedObject
{
    public required string ObjectId { get; init; }

    public required string ParentId { get; init; }

    public required int Depth { get; init; }

    public string? ParentName { get; init; }

    public List<(PROPERTYKEY Key, object? Value)> Properties { get; init; } = [];

    public string? PropertyError { get; init; }

    public string? Name => Get<string>(PInvoke.WPD_OBJECT_NAME);

    public string? OriginalFileName => Get<string>(PInvoke.WPD_OBJECT_ORIGINAL_FILE_NAME);

    public string? PersistentUniqueId => Get<string>(PInvoke.WPD_OBJECT_PERSISTENT_UNIQUE_ID);

    public Guid? ContentType => GetStruct<Guid>(PInvoke.WPD_OBJECT_CONTENT_TYPE);

    public Guid? Format => GetStruct<Guid>(PInvoke.WPD_OBJECT_FORMAT);

    public ulong? Size => GetNumber(PInvoke.WPD_OBJECT_SIZE);

    public DateTime? DateCreated => GetStruct<DateTime>(PInvoke.WPD_OBJECT_DATE_CREATED);

    public DateTime? DateModified => GetStruct<DateTime>(PInvoke.WPD_OBJECT_DATE_MODIFIED);

    public bool? CanDelete => GetStruct<bool>(PInvoke.WPD_OBJECT_CAN_DELETE);

    public ulong? Width => GetNumber(PInvoke.WPD_MEDIA_WIDTH);

    public ulong? Height => GetNumber(PInvoke.WPD_MEDIA_HEIGHT);

    public ulong? Duration => GetNumber(PInvoke.WPD_MEDIA_DURATION);

    public string FileName => OriginalFileName ?? Name ?? string.Empty;

    public string Extension => Path.GetExtension(FileName).TrimStart('.').ToUpperInvariant();

    public bool IsContainer =>
        ContentType == PInvoke.WPD_CONTENT_TYPE_FOLDER || ContentType == PInvoke.WPD_CONTENT_TYPE_FUNCTIONAL_OBJECT;

    public bool IsImage => ContentType == PInvoke.WPD_CONTENT_TYPE_IMAGE;

    public bool IsVideo => ContentType == PInvoke.WPD_CONTENT_TYPE_VIDEO;

    public bool IsMediaLike =>
        IsImage || IsVideo || (!IsContainer && Analysis.DcimNameAnalyzer.Parse(FileName) is { Kind: not Analysis.DcimResourceKind.Other });

    /// <summary>PTP ObjectCompressedSize is 32-bit; 0xFFFFFFFF means "4 GiB or larger — ask with MTP ObjectSize".</summary>
    public bool SizeIsPtp32BitSentinel => Size == 0xFFFFFFFF;

    private T? Get<T>(PROPERTYKEY key)
        where T : class =>
        Properties.FirstOrDefault(p => p.Key.fmtid == key.fmtid && p.Key.pid == key.pid).Value as T;

    private T? GetStruct<T>(PROPERTYKEY key)
        where T : struct =>
        Properties.FirstOrDefault(p => p.Key.fmtid == key.fmtid && p.Key.pid == key.pid).Value is T value ? value : null;

    private ulong? GetNumber(PROPERTYKEY key) =>
        Properties.FirstOrDefault(p => p.Key.fmtid == key.fmtid && p.Key.pid == key.pid).Value switch
        {
            ulong u => u,
            uint u => u,
            long l when l >= 0 => (ulong)l,
            int i when i >= 0 => (ulong)i,
            ushort s => s,
            _ => null,
        };
}
