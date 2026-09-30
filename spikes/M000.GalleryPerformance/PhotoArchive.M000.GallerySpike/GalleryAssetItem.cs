using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Media;
using PhotoArchive.M000.GallerySpike.Core;
using Windows.UI;

namespace PhotoArchive.M000.GallerySpike;

public sealed class GalleryAssetItem : INotifyPropertyChanged
{
    private SyntheticAssetRecord? _record;
    private Brush _thumbnailBrush = new SolidColorBrush(Color.FromArgb(255, 54, 54, 54));
    private string _thumbnailLabel = "WAIT";
    private readonly TaskCompletionSource<SyntheticAssetRecord> _metadataReady = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public GalleryAssetItem(int index)
    {
        Index = index;
        TileWidth = 124 + (index % 4) * 22;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Index { get; }

    public double TileWidth { get; }

    public SyntheticAssetRecord? Record => _record;

    public string FileName => _record?.FileName ?? $"Asset {Index:N0}";

    public string CaptureDateText => _record?.CaptureDate.LocalDateTime.ToString("yyyy-MM-dd HH:mm") ?? "metadata pending";

    public Brush ThumbnailBrush
    {
        get => _thumbnailBrush;
        private set => SetField(ref _thumbnailBrush, value);
    }

    public string ThumbnailLabel
    {
        get => _thumbnailLabel;
        private set => SetField(ref _thumbnailLabel, value);
    }

    public void ApplyMetadata(SyntheticAssetRecord record)
    {
        _record = record;
        _metadataReady.TrySetResult(record);
        OnPropertyChanged(nameof(Record));
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(CaptureDateText));
    }

    public Task<SyntheticAssetRecord> WaitForMetadataAsync(CancellationToken cancellationToken) =>
        _metadataReady.Task.WaitAsync(cancellationToken);

    public void ApplyThumbnail(SyntheticThumbnail thumbnail)
    {
        ThumbnailBrush = new SolidColorBrush(Color.FromArgb(255, thumbnail.R, thumbnail.G, thumbnail.B));
        ThumbnailLabel = thumbnail.Label;
    }

    public void MarkThumbnailCancelled() => ThumbnailLabel = "CANCEL";

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
