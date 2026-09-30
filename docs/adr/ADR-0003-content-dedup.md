# ADR-0003: Use SHA-256 Content Identity for Exact Deduplication

**Status:** Accepted

## Context
Filenames, dates, and visual similarity are insufficient to prove byte-identical content.

## Decision
Use SHA-256 resource bytes as canonical exact-content identity. Connector identifiers and metadata may accelerate candidate matching but do not replace content verification.

## Consequences
Exact duplicates from multiple devices need only one physical resource copy. Hashing is required during definitive import/verification but not during every device enumeration.
