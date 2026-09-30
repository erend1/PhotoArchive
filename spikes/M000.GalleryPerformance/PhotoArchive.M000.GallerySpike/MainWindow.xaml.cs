using System.Diagnostics;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PhotoArchive.M000.GallerySpike.Core;

namespace PhotoArchive.M000.GallerySpike;

public sealed partial class MainWindow : Window, IGallerySpikeView
{
    private readonly GallerySpikeOptions _options;
    private readonly SyntheticAssetPageSource _pages;
    private readonly SyntheticThumbnailService _thumbnails;
    private readonly VirtualizedGallerySource _items;
    private readonly GalleryPresenter _presenter;
    private readonly Dictionary<int, TaskCompletionSource<bool>> _realizationWaiters = new();
    private bool _benchmarkRunning;

    public MainWindow(
        GallerySpikeOptions options,
        SyntheticAssetPageSource pages,
        SyntheticThumbnailService thumbnails,
        VirtualizedGallerySource items)
    {
        SpikeStartupTrace.Write("MainWindow constructor entered.");
        _options = options;
        _pages = pages;
        _thumbnails = thumbnails;
        _items = items;
        _presenter = new GalleryPresenter(this, thumbnails);

        SpikeStartupTrace.Write("MainWindow.InitializeComponent starting.");
        InitializeComponent();
        SpikeStartupTrace.Write("MainWindow.InitializeComponent completed.");
        Title = "PhotoArchive M000 Gallery Spike";
        GalleryItemsView.ItemsSource = _items;
        SpikeStartupTrace.Write("ItemsView.ItemsSource assigned.");
    }

    public void SetStatus(string status) => StatusText.Text = status;

    public void SetSelectionCount(int count) => SelectionText.Text = $"Selected: {count:N0}";

    private async void Root_Loaded(object sender, RoutedEventArgs e)
    {
        SpikeStartupTrace.Write("Root.Loaded entered.");
        SetStatus($"Virtual source ready: {_options.DatasetSize:N0} synthetic assets; originals are never read.");

        if (Environment.GetCommandLineArgs().Any(static arg => string.Equals(arg, "--benchmark", StringComparison.OrdinalIgnoreCase)))
        {
            SpikeStartupTrace.Write("Automated benchmark requested.");
            await RunBenchmarkAsync(exitWhenFinished: true);
        }
    }

    private void AssetTile_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not GalleryAssetItem item)
        {
            return;
        }

        GalleryTileMetrics.OnLoaded(element);
        _presenter.OnItemRealized(item);

        if (_realizationWaiters.Remove(item.Index, out var waiter))
        {
            waiter.TrySetResult(true);
        }
    }

    private void AssetTile_Unloaded(object sender, RoutedEventArgs e)
    {
        GalleryTileMetrics.OnUnloaded();
        if (sender is FrameworkElement element && element.DataContext is GalleryAssetItem item)
        {
            _presenter.OnItemUnrealized(item);
        }
    }

    private void GalleryItemsView_SelectionChanged(ItemsView sender, ItemsViewSelectionChangedEventArgs args) =>
        _presenter.OnSelectionChanged(sender.SelectedItems.Count);

    private async void Jump25_Click(object sender, RoutedEventArgs e) => await JumpToAsync(_options.DatasetSize / 4);
    private async void Jump50_Click(object sender, RoutedEventArgs e) => await JumpToAsync(_options.DatasetSize / 2);
    private async void Jump75_Click(object sender, RoutedEventArgs e) => await JumpToAsync((_options.DatasetSize * 3) / 4);
    private async void JumpEnd_Click(object sender, RoutedEventArgs e) => await JumpToAsync(_options.DatasetSize - 1);
    private async void RunBenchmark_Click(object sender, RoutedEventArgs e) => await RunBenchmarkAsync(exitWhenFinished: false);

    private async Task<GalleryJumpMeasurement> JumpToAsync(int targetIndex)
    {
        var waiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _realizationWaiters[targetIndex] = waiter;
        _presenter.OnJumpRequested(targetIndex);

        var stopwatch = Stopwatch.StartNew();
        GalleryItemsView.StartBringItemIntoView(targetIndex, new BringIntoViewOptions { AnimationDesired = false });
        await waiter.Task.WaitAsync(TimeSpan.FromSeconds(15));
        stopwatch.Stop();

        var measurement = new GalleryJumpMeasurement(targetIndex, stopwatch.Elapsed.TotalMilliseconds);
        _presenter.OnJumpCompleted(targetIndex, stopwatch.Elapsed);
        return measurement;
    }

    private async Task RunBenchmarkAsync(bool exitWhenFinished)
    {
        if (_benchmarkRunning)
        {
            return;
        }

        _benchmarkRunning = true;
        SpikeStartupTrace.Write("RunBenchmarkAsync entered.");
        try
        {
            var firstUsable = await _presenter.FirstUsableMilliseconds.WaitAsync(TimeSpan.FromSeconds(15));
            await Task.Delay(250);
            var process = Process.GetCurrentProcess();
            process.Refresh();
            var initialWorkingSet = process.WorkingSet64;

            GalleryItemsView.Select(0);
            GalleryItemsView.Select(1);
            GalleryItemsView.Select(2);

            var jumps = new List<GalleryJumpMeasurement>();
            foreach (var index in new[] { _options.DatasetSize / 4, _options.DatasetSize / 2, (_options.DatasetSize * 3) / 4, _options.DatasetSize - 1, 0 })
            {
                jumps.Add(await JumpToAsync(index));
                await Task.Delay(100);
            }

            await Task.Delay(750);
            process.Refresh();
            var afterFastScroll = process.WorkingSet64;

            var report = GalleryBenchmarkReport.Create(
                _options,
                _items,
                _pages,
                _thumbnails,
                firstUsable,
                initialWorkingSet,
                afterFastScroll,
                GalleryItemsView.SelectedItems.Count,
                jumps);

            var reportPath = GetReportPath();
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            await File.WriteAllTextAsync(
                reportPath,
                JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

            SpikeStartupTrace.Write($"Benchmark report written with recommendation {report.Recommendation}.");
            SetStatus($"Benchmark complete: {report.Recommendation}. Report: {reportPath}");

            if (exitWhenFinished)
            {
                Application.Current.Exit();
            }
        }
        catch (Exception ex)
        {
            SpikeStartupTrace.Write($"Benchmark failed: {ex}");
            SetStatus($"Benchmark failed: {ex.GetType().Name}: {ex.Message}");
            await WriteFailureReportAsync(ex);
            if (exitWhenFinished)
            {
                Application.Current.Exit();
            }
        }
        finally
        {
            _benchmarkRunning = false;
        }
    }

    private static string GetReportPath()
    {
        var reportArg = Environment.GetCommandLineArgs().FirstOrDefault(static arg => arg.StartsWith("--report=", StringComparison.OrdinalIgnoreCase));
        if (reportArg is not null)
        {
            return Path.GetFullPath(reportArg["--report=".Length..].Trim('"'));
        }

        return Path.Combine(AppContext.BaseDirectory, "gallery-spike-benchmark.json");
    }

    private static Task WriteFailureReportAsync(Exception exception)
    {
        var payload = JsonSerializer.Serialize(
            new
            {
                status = "FAILED",
                exception = exception.ToString(),
                recordedUtc = DateTimeOffset.UtcNow.ToString("O"),
                recommendation = "WINUI_GALLERY_BLOCKED",
            },
            new JsonSerializerOptions { WriteIndented = true });

        var path = GetReportPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return File.WriteAllTextAsync(path, payload);
    }
}
