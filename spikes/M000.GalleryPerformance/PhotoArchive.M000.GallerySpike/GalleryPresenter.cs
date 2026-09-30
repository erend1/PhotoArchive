using PhotoArchive.M000.GallerySpike.Core;

namespace PhotoArchive.M000.GallerySpike;

public sealed class GalleryPresenter
{
    private readonly IGallerySpikeView _view;
    private readonly SyntheticThumbnailService _thumbnails;
    private readonly Dictionary<int, CancellationTokenSource> _thumbnailWork = new();
    private readonly TaskCompletionSource<double> _firstUsable = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly DateTimeOffset _processStartUtc;

    public GalleryPresenter(IGallerySpikeView view, SyntheticThumbnailService thumbnails)
    {
        _view = view;
        _thumbnails = thumbnails;
        _processStartUtc = System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime();
    }

    public Task<double> FirstUsableMilliseconds => _firstUsable.Task;

    public void OnItemRealized(GalleryAssetItem item)
    {
        if (_thumbnailWork.Remove(item.Index, out var previous))
        {
            previous.Cancel();
            previous.Dispose();
        }

        var cts = new CancellationTokenSource();
        _thumbnailWork[item.Index] = cts;
        _ = LoadThumbnailAsync(item, cts);
    }

    public void OnItemUnrealized(GalleryAssetItem item)
    {
        if (_thumbnailWork.Remove(item.Index, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    public void OnSelectionChanged(int selectedCount) => _view.SetSelectionCount(selectedCount);

    public void OnJumpRequested(int targetIndex) => _view.SetStatus($"Jumping to item {targetIndex:N0}…");

    public void OnJumpCompleted(int targetIndex, TimeSpan elapsed) =>
        _view.SetStatus($"Item {targetIndex:N0} realized in {elapsed.TotalMilliseconds:N0} ms");

    private async Task LoadThumbnailAsync(GalleryAssetItem item, CancellationTokenSource cts)
    {
        try
        {
            var record = await item.WaitForMetadataAsync(cts.Token);
            var thumbnail = await _thumbnails.LoadAsync(record, cts.Token);
            if (!cts.IsCancellationRequested)
            {
                item.ApplyThumbnail(thumbnail);
                _firstUsable.TrySetResult((DateTimeOffset.UtcNow - _processStartUtc).TotalMilliseconds);
            }
        }
        catch (OperationCanceledException)
        {
            item.MarkThumbnailCancelled();
        }
        finally
        {
            if (_thumbnailWork.TryGetValue(item.Index, out var current) && ReferenceEquals(current, cts))
            {
                _thumbnailWork.Remove(item.Index);
                cts.Dispose();
            }
        }
    }
}
