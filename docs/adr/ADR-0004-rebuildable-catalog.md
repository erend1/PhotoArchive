# ADR-0004: Treat SQLite as a Rebuildable Catalog

**Status:** Accepted

## Context
A long-lived personal archive should not become unreadable because one application database is lost or corrupted.

## Decision
Use EF Core SQLite as the high-performance catalog while retaining durable manifests and normal media files. Design catalog rebuild/recovery as a supported operation.

## Consequences
Persistence remains convenient without making the database the sole source of truth. Some application-only metadata may still depend on manifests/backups for complete recovery.
