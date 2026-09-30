# M000 — Technical Feasibility

## Objective
Eliminate the highest-risk technical unknowns before committing to production implementation. M000 is an evidence-producing milestone, not a production-feature milestone.

## Exit Rule
Do not start M001 Archive Core until M000 has produced a written feasibility report and the architecture has been updated to reflect the findings.

---

## Spike A — Apple Device Access on Windows

### Goal
Determine what can be accomplished on Windows using documented/supported mechanisms, without a Mac and without undocumented/private Apple APIs.

### Test Environment
Record:
- Windows version/build
- iPhone model
- iOS version
- iCloud Photos on/off
- Optimize iPhone Storage on/off
- installed Apple Windows components
- connection method

### Capabilities to Test
For each capability mark `SUPPORTED`, `LIMITED`, `UNSUPPORTED`, or `REQUIRES_COMPANION` and attach evidence/code notes:

1. Detect connected iPhone.
2. Establish trusted/authorized connection.
3. Enumerate photo/video assets.
4. Obtain lightweight metadata without reading full originals.
5. Obtain stable source identifier suitable for same-device rematching.
6. Obtain thumbnails on demand.
7. Read original resource bytes.
8. Read file/resource size before transfer, if available.
9. Preserve capture date/location metadata.
10. Identify Live Photo components.
11. Retrieve all required Live Photo resources.
12. Detect edited/current representation.
13. Retrieve edited representation and/or adjustment data when available.
14. Delete an asset from the device Photos library.
15. Write/archive media back into the device Photos library.
16. Observe device-library changes incrementally, if supported.
17. Behavior when original exists only in iCloud and not locally on device.
18. Failure behavior when device disconnects mid-enumeration or mid-transfer.

### Hard Constraints
- No private/undocumented Apple APIs.
- No solution that requires unsafe reverse engineering for core V1 guarantees.
- If a capability cannot be supported safely, report it rather than simulating support.

### Deliverables
- Minimal proof-of-concept code.
- Capability matrix.
- Recommended V1 connector strategy.
- Explicit list of features deferred to a future iOS PhotoKit companion, if any.

### Decision Outcome
One of:
- `WINDOWS_CONNECTOR_V1_VIABLE`
- `WINDOWS_IMPORT_ONLY_VIABLE`
- `COMPANION_REQUIRED_FOR_CORE_WORKFLOW`
- `CORE_WORKFLOW_NOT_VIABLE_WITH_CURRENT_CONSTRAINTS`

---

## Spike B — Gallery Performance

### Goal
Validate WinUI 3 as the V1 gallery framework for a large local library.

### Dataset
Generate at least 100,000 synthetic catalog rows with realistic date distribution and metadata. Use generated placeholder thumbnails; no copyrighted/user media required.

### Prototype
- WinUI 3
- ItemsView first
- virtualized layout
- date-descending ordering
- paged/data-virtualized source where appropriate
- lazy thumbnail requests
- selection
- cancellation/deprioritization of off-screen thumbnail work

### Measure
Record:
- process startup time
- time to first usable gallery
- memory after initial load
- memory after extended scroll
- scroll responsiveness
- number of instantiated item containers relative to dataset size
- behavior during rapid scroll
- filter/query latency

### Pass Criteria
- UI must not instantiate/render the full dataset at once.
- Gallery must become usable without scanning all media files.
- Rapid scrolling must remain responsive on the target development machine.
- Memory use must scale with visible/near-visible working set rather than linearly with total assets.

If ItemsView is insufficient, test a lower-level WinUI approach before proposing a framework change.

### Deliverables
- Benchmark/profiling notes.
- Prototype branch/code.
- Recommended gallery control/layout/data-source strategy.

---

## Spike C — Media Compatibility

### Goal
Determine V1 preservation, thumbnail, preview, and playback support.

### Formats
At minimum test:
- JPEG
- PNG
- HEIC/HEIF
- Live Photo pair/resources
- H.264 MOV/MP4
- HEVC/H.265 MOV/MP4
- DNG/ProRAW

### Capability Matrix
For each format record:
- archive original bytes
- read core metadata
- generate small thumbnail
- generate medium preview
- full-resolution viewing
- playback where applicable
- dependency required
- license/redistribution impact

### Principles
- Preservation support is more important than preview support.
- Unsupported preview must not block archiving the original resource.
- Do not rely silently on paid Store codecs or machine-specific optional components unless explicitly accepted by architecture.

### Deliverables
- Media compatibility matrix.
- Decoder/playback dependency recommendation.
- Third-party license notes.

---

## Spike D — EF Core / Archive Portability Sanity Check

### Goal
Prove that a self-contained archive folder can move between Windows machines/paths without manual database setup.

### Test
1. Create archive folder and SQLite DB through application/bootstrap code.
2. Apply migrations automatically.
3. Insert representative catalog data.
4. Close application.
5. Move archive root to a different path/drive letter.
6. Reopen as an existing archive.
7. Verify relative media paths, manifests, and catalog access still work.
8. Simulate missing/corrupt catalog and confirm rebuild design is feasible.

### Deliverables
- Proof-of-concept.
- Migration/bootstrap recommendation.
- Any path-handling rules to promote into ADRs.

---

## M000 Final Report
The milestone owner must publish a concise report containing:
- environment,
- results for Spikes A-D,
- blockers,
- assumptions disproved,
- architecture changes required,
- V1 scope changes,
- recommended next milestone issue breakdown.
