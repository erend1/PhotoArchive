# Agent Bootstrap and Handoff Protocol

## Goal
Make every coding session disposable. A fresh human or agent should be able to reconstruct project context from the repository and GitHub metadata alone.

## Load Protocol
When starting a task:
1. Pull/fetch the latest `main` and record the base commit.
2. Read `AGENTS.md`.
3. Read `docs/PROJECT_OVERVIEW.md`.
4. Read `docs/ARCHITECTURE.md`.
5. Read `docs/SAFETY_AND_INTEGRITY.md`.
6. Read ADRs referenced by the issue or milestone.
7. Read the active milestone specification.
8. Read the assigned GitHub Issue completely.
9. Inspect existing implementation and nearby tests before proposing changes.
10. State any mismatch between issue assumptions and current `main` before changing architecture.

## Work Protocol
- Stay within the issue scope.
- Prefer the smallest change that fully satisfies acceptance criteria.
- Preserve public contracts unless the issue explicitly changes them.
- Add tests before or with behavior changes where practical.
- Run the narrowest relevant tests during development, then the full required suite before handoff.
- If the issue reveals an architectural conflict, stop scope expansion and propose/update an ADR rather than inventing a local workaround.

## Save Protocol
Before ending a session:
1. Commit completed coherent work.
2. Push/update the PR if available.
3. Update the issue or PR with:
   - Completed
   - Remaining
   - Tests run and results
   - Known limitations
   - Blockers
   - Last commit
4. Do not leave essential decisions only in chat.

## Review Protocol
Reviewers should inspect:
- correctness,
- data-loss risk,
- integrity guarantees,
- architecture boundaries,
- DI usage,
- concurrency/cancellation,
- error handling,
- migration/recovery behavior,
- performance implications,
- tests and false confidence,
- scope creep,
- dependency/license impact.

## Risk-Based Routing
Prefer stronger review/agent capacity for:
- destructive operations,
- transactional filesystem code,
- concurrency/race conditions,
- EF Core migrations/recovery,
- Apple device feasibility/connectors,
- archive corruption recovery,
- gallery performance regressions,
- cross-cutting refactors.

Straightforward UI, mappings, small presenters, documentation, and localized tests can be delegated to smaller independent tasks.
