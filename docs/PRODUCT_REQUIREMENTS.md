# Product Requirements

## Users and Archives
- An archive is a shared media space, not partitioned by person/user.
- Users who want separation create separate archives rooted at different folders.
- Source devices may be tracked for provenance and matching without creating user-specific storage partitions.
- Multiple archives may be supported by the application; cross-archive synchronization is out of scope for early versions.

## Archive Location
- Archive root is selected by the user.
- Valid locations include internal HDD/SSD and removable HDD/SSD.
- Online/cloud database or mandatory network storage is out of scope for V1.
- If the archive root is unavailable, Gallery is unavailable; V1 does not maintain a separate offline thumbnail cache outside the archive.

## Gallery
- Gallery is the default application screen.
- Large libraries must be virtualized and must not require scanning original media on every startup.
- Primary navigation/filtering: chronological browsing, date, photo/video, favorite, album, source device, filename.
- Recently Imported is a convenience view, not a critical subsystem.
- Viewer is read-only with zoom/navigation/fullscreen and basic video playback.
- No pixel editing in V1.

## Device Workflow
- Device discovery/enumeration must remain responsive and progressively usable where connector capabilities permit.
- Device metadata enumeration should be lightweight and should not hash every asset on connection.
- Device session state is ephemeral; only durable mappings/observations are persisted.
- Core status model includes Archived, Unknown, Not Archived, plus internal/special states such as Partially Archived and Update Available.
- Metadata similarity alone must never authorize device deletion.
- Import and device cleanup are independent workflows.
- `Import All Unarchived` must be idempotent and must not create content duplicates.
- `Remove from Device` is available only for conclusively safe assets according to preservation rules.
- Future/connector-dependent Archive-to-Device transfer should support recovery and selective transfer; unsupported actions must not be shown as fake UI.

## Media Preservation
- Preserve original media bytes.
- Model logical assets with one or more resources.
- Live Photos preserve required still and motion resources.
- Edited assets preserve original plus current rendered representation where available; adjustment/edit state is retained opportunistically when safely obtainable.
- Location, capture date, favorite, album metadata, media type, dimensions, duration, source provenance, and import time may be cataloged.
- Favorite is a simple asset property/filter; no special physical folder system.
- Album membership is metadata, not physical archive folder layout.
- RAW/DNG/unknown supported media should be preservation-first: inability to preview must not block safe archival.

## Deduplication
- SHA-256 resource content identity is canonical for byte-identical deduplication.
- Same filename/date/size/dimensions or visual similarity is not deduplication proof.
- Similar photos captured seconds apart remain separate assets unless a future explicit similarity feature is added.

## Import
- Imports are transactional and use staging.
- V1 does not require byte-range resume; interrupted current-resource transfer may restart.
- Successful archive state requires complete transfer, integrity verification, durable metadata/catalog commit, and final verified state.
- Import must remain separate from thumbnail/preview background work.

## Thumbnails and Previews
- Small thumbnails are generated for fast Gallery browsing.
- Medium previews may be generated on demand.
- Original media remains the source for full-quality viewing.
- Thumbnail and preview caches are disposable, rebuildable, and excluded from backup.

## Trash
- Deleting from the primary archive moves the asset to Trash.
- V1 has no automatic Trash expiration.
- User can Restore, Delete Permanently, or Empty Trash explicitly.

## Backup
- V1 supports a simple user-defined backup target.
- Backup is a verified secondary copy, not a snapshot/version-history system.
- Backup includes durable archive media/metadata and Trash, but excludes disposable thumbnails/previews/staging/logs.
- Normal backup should process only necessary new/changed work rather than re-hashing the entire archive every run.
- Full verification is a separate explicit operation.

## Platform
- V1 desktop application is Windows-only.
- .NET 10 LTS, WinUI 3, MVP, EF Core SQLite, DI, xUnit.
- Platform-independent archive format is desirable, but cross-platform desktop UI is not an early requirement.
