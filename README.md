# Local Photo Archive

A Windows-first, local-first photo/video archive focused on preservation, integrity, fast browsing, device-aware import, and simple verified backup.

> Status: architecture/design phase. M000 technical feasibility is the first implementation milestone.

## Core Principles
- Keep original media immutable.
- Keep archive files human-readable outside the application.
- Use content-based deduplication.
- Separate import from device cleanup.
- Guard destructive device actions with verified archive state.
- Keep the SQLite catalog rebuildable.
- Prefer simple, explicit workflows over hidden automation.

## Technology Direction
- .NET 10 LTS
- WinUI 3
- MVP
- EF Core + SQLite
- Dependency Injection
- xUnit

## Start Here
Contributors and coding agents: read `AGENTS.md` and `docs/agent/BOOTSTRAP.md` first.
