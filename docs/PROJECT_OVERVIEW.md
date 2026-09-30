# Project Overview

## Working Name
Local Photo Archive (working title; final product name TBD)

## Problem
The product provides a local-first, human-readable, integrity-focused photo/video archive for users who do not want their long-term media preservation to depend on a recurring cloud-storage subscription.

## Primary Use Case
A user connects an iPhone or imports media from another source, sees which assets are already safely archived, archives new assets, reviews media in a fast local gallery, and optionally removes verified assets from the device. A separate backup target protects against primary-storage failure.

## V1 Product Principles
- Windows first.
- Local storage only; no required cloud service.
- Human-readable archive layout.
- Original media bytes are preserved.
- Content-based deduplication.
- Fast gallery experience with virtualization and generated thumbnails.
- Device import and device cleanup are separate workflows.
- Device deletion is guarded by verified archive state.
- Trash protects against user error.
- Backup protects against storage failure.
- The archive must remain useful even if the application disappears.

## Initial Scale
Design for tens of thousands of assets and multiple terabytes without optimizing narrowly for the initial archive size.

## Non-Goals for Early Versions
- Photo editing.
- Face recognition.
- Semantic AI search.
- Visual-similarity deletion.
- Cloud synchronization.
- NAS/network archive as a primary V1 requirement.
- Multi-archive synchronization.
- Complex backup version history.
