# ADR-0008: Keep V1 Backup Simple and Verified

**Status:** Accepted

## Context
A sophisticated versioned backup engine would delay the first usable product.

## Decision
V1 supports a user-selected secondary backup target. Copy necessary durable archive data, verify newly copied resources, exclude disposable caches, and keep version-history/snapshot retention out of scope.

## Consequences
Backup addresses storage failure with low conceptual complexity. Historical recovery beyond current Trash/backup state is deferred.
