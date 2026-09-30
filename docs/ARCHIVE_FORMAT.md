# Archive Format

## Goals
- Human-readable and application-independent media access.
- Stable relative paths so archives can move between drives/computers.
- Application metadata isolated from user media.
- Rebuildable caches and catalog.

## Layout

```text
PhotoArchive/
  Media/
    2026/
      09/
        IMG_4821.HEIC
        IMG_4821.MOV
        IMG_4837__a18c94f2.HEIC
  .archive/
    archive.json
    catalog.sqlite
    manifests/
    thumbnails/
    previews/
    staging/
    trash/
    logs/
  README.txt
```

`.archive/` may be marked Hidden on Windows for convenience, but it must not be ACL-locked or encrypted merely to prevent other applications/users from reading the archive.

## Media Organization
- Primary folder key: capture year/month.
- Preferred path: `Media/YYYY/MM/`.
- Capture date fallback chain should be explicit and recorded, e.g. trusted embedded capture date -> source-library capture date -> filesystem timestamp -> import time.

## Filenames
- Preserve original filename where practical.
- Filename is not identity.
- On path collision with different content, append a deterministic short hash suffix, e.g. `IMG_4821__a18c94f2.HEIC`.
- Exact collision behavior must preserve both distinct resources and remain deterministic.

## Live Photos / Multi-resource Assets
Related resources may share a base filename but are linked logically through manifest/catalog state.

## Internal Directories
- `manifests/`: durable asset metadata.
- `thumbnails/`: disposable small gallery cache.
- `previews/`: disposable medium-preview cache.
- `staging/`: incomplete/in-progress import transactions; never considered archived media.
- `trash/`: recoverable deleted archive assets.
- `logs/`: operational logs; not part of backup requirements.

## Portability
All durable media paths stored in the catalog/manifests should be archive-relative where possible. Drive letters and absolute archive-root paths must not be required to reopen a moved archive.
