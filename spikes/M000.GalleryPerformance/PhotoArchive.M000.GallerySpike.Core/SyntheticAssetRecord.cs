namespace PhotoArchive.M000.GallerySpike.Core;

public sealed record SyntheticAssetRecord(
    int Index,
    long AssetId,
    DateTimeOffset CaptureDate,
    string FileName,
    int PixelWidth,
    int PixelHeight,
    bool IsVideo,
    string ThumbnailKey);
