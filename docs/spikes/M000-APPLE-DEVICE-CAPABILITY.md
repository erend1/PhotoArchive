# M000 Spike A — Apple Device Capability on Windows

| | |
|---|---|
| Issue | [#3 — [M000] Apple device capability spike](https://github.com/erend1/PhotoArchive/issues/3) |
| Branch / base | `issue/3-apple-device-capability`, based on `main` @ `b92603c` |
| Probe | [`spikes/M000.AppleDevice/`](../../spikes/M000.AppleDevice/README.md) (spike-only) |
| Evidence | [No-device run](../../spikes/M000.AppleDevice/evidence/2026-10-01-dev-machine-no-iphone.md) · [Real iPhone, iOS 27.0, iCloud Photos + Optimize Storage](../../spikes/M000.AppleDevice/evidence/2026-10-02-iphone-ios27-icloud-optimize.md) |
| Report date | 2026-10-02 (revised after real-device sessions on 2026-10-01 and 2026-10-02) |
| Decision token | at the end of this document (§14) |

## 1. Summary

**Primary question:** can the intended device workflow be implemented acceptably from Windows alone, or is a native
iOS PhotoKit companion required?

**Answer:** Windows alone can **import** what the iPhone exposes over USB, under preconditions that Windows cannot
verify. It cannot do device cleanup, write-back, or anything that needs the Photos library itself. Those require a
PhotoKit companion, which cannot be built today because there is no Mac.

What was executed on a real iPhone (iOS 27.0, iCloud Photos **on**, Optimize iPhone Storage **on**; ~4,500 items in
Photos), through the documented **Windows Portable Devices (WPD)** API over Windows' in-box PTP class driver:

- **Detection, trust, thumbnails and enumeration work.** Detection worked without the Apple Devices app installed.
  Thumbnails: 20/20 device thumbnails, median 25 ms. Enumeration: 42 objects in 0.03 s.
- **Original bytes are delivered byte-exact and repeatably *only* with *Transfer to Mac or PC = Keep Originals*.**
  With *Automatic* (the setting the phone had), the same captures arrive as converted JPEGs. Their names, sizes,
  format codes and even leading bytes look like genuine JPEGs, so Windows cannot detect the substitution.
- **Live Photos and edits are exposed as files:** the still `.HEIC` plus `.MOV` (pairing provable from a shared content
  identifier in the bytes), and an `IMG_E` edited render plus a `.AAE` adjustment sidecar. The original stayed
  byte-identical after editing.
- **Only items resident on the phone are visible:** 14–26 files over USB versus ~4,500 in Photos, and the visible set
  changed between sessions. iCloud-only items are simply absent, and Windows cannot even count them.
- **No stable identifiers:** WPD "persistent" IDs changed for every item on every reconnect. Content SHA-256 is stable.
- **No deletion or write-back:** the phone declares its storage *read-only without object deletion*, object creation
  is not offered, and Windows.Media.Import reports the storage as ReadOnly.
- **Fragile sessions:**
  - A phone connected while locked shows empty storage until it is unlocked and trusted.
  - A new capture triggers a device reset, and the view stays empty until a replug.
  - Changing settings mid-session makes reads fail, which is safe because failed reads are never promoted.

**Recommendation:** Windows-only **import** is viable for V1 as a read-only connector with explicit, user-confirmed
prerequisites. Cleanup and write-back are deferred to a future PhotoKit companion. Users whose originals live in
iCloud (Optimize Storage) cannot archive their library through the Windows device connector. The final token is in
§14.

## 2. Scope, constraints and method

- Only documented, supported mechanisms are eligible for the recommended architecture. Researched and **rejected**
  as unsuitable (never used by the probe): libimobiledevice / usbmuxd / lockdownd / AFC (reverse-engineered Apple
  protocols), Apple's private `MobileDevice` / `iTunesMobileDevice` libraries shipped with Apple software, parsing
  iTunes / Apple Devices backups (undocumented format; also excludes iCloud Photos content), jailbreak tooling, and
  private iCloud web endpoints.
- No Mac is available. On 2026-10-01 and 2026-10-02 the owner connected a real iPhone to the development PC. All device
  operations were read-only; the guarded delete/write experiments were not run.
- Method: (1) official Apple and Microsoft documentation, quoted with links in §5; (2) a reproducible Windows probe
  (§9) executed with and without a device; (3) further local experiments where gaps remain (§10).

### Vocabulary

| Status | Meaning |
|---|---|
| `SUPPORTED` | The Windows-only path provides the capability adequately for the product's purpose. |
| `LIMITED` | Available with material limitations, preconditions or heuristics (stated in the row). |
| `UNSUPPORTED` | Neither the Windows path nor a companion provides it (not used below: every gap has a documented PhotoKit equivalent). |
| `REQUIRES_COMPANION` | Not available from Windows through documented mechanisms; documented PhotoKit API provides it. |

| Evidence | Meaning |
|---|---|
| `EXECUTED` | Observed by running the probe against a real iPhone (or, for Windows-only facts, on the PC). |
| `LOCAL_EXECUTION_REQUIRED` | Not yet observed; the committed harness measures it when run on a PC with a trusted iPhone. |
| `OFFICIAL_DOCUMENTATION` | Stated in Apple or Microsoft documentation (§5) but not observed in this spike. |
| `UNKNOWN` | Neither documented nor measurable with the current harness. |

All `EXECUTED` device results come from **one iPhone configuration** (iOS 27.0, iCloud Photos on, Optimize Storage
on). In the requested five-category view:

1. Proven by real-device execution: every `EXECUTED` row in §4.
2. Proven by a local harness the user can run: the same harness is committed. Remaining runs are listed in §10.
3. Supported by official documentation but not yet verified: rows 6 and 12.
4. Unknown or blocked: §10 open items, chiefly an iCloud-off library, hidden/Shared Library items, and standalone
   videos.
5. Requires an iOS companion / PhotoKit: every `REQUIRES_COMPANION` row.

## 3. Test environment (M000 required fields)

| Field | Value |
|---|---|
| Windows version/build | Windows 11 Pro 25H2, build 26200.8246, x64 |
| .NET | SDK 10.0.401, runtime 10.0.12 |
| WPD components | PortableDeviceApi.dll 10.0.26100.8875; PortableDeviceTypes.dll 10.0.26100.5074; wpdmtp.dll 10.0.26100.8115 |
| Installed Apple Windows components | **No Apple Devices / iTunes / iCloud Store package**; no Apple Mobile Device service; `appleusb.inf` (Apple, Inc. 538.0.0.0) in the driver store; iCloud Outlook add-in 15.0.0.215 |
| Driver binding with the iPhone attached | USB composite device → Apple `appleusb.inf` (service `usbccgp`); PTP interface `MI_00` → Microsoft in-box `wpdmtp.inf` (`WUDFWpdMtp`) |
| iPhone model | Not reported over WPD ("Apple iPhone", 64 GB storage) |
| iOS version | **27.0** (`WPD_DEVICE_FIRMWARE_VERSION`) |
| iCloud Photos | **On** |
| Optimize iPhone Storage | **On** (~4,500 items in Photos; most originals in iCloud) |
| Camera > Formats | High Efficiency |
| Transfer to Mac or PC | Automatic (initial), Keep Originals (from E4) |
| Connection method | USB cable, replugged several times |

## 4. Windows-side capability matrix

Mechanism key: **WPD** = Windows Portable Devices COM API over the in-box MTP/PTP class driver; **WMI** =
`Windows.Media.Import`. Experiment IDs (E#) refer to §10 and the probe README; detailed results are in the
[device evidence file](../../spikes/M000.AppleDevice/evidence/2026-10-02-iphone-ios27-icloud-optimize.md).

| # | Capability | Status | Evidence | Windows mechanism / exact method | Exp. | Observed / notes |
|---|---|---|---|---|---|---|
| 1 | Device detection | `SUPPORTED` | `EXECUTED` | WPD `IPortableDeviceManager::GetDevices`; WMI `PhotoImportManager.FindAllSourcesAsync` | E1 | One device, "Apple Inc." / "Apple iPhone". Worked without the Apple Devices app; Apple still documents Apple Devices as the PC prerequisite. |
| 2 | Trust / pairing | `SUPPORTED` | `EXECUTED` | None (user-mediated): unlock, tap **Trust** | E1 | Connected while locked → storage visible but **empty** (WPD and WMI: 0 items). On unlock the Trust prompt appeared; after Trust the content appeared on the same connection. Locking *after* connecting did not interrupt reads. |
| 3 | Photo/video enumeration | `LIMITED` | `EXECUTED` | WPD `IPortableDeviceContent::EnumObjects`; WMI `FindItemsAsync` | E3, E7 | Lists **file objects of items resident on the phone only**: 14–26 files vs ~4,500 in Photos, set changed between sessions. Layout: month folders `YYYYMM_a` under "Internal Storage". Hidden / Recently Deleted / Shared Library untested (E8). |
| 4 | Lightweight metadata without reading originals | `LIMITED` | `EXECUTED` | WPD `IPortableDeviceProperties::GetValues` | E3 | Exposed per object: original file name, size, created and modified date (always equal, 1-s precision), format code, content type, parent, PUID. **Not** exposed: `WPD_OBJECT_NAME`, width/height, duration, `CAN_DELETE`, location, favorites, albums, edit state. |
| 5 | Progressive enumeration | `SUPPORTED` | `EXECUTED` | WPD `IEnumPortableDeviceObjectIDs::Next` in batches | E3 | 42 objects in 0.03 s, first object after 7 ms. A large resident library (iCloud off) was not available to measure. WMI returns items only after completion. |
| 6 | Cancellable enumeration | `SUPPORTED` | `OFFICIAL_DOCUMENTATION` | Stop between `Next` batches / `Cancel`; WMI `IAsyncOperationWithProgress` cancellation | E3 | Not exercised: enumerations finished in milliseconds. |
| 7 | Capture date | `LIMITED` | `EXECUTED` | `WPD_OBJECT_DATE_CREATED`; EXIF `DateTimeOriginal` after transfer | E3, E6 | WPD dates at 1-s precision; EXIF `DateTimeOriginal` present in originals and in converted JPEGs. Library-level date edits are not exposed. |
| 8 | Dimensions | `LIMITED` | `EXECUTED` | Parse bytes after transfer | E3 | `WPD_MEDIA_WIDTH/HEIGHT` not exposed (0 of 18–21 images). |
| 9 | Duration | `LIMITED` | `EXECUTED` | Parse the MOV after transfer | E6 | No media properties on the `.MOV` (content type UNSPECIFIED). No standalone videos were resident. |
| 10 | Filename / filename-like metadata | `LIMITED` | `EXECUTED` | `WPD_OBJECT_ORIGINAL_FILE_NAME`; WMI `PhotoImportItem.Name` | E3, E4 | DCIM names only (`WPD_OBJECT_NAME` absent). **Name and extension depend on the transfer setting:** the same capture is `IMG_n.JPG` under Automatic and `IMG_n.HEIC` under Keep Originals. Not identity. |
| 11 | Stable identifier — device object (same-device rematch) | `REQUIRES_COMPANION` | `EXECUTED` | `WPD_OBJECT_PERSISTENT_UNIQUE_ID` | E5 | GUID-shaped PUIDs: stable within a connection (16/16) but **changed for 14/14 and 10/10 items across reconnects**; WPD's "must be stored across sessions" contract is not honoured. Stable source identity needs PhotoKit. |
| 12 | Stable identifier — Photos asset | `REQUIRES_COMPANION` | `OFFICIAL_DOCUMENTATION` | None on Windows | — | PhotoKit `localIdentifier`; `cloudIdentifierMappings(forLocalIdentifiers:)` (iOS 15+). |
| 13 | Thumbnails on demand | `SUPPORTED` | `EXECUTED` | WPD `GetStream(WPD_RESOURCE_THUMBNAIL)`; WMI `PhotoImportItem.Thumbnail` | E3, E11 | 20/20 JPEG thumbnails (160×120, 74×160 for screenshots), median 25 ms, max 61 ms; WMI 5/5. |
| 14 | Original photo bytes | `LIMITED` | `EXECUTED` | WPD `GetStream(WPD_RESOURCE_DEFAULT)` → staging → size → SHA-256 → signature → re-read | E4 | **Keep Originals:** byte-exact HEIC, identical SHA-256 on re-read and across reconnects. **Automatic:** converted JPEG (about 2× larger) whose name, size, EXIF format code and `FFD8FFE0` header match genuine JPEGs, so Windows **cannot detect it**. Resident items only. |
| 15 | Original video bytes | `LIMITED` | `EXECUTED` | Same as #14 | E6 | Live Photo `.MOV` read byte-exact (4.4 MB, ~25 MB/s). Standalone HEVC videos, H.264 conversion under Automatic, and ≥ 4 GiB files (PTP 32-bit size field) untested. |
| 16 | HEIC originals | `LIMITED` | `EXECUTED` | Same as #14 | E4 | Only with Keep Originals. HEIC objects are reported with the EXIF/JPEG format code `0x3801`; only the extension and the bytes reveal HEIC. |
| 17 | MOV originals | `LIMITED` | `EXECUTED` | Same as #14 | E6 | QuickTime `0x300D` objects with content type UNSPECIFIED; standalone videos untested. |
| 18 | Size before transfer | `LIMITED` | `EXECUTED` | `WPD_OBJECT_SIZE`; WMI `PhotoImportItem.SizeInBytes` | E4 | Always equalled the bytes delivered, under both settings. It describes the delivered representation, not the original, and changes with the transfer setting. |
| 19 | Live Photo still resource | `SUPPORTED` | `EXECUTED` | DCIM `.HEIC` object | E6 | When resident. |
| 20 | Live Photo motion resource | `SUPPORTED` | `EXECUTED` | DCIM `.MOV` object with the same DCIM number | E6 | When resident. |
| 21 | Relationship between Live Photo resources | `LIMITED` | `EXECUTED` | Inferred from the DCIM number; **proven from bytes** by the shared Apple content identifier (QuickTime `com.apple.quicktime.content.identifier` / AVFoundation `quickTimeMetadataContentIdentifier`) | E6, E11 | WPD exposes no relationship. WMI groups the `.MOV` as the item and the `.HEIC` as its *sidecar*. |
| 22 | Edited / current representation discovery | `LIMITED` | `EXECUTED` | DCIM `IMG_E*` object | E6 | Appeared after crop + filter; the `IMG_E` convention is not Apple-documented. |
| 23 | Original + edited representation together | `LIMITED` | `EXECUTED` | Both DCIM objects | E6 | Both exposed; the original stayed byte-identical after the edit (same SHA-256). |
| 24 | Adjustment / edit metadata | `LIMITED` | `EXECUTED` | `.AAE` sidecar | E6 | 1,072-byte XML plist; keys `adjustmentBaseVersion`, `adjustmentData`, `adjustmentEditorBundleID`, `adjustmentFormatIdentifier` (= `com.apple.photo`), `adjustmentFormatVersion`, `adjustmentRenderTypes`, `adjustmentTimestamp`. `adjustmentData` is opaque. |
| 25 | Favorite metadata | `REQUIRES_COMPANION` | `EXECUTED` | — | E3 | No favorite property among the exposed WPD properties. PhotoKit `isFavorite`. |
| 26 | Album membership | `REQUIRES_COMPANION` | `EXECUTED` | — | E3 | Month folders only; no album structure or property. PhotoKit `PHAssetCollection`. |
| 27 | Location metadata | `LIMITED` | `EXECUTED` | Embedded EXIF GPS / QuickTime location after transfer | E4, E6 | GPS present in originals and converted JPEGs; QuickTime location in the Live Photo `.MOV`; no WPD location property; library-level location edits not exposed. |
| 28 | Delete from the iPhone Photos library | `REQUIRES_COMPANION` | `EXECUTED` | — | E2 | The phone declares `WPD_STORAGE_ACCESS_CAPABILITY = READ_ONLY_WITHOUT_OBJECT_DELETION`; WMI `SupportedAccessMode = ReadOnly`; no `CAN_DELETE` on objects (the generic driver still lists a delete command). Observed with iCloud Photos on; iCloud-off untested. PTP delete semantics are undocumented either way. |
| 29 | Write / import / restore into the Photos library | `REQUIRES_COMPANION` | `EXECUTED` | — | E2 | Create-with-data and property-set are not offered; storage read-only. The documented alternative, Apple Devices **Sync Photos**, is PC → phone only, requires iCloud Photos off, and has no API. |
| 30 | Observe library changes incrementally | `LIMITED` | `EXECUTED` | WPD events (`IPortableDevice::Advise`), then re-enumeration | E9 | **No `OBJECT_ADDED` for new captures.** A `DEVICE_RESET` arrived, and the view stayed empty until a replug. Persistent change history needs PhotoKit `fetchPersistentChanges(since:)`. |
| 31 | Behaviour with iCloud Photos enabled | `LIMITED` | `EXECUTED` | Same USB/PTP path | E7 | Resident subset only; read-only storage; deletions on a device propagate to iCloud (Apple). |
| 32 | Optimize iPhone Storage: original not resident | `REQUIRES_COMPANION` | `EXECUTED` | — | E7 | Non-resident items are **not listed at all** (under 1% of the library visible). No reduced derivatives were observed among listed items. PhotoKit can fetch originals on demand (`isNetworkAccessAllowed`). |
| 33 | Failures: stale session / disconnect | `SUPPORTED` | `EXECUTED` | COM `HRESULT`s + staging that never promotes failures | E4 | After the device's state changed mid-session, reads failed with `0x8007001E` (read fault) or `0x80042007` (consistent with PTP Incomplete_Transfer). 0 partial files were left and nothing was promoted. A physical unplug mid-transfer is untested (E10). |
| 34 | Photos-library (asset-level) access as such | `REQUIRES_COMPANION` | `EXECUTED` | — | — | The PTP view has no library semantics (rows 4, 11, 25, 26). |

### 4.1 DCIM / file-transfer access vs. Photos-library access

| Aspect | DCIM over PTP (Windows, observed) | Photos library (PhotoKit, documented) |
|---|---|---|
| Unit | File object in month folders; resident items only | `PHAsset` with typed `PHAssetResource`s, incl. iCloud items |
| Identity | Session-scoped object IDs and PUIDs | `localIdentifier`; cloud identifier mappings |
| Representation | Depends on the iPhone transfer setting (original or converted) | Original resources by type; conversion is explicit |
| Live Photos / edits | Separate files; relationships by name (plus content identifier in bytes) | Explicit resource types (`.photo`, `.pairedVideo`, `.fullSizePhoto`, `.adjustmentData`, …) |
| Library metadata | None | Favorites, albums, hidden, location, dates, … |
| Delete / create | Storage declared read-only; create not offered | Documented change requests with system alerts |
| Change tracking | Device reset → reconnect and re-enumerate | Persistent change tokens (iOS 16+) |

### 4.2 Session behaviour the connector must handle (observed)

| Situation | What Windows sees | Required handling |
|---|---|---|
| Connected while locked | Device + empty storage | Prompt to unlock and Trust; content appears on the same connection |
| Phone locks during a session | Session continues; reads succeed | None |
| Photo taken while connected | `WPD_EVENT_DEVICE_RESET`; storage empty until replug | Ask the user to replug; re-enumerate |
| Transfer setting changed while connected | Listing unchanged; reads fail (`0x8007001E`, `0x80042007`) | Treat as retryable after reconnect; never promote |
| Reconnect | All object IDs and PUIDs change | Rematch by content, never by IDs |

## 5. Official statements this report relies on

| Source | Statement used |
|---|---|
| Apple, [Transfer photos from your iPhone or iPad](https://support.apple.com/en-us/120267) (published 2026-09-14) | Import to a Windows PC: "Install the Apple Devices app from the Microsoft Store", connect by USB, "If asked, unlock…", "Trust This Computer, tap Trust or Allow", then use Microsoft Photos. "If you have iCloud Photos turned on, you must download the original, full resolution versions of your photos to your iPhone or iPad before you import to your PC." |
| Apple, [Using HEIF or HEVC media on Apple devices](https://support.apple.com/en-us/116944) (2025-12-05) | "When you import HEIF or HEVC media from an attached iPhone or iPad into Photos, Image Capture, or a PC, the media might be converted to JPEG or H.264. If you don't want it to be converted, open Settings, tap Apps, tap Photos, then scroll down and tap Keep Originals." **Confirmed by E4.** |
| Apple, [About the 'Trust This Computer' alert](https://support.apple.com/en-us/109054) (2025-12-04) | "Trusted computers can sync with your device and access your device's photos, videos, contacts, and other content. These computers remain trusted unless you change which computers you trust or erase your device." |
| Apple, [Set up and use iCloud Photos](https://support.apple.com/en-us/108782) (2026-09-14) | "When you delete photos and videos on one device, they're deleted everywhere that you use iCloud Photos … Recently Deleted folder for 30 days." With Optimize Storage, "your device keeps space-saving versions … while iCloud Photos stores … the original, high-resolution version." |
| Apple, [If your computer doesn't recognize your iPhone or iPad](https://support.apple.com/en-us/108643) (2026-08-12) | Windows: open Apple Devices, connect, trust; check cable (must support data), port, software conflicts, latest Windows/iOS. |
| Apple Devices User Guide for Windows: [Sync photos](https://support.apple.com/guide/devices-windows/sync-photos-to-your-device-mchl4af095d3/windows), [Intro to syncing](https://support.apple.com/guide/devices-windows/syncing-overview-mchl923c1147/windows) | Photo sync is PC → device; "You can't use the syncing method … unless you turn off iCloud Photos"; synced albums are removed by deselecting and re-syncing; "If you delete an automatically synced item from your Windows device, the deleted item is removed from your Apple device the next time you sync." |
| Apple, [Use Apple Devices to share files…](https://support.apple.com/en-us/120402) (2025-03-27) | File Sharing copies files between the PC and apps that support File Sharing, manually through the Apple Devices UI. |
| Apple, [Delete and recover photos and videos in iCloud for Windows](https://support.apple.com/guide/icloud-windows/delete-and-recover-photos-and-videos-icw5c1e2d1a7/icloud) | Deleting in the iCloud Photos folder deletes "from all your devices that have iCloud Photos turned on" (a cloud mechanism, not device access). |
| Microsoft, [Import photos and videos from phone to PC](https://support.microsoft.com/en-us/windows/import-photos-and-videos-from-phone-to-pc-198f2301-e9a7-c734-5f39-a8946a5ebc99) | "Your PC can't find the device if the device is locked." **Consistent with E1 for a phone connected while locked.** iPhone: "If iCloud is enabled … you may not be able to download your photos or videos if they exist on iCloud but not on your device … use iCloud for Windows." Phone Link photos require Android. |
| Microsoft Learn, [Introduction to WPD Drivers](https://learn.microsoft.com/en-us/windows-hardware/drivers/portable/wpd-drivers-overview) | WPD "implements a class driver solution for … Picture Transfer Protocol (PTP) over USB …"; "The WPD MTP driver also supports Picture Transfer Protocol (PTP) devices." |
| Microsoft Learn, [Object Properties](https://learn.microsoft.com/en-us/windows/win32/wpd_sdk/object-properties), [Retrieving an Object Id from a PUID](https://learn.microsoft.com/en-us/windows/win32/wpd_sdk/retrieving-an-object-identifier-from-a-persistent-unique-identifier) | `WPD_OBJECT_ID` "need not be stored across sessions"; `WPD_OBJECT_PERSISTENT_UNIQUE_ID` "must be stored across sessions". **Not honoured by this iPhone (E5).** |
| Microsoft Learn, [IPortableDeviceContent::Delete](https://learn.microsoft.com/en-us/windows/win32/api/portabledeviceapi/nf-portabledeviceapi-iportabledevicecontent-delete), [Storage properties](https://learn.microsoft.com/en-us/windows/win32/wpd_sdk/storage-properties) | Generic object deletion with per-object results; `WPD_STORAGE_ACCESS_CAPABILITY` "identifies any write-protection that globally affects this storage. This takes precedence over access specified on individual objects." |
| Microsoft Learn, [Windows.Media.Import](https://learn.microsoft.com/en-us/uwp/api/windows.media.import?view=winrt-26100) and [Import media from a device](https://learn.microsoft.com/en-us/windows/uwp/audio-video-camera/import-media-from-a-device) | `PhotoImportStorageMedium.SupportedAccessMode` (`ReadWrite`, `ReadOnly`, `ReadAndDelete`); `PhotoImportItem` (`Name`, `ItemKey`, `SizeInBytes`, `Date`, `Sibling`, `Sidecars`, `VideoSegments`, `Thumbnail`); find/import/delete are `IAsyncOperationWithProgress` and cancellable; `DeleteImportedItemsFromSourceAsync` deletes the items imported in that session. |
| Apple Developer, [PhotoKit](https://developer.apple.com/documentation/photokit) and the pages cited in §7 | Resource types, resource manager, change requests, access levels, cloud identifiers, persistent changes, background resource upload. |

Community reports that shaped the experiments, and what the device showed:

- `IMG_E*` renders and `.AAE` sidecars in DCIM: **confirmed**.
- DCIM deletion blocked while iCloud Photos is enabled: **consistent** with the storage declaration observed with
  iCloud Photos on.
- Copy failures `0x8007001F` attributed to *Automatic*: **not observed**. The observed failures were `0x8007001E` and
  `0x80042007`, on stale sessions.

## 6. Windows-side mechanisms: roles and limits

| Mechanism | Role | Limits relevant to PhotoArchive |
|---|---|---|
| **WPD / PTP** (in-box MTP/PTP class driver) | The documented programmatic path to the iPhone's camera-style view; what Microsoft Photos and File Explorer use. | Resident file objects only; no asset/library semantics; storage declared read-only without deletion (iCloud on); bytes depend on an iPhone setting Windows cannot read or detect; session-scoped identifiers; resets on library changes. |
| **Windows.Media.Import** | Higher-level WinRT import API over the same transport: progress, cancellation, sibling/sidecar grouping, `DeleteImportedItemsFromSourceAsync`. | Items only after the find completes; storage reported ReadOnly (so no delete); groups Live Photos inverted (the `.MOV` as the item, the still as its sidecar); same PTP limits. |
| **Microsoft Photos import** | Consumer app documented by Apple and Microsoft for iPhone import. | No API; same transport and limits. |
| **Apple Devices for Windows** | Apple's documented prerequisite for importing to a PC. Also device management, backup/restore, music/movie/TV/photo sync *to* the device, File Sharing with apps, and Wi-Fi sync after cable setup. | No public API. Photo sync is PC → phone only and only with iCloud Photos off. Not required for WPD access on the test PC, where Apple's USB driver package was already present. |
| **iCloud for Windows** | Documented cloud access to iCloud Photos (download; deletes propagate to all devices). | A cloud mechanism: outside V1 (no cloud requirement) and not a device connector. A user-exported folder could be imported as an ordinary folder source (product decision). |
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

### 7.4 What the device evidence adds to the companion case

- **Original fidelity:** PhotoKit hands out typed original resources; the PTP path delivers originals only when a
  user setting is right, and Windows cannot tell.
- **Coverage:** with Optimize Storage, under 1% of this library was visible over USB. PhotoKit can fetch any original
  on demand, per resource, without first downloading the whole library onto the phone.
- **Identity:** PhotoKit `localIdentifier`s persist; PTP identifiers changed on every reconnect.
- **Safe cleanup:** only PhotoKit enumerates an asset's **complete** resource set. A companion can send every
  required resource with its SHA-256, wait for the archive to confirm durable verified storage of all of them, and
  only then offer deletion through the system-confirmed PhotoKit request. That is the "conclusively safe" evidence
  the safety rules require; the phone's PTP storage does not even allow deletion.

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
- **Commands** (`Probes/`): `env`, `devices [--watch]`, `inspect`, `enumerate`, `thumbs`, `copy [--newest N]`
  (staging → size check → SHA-256 → signature check → re-read → atomic promote; partials removed on failure),
  `identity-snapshot` / `identity-compare`, `watch`, `wmi-sources` / `wmi-find [--import N]`, `report`, and the guarded
  `delete-experiment` / `write-experiment`.
- **Analysis** (`Analysis/`): DCIM name grouping, container sniffing, presence-only EXIF/QuickTime scan (GPS,
  DateTimeOriginal, Apple MakerNote, QuickTime location, Live Photo content identifier), identifier-stability comparison.
- **Privacy**: output outside the repository; fail-closed sanitized reports (hardened after the first device run);
  media-writing commands refuse to run inside a git working tree.
- **Tests**: 78 unit tests for the device-independent logic.

Executed against the iPhone: `devices`, `inspect`, `report` (incl. thumbnails), `enumerate`, `copy` (22 copy
attempts plus 15 verification re-reads), `identity-snapshot` (8) / `identity-compare` (6), `watch`, `wmi-sources`,
`wmi-find`.

## 10. Experiments: status and what remains

| ID | Command / action | Status | Result (one line) |
|---|---|---|---|
| E0 | `scripts/Collect-Environment.ps1`; `env` | Done | Apple USB driver + in-box `wpdmtp.inf`; no Apple Devices app. |
| E1 | `devices`, connect locked → unlock → Trust; lock mid-session | Done | Locked connect = empty storage; Trust on unlock; locking later is harmless. |
| E2 | `inspect` | Done | iOS 27.0; storage READ_ONLY_WITHOUT_OBJECT_DELETION; create not offered. |
| E3 | `report` | Done | 42 objects; property set in row 4; 20/20 thumbnails. |
| E4 | `copy` under Automatic, then Keep Originals | Done | Automatic = undetectable converted JPEG; Keep Originals = byte-exact HEIC. |
| E5 | `identity-snapshot` / `identity-compare` across reconnects | Done | PUIDs stable per connection, changed on every reconnect. |
| E6 | Live Photo + crop/filter, `copy --newest 6` | Done | Still + MOV paired by content identifier; `IMG_E` + `.AAE`; original unchanged. |
| E7 | Photos count vs USB listing (Optimize Storage) | Done | Under 1% visible; non-resident items absent. |
| E8 | Hidden / Recently Deleted / Shared Library | **Not run** | — |
| E9 | `watch` while capturing | Done | No `OBJECT_ADDED`; `DEVICE_RESET`, empty until replug. |
| E10 | Unplug during a large transfer | **Not run** | Stale-session failures observed instead (row 33). |
| E11 | `wmi-find` (`--import` not run) | Partly | 18 items; `.MOV` item with `.HEIC` sidecar; `.AAE` sidecar. |
| E12 / E13 | Guarded delete / write | **Not run** | Not needed for the decision: deletion and creation are not offered by the device. |

Still required before production work:

1. **A library with iCloud Photos off** (all originals resident):
   - enumeration and transfer performance at tens of thousands of items;
   - whether the storage then allows deletion (it might offer PTP delete; the semantics would still be undocumented).
2. **Standalone videos:** HEVC under Keep Originals, H.264 conversion under Automatic, and ≥ 4 GiB files.
3. **E8, E10, E11 import.**
4. **WPD from a packaged (MSIX) WinUI 3 app.**

Pre-registered triggers that would have moved the decision to `COMPANION_REQUIRED_FOR_CORE_WORKFLOW`:

- E4 not byte-exact with Keep Originals: **not triggered**.
- E6 Live Photo motion or edited originals missing: **not triggered**.
- E7 derivatives exposed as originals: **not triggered**. Non-resident items were absent rather than substituted.
  Coverage is the limitation: requiring originals on the phone is not feasible for this tester (5.4 GB free on a
  64 GB phone). §14 explains how that bears on the decision.

## 11. Failed approaches, blocked items, and their meaning

| Item | Outcome | Meaning |
|---|---|---|
| Unattended run on 2026-10-01 | 4/4 original reads failed (`0x8007001E`, `0x80042007`, one 12-byte payload); WMI found 0 items | **Not a capability limit:** the same files read correctly on 2026-10-02, the error codes were reproduced by changing device state mid-session, and locking was ruled out. Illustrates the session fragility in §4.2. |
| JPEG header heuristic for conversion | Converted and genuine JPEG originals both start with `FFD8FFE0` (JFIF) | No byte-level signal distinguishes converted output; only a session listing `.HEIC` objects proves Keep Originals is active. |
| WPD format GUIDs for HEIF/HEIC | No such constants in Win32 metadata; the phone reports HEIC with the EXIF code anyway | Extension + signature are the only HEIC indicators. |
| Hot Restart for a Mac-less companion | Not supported in Visual Studio 2026 (Microsoft) | Not a viable no-Mac route. |
| Apple Devices / iCloud for Windows as connectors | No public API (Apple Devices); cloud mechanism (iCloud for Windows) | Not usable as a V1 device connector. |
| Reverse-engineered device protocols | Rejected by policy | Not evaluated beyond identifying them as unsuitable. |

## 12. Recommended V1 connector strategy

1. **Read-only connector.** Build `PhotoArchive.Device.Apple` V1 on WPD/PTP: request `GENERIC_READ` only and ship no
   delete/write code paths.
2. **Originals prerequisite.** Require *Transfer to Mac or PC = Keep Originals*, confirmed by the user at
   connection. A session that lists any `.HEIC`/HEVC object positively shows that Keep Originals is active. A session
   with none is ambiguous (Automatic, or a Most Compatible camera); warn and record that in provenance. Never label
   bytes as original when the session cannot show it.
3. **Coverage honesty.** State that only photos stored on the iPhone are visible and that iCloud-only items are not.
   Windows cannot count the missing items, so it must make no completeness claims.
4. **Session handling** (§4.2):
   - empty storage → ask the user to unlock and Trust;
   - device reset → re-enumerate, and ask for a replug if the storage is still empty;
   - read faults → retry after reconnect;
   - all IDs → treat as session-scoped.
5. **Import pipeline** (ADR-0005): staging → size check → SHA-256 → signature vs. extension → commit → manifest →
   catalog → verified.
   - Group resources by DCIM number and verify Live Photo pairs by content identifier.
   - Store `IMG_E*` as an edited representation and `.AAE` as adjustment data, with grouping provenance.
6. **Device status.** Show **Archived** only when content-verified (SHA-256) in the current session; otherwise
   *Unknown*. Names and sizes change with the transfer setting, so metadata matching cannot even be trusted as a hint
   across sessions.
7. **No fake actions.** No Remove-from-Device and no Archive-to-Device UI in the Windows connector.
8. **Library metadata.** Favorites and albums are archive-level metadata in V1.

Deferred to a future iOS PhotoKit companion:

- Photos-library cleanup with complete-resource verification.
- Restore / write-back.
- Favorites, albums, hidden, captions, keywords, ratings.
- Photos asset identity.
- Persistent change tracking.
- **iCloud-only originals (Optimize Storage users).**
- Resource-typed edits and adjustment data.
- Background backup (iOS 26.1+).

## 13. Architectural concerns for the M000 integration agent

1. **Connector capability model (ADR candidate):** `Device.Abstractions` needs explicit capability flags (enumerate,
   read originals, original-fidelity guarantee, thumbnails, delete, write-back, asset identity, favorites/albums, edit
   state, persistent changes, residency/coverage) so the UI shows only real actions and honest coverage.
2. **Representation provenance (ADR candidate):** the same capture arrives as different bytes depending on a device
   setting. Manifests should record connector, session evidence (e.g. "HEIC visible in session") and signature
   checks, so a converted JPEG is never presented as the original.
3. **Duplicate representations:** importing under *Automatic* and later under *Keep Originals* yields two different
   byte sets of one capture. SHA-256 deduplication cannot merge them. Decide whether to detect this (same DCIM number +
   capture time, different format) and how to present it.
4. **"Archived" on the device view (ADR candidate):** content-verified only. Identifiers are session-scoped, and
   names/sizes depend on settings.
5. **Device descriptors are object-level for PTP.** ADR-0001 holds, but asset grouping from a PTP connector is
   heuristic (verifiable for Live Photos via content identifiers); descriptors must carry grouping provenance.
6. **iCloud users:** Windows import covers only the resident subset, and cleanup is impossible there (read-only
   storage). Removing items with iCloud Photos on deletes them from iCloud everywhere. Product decision: is
   migrating iCloud libraries a V1 goal? If so, the options are a PhotoKit companion, or importing a folder the user
   exported with Apple's own tools (not a device connector; keep it optional to respect the no-cloud-requirement rule).
7. **Session robustness:** handle resets, locked connects and stale sessions as normal states (§4.2), not errors.
8. **Large files:** PTP/MTP ObjectInfo sizes are 32-bit (larger objects report `0xFFFFFFFF`); streaming multi-GB videos; staging free-space checks.
9. **Threading/COM:** keep WPD objects on a dedicated MTA worker (`PortableDeviceFTM`), stream on background threads,
   propagate cancellation, handle device-removal errors as normal outcomes.
10. **Packaging:** WPD access from a packaged (MSIX) full-trust WinUI 3 app was not tested; verify before choosing packaging.
11. **CI:** two other M000 branches add a root `PhotoArchive.sln`, and the workflow builds only the first `*.sln` it
    finds (`Select-Object -First 1`). After merges, spikes can silently stop being built/tested; build all solutions or
    one root solution that includes the spikes.
12. **Companion decision:** a companion needs new ADRs (companion architecture, local-network protocol, pairing and
    authentication) and an organizational decision on Mac access and Apple Developer Program enrollment.
13. **Dependencies:** `Microsoft.Windows.CsWin32` (MIT, build-time source generator) and xUnit (test-only); no runtime
    redistribution impact.

## 14. Decision

Evaluated against the completed matrix:

- **Not `WINDOWS_CONNECTOR_V1_VIABLE`.** The phone declares its storage read-only without object deletion and offers
  no object creation (`EXECUTED`). PTP has no documented Photos-library semantics in any case. Remove from Device
  and write-back cannot be provided from Windows, and neither can favorites, albums, asset identity or iCloud-only
  originals.
- **Not `CORE_WORKFLOW_NOT_VIABLE_WITH_CURRENT_CONSTRAINTS`.** Windows import of resident originals works and is
  verifiable (`EXECUTED`). The companion path is realistic once Mac access and program membership exist.
- **Not `COMPANION_REQUIRED_FOR_CORE_WORKFLOW` on this evidence.** None of the pre-registered integrity triggers
  fired:
  - with Keep Originals, originals are byte-exact;
  - Live Photo motion and edit resources are exposed;
  - non-resident items are absent rather than substituted.

  The product's core (archive originals with verified integrity; gallery; trash; backup) is achievable through the
  Windows connector for users whose originals are on the phone. Cleanup is optional and separate by design.
- **The caveat that decides how far this reaches.** For users with iCloud Photos + Optimize Storage (this tester:
  under 1% visible), the Windows device connector cannot archive the library. If the product owner makes migrating
  such libraries a V1 goal, this evidence supports the companion route for that goal.
- **Conversion caveat.** With *Automatic*, Windows silently receives converted JPEGs. The connector must require Keep
  Originals and record what it can and cannot prove.

WINDOWS_IMPORT_ONLY_VIABLE
