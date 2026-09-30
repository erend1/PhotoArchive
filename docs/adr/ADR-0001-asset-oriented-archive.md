# ADR-0001: Use an Asset-Oriented Archive Model

**Status:** Accepted

## Context
A Photos-library item may contain multiple underlying resources such as a still image and Live Photo motion component, or original plus edited representation.

## Decision
Model a logical asset separately from its one-or-more media resources. Preservation and deletion safety operate on required resource sets rather than assuming one file equals one photo.

## Consequences
Live Photos and edited assets can be preserved correctly. Data model and manifests are slightly richer than a file-only design.
