# Architecture

## Technology Baseline
- .NET 10 LTS
- WinUI 3 / Windows App SDK
- MVP presentation pattern
- Microsoft.Extensions.DependencyInjection
- EF Core + SQLite
- Microsoft.Extensions.Logging
- xUnit

## Architectural Style
Clean-architecture-inspired layering with explicit boundaries around volatile/external systems. Avoid abstraction for abstraction's sake.

Suggested projects:

```text
src/
  PhotoArchive.Domain/
  PhotoArchive.Application/
  PhotoArchive.Infrastructure/
  PhotoArchive.Device.Abstractions/
  PhotoArchive.Device.Apple/
  PhotoArchive.Desktop/

tests/
  PhotoArchive.Domain.Tests/
  PhotoArchive.Application.Tests/
  PhotoArchive.Infrastructure.Tests/
  PhotoArchive.IntegrationTests/
```

## Layer Responsibilities

### Domain
Framework-independent core concepts such as ArchiveAsset, MediaResource, Album metadata, SourceObservation, archive/trash states, backup state, and value objects.

### Application
Use cases and orchestration: import, compare device, archive asset, trash/restore, verify archive, backup, catalog rebuild.

### Infrastructure
EF Core/SQLite, filesystem, manifests, hashing, thumbnail/preview generation, backup implementation, logging adapters.

### Device Abstractions
Device/session contracts and connector-neutral asset descriptors.

### Apple Device Adapter
Only documented/supported mechanisms. Actual capability set is gated by M000 feasibility.

### Desktop
WinUI 3 Views, Presenters, navigation, UI state, virtualization, dialogs.

## Presentation Pattern
MVP with Passive/Supervising View. Views handle UI concerns and forward user intent. Presenters coordinate application use cases and produce bindable view state where useful.

## Archive Storage Model
The archive root is user-selected and device-independent. The application installation is separate from archive contents.

```text
PhotoArchive/
  Media/
    YYYY/
      MM/
  .archive/
    catalog.sqlite
    manifests/
    thumbnails/
    previews/
    staging/
    trash/
    logs/
    archive.json
  README.txt
```

`Media/` is intentionally human-readable and directly accessible to other applications/devices. `.archive/` is application-managed metadata, not access-restricted storage.

## Catalog
EF Core SQLite catalog is a high-performance index. It is rebuildable and must not be the only durable representation of archive state.

## Device Sessions
Connected-device enumeration is ephemeral and session-oriented. Device metadata should not be copied wholesale into the persistent catalog on every connection. Persist only durable mappings/observations needed for future identity resolution and audit.

## Extensibility Boundaries
Likely interfaces include:
- device connectors,
- media decoding/thumbnail generation,
- filesystem/archive storage,
- backup target,
- hashing,
- clock only where testability materially benefits.
