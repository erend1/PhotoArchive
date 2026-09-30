# M000 Media Compatibility Spike

Issue: #5 — `[M000] Media compatibility spike`  
Branch: `issue/5-media-compatibility`  
Base `main`: `b92603cf900253cc0cc12a91b443fd6262ad35d9`  
Evidence date: 2026-09-30

## Decision

**Preservation capability and viewing capability are separate.** A resource is archivable when its original bytes can be copied, hashed, committed, and represented durably. Thumbnail, preview, or playback failure is a presentation capability state and must not reject an otherwise safe original.

V1 direction:

- preserve every required original resource as immutable opaque bytes and verify SHA-256 + length;
- keep Live Photos as one logical asset with independently preserved still/motion resources;
- use WIC / `Windows.Graphics.Imaging` for still derivatives, with JPEG/PNG as the guaranteed baseline and runtime detection for HEIF/DNG;
- use Windows Media Foundation via Windows media APIs for H.264 MOV/MP4, with runtime detection for HEVC;
- keep thumbnails/previews disposable under `.archive/`;
- do not bundle libheif, FFmpeg, or ImageMagick/Magick.NET in V1 based on this spike alone.

Evidence labels used below:

- **PROVEN BY EXECUTION** — exercised by automated spike tests/fixtures.
- **DOCUMENTED PLATFORM CAPABILITY** — supported by current platform documentation, but not necessarily exercised for every optional codec.
- **NOT YET EMPIRICALLY VERIFIED** — no execution claim is made; a reproduction path is documented.

## Environment and fixtures

- Target: `.NET 10`, `net10.0-windows10.0.19041.0`.
- Windows App SDK: not required by the isolated spike; production remains WinUI 3 / Windows App SDK per ADR-0009.
- Test packages: `Microsoft.NET.Test.Sdk` 18.10.1, `xunit.v3` 4.0.1, `xunit.runner.visualstudio` 4.0.0.
- Runtime media packages/native libraries introduced: **none**.
- Windows CI: `windows-latest`; workflow records Windows edition/version/build, architecture, `dotnet --info`, and codec inventory. Exact run data is recorded in the PR handoff after execution.
- Generated committed JPEG/PNG/H.264/HEVC fixtures contain synthetic pixels only. FFmpeg 7.1.5 on Debian x64 was used **only to generate fixtures**, not as a PhotoArchive runtime dependency.
- HEIC and DNG committed coverage uses deterministic structural fixtures generated in code. These prove byte preservation/container/core-metadata handling, not real HEIC/RAW pixel rendering.

Fixture provenance, hashes, and generation commands: `spikes/M000.MediaCompatibility/fixtures/README.md`.

## Compatibility matrix

| Media type | PRESERVE | METADATA | THUMBNAIL | PREVIEW | PLAYBACK | DEPENDENCIES | LICENSE NOTES | NOTES |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| JPEG | **PASS** | **PASS** | **PASS** | **PASS** | N/A | Built-in WIC / `Windows.Graphics.Imaging` | Windows OS component | **PROVEN:** hash-preserving copy, EXIF date/orientation/GPS/dimensions; generated valid JPEG Windows decode/scale test. |
| PNG | **PASS** | **PASS** | **PASS** | **PASS** | N/A | Built-in WIC / `Windows.Graphics.Imaging` | Windows OS component | **PROVEN:** hash-preserving copy, dimensions, generated valid PNG Windows decode/scale test. |
| HEIC / HEIF | **PASS** | **LIMITED** | **LIMITED** | **LIMITED** | N/A | Optional WIC HEIF decoder; HEVC-coded content may require corresponding optional codec | Optional Windows component; no silent Store assumption. Bundled libheif would add LGPL/native/transitive obligations | **PROVEN:** preservation + structural HEIF dimensions/brand. **NOT VERIFIED:** real HEIC pixel decode/Apple EXIF with committed fixture. |
| Live Photo still + motion | **PASS** | **LIMITED** | **LIMITED** | **LIMITED** | **LIMITED** | Constituent still/video capabilities | Same as constituent resources | **PROVEN:** one logical asset, two roles, independent hashes/paths survive JSON round-trip; no flattening. Apple-origin pairing metadata not yet exercised. |
| MOV / H.264 | **PASS** | **PASS** | **PASS** | **PASS** | **PASS** | Windows Media Foundation / media APIs | Windows OS component | **PROVEN:** preservation + structural metadata; generated MOV open/frame-thumbnail test. |
| MOV / HEVC | **PASS** | **PASS** | **LIMITED** | **LIMITED** | **LIMITED** | Media Foundation + available HEVC decoder | Optional Windows codec; runtime detection required | **PROVEN:** preservation/metadata. Test inventories HEVC codecs and attempts open/frame extraction without making failure an archive failure. |
| MP4 / H.264 | **PASS** | **PASS** | **PASS** | **PASS** | **PASS** | Windows Media Foundation / media APIs | Windows OS component | Same execution path as H.264 MOV; generated MP4 is exercised. |
| MP4 / HEVC | **PASS** | **PASS** | **LIMITED** | **LIMITED** | **LIMITED** | Media Foundation + available HEVC decoder | Optional Windows codec; runtime detection required | Same limitation as HEVC MOV. |
| DNG / representative RAW | **PASS** | **LIMITED** | **LIMITED** | **LIMITED** | N/A | WIC DNG / optional RAW extension depending on file | Windows codec availability varies; no third-party RAW renderer selected | **PROVEN:** preservation + deterministic TIFF/DNG tags. **NOT VERIFIED:** real ProRAW/RAW mosaic rendering/color workflow. |

## Executed evidence

Automated tests cover:

- SHA-256/length preserving copy for JPEG, PNG, HEIC structure, MOV/MP4 H.264/HEVC structures, and DNG structure;
- JPEG EXIF capture date, dimensions, orientation, GPS;
- PNG dimensions;
- HEIF container/dimensions and HEVC coding-family recognition on a structural fixture;
- MOV/MP4 codec, dimensions, duration, creation-time parsing on structural fixtures;
- DNG/TIFF dimensions, orientation, capture date, DNG version;
- Live Photo-style manifest relationship: `OriginalPhoto` + `LivePhotoMotion`, independent hashes and archive-relative paths;
- graceful HEIC/HEVC unsupported-view policy while preservation remains allowed;
- Windows JPEG/PNG decode + scaled pixel extraction;
- Windows H.264 MOV/MP4 open + frame-thumbnail extraction;
- Windows H.264/HEVC media decoder inventory and HEIF/DNG WIC decoder inventory;
- HEVC preservation followed by a conditional open/frame attempt; absence is recorded as LIMITED, not FAIL for preservation.

## Documented platform capability

Current Microsoft Windows documentation states that installed codecs can be queried at runtime; H.264 is a Windows media baseline, while H.265/HEVC support is not universal and can require a corresponding optional codec pack. WIC documentation lists JPEG/PNG/DNG capabilities and a HEIF extension. Media Foundation supports MPEG-4 media sources including `.mov` and `.mp4` and H.264 decode.

References checked 2026-09-30:

- Microsoft Supported codecs: `https://learn.microsoft.com/windows/apps/develop/media-authoring-processing/supported-codecs`
- WIC native codecs: `https://learn.microsoft.com/windows/win32/wic/-wic-about-windows-imaging-codec`
- Media Foundation supported formats: `https://learn.microsoft.com/windows/win32/medfound/supported-media-formats-in-media-foundation`
- `BitmapDecoder.CreateAsync`, `CodecQuery.FindAllAsync`, `MediaClip.CreateFromFileAsync`, `MediaComposition.GetThumbnailAsync` API documentation.

## Not yet empirically verified

No PASS is claimed for:

- real HEIC pixel decoding on clean Windows installations with/without optional HEIF/HEVC components;
- Apple HEIC EXIF/location/auxiliary metadata;
- Apple-origin Live Photo pairing identifiers/metadata;
- ProRAW or representative RAW mosaic development;
- final WinUI `MediaPlayerElement` audiovisual UX;
- HDR/color-management/RAW-development correctness.

Optional local HEIC/DNG experiment without committing private media:

```powershell
$env:PHOTOARCHIVE_HEIC_FIXTURE = 'C:\path\to\sample.heic'
$env:PHOTOARCHIVE_DNG_FIXTURE  = 'C:\path\to\sample.dng'
dotnet test .\spikes\M000.MediaCompatibility\M000.MediaCompatibility.sln -c Release --logger "console;verbosity=detailed"
```

Missing environment variables log `NOT_RUN`; they never become PASS.

## Dependency and licensing review

**Windows-native APIs — recommended baseline.** WIC/`Windows.Graphics.Imaging` and Media Foundation avoid shipping a separate native decoder stack. Optional codec variability is handled with runtime capability detection and explicit unsupported-view states.

**MetadataExtractor 2.9.3 — candidate, not selected here.** Managed .NET, Apache-2.0, low deployment complexity; supports common JPEG/TIFF/PNG/QuickTime metadata. M001 should validate it against real iPhone HEIC/Live Photo/ProRAW samples before adoption.

**libheif — defer.** HEIF/HEIC functionality is strong, but core licensing is LGPL v3-or-later and HEIC decoding commonly brings additional native codec dependencies. Native packaging/architecture/security patching is non-trivial, and malformed-media parsing is a security-sensitive boundary.

**FFmpeg — defer as runtime baseline.** Broad support, but redistribution requires deliberate native build/source/license compliance. FFmpeg is LGPL 2.1-or-later by default; builds using GPL components become GPL. Codec patent/licensing questions are separate.

**Magick.NET/ImageMagick — defer.** Managed package licensing is permissive, but full packages add native/delegate surface and HEIF still inherits delegate codec concerns. WIC is sufficient for the baseline JPEG/PNG derivative path.

## Recommended V1 stack

### Preservation

1. copy each required source resource to staging without mutation;
2. verify SHA-256 + byte length;
3. deduplicate only by exact byte identity;
4. commit original file using original extension/name where practical;
5. treat format/MIME as descriptive metadata, not identity;
6. require all preservation-required resources before a multi-resource asset is deletion-eligible;
7. generate derivatives only after original commit; derivative failure cannot invalidate a verified original;
8. preserve unknown/unsupported formats as opaque bytes unless another safety rule explicitly rejects them.

### Thumbnail/preview

- WIC / `Windows.Graphics.Imaging` first.
- JPEG/PNG: baseline derivative support.
- HEIC: runtime decoder probe; if absent, preserve and return `DecoderUnavailable`/placeholder.
- DNG/RAW: use available WIC embedded/installed-codec decode where possible; otherwise preserve + unsupported preview.
- Keep metadata extraction separate from pixel decode; complete metadata must not be an archive-commit prerequisite.
- Video thumbnails: Media Foundation-backed frame extraction when codec is available.

### Video playback

- Use Windows media stack (`MediaPlayer`/`MediaPlayerElement` in production, Media Foundation underneath).
- H.264 MOV/MP4: V1 baseline.
- HEVC MOV/MP4: conditional runtime capability; never silently assume Microsoft Store codec state.
- Missing codec: archive remains valid; viewer surfaces a specific decoder-unavailable state/open-with option.
- Do not bundle FFmpeg solely to hide HEVC optional-codec behavior without a separate ADR/dependency review.

## Deferred

- bundled cross-machine HEIC/HEVC decoder stack;
- ProRAW/camera RAW development, HDR/color correctness;
- advanced HEIF auxiliary/depth/gain-map rendering;
- synchronized Live Photo motion UX;
- edits/adjustment interpretation;
- AI/face/semantic processing;
- any original-media transcoding for compatibility.

## M001 Archive Core implications

- `ArchiveAsset` preservation state depends on required `MediaResource` integrity, not viewability.
- Each resource retains independent hash, length, relative path, role, and descriptive media type.
- Live Photo completeness is a required-resource-set rule.
- Metadata extraction is best-effort with provenance; failure cannot mutate originals or invent values.
- Derivative state must not affect content identity, deduplication, verification, backup eligibility, or device-deletion safety.
- Unknown formats remain preservable as opaque bytes.

No ADR conflict was found; ADR-0001, ADR-0002, ADR-0003, and ADR-0009 already support these conclusions.

## M002 Gallery implications

- request derivatives asynchronously from a capability-aware service;
- render explicit `DecoderUnavailable` / `UnsupportedFormat` placeholders without hiding the asset;
- keep thumbnail/preview caches rebuildable and disposable;
- do not scan/decode originals merely to populate gallery rows when metadata/cache exists;
- use H.264 Windows-native playback as baseline and expose HEVC/HEIF capability without calling the archive corrupt;
- present a Live Photo as one gallery asset backed by multiple originals.

## Reproduce

```powershell
dotnet restore .\spikes\M000.MediaCompatibility\M000.MediaCompatibility.sln
dotnet build .\spikes\M000.MediaCompatibility\M000.MediaCompatibility.sln --no-restore --configuration Release
dotnet test .\spikes\M000.MediaCompatibility\M000.MediaCompatibility.sln --no-build --configuration Release --logger "console;verbosity=detailed"
```

CI evidence workflow: `.github/workflows/m000-media-compatibility.yml`.
