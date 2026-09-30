# ADR-0005: Require Transactional Import Through Staging

**Status:** Accepted

## Context
Interrupted copies, application crashes, and device disconnects must not create false 'Archived' states.

## Decision
Transfer resources to staging first; complete, hash, deduplicate, commit final media, persist manifest/catalog state, then mark Verified. V1 does not require byte-range resume.

## Consequences
Failures are recoverable and visible. Import code is more structured but materially safer.
