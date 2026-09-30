# ADR-0009: Use .NET 10, WinUI 3, MVP, DI, and EF Core SQLite

**Status:** Accepted

## Context
The first desktop product targets Windows, should teach modern Windows/.NET development, and should preserve clean presentation/persistence boundaries.

## Decision
Use .NET 10 LTS, WinUI 3/Windows App SDK, MVP (Passive/Supervising View), Microsoft DI, EF Core SQLite Code First/migrations, and xUnit. Keep domain/application layers framework-independent.

## Consequences
The product is optimized for Windows rather than cross-platform UI. EF Core simplifies schema evolution and local DB bootstrap while the catalog remains rebuildable.
