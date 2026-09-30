using System.Collections;
using PhotoArchive.M000.GallerySpike.Core;

namespace PhotoArchive.M000.GallerySpike;

public sealed class VirtualizedGallerySource : IList
{
    private readonly SyntheticAssetPageSource _pageSource;
    private readonly GallerySpikeOptions _options;
    private readonly Dictionary<int, GalleryAssetItem> _items = new();
    private readonly Queue<int> _insertionOrder = new();
    private int _createdItemCount;

    public VirtualizedGallerySource(SyntheticAssetPageSource pageSource, GallerySpikeOptions options)
    {
        _pageSource = pageSource;
        _options = options;
    }

    public int Count => _options.DatasetSize;
    public bool IsReadOnly => true;
    public bool IsFixedSize => true;
    public bool IsSynchronized => false;
    public object SyncRoot => this;
    public int CachedItemCount => _items.Count;
    public int CreatedItemCount => Volatile.Read(ref _createdItemCount);

    public object? this[int index]
    {
        get => GetItem(index);
        set => throw new NotSupportedException();
    }

    public GalleryAssetItem GetItem(int index)
    {
        if ((uint)index >= (uint)Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (_items.TryGetValue(index, out var existing))
        {
            return existing;
        }

        var item = new GalleryAssetItem(index);
        _items[index] = item;
        _insertionOrder.Enqueue(index);
        Interlocked.Increment(ref _createdItemCount);
        TrimCache();
        _ = LoadMetadataAsync(item);
        return item;
    }

    public int Add(object? value) => throw new NotSupportedException();
    public void Clear() => throw new NotSupportedException();
    public bool Contains(object? value) => value is GalleryAssetItem item && item.Index >= 0 && item.Index < Count;
    public int IndexOf(object? value) => value is GalleryAssetItem item ? item.Index : -1;
    public void Insert(int index, object? value) => throw new NotSupportedException();
    public void Remove(object? value) => throw new NotSupportedException();
    public void RemoveAt(int index) => throw new NotSupportedException();
    public void CopyTo(Array array, int index)
    {
        for (var i = 0; i < Count; i++)
        {
            array.SetValue(GetItem(i), index + i);
        }
    }

    public IEnumerator GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
        {
            yield return GetItem(i);
        }
    }

    private async Task LoadMetadataAsync(GalleryAssetItem item)
    {
        var pageIndex = item.Index / _options.PageSize;
        var page = await _pageSource.GetPageAsync(pageIndex).ConfigureAwait(true);
        var record = page[item.Index % _options.PageSize];
        item.ApplyMetadata(record);
    }

    private void TrimCache()
    {
        while (_items.Count > _options.ItemCacheSize && _insertionOrder.TryDequeue(out var oldest))
        {
            _items.Remove(oldest);
        }
    }
}
