# Safety and Integrity

## Fundamental Guarantee
The application must prefer refusing or deferring a destructive action over claiming an asset is safely archived without conclusive evidence.

## Original Media
- Original imported resources are immutable.
- Application metadata changes must not rewrite original media bytes.
- Edited representations and adjustment data, where available, are stored as additional resources/metadata rather than overwriting originals.

## Transactional Import
A resource is not `Archived` merely because transfer started or a destination file exists.

Conceptual stages:
1. Discovered
2. Queued
3. Copying to staging
4. Complete in staging
5. Hashing
6. Deduplication check
7. Commit to final location
8. Manifest commit
9. Catalog commit
10. Verified

Incomplete transfers remain outside `Media/` and must be recoverable/retryable without ambiguity. V1 does not require byte-range resume; retry may restart the current resource.

## Identity and Deduplication
- Canonical content equality is byte-content equality via SHA-256.
- Filename/timestamp/dimensions/filesize/visual similarity are not proof of equality.
- Connector-specific identifiers may accelerate matching but do not replace content identity.

## Device Deletion
Device removal is allowed only for assets whose required archive resources are conclusively safe according to current preservation rules. Unknown, partially archived, or update-available assets are not eligible by default.

## Trash
Primary archive deletion moves assets to Trash. Trash has no automatic purge in V1. Users may restore or permanently delete items explicitly.

## Backup
V1 backup is a verified secondary copy, not a version-history system. New/changed resources are copied and verified. Thumbnail/preview caches are excluded.

## Verification
- Import verification confirms newly committed resources.
- Quick health checks validate structural expectations without hashing the entire archive.
- Full verification may re-hash all resources on explicit request.
