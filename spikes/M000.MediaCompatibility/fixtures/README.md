# Generated fixture provenance

No personal or third-party media is committed here.

Committed generated files:

| File | Characteristics | SHA-256 |
| --- | --- | --- |
| `generated/fixture.jpg` | JPEG, 64x48 synthetic blue frame | `0de42df38d04accc98d71ce850f9d773873d34a98abd9186ef5b594a26223104` |
| `generated/fixture.png` | PNG, 64x48 synthetic red frame | `4c3a7f528150b57f765cdd2f8567adc6d5b7354fb8bbb24c9c3e22f2bd068ed9` |
| `generated/fixture-h264.mov` | QuickTime/MOV, H.264, 64x48, 1 s, 5 fps, no audio | `944d6241c41d98543255c3b346dd7010c7571ee2bdf7b04de33b6b3b2ac5c819` |
| `generated/fixture-h264.mp4` | MP4, H.264, 64x48, 1 s, 5 fps, no audio | `ed6386e4ffd4526c998cc4d7720a1d46f50f2724b55fe6ea6a4ade2f7472c1e9` |
| `generated/fixture-hevc.mov` | QuickTime/MOV, HEVC/H.265 (`hvc1`), 64x48, 1 s, 5 fps, no audio | `adcee1888205d81ae3f5d05a5b3aa7c3a367d87e07c58d1868ceb631549a8c70` |
| `generated/fixture-hevc.mp4` | MP4, HEVC/H.265 (`hvc1`), 64x48, 1 s, 5 fps, no audio | `156cd6ab528f8f5e1a2dad22a5d8c85cb40fe32d6835f012b7d3e6e026f3f24c` |

Generation used FFmpeg 7.1.5. Representative commands:

```bash
ffmpeg -f lavfi -i color=c=red:s=64x48:d=1 -frames:v 1 fixture.png
ffmpeg -f lavfi -i color=c=blue:s=64x48:d=1 -frames:v 1 -q:v 2 fixture.jpg
ffmpeg -f lavfi -i testsrc=size=64x48:rate=5:duration=1 -c:v libx264 -pix_fmt yuv420p -movflags +faststart -metadata creation_time=2026-09-30T10:15:00Z fixture-h264.mp4
ffmpeg -f lavfi -i testsrc=size=64x48:rate=5:duration=1 -c:v libx265 -pix_fmt yuv420p -tag:v hvc1 -movflags +faststart -metadata creation_time=2026-09-30T10:15:00Z fixture-hevc.mp4
```

The MOV variants use the same video settings with `.mov` output.

HEIC and DNG are represented by deterministic structural fixtures generated in `SyntheticMediaFactory`. They prove preservation and metadata/container handling, not pixel rendering. Use the optional environment-variable procedure in the spike README to exercise a locally supplied real HEIC/DNG file without committing it.
