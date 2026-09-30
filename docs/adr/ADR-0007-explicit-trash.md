# ADR-0007: Use Explicit Trash Without Automatic Expiration

**Status:** Accepted

## Context
Local storage does not require copying iCloud's 30-day automatic deletion behavior, and accidental deletion is more costly than temporary extra disk use.

## Decision
Archive deletion moves media to Trash. V1 never auto-purges Trash. Permanent deletion requires explicit user action.

## Consequences
User-error recovery is straightforward. Users must manage Trash size themselves.
