# M000 Gallery Performance Spike

## Scope

Issue #4 validates the WinUI 3 gallery direction only. The spike is isolated under `spikes/M000.GalleryPerformance/` and does not create production Gallery, Domain, Application, Infrastructure, or device-layer contracts.

## Prototype

- .NET 10
- WinUI 3 / Windows App SDK 2.5.1
- `ItemsView` + `LinedFlowLayout`
- MVP-style presenter boundary: the XAML view forwards realization, selection, and jump intents to `GalleryPresenter`; the presenter coordinates thumbnail work and view status.
- `VirtualizedGallerySource` exposes 100,000 logical items by indexed access. It creates view-state objects only as ItemsView asks for them and caps its retained item cache at 4,096.
- `SyntheticAssetPageSource` generates deterministic metadata in pages of 256. It does not enumerate or open original media files.
- `SyntheticThumbnailService` generates deterministic in-memory placeholder colors after a small asynchronous delay. Realized tiles request work; unrealized tiles cancel obsolete requests.
- Multiple selection is enabled directly on `ItemsView`.
- Fast-scroll evidence uses `ItemsView.StartBringItemIntoView` against 25%, 50%, 75%, end, and start positions.

## Reproduce

### Build and tests

```powershell
dotnet restore PhotoArchive.sln
dotnet build PhotoArchive.sln -c Release
dotnet test tests/PhotoArchive.M000.GallerySpike.Core.Tests/PhotoArchive.M000.GallerySpike.Core.Tests.csproj -c Release --no-build
```

### Interactive Windows run

```powershell
dotnet run --project spikes/M000.GalleryPerformance/PhotoArchive.M000.GallerySpike/PhotoArchive.M000.GallerySpike.csproj -c Release -p:Platform=x64
```

Use the 25%/50%/75%/End buttons or `Run benchmark`. The benchmark writes `gallery-spike-benchmark.json` next to the executable unless `--report=<path>` is supplied.

### Automated evidence run

The branch workflow `.github/workflows/gallery-spike.yml` builds/tests on `windows-latest`, captures Windows/CPU/RAM/GPU details, launches the self-contained WinUI spike with `--benchmark`, and uploads `m000-gallery-spike-evidence`.

## What is measured

The JSON report records:

- Windows, CPU, RAM, GPU, .NET, Windows App SDK, process architecture
- dataset/page size
- first usable gallery latency
- process working set after initial realization and after the fast-scroll sequence
- peak simultaneously realized tile elements and unique tile elements created
- number of item view-state objects created/cached
- catalog pages read and metadata rows materialized
- thumbnail requested/completed/cancelled counts and peak concurrency
- selected item count
- per-jump realization latency

## Interpretation rules

The spike must demonstrate all of the following before `WINUI_ITEMS_VIEW_VIABLE` is accepted:

1. 100,000 logical assets are present.
2. Peak realized tiles are far below the dataset size (the automated guard uses `< 2,000`).
3. Metadata materialization remains below half the dataset during the representative fast-scroll sequence.
4. Each representative jump realizes its target within 5 seconds on the recorded machine.
5. The report is produced without reading any original media path.
6. Multi-selection contains at least three selected items.
7. Thumbnail cancellation is observable when realized items leave the viewport before synthetic work completes.

If ItemsView violates the working-set/scroll criteria, the next experiment is a documented lower-level WinUI primitive (normally `ItemsRepeater`/`ScrollView`) rather than a custom virtualization framework.

## Known measurement limitation

GitHub-hosted Windows runners are useful for reproducibility but are not a substitute for the target developer machine. The final issue/PR handoff must preserve the runner measurements and should add one local-development-machine run before M002 performance budgets are frozen.

## Failed/abandoned approaches

- No giant `ObservableCollection` of 100,000 item view models: rejected because it defeats the data-virtualization objective before ItemsView is tested.
- No eager thumbnail generation: rejected because it scales work with catalog size instead of viewport realization.
- No custom virtualization control: not justified until ItemsView measurements fail.

## Recommendation

The authoritative recommendation is the token in the committed benchmark evidence / Issue handoff after the Windows run. Do not infer viability from build success alone.
