using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using PhotoArchive.M000.GallerySpike.Core;

namespace PhotoArchive.M000.GallerySpike;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();

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
    }

    public IServiceProvider Services { get; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = Services.GetRequiredService<MainWindow>();
        _window.Activate();
    }
}
