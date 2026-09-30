using System.Collections.Concurrent;

namespace PhotoArchive.M000.GallerySpike.Core;

public sealed class SyntheticAssetPageSource
{
    private static readonly DateTimeOffset NewestCapture = new(2026, 9, 30, 9, 0, 0, TimeSpan.Zero);
    private readonly ConcurrentDictionary<int, Lazy<Task<IReadOnlyList<SyntheticAssetRecord>>>> _pages = new();
    private int _pageReadCount;
    private int _rowsMaterialized;

    public SyntheticAssetPageSource(int totalCount = 100_000, int pageSize = 256)
    {
        if (totalCount < 100_000)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCount), "The M000 spike must exercise at least 100,000 assets.");
        }

        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        TotalCount = totalCount;
        PageSize = pageSize;
    }

    public int TotalCount { get; }

    public int PageSize { get; }

    public int PageReadCount => Volatile.Read(ref _pageReadCount);

    public int RowsMaterialized => Volatile.Read(ref _rowsMaterialized);

    public Task<IReadOnlyList<SyntheticAssetRecord>> GetPageAsync(int pageIndex, CancellationToken cancellationToken = default)
    {
        var maxPage = (TotalCount - 1) / PageSize;
        if (pageIndex < 0 || pageIndex > maxPage)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        }

        var lazy = _pages.GetOrAdd(
            pageIndex,
            static (index, source) => new Lazy<Task<IReadOnlyList<SyntheticAssetRecord>>>(() => source.GeneratePageAsync(index)),
            this);

        return AwaitWithCancellationAsync(lazy.Value, cancellationToken);
    }

    private async Task<IReadOnlyList<SyntheticAssetRecord>> GeneratePageAsync(int pageIndex)
    {
        // Represents a small catalog/page fetch without touching original media files.
        await Task.Delay(2).ConfigureAwait(false);

        var first = pageIndex * PageSize;
        var count = Math.Min(PageSize, TotalCount - first);
        var rows = new SyntheticAssetRecord[count];

        for (var offset = 0; offset < count; offset++)
        {
            var index = first + offset;
            rows[offset] = CreateRecord(index);
        }

        Interlocked.Increment(ref _pageReadCount);
        Interlocked.Add(ref _rowsMaterialized, count);
        return rows;
    }

    private static SyntheticAssetRecord CreateRecord(int index)
    {
        var hash = unchecked((uint)(index * 2654435761u));
        var portrait = (hash & 1) == 0;
        var width = portrait ? 3024 : 4032;
        var height = portrait ? 4032 : 3024;
        var isVideo = index % 11 == 0;
        var captureDate = NewestCapture.AddMinutes(-(long)index * 47L);
        var extension = isVideo ? ".mp4" : ".jpg";

        return new SyntheticAssetRecord(
            index,
            10_000_000L + index,
            captureDate,
            $"SYN_{index:D6}{extension}",
            width,
            height,
            isVideo,
            $"synthetic://thumb/{index:D6}");
    }

    private static async Task<T> AwaitWithCancellationAsync<T>(Task<T> task, CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled)
        {
            return await task.ConfigureAwait(false);
        }

        return await task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}
