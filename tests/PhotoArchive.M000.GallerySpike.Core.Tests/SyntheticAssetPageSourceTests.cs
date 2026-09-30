using PhotoArchive.M000.GallerySpike.Core;

namespace PhotoArchive.M000.GallerySpike.Core.Tests;

public sealed class SyntheticAssetPageSourceTests
{
    [Fact]
    public async Task Generates_at_least_100k_records_in_chronological_order_without_eager_materialization()
    {
        var source = new SyntheticAssetPageSource();

        Assert.Equal(100_000, source.TotalCount);
        Assert.Equal(0, source.RowsMaterialized);

        var first = await source.GetPageAsync(0);
        var far = await source.GetPageAsync(300);

        Assert.Equal(512, source.RowsMaterialized);
        Assert.True(first[0].CaptureDate > far[0].CaptureDate);
        Assert.StartsWith("synthetic://thumb/", first[0].ThumbnailKey);
    }

    [Fact]
    public async Task Reuses_same_page_fetch_instead_of_materializing_duplicate_page_rows()
    {
        var source = new SyntheticAssetPageSource();

        var first = await source.GetPageAsync(4);
        var second = await source.GetPageAsync(4);

        Assert.Same(first, second);
        Assert.Equal(1, source.PageReadCount);
        Assert.Equal(source.PageSize, source.RowsMaterialized);
    }
}
