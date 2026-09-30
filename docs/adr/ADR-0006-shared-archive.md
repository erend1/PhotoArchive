# ADR-0006: Use a Shared Archive Rather Than Per-User Partitions

**Status:** Accepted

## Context
The intended archive is a common family/personal collection. Per-user physical/logical partitions add complexity and duplicate concepts unnecessarily.

## Decision
One archive is one shared media space. Device/source provenance is metadata only. Users wanting separation create another archive root.

## Consequences
UI and storage remain simple. Future cross-archive tooling can be added without changing the meaning of a single archive.
