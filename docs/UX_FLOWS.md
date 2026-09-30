# UX Flows

## Main Navigation
- Gallery
- Device
- Trash
- Backup
- Health
- Settings

Import and cleanup live under Device. Albums/Favorites live under Gallery.

## App Launch
1. Resolve last archive.
2. If available, open Gallery directly.
3. If unavailable, show archive-unavailable state with Retry / Open Another Archive / Create New Archive.
4. Do not show stale Gallery from an application-level offline cache in V1.

## Gallery
- Virtualized chronological grid.
- Fast filters for date, photo/video, favorite, album, source device, filename.
- Small thumbnails from archive cache.
- Viewer supports zoom/navigation/fullscreen; video supports basic playback.
- No media editing.

## Device Connect
1. Create ephemeral DeviceSession.
2. Enumerate lightweight metadata progressively where possible.
3. Begin showing assets before full enumeration completes.
4. Request thumbnails only for visible/near-visible items.
5. Compare incoming descriptors with persistent archive catalog/source mappings.
6. Present `All`, `Not Archived`, `Archived`; expose advanced states only where useful.

## Archive Device Assets
1. User selects assets or `Import All Unarchived`.
2. Resources transfer to staging.
3. Hash/dedup/commit/verify pipeline runs.
4. Device originals remain untouched.
5. Completed import summary reports newly archived, already-existing, and failed assets.
6. Optional action opens Recently Imported in Gallery.

## Phone Cleanup
1. User intentionally enters cleanup/review mode or filters Device view.
2. Only conclusively archived assets are eligible for removal.
3. Unknown/partial/update-available assets block destructive action.
4. Device deletion never occurs as an automatic side effect of import.

## Trash
- Delete from Gallery -> move to Trash.
- Trash supports Restore, Delete Permanently, Empty Trash.
- No automatic expiration in V1.

## Backup
- User selects/configures backup target.
- `Backup Now` shows concise progress.
- New/changed resources are copied and verified.
- Disposable caches are not copied.
- Full historical versioning is out of scope for V1.

## Health
- Quick Check: structural checks, expected files, sizes/state, incomplete staging, obvious missing/untracked items.
- Full Verify: explicitly re-hash archive resources.
