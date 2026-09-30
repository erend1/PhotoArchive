# Data Model

## Principles
- A logical asset is not necessarily a single file.
- Resource bytes provide canonical content identity.
- Connector/device identifiers are accelerators/provenance, not universal archive identity.
- Catalog data should be rebuildable from durable archive state as far as practical.

## Core Concepts

### ArchiveAsset
Representative fields:
- `AssetId`
- `CaptureDate`
- `CaptureDateSource`
- `MediaKind`
- `Location` (optional)
- `IsFavorite`
- `Width` / `Height` where meaningful
- `Duration` where meaningful
- `ImportedAt`
- lifecycle state (`Active`, `Trash`, etc.)

### MediaResource
Representative fields:
- `ResourceId`
- `AssetId`
- `Role`
- `RelativePath`
- `Sha256`
- `ByteLength`
- format/MIME information

Potential roles include:
- OriginalPhoto
- OriginalVideo
- LivePhotoMotion
- EditedPhoto
- EditedVideo
- AdjustmentData
- OtherPreservedResource

### SourceDevice
Representative fields:
- `DeviceId`
- `DisplayName`
- connector type
- connector-specific durable identity when available

### SourceObservation
Tracks a relationship observed on a source device without partitioning the archive by user.
Representative fields:
- `DeviceId`
- connector-specific source asset identifier
- archive `AssetId` when conclusively mapped
- first/last seen timestamps
- connector-specific cloud/local identifier where applicable
- source favorite/album observations when useful

### Album
Archive-level logical metadata only. Albums do not determine physical media paths.

### Manifest
Durable per-asset metadata sufficient to recover important catalog state. Exact schema will evolve, but must include resource paths, hashes, byte lengths, roles, and core asset metadata.

## Identity Rules
- Resource equality: SHA-256 over resource bytes.
- Asset identity: application-generated stable ID representing a logical asset and its resource set.
- Same resource may be observed from multiple devices without creating duplicate physical content.

## Favorite and Albums
Archive `IsFavorite` is intentionally simple. Source-device favorite/album observations may seed or inform archive metadata but do not create user-specific views/storage partitions.

## Edited Assets
An asset may contain both original and current edited representations. The archive must not claim a reversible edit history when the external edit system does not expose portable semantics.
