using System.Runtime.InteropServices;
using PhotoArchive.M000.GallerySpike.Core;

namespace PhotoArchive.M000.GallerySpike;

public sealed record GalleryJumpMeasurement(int TargetIndex, double Milliseconds);

public sealed record GalleryBenchmarkReport(
    string RecordedUtc,
    string Windows,
    string Cpu,
    string Ram,
    string Gpu,
    string DotNet,
    string WindowsAppSdk,
    string ProcessArchitecture,
    int DatasetSize,
    int PageSize,
    double FirstUsableGalleryMs,
    double InitialWorkingSetMiB,
    double AfterFastScrollWorkingSetMiB,
    int PeakRealizedTiles,
    int UniqueTileElementsCreated,
    int ViewModelItemsCreated,
    int ViewModelItemsCached,
    int PagesRead,
    int MetadataRowsMaterialized,
    int ThumbnailRequested,
    int ThumbnailCompleted,
    int ThumbnailCancelled,
    int ThumbnailPeakInFlight,
    int SelectedItems,
    IReadOnlyList<GalleryJumpMeasurement> FastScrollJumps,
    string Recommendation)
{
    public const string WindowsAppSdkVersion = "2.5.1";

    public static GalleryBenchmarkReport Create(
        GallerySpikeOptions options,
        VirtualizedGallerySource items,
        SyntheticAssetPageSource pages,
        SyntheticThumbnailService thumbnails,
        double firstUsableMs,
        long initialWorkingSet,
        long afterScrollWorkingSet,
        int selectedItems,
        IReadOnlyList<GalleryJumpMeasurement> jumps)
    {
        var viable = GalleryTileMetrics.PeakActive is > 0 and < 2_000
            && items.CreatedItemCount < options.DatasetSize
            && pages.RowsMaterialized < options.DatasetSize / 2
            && selectedItems >= 3
            && thumbnails.Completed > 0
            && thumbnails.Cancelled > 0
            && jumps.Count == 5
            && jumps.All(static jump => jump.Milliseconds < 5_000);

        return new GalleryBenchmarkReport(
            DateTimeOffset.UtcNow.ToString("O"),
            Environment.GetEnvironmentVariable("GALLERY_SPIKE_WINDOWS") ?? Environment.OSVersion.VersionString,
            Environment.GetEnvironmentVariable("GALLERY_SPIKE_CPU") ?? $"{Environment.ProcessorCount} logical processors",
            Environment.GetEnvironmentVariable("GALLERY_SPIKE_RAM") ?? "not supplied",
            Environment.GetEnvironmentVariable("GALLERY_SPIKE_GPU") ?? "not supplied",
            RuntimeInformation.FrameworkDescription,
            WindowsAppSdkVersion,
            RuntimeInformation.ProcessArchitecture.ToString(),
            options.DatasetSize,
            options.PageSize,
            firstUsableMs,
            ToMiB(initialWorkingSet),
            ToMiB(afterScrollWorkingSet),
            GalleryTileMetrics.PeakActive,
            GalleryTileMetrics.Created,
            items.CreatedItemCount,
            items.CachedItemCount,
            pages.PageReadCount,
            pages.RowsMaterialized,
            thumbnails.Requested,
            thumbnails.Completed,
            thumbnails.Cancelled,
            thumbnails.PeakInFlight,
            selectedItems,
            jumps,
            viable ? "WINUI_ITEMS_VIEW_VIABLE" : "WINUI_ALTERNATIVE_CONTROL_REQUIRED");
    }

    private static double ToMiB(long bytes) => Math.Round(bytes / 1024d / 1024d, 2);
}
