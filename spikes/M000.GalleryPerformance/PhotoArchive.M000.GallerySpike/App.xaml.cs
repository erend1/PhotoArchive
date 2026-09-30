using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using PhotoArchive.M000.GallerySpike.Core;

namespace PhotoArchive.M000.GallerySpike;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        SpikeStartupTrace.Write("App constructor entered.");
        UnhandledException += (_, args) => SpikeStartupTrace.Write($"Application.UnhandledException: {args.Exception}");

        InitializeComponent();
        SpikeStartupTrace.Write("App.InitializeComponent completed.");

        Services = new ServiceCollection()
            .AddSingleton(new GallerySpikeOptions(100_000, 256, 4_096))
            .AddSingleton(static provider =>
            {
                var options = provider.GetRequiredService<GallerySpikeOptions>();
                return new SyntheticAssetPageSource(options.DatasetSize, options.PageSize);
            })
            .AddSingleton<SyntheticThumbnailService>()
            .AddSingleton<VirtualizedGallerySource>()
            .AddSingleton<MainWindow>()
            .BuildServiceProvider();

        SpikeStartupTrace.Write("Dependency injection container built.");
    }

    public IServiceProvider Services { get; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        SpikeStartupTrace.Write("App.OnLaunched entered.");
        _window = Services.GetRequiredService<MainWindow>();
        SpikeStartupTrace.Write("MainWindow resolved from DI.");
        _window.Activate();
        SpikeStartupTrace.Write("MainWindow.Activate returned.");
    }
}
