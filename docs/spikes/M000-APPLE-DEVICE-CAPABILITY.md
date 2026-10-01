# M000 Spike A — Apple Device Capability on Windows

| | |
|---|---|
| Issue | [#3 — [M000] Apple device capability spike](https://github.com/erend1/PhotoArchive/issues/3) |
| Branch / base | `issue/3-apple-device-capability`, based on `main` @ `b92603c` |
| Probe | [`spikes/M000.AppleDevice/`](../../spikes/M000.AppleDevice/README.md) (spike-only) |
| Evidence files | [`spikes/M000.AppleDevice/evidence/`](../../spikes/M000.AppleDevice/evidence/) |
| Report date | 2026-10-01 |
| Decision token | at the end of this document (§14) |

## 1. Summary

**Primary question:** can the intended device workflow be implemented acceptably from Windows alone, or is a native
iOS PhotoKit companion required?

**Answer (current evidence):**

- The strongest documented Windows path is the iPhone's **PTP camera interface** (the "DCIM view") read through the
  **Windows Portable Devices (WPD)** API, or through **Windows.Media.Import** on top of the same transport. Apple
  documents this path for importing photos to a PC (Apple Devices installed, phone unlocked, computer trusted).
- That path can plausibly support **import**: detection, progressive and cancellable enumeration, DCIM file names,
  sizes and dates, device thumbnails, and original bytes — **with documented preconditions**: the iPhone setting
  *Transfer to Mac or PC = Keep Originals* (otherwise Apple says media "might be converted to JPEG or H.264"), and
  originals must be resident on the phone (with iCloud Photos, Apple says to download originals to the phone first).
- It is **not** Photos-library access. Nothing documented on Windows exposes Photos asset identity, favorites, albums,
  edit semantics, iCloud-only originals, persistent change history, documented-safe deletion, or write-back/restore.
  These require a **PhotoKit companion**, which is architecturally valid but cannot be built today because building,
  signing and running iOS apps requires Xcode on a Mac (and the Apple Developer Program for distribution).
- **No iPhone was attached to the execution environment of this spike.** Nothing iPhone-specific is `EXECUTED`.
  Windows-side API availability is `EXECUTED`; iPhone behaviour is either `OFFICIAL_DOCUMENTATION` or
  `LOCAL_EXECUTION_REQUIRED`, and a ready-to-run harness with an ordered experiment plan is committed.

**Recommendation:** Windows-only **import** is viable for V1 under explicit prerequisites; safe device cleanup and
write-back are not available from Windows and are deferred to a future PhotoKit companion. The final token is in §14;
§10 lists the local results that would change it.

## 2. Scope, constraints and method

- Only documented, supported mechanisms are eligible for the recommended architecture. Researched and **rejected**
  as unsuitable (never used by the probe): libimobiledevice / usbmuxd / lockdownd / AFC (reverse-engineered Apple
  protocols), Apple's private `MobileDevice` / `iTunesMobileDevice` libraries shipped with Apple software, parsing
  iTunes / Apple Devices backups (undocumented format; also excludes iCloud Photos content), jailbreak tooling, and
  private iCloud web endpoints.
- No Mac is available. Real iPhones exist, but none was connected to this execution environment (a Windows 11 PC:
  no `VID_05AC` USB node, no WPD device; only Bluetooth pairing records).
- Method: (1) official Apple and Microsoft documentation, quoted with links in §5; (2) a reproducible Windows probe
  (§9) executed here for everything that does not need a phone; (3) explicit local experiments (§10) for the rest.

### Vocabulary

| Status | Meaning |
|---|---|
| `SUPPORTED` | The Windows-only path provides the capability adequately for the product's purpose. |
| `LIMITED` | Available with material limitations, preconditions or heuristics (stated in the row). |
| `UNSUPPORTED` | Neither the Windows path nor a companion provides it (not used below: every gap has a documented PhotoKit equivalent). |
| `REQUIRES_COMPANION` | Not available from Windows through documented mechanisms; documented PhotoKit API provides it. |

| Evidence | Meaning |
|---|---|
| `EXECUTED` | Observed by running code in this spike. |
| `LOCAL_EXECUTION_REQUIRED` | No Apple/Microsoft document states it for iPhone; the committed harness measures it when run on a PC with a trusted iPhone. |
| `OFFICIAL_DOCUMENTATION` | Stated in Apple or Microsoft documentation (§5) but not yet observed on a device in this spike. |
| `UNKNOWN` | Neither documented nor measurable with the current harness. |

Statuses are planning classifications and are **provisional wherever the evidence is not `EXECUTED`**. In the
requested five-category view: (1) proven by real-device execution — **none**; (2) proven by a local harness the user
can run — **harness ready, results pending** (only Windows-side API availability is proven); (3) supported by official
documentation but not yet verified — every `OFFICIAL_DOCUMENTATION` row; (4) unknown/blocked — §10 open questions;
(5) requires iOS companion/PhotoKit — every `REQUIRES_COMPANION` row.

## 3. Test environment (M000 required fields)

| Field | Value |
|---|---|
| Windows version/build | Windows 11 Pro 25H2, build 26200.8246, x64 (`EXECUTED`) |
| .NET | SDK 10.0.401, runtime 10.0.12 |
| WPD components | PortableDeviceApi.dll 10.0.26100.8875; PortableDeviceTypes.dll 10.0.26100.5074; wpdmtp.dll 10.0.26100.8115 |
| Windows.Media.Import | `PhotoImportManager.IsSupportedAsync()` = **true**; 0 sources (`EXECUTED`) |
| Installed Apple Windows components | No Apple Devices / iTunes / iCloud Store package; no Apple Mobile Device service; `appleusb.inf` (Apple Inc., 538.0.0.0, 2023-06-14) present in the driver store; iCloud Outlook add-in 15.0.0.215 |
| iPhone model / iOS version | **Not tested** — no device attached |
| iCloud Photos / Optimize iPhone Storage | Not applicable to this run; must be recorded per local run |
| Connection method | None (USB experiments pending) |

Raw, sanitized output: [`evidence/2026-10-01-dev-machine-no-iphone.md`](../../spikes/M000.AppleDevice/evidence/2026-10-01-dev-machine-no-iphone.md).

## 4. Windows-side capability matrix

Mechanism key: **WPD** = Windows Portable Devices COM API over the in-box MTP/PTP class driver; **WMI** =
`Windows.Media.Import`. Experiment IDs (E#) refer to §10 and the probe README.

| # | Capability | Status | Evidence | Windows mechanism / exact method | Verify | Notes |
|---|---|---|---|---|---|---|
| 1 | Device detection | `SUPPORTED` | `OFFICIAL_DOCUMENTATION` | WPD `IPortableDeviceManager::GetDevices` (+ device-interface arrival notifications); WMI `PhotoImportManager.FindAllSourcesAsync` | E1 | Apple documents USB import to a Windows PC; Microsoft documents a WPD class driver for PTP. Both APIs `EXECUTED` here with 0 devices. |
| 2 | Trust / pairing | `SUPPORTED` | `OFFICIAL_DOCUMENTATION` | None (user-mediated): unlock with passcode, tap **Trust**; app can only detect the resulting state | E1 | Apple: trusted computers "can … access your device's photos, videos…"; trust persists until *Reset Location & Privacy*. Microsoft: "Your PC can't find the device if the device is locked." Visibility while a trusted phone is locked: E1. |
| 3 | Photo/video enumeration | `LIMITED` | `OFFICIAL_DOCUMENTATION` | WPD `IPortableDeviceContent::EnumObjects` over the DCIM tree; WMI `PhotoImportSession.FindItemsAsync` | E3, E8 | Enumerates **DCIM file objects, not Photos assets**. Microsoft: items that "exist on iCloud but not on your device" may not be importable. Hidden / Recently Deleted / Shared Library visibility unknown. |
| 4 | Lightweight metadata without reading originals | `LIMITED` | `LOCAL_EXECUTION_REQUIRED` | WPD `IPortableDeviceProperties::GetValues` (PTP ObjectInfo-derived properties) | E3 | At most name, size, dates, format, maybe pixel size. Never favorites, albums, location or edit state. The probe prints a per-property presence table. |
| 5 | Progressive enumeration | `SUPPORTED` | `OFFICIAL_DOCUMENTATION` | WPD `IEnumPortableDeviceObjectIDs::Next` in batches, folder by folder (WMI only reports counts until completion) | E3 | Time-to-first-object and throughput on a large library unknown. |
| 6 | Cancellable enumeration | `SUPPORTED` | `OFFICIAL_DOCUMENTATION` | Stop between `Next` batches, `IEnumPortableDeviceObjectIDs::Cancel`, `IPortableDevice::Cancel`; WMI `IAsyncOperationWithProgress` cancellation | E3 | Cancellation latency unknown (`--cancel-after`, `--cancel-after-ms`). |
| 7 | Capture date | `LIMITED` | `LOCAL_EXECUTION_REQUIRED` | `WPD_OBJECT_DATE_CREATED` before transfer; EXIF `DateTimeOriginal`/`OffsetTimeOriginal` from bytes after transfer | E3, E6 | Library-level date edits are not exposed; device-side timezone semantics unknown. PhotoKit `creationDate` needs the companion. |
| 8 | Dimensions | `LIMITED` | `LOCAL_EXECUTION_REQUIRED` | `WPD_MEDIA_WIDTH/HEIGHT` if exposed; otherwise parse bytes after transfer | E3 | |
| 9 | Duration | `LIMITED` | `LOCAL_EXECUTION_REQUIRED` | `WPD_MEDIA_DURATION` if exposed; otherwise parse the MOV header after transfer | E3 | The PTP ObjectInfo dataset has no duration field; exposure through WPD is unverified. |
| 10 | Filename / filename-like metadata | `SUPPORTED` | `LOCAL_EXECUTION_REQUIRED` | `WPD_OBJECT_ORIGINAL_FILE_NAME` / `WPD_OBJECT_NAME`; WMI `PhotoImportItem.Name` | E3 | DCIM names (`IMG_1234.HEIC`) only: not identity, recycled after 9999, not unique across DCIM folders, may differ from PhotoKit `originalFilename`. |
| 11 | Stable identifier — device object (same-device rematch accelerator) | `LIMITED` | `LOCAL_EXECUTION_REQUIRED` | `WPD_OBJECT_PERSISTENT_UNIQUE_ID` (WPD contract: "must be stored across sessions"); `WPD_OBJECT_ID` is session-scoped | E5 | Identifies a DCIM object, not a Photos asset. Whether the iPhone/driver honours the PUID contract, and whether PUIDs are reused after deletion, is measured by `identity-snapshot`/`identity-compare`. Never identity proof (ADR-0003). |
| 12 | Stable identifier — Photos asset | `REQUIRES_COMPANION` | `OFFICIAL_DOCUMENTATION` | None on Windows | — | PhotoKit `localIdentifier`; cross-device `cloudIdentifierMappings(forLocalIdentifiers:)` (iOS 15+). |
| 13 | Thumbnails on demand | `SUPPORTED` | `LOCAL_EXECUTION_REQUIRED` | WPD `IPortableDeviceResources::GetStream(WPD_RESOURCE_THUMBNAIL)`; WMI `PhotoImportItem.Thumbnail` | E3 | Size, format and latency unknown. Gallery thumbnails are generated from archived originals anyway (PRODUCT_REQUIREMENTS). |
| 14 | Original photo bytes | `LIMITED` | `OFFICIAL_DOCUMENTATION` | WPD `GetStream(WPD_RESOURCE_DEFAULT)` → staging → size check → SHA-256 → signature check | E3, E4 | Apple: USB import "might be converted to JPEG or H.264" unless **Keep Originals**. Windows cannot read that setting; conversion is detectable only when the advertised name keeps the original extension. Resident originals only. |
| 15 | Original video bytes | `LIMITED` | `OFFICIAL_DOCUMENTATION` | Same as #14 | E3, E4, E10 | Additionally: PTP's 32-bit object size field cannot express ≥ 4 GiB (`0xFFFFFFFF` sentinel), so pre-transfer size checks need a fallback for long videos. |
| 16 | HEIC originals | `LIMITED` | `OFFICIAL_DOCUMENTATION` | Same as #14 | E4 | Requires **Keep Originals** (Apple). Preservation does not require decoding; preview is Spike C's scope. |
| 17 | MOV originals (HEVC/H.264) | `LIMITED` | `OFFICIAL_DOCUMENTATION` | Same as #14 | E4 | HEVC may be converted to H.264 under *Automatic* (Apple). |
| 18 | Size before transfer | `LIMITED` | `LOCAL_EXECUTION_REQUIRED` | `WPD_OBJECT_SIZE`; WMI `PhotoImportItem.SizeInBytes` | E3, E4 | 32-bit limit (#15); converted output may not match the advertised size. |
| 19 | Live Photo still resource | `LIMITED` | `LOCAL_EXECUTION_REQUIRED` | DCIM image object | E6 | Not documented by Apple for PTP. |
| 20 | Live Photo motion resource | `LIMITED` | `LOCAL_EXECUTION_REQUIRED` | DCIM `.MOV` object with the same DCIM number | E6 | Not documented by Apple for PTP. |
| 21 | Relationship between Live Photo resources | `LIMITED` | `LOCAL_EXECUTION_REQUIRED` | Not exposed as a relationship. Inferred from the DCIM number; corroborated from bytes by a shared content identifier (QuickTime key `com.apple.quicktime.content.identifier`, documented in AVFoundation as `quickTimeMetadataContentIdentifier`) | E6 | The probe reports "still and motion bytes share a UUID token". WMI sibling grouping unknown (E11). PhotoKit: `.photo` + `.pairedVideo` resources of one asset. |
| 22 | Edited / current representation discovery | `LIMITED` | `LOCAL_EXECUTION_REQUIRED` | DCIM `IMG_E*` objects (community-reported convention, not Apple-documented) | E6 | PhotoKit: `hasAdjustments`, `.fullSizePhoto` / `.fullSizeVideo`. |
| 23 | Original + edited representation together | `LIMITED` | `LOCAL_EXECUTION_REQUIRED` | Both DCIM objects, if both are exposed | E6 | The probe counts "edited render without an exposed original" as a completeness red flag. |
| 24 | Adjustment / edit metadata | `LIMITED` | `LOCAL_EXECUTION_REQUIRED` | `.AAE` plist sidecar, if exposed | E6 | Apple-private, opaque data: preserve opportunistically as `AdjustmentData`; no portable semantics (consistent with DATA_MODEL). |
| 25 | Favorite metadata | `REQUIRES_COMPANION` | `OFFICIAL_DOCUMENTATION` | None documented on Windows | E3 (confirms absence) | PhotoKit `PHAsset.isFavorite`. |
| 26 | Album membership | `REQUIRES_COMPANION` | `OFFICIAL_DOCUMENTATION` | None documented on Windows | E3 (confirms absence) | PhotoKit `PHAssetCollection`. Apple Devices only syncs PC folders *to* the phone. |
| 27 | Location metadata | `LIMITED` | `LOCAL_EXECUTION_REQUIRED` | Embedded EXIF GPS / QuickTime ISO 6709 in the original bytes, after transfer | E3, E6 | Library-level location edits are not exposed. PhotoKit `PHAsset.location` via companion. |
| 28 | Delete from the iPhone Photos library | `REQUIRES_COMPANION` | `OFFICIAL_DOCUMENTATION` | No documented Photos-library delete from Windows. Generic WPD `IPortableDeviceContent::Delete` and WMI `DeleteImportedItemsFromSourceAsync` exist, but Apple documents no semantics for PTP deletes | E2 (declared), E12 (guarded) | Even if a PTP delete works, its effect (Recently Deleted? whole asset or one resource? iCloud/Shared Library propagation?) is undocumented, so it cannot back a conclusively safe V1 cleanup. PhotoKit `PHAssetChangeRequest.deleteAssets` with a system confirmation alert. |
| 29 | Write / import / restore into the Photos library | `REQUIRES_COMPANION` | `OFFICIAL_DOCUMENTATION` | Only Apple Devices **Sync Photos** (PC folder → phone): requires iCloud Photos off, manual, removal only by re-sync, no API. WPD object creation expected to be refused | E2, E13 | PhotoKit `PHAssetCreationRequest` (incl. Live Photo resources). |
| 30 | Observe library changes incrementally | `LIMITED` | `LOCAL_EXECUTION_REQUIRED` | WPD events while connected (`IPortableDevice::Advise`); otherwise full re-enumeration and diff | E9 | Persistent change history: PhotoKit `fetchPersistentChanges(since:)` (iOS 16+) → companion. |
| 31 | Behaviour with iCloud Photos enabled | `LIMITED` | `OFFICIAL_DOCUMENTATION` | Same USB/PTP path | E7 | Resident originals only (Apple, Microsoft); deletions on a device propagate to iCloud and all devices (Apple); Apple Devices photo sync is disabled (Apple). |
| 32 | Optimize iPhone Storage: original not resident on the phone | `REQUIRES_COMPANION` | `OFFICIAL_DOCUMENTATION` | USB/PTP cannot download iCloud-only originals (Apple: download originals to the phone first; Microsoft: use iCloud for Windows). iCloud for Windows is a cloud path, outside V1 scope | E7 | **Safety-critical unknown:** whether PTP lists such items at all, or exposes the "space-saving version" under the original's name. PhotoKit downloads originals with `isNetworkAccessAllowed = true`. |
| 33 | Disconnect mid-enumeration / mid-transfer | `SUPPORTED` | `LOCAL_EXECUTION_REQUIRED` | COM failure `HRESULT`s surface to the caller; staging files are never promoted (probe design) | E10 | Exact HRESULTs unknown; the probe records them. |
| 34 | Photos-library (asset-level) access as such | `REQUIRES_COMPANION` | `OFFICIAL_DOCUMENTATION` | — | — | See §4.1 and §6: every documented Windows path is camera-style file transfer, sync-to-device, backup, app file sharing, or cloud. |

### 4.1 DCIM / file-transfer access vs. Photos-library access

| Aspect | DCIM over PTP (Windows) | Photos library (PhotoKit, on iOS) |
|---|---|---|
| Unit | File object in a virtual DCIM tree | `PHAsset` with typed `PHAssetResource`s |
| Identity | Session object ID; PUID of unverified stability | `localIdentifier`; cloud identifier mappings |
| Live Photos / edits | Separate files; relationships inferred from names (and content identifiers in bytes) | Explicit resource types (`.photo`, `.pairedVideo`, `.fullSizePhoto`, `.adjustmentData`, …) |
| Library metadata | None (favorites, albums, hidden, captions, keywords, ratings, location edits absent) | Documented properties and collections |
| iCloud-only originals | Not obtainable | Downloadable on request |
| Delete / create | No documented semantics / sync-only alternative | Documented change requests with system alerts |
| Change tracking | In-session events only | Persistent change tokens (iOS 16+) |

## 5. Official statements this report relies on

| Source | Statement used |
|---|---|
| Apple, [Transfer photos from your iPhone or iPad](https://support.apple.com/en-us/120267) (published 2026-09-14) | Import to a Windows PC: "Install the Apple Devices app from the Microsoft Store", connect by USB, "If asked, unlock…", "Trust This Computer, tap Trust or Allow", then use Microsoft Photos. "If you have iCloud Photos turned on, you must download the original, full resolution versions of your photos to your iPhone or iPad before you import to your PC." |
| Apple, [Using HEIF or HEVC media on Apple devices](https://support.apple.com/en-us/116944) (2025-12-05) | "When you import HEIF or HEVC media from an attached iPhone or iPad into Photos, Image Capture, or a PC, the media might be converted to JPEG or H.264. If you don't want it to be converted, open Settings, tap Apps, tap Photos, then scroll down and tap Keep Originals." |
| Apple, [About the 'Trust This Computer' alert](https://support.apple.com/en-us/109054) (2025-12-04) | "Trusted computers can sync with your device and access your device's photos, videos, contacts, and other content. These computers remain trusted unless you change which computers you trust or erase your device." |
| Apple, [Set up and use iCloud Photos](https://support.apple.com/en-us/108782) (2026-09-14) | "When you delete photos and videos on one device, they're deleted everywhere that you use iCloud Photos … Recently Deleted folder for 30 days." With Optimize Storage, "your device keeps space-saving versions … while iCloud Photos stores … the original, high-resolution version." |
| Apple, [If your computer doesn't recognize your iPhone or iPad](https://support.apple.com/en-us/108643) (2026-08-12) | Windows: open Apple Devices, connect, trust; check cable (must support data), port, software conflicts, latest Windows/iOS. |
| Apple Devices User Guide for Windows: [Sync photos](https://support.apple.com/guide/devices-windows/sync-photos-to-your-device-mchl4af095d3/windows), [Intro to syncing](https://support.apple.com/guide/devices-windows/syncing-overview-mchl923c1147/windows) | Photo sync is PC → device; "You can't use the syncing method … unless you turn off iCloud Photos"; synced albums are removed by deselecting and re-syncing; "If you delete an automatically synced item from your Windows device, the deleted item is removed from your Apple device the next time you sync." |
| Apple, [Use Apple Devices to share files…](https://support.apple.com/en-us/120402) (2025-03-27) | File Sharing copies files between the PC and apps that support File Sharing, manually through the Apple Devices UI. |
| Apple, [Delete and recover photos and videos in iCloud for Windows](https://support.apple.com/guide/icloud-windows/delete-and-recover-photos-and-videos-icw5c1e2d1a7/icloud) | Deleting in the iCloud Photos folder deletes "from all your devices that have iCloud Photos turned on" (a cloud mechanism, not device access). |
| Microsoft, [Import photos and videos from phone to PC](https://support.microsoft.com/en-us/windows/import-photos-and-videos-from-phone-to-pc-198f2301-e9a7-c734-5f39-a8946a5ebc99) | "Your PC can't find the device if the device is locked." iPhone: Trust prompt; "If iCloud is enabled … you may not be able to download your photos or videos if they exist on iCloud but not on your device … use iCloud for Windows." Phone Link photos require Android. |
| Microsoft Learn, [Introduction to WPD Drivers](https://learn.microsoft.com/en-us/windows-hardware/drivers/portable/wpd-drivers-overview) | WPD "implements a class driver solution for … Picture Transfer Protocol (PTP) over USB …"; "The WPD MTP driver also supports Picture Transfer Protocol (PTP) devices." |
| Microsoft Learn, [Object Properties](https://learn.microsoft.com/en-us/windows/win32/wpd_sdk/object-properties), [Retrieving an Object Id from a PUID](https://learn.microsoft.com/en-us/windows/win32/wpd_sdk/retrieving-an-object-identifier-from-a-persistent-unique-identifier) | `WPD_OBJECT_ID` "need not be stored across sessions"; `WPD_OBJECT_PERSISTENT_UNIQUE_ID` "must be stored across sessions"; "Some devices … generate the PUID based on a hash of selected object data." |
| Microsoft Learn, [IPortableDeviceContent::Delete](https://learn.microsoft.com/en-us/windows/win32/api/portabledeviceapi/nf-portabledeviceapi-iportabledevicecontent-delete), [Storage properties](https://learn.microsoft.com/en-us/windows/win32/wpd_sdk/storage-properties) | Generic object deletion with per-object results; `WPD_STORAGE_ACCESS_CAPABILITY` "identifies any write-protection that globally affects this storage". |
| Microsoft Learn, [Windows.Media.Import](https://learn.microsoft.com/en-us/uwp/api/windows.media.import?view=winrt-26100) and [Import media from a device](https://learn.microsoft.com/en-us/windows/uwp/audio-video-camera/import-media-from-a-device) | `PhotoImportStorageMedium.SupportedAccessMode` (`ReadWrite`, `ReadOnly`, `ReadAndDelete`); `PhotoImportItem` (`Name`, `ItemKey`, `SizeInBytes`, `Date`, `Sibling`, `Sidecars`, `VideoSegments`, `Thumbnail`); find/import/delete are `IAsyncOperationWithProgress` and cancellable; `DeleteImportedItemsFromSourceAsync` deletes the items imported in that session. |
| Apple Developer, [PhotoKit](https://developer.apple.com/documentation/photokit) and the pages cited in §6 | Resource types, resource manager, change requests, access levels, cloud identifiers, persistent changes, background resource upload. |

Community reports (not evidence, used only to design experiments): `IMG_E*` edited renders and `.AAE` sidecars in
DCIM; copy failures `0x8007001F` linked to the *Automatic* conversion; DCIM deletion being blocked when iCloud Photos is
enabled. Each is tested by an experiment rather than assumed.

## 6. Windows-side mechanisms: roles and limits

| Mechanism | Role | Limits relevant to PhotoArchive |
|---|---|---|
| **WPD / PTP** (in-box MTP/PTP class driver) | The documented programmatic path to the iPhone's camera-style DCIM view; what Microsoft Photos and File Explorer use. | File objects only; no asset/library semantics; behaviour of delete/create on iOS undocumented; conversion depends on an iPhone setting Windows cannot read; resident originals only; 32-bit PTP sizes. |
| **Windows.Media.Import** | Higher-level WinRT import API over the same transport; adds sibling/sidecar grouping, progress, cancellation and `DeleteImportedItemsFromSourceAsync`. | Items only after the find completes (progress = counts); import writes to a folder itself; delete is bound to the same session's import result; same PTP limits. |
| **Microsoft Photos import** | Consumer app documented by Apple and Microsoft for iPhone import. | No API; same transport and limits. |
| **Apple Devices for Windows** | Apple's documented prerequisite for importing to a PC; device management, backup/restore, music/movie/TV/photo sync *to* the device, File Sharing with apps, Wi-Fi sync after cable setup. | No public API. Photo sync is PC → phone only and only with iCloud Photos off. Does not itself import photos from the phone. |
| **iCloud for Windows** | Documented cloud access to iCloud Photos (download; deletes propagate to all devices). | A cloud mechanism: outside V1 (no cloud requirement) and not a device connector. Possible future optional *folder* import source. |
| **Phone Link** | — | Photos feature requires Android (Microsoft). |
| **Rejected (§2)** | — | Reverse-engineered or private Apple protocols/libraries, backup parsing, jailbreak, private iCloud APIs. |

## 7. PhotoKit companion analysis (research only; nothing built)

### 7.1 What PhotoKit documents

| Capability | PhotoKit API (iOS) | Notes |
|---|---|---|
| Full-library enumeration incl. iCloud items | `PHAsset.fetchAssets(with:)`; collections via `PHAssetCollection` | "Assets contain only metadata. The underlying image or video data … might not be stored on the local device." |
| Asset metadata | `creationDate`, `modificationDate`, `location`, `pixelWidth/Height`, `duration`, `isFavorite`, `isHidden`, `hasAdjustments`, `mediaType`, `mediaSubtypes`, `sourceType`, `burstIdentifier`, `rating`, `playbackStyle` | Documented `PHAsset` properties. |
| Identity | `localIdentifier`; `PHPhotoLibrary.cloudIdentifierMappings(forLocalIdentifiers:)` (iOS 15+) | Cloud identifiers are for cross-device mapping; Apple warns the lookup is expensive. |
| Resources and originals | `PHAssetResource.assetResources(for:)`; types `.photo`, `.video`, `.pairedVideo`, `.fullSizePhoto`, `.fullSizeVideo`, `.fullSizePairedVideo`, `.adjustmentData`, `.adjustmentBasePhoto`, `.adjustmentBaseVideo`, `.adjustmentBasePairedVideo`, `.alternatePhoto`, `.audio`; `originalFilename`, `uniformTypeIdentifier` | Apple: use these objects "when backing up or restoring assets"; "An edited asset contains resources representing asset content before and after the edit, as well as a resource corresponding to the PHAdjustmentData object." |
| Reading bytes | `PHAssetResourceManager.requestData(for:options:dataReceivedHandler:completionHandler:)` (chunked, cancellable) / `writeData(for:toFile:options:)` | `isNetworkAccessAllowed = true` downloads iCloud-only data; `false` fails with an error indicating network access is required — this also tells the companion whether an original is resident. |
| Thumbnails | `PHImageManager` / `PHCachingImageManager` | "the Photos framework can manage downloading, generating, and caching thumbnails." |
| Live Photos | `.photo` + `.pairedVideo` resources; `PHLivePhoto.request(withResourceFileURLs:…)`; import via `PHAssetCreationRequest` | Photos "validates that the files and their metadata can be loaded as a Live Photo." |
| Delete | `PHAssetChangeRequest.deleteAssets(_:)` inside `PHPhotoLibrary.performChanges` | "For each call to this method, iOS shows an alert asking the user for permission to edit the contents of the photo library." With iCloud Photos, deletions sync everywhere; Recently Deleted keeps them 30 days (Apple support). |
| Create / restore | `PHAssetCreationRequest.addResource(with:fileURL:options:)`; preflight `supportsAssetResourceTypes(_:)`; `creationDate`, `location`, `isFavorite`, `isHidden` on change requests | A restored asset is a new asset (new `localIdentifier`). Whether an edited asset can be restored with original + render + adjustment data is to be validated with `supportsAssetResourceTypes`. |
| Persistent change tracking | `fetchPersistentChanges(since:)` (iOS 16+) with `PHPersistentChangeToken` | Tokens can expire (`persistentChangeTokenExpired`). |
| Background backup | Photos **Background Resource Upload** extension (`com.apple.photos.background-upload`; iOS 26.1, async protocol iOS 27) with `PHAssetResourceUploadJob` | System-scheduled uploads of resources to an HTTP endpoint; auto-downloads iCloud resources first; resumable-upload protocol; requires full library access; `BackgroundUploadURLBase` fixed in Info.plist; not available in Simulator; Developer Mode testing requires running from Xcode. Fitness for a LAN-only PC endpoint is **UNKNOWN**. |

### 7.2 Permissions

- `NSPhotoLibraryUsageDescription` (read/write) and `NSPhotoLibraryAddUsageDescription` (add-only); `PHAccessLevel.readWrite` / `.addOnly`.
  A backup/cleanup app needs **full** read/write access; with limited access the app sees only the user's selection.
- Every `performChanges` (delete, create, edit) is confirmed by a system alert.
- Local network: `NSLocalNetworkUsageDescription`, `NSBonjourServices` for Bonjour; first use shows the local network
  privacy alert (TN3179); raw multicast additionally needs the `com.apple.developer.networking.multicast` entitlement.

### 7.3 Communication with the Windows application

- No documented API lets a Windows app talk to an iOS app over USB (Apple Devices File Sharing is manual UI only),
  so a companion would use the **local network**.
- Discovery: Bonjour on iOS; Windows has native DNS-SD (`DnsServiceRegister` / `DnsServiceBrowse`, Windows 10+ desktop
  apps). Fallback: QR-code / manual pairing.
- Transport/security (design work): TLS with a pairing-time key exchange, per-resource SHA-256 computed on both sides.
- iOS suspends apps in the background: interactive transfers happen with the companion in the foreground; the iOS 26.1+
  background upload extension is the documented background path but requires an HTTP(S) server reachable at the
  build-time `BackgroundUploadURLBase` — a prototype must show whether a LAN PC endpoint is acceptable.
- Windows: a listening desktop app needs a firewall rule; an outbound-from-Windows design needs the phone to listen.

### 7.4 Why the companion closes the safety gap for cleanup

Only PhotoKit enumerates an asset's **complete** resource set. A companion can send every required resource of an
asset with its SHA-256, the Windows archive confirms durable verified storage of all of them, and only then can the
companion offer deletion of that asset through the system-confirmed PhotoKit request. That is the "conclusively safe"
evidence the safety rules require; DCIM file transfer cannot provide it.

## 8. Implications of having no Mac

| Dimension | Finding |
|---|---|
| Product capability | Unaffected: PhotoKit provides every `REQUIRES_COMPANION` row through documented APIs. No Mac does **not** make the companion architecturally invalid. |
| Development / build | iOS apps are built with Xcode, whose system requirements list only macOS hosts (current Xcode 27.2 beta: macOS Tahoe 26.6 or later). .NET for iOS also needs a Mac build host ("Pair to Mac"); Microsoft documents that **Hot Restart is not supported in Visual Studio 2026** (VS 2022 only, debug builds only, paid membership required). Swift Playground (iPad or Mac) can build SwiftUI apps with *Photo Library* and *Local Network* capabilities and upload them to App Store Connect, but without a Mac it needs an iPad, its documentation covers app projects rather than app extensions (such as the Photos background-upload extension), and its fitness for this app is unverified. Hosted macOS (cloud Mac rental or macOS CI runners) is possible; Xcode Cloud requires program membership and is set up from Xcode. |
| Testing | A physical iPhone is required (the background upload extension is unavailable in Simulator; Developer Mode testing requires running from Xcode with development signing). The free Personal Team allows on-device testing only from Xcode, with profiles expiring after 7 days, up to 3 devices and 3 apps per device. |
| Deployment | Apple Developer Program: 99 USD per membership year (TestFlight, App Store, App Store Connect). App Review applies (Photos and local-network purpose strings, privacy manifest). |
| Present-day feasibility | **Today the team cannot build, sign, install or debug a companion.** Minimum to start: access to a Mac running a supported macOS (owned, borrowed or rented), Apple Developer Program enrollment for anything beyond 7-day personal builds, and a test iPhone. Hence the companion is a later milestone, not part of V1. |

## 9. Proof of concept (spike-only)

`spikes/M000.AppleDevice/src/M000.AppleDevice.Probe` (.NET 10, `net10.0-windows10.0.19041.0`):

- **WPD layer** (`Wpd/`): CsWin32-generated declarations from Microsoft's Win32 metadata (no hand-written vtables);
  devices opened with `WPD_CLIENT_DESIRED_ACCESS = GENERIC_READ` except in the two guarded experiments.
- **Commands** (`Probes/`): `env`, `devices [--watch]`, `inspect` (supported commands incl. delete/create,
  `WPD_STORAGE_ACCESS_CAPABILITY`, events, firmware), `enumerate` (batched, cancellable, every property per object,
  timing, property-presence table, DCIM naming analysis), `thumbs`, `copy` (staging → size check → SHA-256 →
  signature check → optional re-read → atomic promote; partials removed on failure), `identity-snapshot` /
  `identity-compare`, `watch` (WPD events), `wmi-sources` / `wmi-find [--import N]`, `report` (all read-only
  experiments), and the guarded `delete-experiment` / `write-experiment`.
- **Analysis** (`Analysis/`): DCIM name grouping (Live Photo / edit candidates, completeness red flags), container
  sniffing (detects converted bytes), presence-only EXIF/QuickTime scan (GPS, DateTimeOriginal, Apple MakerNote,
  QuickTime location, Live Photo content identifier), identifier-stability comparison.
- **Privacy**: output outside the repository; sanitized reports; media-writing commands refuse to run inside a git
  working tree.
- **Tests**: 75 unit tests for the device-independent logic (`dotnet test` on the spike solution).

Executed here: build, tests, `env`, `devices`, `wmi-sources`, `report`, `inspect` (no device → exit 3 with guidance).

## 10. Local experiments still required

Exact steps are in [`spikes/M000.AppleDevice/README.md`](../../spikes/M000.AppleDevice/README.md). Build once:

```powershell
dotnet build spikes/M000.AppleDevice/M000.AppleDevice.sln -c Release
$probe = "spikes/M000.AppleDevice/src/M000.AppleDevice.Probe/bin/Release/net10.0-windows10.0.19041.0/apple-device-probe.exe"
```

| ID | Command / action | Resolves rows | Open question |
|---|---|---|---|
| E0 | `scripts/Collect-Environment.ps1`; `& $probe env` | §3 | Driver bound to each Apple USB interface; Apple Devices version. |
| E1 | `& $probe devices --watch 180` + plug locked → unlock → Trust → lock → unplug | 1, 2 | Is DCIM visible while a trusted phone is locked? Detection latency. |
| E2 | `& $probe inspect` | 28, 29 | Does the driver advertise delete / create-with-data? `WPD_STORAGE_ACCESS_CAPABILITY`? iOS version reported? |
| E3 | `& $probe report` | 3–13, 18, 25–27 | Which properties exist; enumeration latency; thumbnails; verified copies. |
| E4 | `& $probe copy --count 12` with *Keep Originals*, then with *Automatic* | 14–18 | Byte-exact originals with Keep Originals? What changes under Automatic (names, formats, sizes, errors)? |
| E5 | `identity-snapshot` → replug → `identity-snapshot` → `identity-compare`; repeat after restart and after add/delete | 11 | Are PUIDs stable and never reused? |
| E6 | Take a Live Photo, edit a photo and a video, `& $probe copy --count 30` | 7, 19–24, 27 | Are motion files, `IMG_E*`, `.AAE` exposed; do still and motion share a content identifier? |
| E7 | iCloud Photos + Optimize Storage: compare Photos count vs enumeration; `copy --object` a cloud-only item | 3, 31, 32 | **Safety-critical:** are non-resident items absent, or exposed as derivatives? |
| E8 | Hide one throwaway photo, delete another, re-enumerate | 3 | Hidden / Recently Deleted / Shared Library visibility. |
| E9 | `& $probe watch --seconds 120` (take photo, delete photo, lock, unplug) | 30, 33 | Which WPD events arrive? |
| E10 | `& $probe copy --count 60` and unplug mid-run | 33 | HRESULTs; no partial or promoted files. |
| E11 | `& $probe wmi-find --import 5` | 21 | Windows' sibling/sidecar grouping for Live Photos and edits. |
| E12 | `& $probe delete-experiment --i-understand-this-deletes-from-my-iphone` (throwaway photo only) | 28 | Does a PTP delete succeed? Recently Deleted? Whole asset or one resource? iCloud propagation? Prompt on the phone? |
| E13 | `& $probe write-experiment --i-understand-this-may-add-a-test-image-to-my-iphone` | 29 | Is object creation refused? |

Results that would **change the recommendation**:

- E4 shows that originals are not byte-exact even with *Keep Originals* (or HEIC/HEVC cannot be obtained), or E6
  shows that Live Photo motion files or edited originals are not exposed → Windows cannot meet V1 preservation rules →
  `COMPANION_REQUIRED_FOR_CORE_WORKFLOW`.
- E7 shows that, with Optimize Storage, PTP exposes space-saving derivatives indistinguishable from originals → the
  Windows connector must refuse Optimize-Storage libraries; if that prerequisite is unacceptable for target users →
  `COMPANION_REQUIRED_FOR_CORE_WORKFLOW`.
- E12 showing a clean asset-level PTP delete would **not** by itself change the token: the semantics remain undocumented
  and an ADR would have to accept that risk explicitly.

## 11. Failed approaches, blocked items, and their meaning

| Item | Outcome | Meaning |
|---|---|---|
| Real-device execution | Blocked: no iPhone attached to the execution environment | Environment limitation, **not** a capability finding. |
| WPD format GUIDs for HEIF/HEIC | No such constants in Win32 metadata (`WPD_OBJECT_FORMAT_HEIF/HEIC` not found) | The probe decodes the raw PTP format code from the GUID instead and reports it. |
| Hot Restart for a Mac-less companion | Not supported in Visual Studio 2026 (Microsoft) | Not a viable no-Mac route. |
| Apple Devices / iCloud for Windows as connectors | No public API (Apple Devices); cloud mechanism (iCloud for Windows) | Not usable as a V1 device connector. |
| Reverse-engineered device protocols | Rejected by policy | Not evaluated beyond identifying them as unsuitable. |

## 12. Recommended V1 connector strategy

1. Build `PhotoArchive.Device.Apple` V1 as a **read-only WPD/PTP import connector**: request `GENERIC_READ` access
   only and ship no delete/write code paths (driver-side enforcement of read-only sessions is not yet verified).
2. Prerequisite UX: Apple Devices installed; unlock + Trust; strongly recommend *Keep Originals*; explain that items
   stored only in iCloud are not visible over USB.
3. Import through staging: size check (aware of the `0xFFFFFFFF` sentinel), SHA-256, container signature vs. name,
   commit, manifest, catalog, verified state (ADR-0005). Record a suspected conversion in provenance and never label
   such bytes as the original.
4. Group DCIM resources into assets heuristically (DCIM number; Live Photo content identifier from bytes); store
   `IMG_E*` as an edited representation and `.AAE` as adjustment data; keep grouping provenance and confidence.
5. Device status: show **Archived** only when verified by content in the current session or via a durable observation
   whose identifier proved stable (E5); otherwise *Unknown*. No metadata-only "Archived".
6. **No Remove-from-Device and no Archive-to-Device UI** in the Windows connector (unsupported actions must not be fake UI).
7. Favorites and albums are archive-level metadata in V1; device-sourced favorites/albums are deferred.

Deferred to a future iOS PhotoKit companion: Photos-library cleanup with complete-resource verification; restore /
write-back; favorites, albums, hidden, captions, keywords, ratings; Photos asset identity; persistent change tracking;
iCloud-only originals; resource-typed edits and adjustment data; background backup (iOS 26.1+).

## 13. Architectural concerns for the M000 integration agent

1. **Connector capability model (ADR candidate):** `Device.Abstractions` needs explicit capability flags (enumerate,
   read originals, thumbnails, delete, write-back, asset identity, favorites/albums, edit state, persistent changes,
   original-residency awareness) so the UI shows only real actions.
2. **Device descriptors are object-level for PTP.** ADR-0001 holds, but asset grouping from a PTP connector is
   heuristic; descriptors must carry grouping provenance/confidence.
3. **"Archived" on the device view without stable IDs (ADR candidate):** define when the device view may say Archived
   (content-verified) versus Unknown; metadata similarity must not drive it, because users act on that label.
4. **iCloud semantics:** with iCloud Photos on, removing an asset from the phone deletes it from iCloud and every
   device. Any future cleanup feature must present it as such.
5. **Integrity provenance:** the manifest should record how bytes were obtained (connector, transfer setting evidence,
   signature check) so converted media is never mistaken for originals.
6. **Large files:** PTP/MTP ObjectInfo sizes are 32-bit (larger objects report `0xFFFFFFFF`); streaming multi-GB videos; staging free-space checks.
7. **Threading/COM:** keep WPD objects on a dedicated MTA worker (`PortableDeviceFTM`), stream on background threads,
   propagate cancellation, handle device-removal errors as normal outcomes.
8. **Packaging:** WPD access from a packaged (MSIX) full-trust WinUI 3 app was not tested here; verify before choosing
   packaging.
9. **CI:** two other M000 branches add a root `PhotoArchive.sln`, and the workflow builds only the first `*.sln` it
   finds (`Select-Object -First 1`). After merges, spikes can silently stop being built/tested; build all solutions or
   one root solution that includes the spikes.
10. **Companion decision:** a companion needs new ADRs (companion architecture, local-network protocol, pairing and
    authentication) and an organizational decision on Mac access and Apple Developer Program enrollment.
11. **Dependencies:** `Microsoft.Windows.CsWin32` (MIT, build-time source generator) and xUnit (test-only); no runtime
    redistribution impact.

## 14. Decision

- Not `WINDOWS_CONNECTOR_V1_VIABLE`: the Windows-only path cannot provide documented, conclusively safe Photos-library
  deletion (nor favorites, albums, asset identity or iCloud-only originals), and Remove from Device is part of the
  intended device workflow.
- Not `CORE_WORKFLOW_NOT_VIABLE_WITH_CURRENT_CONSTRAINTS`: documented import exists on Windows, and the companion path
  is realistic once Mac access and program membership are arranged.
- Not `COMPANION_REQUIRED_FOR_CORE_WORKFLOW` on current evidence: import, archive status and the gallery are the
  core; cleanup is optional, separate and guarded by design, and Apple documents USB import of originals under
  conditions the product can require and partly verify. §10 lists the local results that would move the decision
  there.
- Windows can enumerate and import (provisionally, pending E3/E4/E6/E7) but cannot safely provide cleanup or
  write-back of the Photos library.

WINDOWS_IMPORT_ONLY_VIABLE
