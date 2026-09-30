# AGENTS.md

## Purpose
This repository is designed for collaborative development by humans and multiple coding agents. The repository, not any single chat session, is the source of truth.

## Mandatory Reading Order
Before making changes, read:
1. `README.md`
2. `docs/PROJECT_OVERVIEW.md`
3. `docs/ARCHITECTURE.md`
4. `docs/SAFETY_AND_INTEGRITY.md`
5. Relevant ADRs under `docs/adr/`
6. The active milestone under `docs/milestones/`
7. The assigned GitHub Issue
8. Existing tests covering the affected area

See `docs/agent/BOOTSTRAP.md` for the complete load/save protocol.

## Core Engineering Rules
- Preserve original media bytes. Original media is immutable after successful import.
- Never treat filename, timestamp, dimensions, filesize, album membership, visual similarity, or other metadata alone as proof that a device asset is safely archived.
- A destructive device operation is allowed only when the archive state is conclusively safe according to the active specification.
- Imports must be transactional. Incomplete transfers must never appear as successfully archived media.
- The SQLite catalog is a rebuildable index, not the sole durable source of truth.
- Media files remain human-readable and usable outside this application.
- Do not use undocumented/private Apple APIs.
- Do not introduce cloud requirements into V1.
- Keep Windows-first assumptions isolated behind appropriate boundaries where practical.
- Use dependency injection at volatility/external-system boundaries.
- Follow SOLID, but avoid ceremonial abstractions and interface proliferation.
- Architectural changes require an ADR or an explicit update to an existing ADR.
- Add or update tests for behavioral changes.
- Never silently weaken integrity, verification, deletion, or recovery guarantees.
- Never store critical project state only in a chat or agent context.

## Presentation Architecture
- Desktop UI: WinUI 3.
- Presentation pattern: MVP, favoring Passive/Supervising View with bindable view state where useful.
- Views must not contain domain, persistence, hashing, filesystem, or device-connector logic.
- Presenters coordinate application use cases and view state.

## Persistence
- EF Core with SQLite.
- Code First migrations.
- The catalog lives inside the archive's internal metadata directory.
- Schema migrations must never modify original media.
- Catalog recovery/rebuild must remain possible from durable archive metadata and media.

## Git Workflow
- Work from current `main`.
- One issue per focused change whenever practical.
- Branch naming convention: `issue/<number>-<short-slug>`.
- Keep commits small, coherent, and reviewable.
- Do not mix unrelated refactors with feature work.
- PRs must link the issue and explicitly report acceptance criteria and tests.

## Handoff Rule
Before stopping incomplete work, record in the Issue or PR:
- completed work,
- remaining work,
- test status,
- known failures/blockers,
- relevant files,
- last meaningful commit.

A new contributor must be able to continue without access to the previous chat.
