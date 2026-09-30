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

### Interactive Windows benchmark

Run this from an interactive Windows desktop (Windows 11 is preferred for the product-target check):

```powershell
.\spikes\M000.GalleryPerformance\run-benchmark.ps1
```

The script captures Windows/CPU/RAM/GPU details, builds the x64 spike, launches `--benchmark`, and writes `artifacts/gallery-winui-benchmark-local.json`. Startup checkpoints are written beside the report as `startup-trace.log`.

### Hosted evidence run

`.github/workflows/gallery-spike.yml` builds/tests on `windows-2022`, captures machine details, attempts the same self-measuring executable, preserves startup/Application-event diagnostics, and uploads `m000-gallery-spike-evidence`.

## What a successful UI run measures

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

## Viability gate

`WINUI_ITEMS_VIEW_VIABLE` is emitted only when all of these are observed in the same successful UI run:

1. 100,000 logical assets are present.
2. Peak simultaneously realized tiles is greater than zero and below 2,000.
3. Fewer than all 100,000 presentation items are instantiated.
4. Metadata materialization remains below half the dataset during the representative fast-scroll sequence.
5. At least three items remain selected.
6. At least one thumbnail completes and at least one obsolete thumbnail request is cancelled.
7. All five representative jumps (25%, 50%, 75%, end, start) realize within 5 seconds.
8. No original-media path is read by the source or thumbnail service.

A successful UI run that reaches ItemsView but fails this gate recommends `WINUI_ALTERNATIVE_CONTROL_REQUIRED`. A runtime that cannot reach the spike view cannot be used to judge ItemsView and is recorded as `WINUI_GALLERY_BLOCKED` until the same harness is run in a suitable interactive Windows session.

## Recorded evidence — 2026-09-30

### Build and non-UI behavior

Authoritative hosted run: GitHub Actions run `36696232844` on `windows-2022`.

- Windows: Microsoft Windows Server 2022 Datacenter 10.0.20348, build 20348
- CPU: AMD EPYC 7763 64-Core Processor
- RAM: 16 GiB
- GPU: Microsoft Hyper-V Video
- .NET SDK: 10.0.401
- Windows App SDK package: 2.5.1
- Dataset configured: 100,000 synthetic assets
- Metadata page size: 256
- Presentation-item cache cap: 4,096
- Release build: succeeded, 0 warnings, 0 errors, 25.87 s
- Core tests: 4 passed, 0 failed, 0 skipped, 122 ms
- Core evidence confirms deterministic 100,000-item generation, descending chronology, page reuse rather than eager full materialization, cancellable thumbnail work, and thumbnail generation without media-file input.

### WinUI runtime blocker

Two independent hosted runner images were attempted:

1. `windows-latest` / Windows Server 2025 Datacenter 10.0.26100: the built x64 executable terminated with native exit code `0xC0000409` in `USER32.dll` before any managed startup checkpoint was written.
2. `windows-2022` / Windows Server 2022 Datacenter 10.0.20348: the same executable again terminated with `0xC0000409` in `USER32.dll` before `App` construction; no `startup-trace.log` and no benchmark JSON were produced.

Because both failures occur before the first line of application-managed startup code, ItemsView, LinedFlowLayout, the 100,000-item source, thumbnail scheduling, scrolling, and selection are never reached in the hosted runs. These failures therefore do **not** demonstrate an ItemsView performance defect, and they do not justify dropping to `ItemsRepeater` or a custom virtualization framework.

UI-only numeric measurements (first usable latency, working set, realized element count, jump latency, and viewport-driven cancellation) are intentionally **not fabricated**. They remain unavailable until the committed harness is executed in an interactive Windows desktop session.

## Acceptance evidence status

| Issue #4 criterion | Status | Evidence |
| --- | --- | --- |
| At least 100,000 synthetic assets | Proven in code/tests | 100,000 logical source; test prevents smaller configured dataset |
| UI does not instantiate/render all controls | Runtime-blocked | Instrumentation is committed, but hosted WinUI terminates before `App` |
| Usable gallery without original-media reads | Runtime-blocked | Data/thumbnail services never accept an original path; usability cannot be measured before native startup succeeds |
| Paged/virtualized metadata | Proven in code/tests | 256-row page source; page reuse/eager-materialization test |
| Lazy viewport-aware thumbnails | Runtime-blocked integration | Requests originate from tile realization; hosted UI never reaches realization |
| Obsolete work cancellation/deprioritization | Proven at service level; runtime-blocked integration | cancellation unit test passes; realization/unrealization wiring is committed |
| Fast scrolling and multi-selection | Runtime-blocked | benchmark harness is committed; hosted UI never reaches it |
| Startup, memory, representative fast-scroll measurements | Blocked | no defensible UI numbers exist because native startup fails first |
| Material bottleneck identified | Proven | hosted WinUI desktop execution fails in USER32 before managed startup |
| Clear recommendation | Proven | `WINUI_GALLERY_BLOCKED` |

## Failed/abandoned approaches

- A giant `ObservableCollection` of 100,000 item view models was rejected because it defeats the data-virtualization objective before ItemsView is tested.
- Eager thumbnail generation was rejected because it scales work with catalog size instead of viewport realization.
- A custom virtualization control was not attempted because there is no measurement showing ItemsView itself is insufficient.
- Initial Actions build failed only because the test project lacked the xUnit global import; the WinUI project had compiled. Adding the import produced a clean full build and 4/4 tests.
- The first benchmark launcher searched `bin/Release`; the WinUI x64 output is under `bin/x64/Release`. The launcher was fixed and the executable was then reached.
- `windows-latest` (Server 2025) was replaced with `windows-2022` to remove OS-image compatibility as a variable. Both runner images failed identically before managed startup.

## M002 constraints / integration concern

Do not promote the spike's `VirtualizedGallerySource`, synthetic thumbnail service, 4,096 cache cap, 2,000-element guard, or 5-second jump guard into production contracts or budgets. They are experimental instrumentation. Keep the ADR-0009 MVP boundary and ItemsView-first direction provisional until `run-benchmark.ps1` produces a successful report on the product-target interactive Windows environment.

The integration agent should require one successful interactive Windows run before M002 Gallery implementation treats ItemsView as validated. If that run reaches ItemsView and fails the viability gate, then run the lower-level documented-control comparison; do not build custom virtualization first.

## Final recommendation

WINUI_GALLERY_BLOCKED
