# M000 Media Compatibility Spike

This project is an isolated feasibility harness for GitHub Issue #5. It does **not** implement PhotoArchive's production media subsystem.

## What it proves

- original-resource copying is byte preserving (SHA-256 + length verification), independent of decoder availability;
- deterministic core metadata parsing on generated JPEG/PNG/HEIF/QuickTime/MP4/DNG structural fixtures;
- Live Photo-style still + motion resources survive JSON manifest round-trip as one logical asset with two independently hashed resources;
- unsupported HEIF/HEVC viewing states do not reject preservation;
- on Windows, generated JPEG/PNG are decoded and scaled through `Windows.Graphics.Imaging`;
- on Windows, generated H.264 MOV/MP4 files are opened and frame thumbnails are generated through `Windows.Media.Editing`;
- Windows codec inventory is recorded for H.264, HEVC, HEIF, and DNG rather than assuming optional codecs exist.

## Commands

From the repository root on Windows with .NET 10 SDK:

```powershell
dotnet restore .\spikes\M000.MediaCompatibility\M000.MediaCompatibility.sln
dotnet build .\spikes\M000.MediaCompatibility\M000.MediaCompatibility.sln --no-restore -c Release
dotnet test .\spikes\M000.MediaCompatibility\M000.MediaCompatibility.sln --no-build -c Release --logger "console;verbosity=detailed"
```

Optional local HEIC/DNG rendering experiments can use private/local media without committing it:

```powershell
$env:PHOTOARCHIVE_HEIC_FIXTURE = 'C:\path\to\sample.heic'
$env:PHOTOARCHIVE_DNG_FIXTURE  = 'C:\path\to\sample.dng'
dotnet test .\spikes\M000.MediaCompatibility\M000.MediaCompatibility.sln -c Release --logger "console;verbosity=detailed"
```

The test reports `NOT_RUN` when those variables are absent. A missing optional fixture is never converted into a PASS.

## Fixture policy

`fixtures/generated/` contains only tiny generated JPEG/PNG/H.264/H.265 media. There are no personal photos or videos. HEIC and DNG coverage uses generated, well-defined structural fixtures in code; because those fixtures do not contain coded image/raw payloads, HEIC/DNG rendering remains explicitly unverified unless the optional local procedure is run.

The committed video/still fixtures were generated on Debian using FFmpeg 7.1.5 from synthetic `color`/`testsrc` sources. FFmpeg is a **fixture-generation tool only** and is not a runtime dependency recommendation.
