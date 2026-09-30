# ADR-0002: Keep Media Human-Readable Outside the Application

**Status:** Accepted

## Context
The archive should remain useful if this application is unavailable and should be browsable/copied by other devices and software.

## Decision
Store original media as normal files under a human-readable `Media/YYYY/MM/` hierarchy. Keep application metadata under `.archive/`. Do not encrypt or lock media merely to force access through the application.

## Consequences
Vendor lock-in is reduced. External modifications are possible and must be detected/reconciled by health tooling rather than prohibited.
