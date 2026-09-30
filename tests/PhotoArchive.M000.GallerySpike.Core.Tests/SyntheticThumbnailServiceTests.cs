using PhotoArchive.M000.GallerySpike.Core;

namespace PhotoArchive.M000.GallerySpike.Core.Tests;

public sealed class SyntheticThumbnailServiceTests
{
    [Fact]
    public async Task Obsolete_thumbnail_work_is_cancellable()
    {
        var source = new SyntheticAssetPageSource();
        var asset = (await source.GetPageAsync(0))[20];
        var thumbnails = new SyntheticThumbnailService();
        using var cts = new CancellationTokenSource();

        var work = thumbnails.LoadAsync(asset, cts.Token);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work);
        Assert.Equal(1, thumbnails.Requested);
        Assert.Equal(1, thumbnails.Cancelled);
        Assert.Equal(0, thumbnails.InFlight);
    }

    [Fact]
    public async Task Synthetic_thumbnail_generation_does_not_require_media_file_input()
    {
        var source = new SyntheticAssetPageSource();
        var asset = (await source.GetPageAsync(0))[1];
        var thumbnails = new SyntheticThumbnailService();

        var thumbnail = await thumbnails.LoadAsync(asset, CancellationToken.None);

        Assert.Equal("PHOTO", thumbnail.Label);
        Assert.Equal(1, thumbnails.Completed);
    }
}
