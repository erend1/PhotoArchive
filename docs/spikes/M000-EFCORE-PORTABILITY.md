# M000 Spike D — EF Core Archive Portability

Issue: #6 — EF Core archive portability spike

## Recommendation

`EFCORE_SQLITE_VIABLE_WITH_CONSTRAINTS`

The archive-contained EF Core + SQLite approach is viable for V1 provided that durable archive state remains outside the catalog, migrations are serialized and preceded by a closed-file catalog backup, and schema evolution stays within SQLite/EF Core provider constraints.

## Scope of the proof

This spike intentionally proves persistence mechanics rather than a production repository layer. It creates this minimal shape:

```text
PhotoArchive/
  Media/
  .archive/
    archive.json
    catalog.sqlite
    manifests/
    backups/
```

Representative catalog entities are `ArchiveAsset`, `MediaResource`, `SourceDevice`, and `SourceObservation`. Media resources store archive-relative paths such as `Media/2026/09/sample.bin`; the archive root itself is not persisted in catalog rows or manifests.

## Recorded versions

- Target framework: .NET 10 (`net10.0`)
- CI SDK: `10.0.x` from the repository `windows-latest` workflow
- EF Core: `Microsoft.EntityFrameworkCore.Sqlite` 10.0.0
- SQLite EF Core provider: `Microsoft.EntityFrameworkCore.Sqlite` 10.0.0
- Test framework: xUnit 2.9.3

The provider carries the SQLite client/native dependencies transitively; this spike does not pin an independent system SQLite installation and requires no database server, account, password, or manual provisioning step.

## Evidence produced by integration tests

`ArchivePortabilityIntegrationTests` uses a unique directory below the OS temporary directory for every test and removes it afterward.

1. **New archive creation** — creates `Media/`, `.archive/archive.json`, `.archive/manifests/`, and `.archive/catalog.sqlite`, then applies both EF Core migrations automatically.
2. **Reopen** — reopens an existing archive without credentials or database configuration outside the archive root.
3. **Move/reopen** — creates and populates an archive under one root, moves the whole directory to a different nested root, reopens it, and resolves the same media through its archive-relative path.
4. **Migration** — creates a real V1 database by migrating only to `202609300001_InitialCatalog`, then applies `202609300002_AddRebuildableAndCatalogOnlyFields` to the existing database.
5. **Pre-migration backup** — when a non-empty catalog has pending migrations, the closed `catalog.sqlite` file is copied to `.archive/backups/catalog.pre-migration.*.sqlite` before migration. The test opens that backup independently, verifies the old schema is present, and verifies the seeded row remains readable.
6. **Migration safety** — hashes a synthetic file in `Media/` before and after migration and proves the bytes are unchanged.
7. **Catalog loss/rebuild** — deletes only `.archive/catalog.sqlite`, verifies media still exists, recreates the schema, and rebuilds core rows from durable manifests without changing media bytes.
8. **Relative-path guard** — round-trips an archive-relative media path and rejects absolute or archive-escaping paths.

CI runs `dotnet restore`, `dotnet build --configuration Release`, and `dotnet test --configuration Release` through the repository workflow.

## Migration findings

Two schema versions are included:

- `202609300001_InitialCatalog`: assets, media resources, source devices, and source observations.
- `202609300002_AddRebuildableAndCatalogOnlyFields`: adds rebuildable `IsFavorite` plus catalog-only `LastIndexedAtUtc`.

The spike applies migrations at archive open. Before mutating an existing non-empty catalog with pending migrations, it disposes the inspection context and copies the database file to a timestamped backup. The current proof deliberately uses SQLite's default single-file journal behavior and does not enable WAL. If production enables WAL, backup must use the SQLite backup API or an equivalent coordinated checkpoint/backup procedure rather than copying only `catalog.sqlite`.

Application startup must serialize migration work for a given archive and surface failures rather than opening partially upgraded state.

## Rebuild findings

### Rebuildable from the durable manifest

- `ArchiveAsset.AssetId`
- capture date
- media kind
- import time
- favorite state
- `MediaResource.ResourceId`
- resource role
- archive-relative resource path
- SHA-256
- byte length
- source device durable ID
- source device display name / connector type as recorded by the manifest
- source asset identifier
- first/last observed timestamps

### Not reconstructable from the current durable manifest

- `ArchiveAsset.LastIndexedAtUtc` (intentionally catalog-only/derived in the spike)
- EF Core migration bookkeeping and SQLite physical layout; these are recreated by migrations
- `SourceObservation.ObservationId` as a database surrogate identity; a rebuild creates a new row identity and consumers must not treat it as durable archive identity
- any future metadata stored only in SQLite and omitted from manifests

Therefore, M001 must define explicitly which fields are durable archive truth and ensure every non-derivable user-important field is written to durable metadata before SQLite can be considered rebuildable.

## SQLite / EF Core constraints relevant to M001

- SQLite lacks several relational concepts such as schemas, sequences, and database-generated concurrency tokens.
- Several SQLite schema changes require EF Core to rebuild a table. Rebuild support is reliable only for artifacts represented in the EF model; manually-created artifacts may require hand-written migration SQL or a custom rebuild path.
- SQLite does not support EF Core idempotent migration scripts because it lacks the procedural language needed to conditionally apply migrations from arbitrary starting states.
- EF Core's SQLite provider uses a migration lock table. A process crash can leave an abandoned `__EFMigrationsLock`, so startup/recovery needs a documented diagnostic path rather than silently deleting migration state.
- `DateTimeOffset` can be stored, but SQLite does not natively support all comparison/ordering operations over it. Production should strongly consider storing normalized UTC `DateTime` values (or another explicit UTC representation) for query-heavy timestamps.
- `MigrateAsync()` should not be wrapped in an application-created explicit transaction; provider migration behavior should own its transaction boundaries.

These are provider constraints, not reasons to abandon SQLite, but they are reasons to keep migrations conservative and integration-tested on real archive copies.

## Portability findings

Portability is achieved by deriving the database path from the selected archive root at runtime and storing durable media references relative to that root. The integration test moves the archive between distinct root paths and reopens it without changing a connection string persisted anywhere.

The hosted CI environment does not guarantee a second writable drive letter, so the automated proof uses different filesystem roots on the same available volume. The architecture has no persisted drive-letter dependency; a later Windows hardware test may additionally repeat the same test across `C:` and another local volume.

## Safety findings

- Schema migration code touches only `.archive/catalog.sqlite` and `.archive/backups/`.
- The recovery path deletes/recreates catalog files only; it does not delete, rewrite, rename, or move `Media/` files.
- Every integration test uses synthetic bytes under disposable temporary directories.
- Catalog deletion demonstrably does not imply media deletion.

## Architectural constraints to carry into M001

1. Keep archive root selection outside persisted resource paths; persist normalized archive-relative media paths.
2. Treat manifests plus media as durable truth and SQLite as a derived index.
3. Back up a closed catalog before any pending migration. Revisit the backup implementation if WAL is enabled.
4. Serialize migrations/rebuilds per archive and fail closed on migration/recovery errors.
5. Do not let SQLite surrogate keys become durable cross-rebuild identities unless those IDs are also persisted in durable metadata.
6. Promote a UTC timestamp storage convention before query-heavy production schema work.
7. Add migration integration tests whenever a migration uses table-rebuild operations or custom SQL.
