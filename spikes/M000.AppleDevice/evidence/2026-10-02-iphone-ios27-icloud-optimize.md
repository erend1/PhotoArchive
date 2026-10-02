# Evidence: real iPhone, iOS 27.0, iCloud Photos + Optimize Storage (2026-10-01 / 2026-10-02)

**Evidence level: EXECUTED** on a real iPhone connected by USB to the development PC. All values below are the
probe's sanitized output: file-name digits are pseudonymized per run (so `IMG_#0009` in one table is not the same
item as `IMG_#0009` in another), dates are reduced to year-month, serials / PnP instance IDs / the user-assigned
device name are redacted, and no coordinates or identifiers are shown. SHA-256 prefixes are given only for the test
photos captured for this spike.

## Test conditions

| Field | Value |
|---|---|
| Windows | Windows 11 Pro 25H2, build 26200.8246, x64; .NET 10.0.12 |
| Apple software on the PC | **Apple Devices not installed** (no Apple Store package, no Apple Mobile Device service) |
| Driver binding (E0) | USB composite device: Apple `appleusb.inf` (oem25.inf, Apple, Inc. 538.0.0.0, service `usbccgp`); PTP interface `MI_00`: Microsoft in-box **`wpdmtp.inf`** (service `WUDFWpdMtp`, 10.0.26100.8115) |
| iPhone | WPD reports "Apple iPhone", firmware **27.0**, 64 GB storage; exact model not reported over WPD |
| iCloud Photos / Optimize iPhone Storage | **On / On** (owner-reported; most originals live in iCloud) |
| Photos library size | About **4,500 items** in Photos (owner-reported, Library > All Photos) |
| Camera > Formats | High Efficiency (HEIC/HEVC) |
| Transfer to Mac or PC | **Automatic** (2026-10-01 and the first part of 2026-10-02), then **Keep Originals** for E4 onward |
| Connection | USB cable; the phone was replugged several times during the session |

## Results by experiment

### E1 - detection, trust, lock behaviour

- Detection: `IPortableDeviceManager::GetDevices` lists one device, manufacturer "Apple Inc.", description "Apple iPhone".
- **Connected while locked:** the WPD device and its "Internal Storage" appear, but the storage has **0 children**;
  Windows.Media.Import finds **0 items**. No Trust prompt can be shown on a locked phone.
- **On unlock:** the Trust prompt appeared and was accepted; content then appeared **on the same connection**
  (28 objects) without replugging.
- **Locking after connecting does not interrupt the session:** with the phone locked, enumeration still returned all
  24 objects, two originals copied completely, and Windows.Media.Import still found 16 items.

### E2 - driver-declared capabilities (`inspect`)

| Fact | Value |
|---|---|
| Device firmware (iOS version as reported) | 27.0 |
| Device model | Apple iPhone |
| Device protocol | MTP: 1.00 |
| Driver advertises ..._CREATE_OBJECT_WITH_PROPERTIES_AND_DATA | no |
| Driver advertises WPD_COMMAND_OBJECT_MANAGEMENT_DELETE_OBJECTS | yes |
| Driver advertises WPD_COMMAND_OBJECT_PROPERTIES_SET | no |
| Driver advertises bulk property retrieval | yes |
| IPortableDevice::Open latency (ms) | 20 |
| Session access requested | GENERIC_READ (read-only) |
| Storage access capability (all storages) | READ_ONLY_WITHOUT_OBJECT_DELETION (1) |

Device object properties:

| Property | Value |
|---|---|
| WPD_OBJECT_ID | <id#0001 len=6 shape=AAAAAA> |
| WPD_OBJECT_CONTAINER_FUNCTIONAL_OBJECT_ID | <container_functional_object_id#0002 len=6 shape=AAAAAA> |
| WPD_OBJECT_PERSISTENT_UNIQUE_ID | <persistent_unique_id#0003 len=6 shape=AAAAAA> |
| WPD_OBJECT_PARENT_ID |  |
| WPD_OBJECT_FORMAT | WPD_OBJECT_FORMAT_PROPERTIES_ONLY [PTP 0x3001] |
| WPD_OBJECT_CONTENT_TYPE | WPD_CONTENT_TYPE_FUNCTIONAL_OBJECT |
| WPD_FUNCTIONAL_OBJECT_CATEGORY | WPD_FUNCTIONAL_CATEGORY_DEVICE |
| WPD_OBJECT_CAN_DELETE | no |
| WPD_DEVICE_FIRMWARE_VERSION | 27.0 |
| WPD_DEVICE_PROTOCOL | MTP: 1.00 |
| WPD_DEVICE_MANUFACTURER | Apple Inc. |
| WPD_DEVICE_MODEL | Apple iPhone |
| WPD_OBJECT_NAME | Apple iPhone |
| WPD_DEVICE_SUPPORTS_NON_CONSUMABLE | no |
| WPD_DEVICE_SERIAL_NUMBER | <serial redacted len=12> |
| WPD_DEVICE_TYPE | 0 |
| WPD_DEVICE_TRANSPORT | 1 |
| WPD_DEVICE_POWER_SOURCE | 0 |
| {463DD662-7FC4-4291-911C-7F4C9CCA9799},3 | (byte vector, 16 bytes) |
| {4D545058-8900-40B3-8F1D-DC246E1E8370},55815 | 0x8004200A |
| WPD_DEVICE_FRIENDLY_NAME | <user-assigned device name redacted> |

Functional category, content types and formats:

Delete command options: `WPD_OPTION_OBJECT_MANAGEMENT_RECURSIVE_DELETE_SUPPORTED=yes`
- Functional category `WPD_FUNCTIONAL_CATEGORY_STORAGE`: objects=1, content types: `WPD_CONTENT_TYPE_IMAGE`, `WPD_CONTENT_TYPE_AUDIO`, `WPD_CONTENT_TYPE_VIDEO`, `WPD_CONTENT_TYPE_UNSPECIFIED`, `WPD_CONTENT_TYPE_FOLDER`
  - `WPD_CONTENT_TYPE_IMAGE` formats: `WPD_OBJECT_FORMAT_EXIF [PTP 0x3801]`, `WPD_OBJECT_FORMAT_PNG [PTP 0x380B]`, `WPD_OBJECT_FORMAT_TIFF [PTP 0x380D]`, `PTP/MTP format 0xB401 (vendor/unknown)`, `PTP/MTP format 0xB402 (vendor/unknown)`
  - `WPD_CONTENT_TYPE_AUDIO` formats: `PTP/MTP format 0x3007 (vendor/unknown)`, `PTP/MTP format 0x3008 (WAV)`, `PTP/MTP format 0x3009 (MP3)`
  - `WPD_CONTENT_TYPE_VIDEO` formats: `WPD_OBJECT_FORMAT_AVI [PTP 0x300A]`, `WPD_OBJECT_FORMAT_MPEG [PTP 0x300B]`, `PTP/MTP format 0x300C (ASF)`, `PTP/MTP format 0xB421 (vendor/unknown)`
  - `WPD_CONTENT_TYPE_UNSPECIFIED` formats: `WPD_OBJECT_FORMAT_UNSPECIFIED [PTP 0x3000]`, `WPD_OBJECT_FORMAT_SCRIPT [PTP 0x3002]`, `PTP/MTP format 0x300D (QuickTime)`
  - `WPD_CONTENT_TYPE_FOLDER` formats: `WPD_OBJECT_FORMAT_PROPERTIES_ONLY [PTP 0x3001]`

Storage object (note `WPD_STORAGE_ACCESS_CAPABILITY = 1`, READ_ONLY_WITHOUT_OBJECT_DELETION):

| Property | Value |
|---|---|
| WPD_STORAGE_TYPE | 3 |
| WPD_STORAGE_FILE_SYSTEM_TYPE | DCF |
| WPD_STORAGE_ACCESS_CAPABILITY | 1 |
| WPD_STORAGE_CAPACITY | 64000000000 |
| WPD_STORAGE_FREE_SPACE_IN_BYTES | 5449334784 |
| WPD_STORAGE_FREE_SPACE_IN_OBJECTS | 3632 |
| WPD_STORAGE_DESCRIPTION | Internal Storage |
| WPD_OBJECT_PERSISTENT_UNIQUE_ID | <persistent_unique_id#0005 len=40 shape=AAA-{99999,Aaaaaaaa Aaaaaaa,99999999999}> |
| WPD_OBJECT_NAME | Internal Storage |
| WPD_STORAGE_SERIAL_NUMBER | <serial redacted len=16> |
| WPD_OBJECT_ID | <id#0006 len=6 shape=a99999> |
| WPD_OBJECT_PARENT_ID | <parent_id#0007 len=6 shape=AAAAAA> |
| WPD_OBJECT_FORMAT | WPD_OBJECT_FORMAT_PROPERTIES_ONLY [PTP 0x3001] |
| WPD_OBJECT_CAN_DELETE | no |
| WPD_OBJECT_CONTENT_TYPE | WPD_CONTENT_TYPE_FUNCTIONAL_OBJECT |
| WPD_FUNCTIONAL_OBJECT_CATEGORY | WPD_FUNCTIONAL_CATEGORY_STORAGE |
| WPD_OBJECT_CONTAINER_FUNCTIONAL_OBJECT_ID | <container_functional_object_id#0008 len=6 shape=a99999> |

### E3 - enumeration, thumbnails, first copy attempt (2026-10-01, Automatic, unattended run)

| Fact | Value |
|---|---|
| Containers (functional objects/folders) | 16 |
| Content type = WPD_CONTENT_TYPE_IMAGE | 21 |
| Content type = WPD_CONTENT_TYPE_VIDEO | 0 |
| Images with WPD_MEDIA_WIDTH/HEIGHT | 0 |
| Media objects where DATE_CREATED == DATE_MODIFIED | 26 |
| Media objects with DATE_CREATED having sub-second precision | 0 |
| Media-like objects | 26 |
| Objects enumerated | 42 |
| Objects where PUID == OBJECT_ID | 0 |
| Objects with PERSISTENT_UNIQUE_ID | 42 |
| Other non-container objects | 5 |
| Time to first media object (ms) | 13 |
| Time to first object (ms) | 7 |
| Total enumeration time (s) | 0.03 |
| WPD_OBJECT_CAN_DELETE absent (media) | 26 |

| Fact | Value |
|---|---|
| API | IPortableDeviceResources::GetSupportedResources / GetResourceAttributes / GetStream(WPD_RESOURCE_THUMBNAIL) |
| Max thumbnail latency (ms) | 61 |
| Median thumbnail latency (ms) | 25 |
| Objects sampled | 20 |
| Thumbnails retrieved | 20 |

| Object (sanitized) | Ext | Supported resources | Thumb | Bytes | Format | Pixels | ms |
|---|---|---|---|---|---|---|---|
| IMG_#0002.DNG | DNG | WPD_RESOURCE_DEFAULT WPD_RESOURCE_THUMBNAIL | yes | 10759 | Jpeg (declared WPD_OBJECT_FORMAT_JFIF [PTP 0x3808]) | 160x120 | 61 |
| IMG_#0003.JPG | JPG | WPD_RESOURCE_DEFAULT WPD_RESOURCE_THUMBNAIL | yes | 7598 | Jpeg (declared WPD_OBJECT_FORMAT_JFIF [PTP 0x3808]) | 160x120 | 31 |
| IMG_#0004.PNG | PNG | WPD_RESOURCE_DEFAULT WPD_RESOURCE_THUMBNAIL | yes | 4514 | Jpeg (declared WPD_OBJECT_FORMAT_JFIF [PTP 0x3808]) | 74x160 | 31 |
| OHOO#0005.JPG | JPG | WPD_RESOURCE_DEFAULT WPD_RESOURCE_THUMBNAIL | yes | 9521 | Jpeg (declared WPD_OBJECT_FORMAT_JFIF [PTP 0x3808]) | 124x160 | 26 |
| IMG_#0001.JPG | JPG | WPD_RESOURCE_DEFAULT WPD_RESOURCE_THUMBNAIL | yes | 8811 | Jpeg (declared WPD_OBJECT_FORMAT_JFIF [PTP 0x3808]) | 160x120 | 27 |
| IMG_#0006.JPG | JPG | WPD_RESOURCE_DEFAULT WPD_RESOURCE_THUMBNAIL | yes | 10160 | Jpeg (declared WPD_OBJECT_FORMAT_JFIF [PTP 0x3808]) | 160x120 | 28 |
| IMG_#0007.JPG | JPG | WPD_RESOURCE_DEFAULT WPD_RESOURCE_THUMBNAIL | yes | 5712 | Jpeg (declared WPD_OBJECT_FORMAT_JFIF [PTP 0x3808]) | 160x120 | 25 |
| IMG_#0008.JPG | JPG | WPD_RESOURCE_DEFAULT WPD_RESOURCE_THUMBNAIL | yes | 5738 | Jpeg (declared WPD_OBJECT_FORMAT_JFIF [PTP 0x3808]) | 160x120 | 25 |
| IMG_#0009.JPG | JPG | WPD_RESOURCE_DEFAULT WPD_RESOURCE_THUMBNAIL | yes | 6685 | Jpeg (declared WPD_OBJECT_FORMAT_JFIF [PTP 0x3808]) | 160x120 | 23 |
| IMG_#0010.JPG | JPG | WPD_RESOURCE_DEFAULT WPD_RESOURCE_THUMBNAIL | yes | 9227 | Jpeg (declared WPD_OBJECT_FORMAT_JFIF [PTP 0x3808]) | 160x120 | 25 |
| IMG_#0011.DNG | DNG | WPD_RESOURCE_DEFAULT WPD_RESOURCE_THUMBNAIL | yes | 8680 | Jpeg (declared WPD_OBJECT_FORMAT_JFIF [PTP 0x3808]) | 160x120 | 26 |
| IMG_#0012.PNG | PNG | WPD_RESOURCE_DEFAULT WPD_RESOURCE_THUMBNAIL | yes | 5652 | Jpeg (declared WPD_OBJECT_FORMAT_JFIF [PTP 0x3808]) | 74x160 | 22 |
| IMG_#0013.PNG | PNG | WPD_RESOURCE_DEFAULT WPD_RESOURCE_THUMBNAIL | yes | 5647 | Jpeg (declared WPD_OBJECT_FORMAT_JFIF [PTP 0x3808]) | 74x160 | 23 |
| IMG_#0014.PNG | PNG | WPD_RESOURCE_DEFAULT WPD_RESOURCE_THUMBNAIL | yes | 4467 | Jpeg (declared WPD_OBJECT_FORMAT_JFIF [PTP 0x3808]) | 74x160 | 22 |
| IMG_#0015.PNG | PNG | WPD_RESOURCE_DEFAULT WPD_RESOURCE_THUMBNAIL | yes | 4515 | Jpeg (declared WPD_OBJECT_FORMAT_JFIF [PTP 0x3808]) | 74x160 | 22 |
| IMG_#0016.PNG | PNG | WPD_RESOURCE_DEFAULT WPD_RESOURCE_THUMBNAIL | yes | 4462 | Jpeg (declared WPD_OBJECT_FORMAT_JFIF [PTP 0x3808]) | 74x160 | 23 |
| IMG_#0017.PNG | PNG | WPD_RESOURCE_DEFAULT WPD_RESOURCE_THUMBNAIL | yes | 4478 | Jpeg (declared WPD_OBJECT_FORMAT_JFIF [PTP 0x3808]) | 74x160 | 24 |
| IMG_#0018.PNG | PNG | WPD_RESOURCE_DEFAULT WPD_RESOURCE_THUMBNAIL | yes | 4463 | Jpeg (declared WPD_OBJECT_FORMAT_JFIF [PTP 0x3808]) | 74x160 | 22 |
| IMG_#0019.JPG | JPG | WPD_RESOURCE_DEFAULT WPD_RESOURCE_THUMBNAIL | yes | 11703 | Jpeg (declared WPD_OBJECT_FORMAT_JFIF [PTP 0x3808]) | 160x120 | 28 |
| IMG_#0020.JPG | JPG | WPD_RESOURCE_DEFAULT WPD_RESOURCE_THUMBNAIL | yes | 11529 | Jpeg (declared WPD_OBJECT_FORMAT_JFIF [PTP 0x3808]) | 160x120 | 28 |

Copy attempt during that run (all four failed or returned a 12-byte payload):

| # | Object (sanitized) | Ext | Reported size | Bytes read | Signature | Ext match | Size match | Re-read | MB/s | EXIF / GPS / DTO / Apple MakerNote | QT location / content-id key | Error |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | IMG_#0002.DNG | DNG | 14954859 | 0 | Unknown | n/a | n/a | n/a | 0.0 |  |  | 0x8007001E (Sistem belirtilen aygıttan okuyamıyor. (0x8007001E)) |
| 2 | IMG_#0003.JPG | JPG | 3303294 | 0 | Unknown | n/a | n/a | n/a | 0.0 |  |  | 0x80042007 (0x80042007) |
| 3 | IMG_#0004.PNG | PNG | 222033 | 12 | Unknown | NO | NO | NO | 0.0 | NO / NO / NO / NO | NO / NO |  |
| 4 | OHOO#0005.JPG | JPG | 156368 | 0 | Unknown | n/a | n/a | n/a | 0.0 |  |  | 0x8007001E (Sistem belirtilen aygıttan okuyamıyor. (0x8007001E)) |

Windows.Media.Import source (storage access mode **ReadOnly**); in that run `FindItemsAsync` returned **0 items**:

| # | Display name | Manufacturer | Model | Type | Protocol | Transport | Mass storage | Locked | Storage media (type/access mode) |
|---|---|---|---|---|---|---|---|---|---|
| 0 | <user-assigned device name redacted> | Apple Inc. | Apple iPhone | Generic | MTP: 1.00 | Usb | False | n/a | Fixed/ReadOnly cap=64000000000 free=5449334784 serial=<serial redacted len=16> |

On 2026-10-02 the same PNG (222,033 bytes) and the same `OHOO…JPG` (156,368 bytes) copied completely with identical
re-reads, and Windows.Media.Import found 16 items. The 2026-10-01 failures are therefore not inherent: their error
codes were reproduced exactly by changing the transfer setting mid-connection (E4), and locking was ruled out (E1).

### E7 - what the device exposes with Optimize Storage on

| Session | Objects | Files exposed | Kinds |
|---|---|---|---|
| 2026-10-01 | 42 | 26 | 13 JPG, 8 PNG, 5 DNG |
| 2026-10-02, before capture | 22 | 14 | 5 JPG, 8 PNG (DNGs no longer listed) |
| 2026-10-02, after captures and Keep Originals | 28 | 20 | 8 HEIC (incl. 1 `IMG_E`), 2 JPG, 8 PNG, 1 MOV, 1 AAE |

Versus about 4,500 items in Photos: **only items resident on the phone are listed (14-26 files, under 1% of the
library), and that set changes between sessions.** Non-resident items were absent rather than exposed as reduced
copies: every listed item that was read delivered complete, repeatable bytes. Layout: month folders shaped
`DDDDDD_A` directly under "Internal Storage" (sanitized folder table from the last snapshot):

| Depth | Container (sanitized) | Shape | Children |
|---|---|---|---|
| 1 | Internal Storage | AAAAAAAA AAAAAAA | 7 |
| 2 | #0002_a | DDDDDD_A | 1 |
| 2 | #0003_a | DDDDDD_A | 14 |
| 2 | #0004_a | DDDDDD_A | 1 |
| 2 | #0005_a | DDDDDD_A | 1 |
| 2 | #0006_a | DDDDDD_A | 1 |
| 2 | #0007_a | DDDDDD_A | 1 |
| 2 | #0008_a | DDDDDD_A | 1 |

Per-object WPD properties actually exposed (last snapshot; the Live Photo `.MOV` and the `.AAE` have content type
UNSPECIFIED, hence "other files"):

| Property | images | videos | other files |
|---|---|---|---|
| WPD_FUNCTIONAL_OBJECT_CATEGORY | 0/18 | 0/0 | 0/2 |
| WPD_OBJECT_CAN_DELETE | 0/18 | 0/0 | 0/2 |
| WPD_OBJECT_CONTAINER_FUNCTIONAL_OBJECT_ID | 18/18 | 0/0 | 2/2 |
| WPD_OBJECT_CONTENT_TYPE | 18/18 | 0/0 | 2/2 |
| WPD_OBJECT_DATE_CREATED | 18/18 | 0/0 | 2/2 |
| WPD_OBJECT_DATE_MODIFIED | 18/18 | 0/0 | 2/2 |
| WPD_OBJECT_FORMAT | 18/18 | 0/0 | 2/2 |
| WPD_OBJECT_ID | 18/18 | 0/0 | 2/2 |
| WPD_OBJECT_NAME | 0/18 | 0/0 | 0/2 |
| WPD_OBJECT_ORIGINAL_FILE_NAME | 18/18 | 0/0 | 2/2 |
| WPD_OBJECT_PARENT_ID | 18/18 | 0/0 | 2/2 |
| WPD_OBJECT_PERSISTENT_UNIQUE_ID | 18/18 | 0/0 | 2/2 |
| WPD_OBJECT_SIZE | 18/18 | 0/0 | 2/2 |
| WPD_STORAGE_ACCESS_CAPABILITY | 0/18 | 0/0 | 0/2 |
| WPD_STORAGE_CAPACITY | 0/18 | 0/0 | 0/2 |
| WPD_STORAGE_DESCRIPTION | 0/18 | 0/0 | 0/2 |
| WPD_STORAGE_FILE_SYSTEM_TYPE | 0/18 | 0/0 | 0/2 |
| WPD_STORAGE_FREE_SPACE_IN_BYTES | 0/18 | 0/0 | 0/2 |
| WPD_STORAGE_FREE_SPACE_IN_OBJECTS | 0/18 | 0/0 | 0/2 |
| WPD_STORAGE_SERIAL_NUMBER | 0/18 | 0/0 | 0/2 |
| WPD_STORAGE_TYPE | 0/18 | 0/0 | 0/2 |

| Extension | WPD_OBJECT_FORMAT | WPD_OBJECT_CONTENT_TYPE | Count |
|---|---|---|---|
| HEIC | WPD_OBJECT_FORMAT_EXIF [PTP 0x3801] | WPD_CONTENT_TYPE_IMAGE | 8 |
| PNG | WPD_OBJECT_FORMAT_PNG [PTP 0x380B] | WPD_CONTENT_TYPE_IMAGE | 8 |
| JPG | WPD_OBJECT_FORMAT_EXIF [PTP 0x3801] | WPD_CONTENT_TYPE_IMAGE | 2 |
| MOV | PTP/MTP format 0x300D (QuickTime) | WPD_CONTENT_TYPE_UNSPECIFIED | 1 |
| AAE | WPD_OBJECT_FORMAT_UNSPECIFIED [PTP 0x3000] | WPD_CONTENT_TYPE_UNSPECIFIED | 1 |

### E9 - in-session events

Listening for 180 s while two photos were taken: **no `WPD_EVENT_OBJECT_ADDED`**. One `WPD_EVENT_DEVICE_RESET`
arrived, after which the storage showed **0 children for the rest of that connection**, even unlocked on the Home
Screen; the new photos appeared only after replugging.

| Local time | Event | Parameters (sanitized) |
|---|---|---|
| 09:23:52 | WPD_EVENT_DEVICE_RESET | WPD_OBJECT_ID=<id#0001 len=6 shape=AAAAAA>, WPD_OBJECT_PERSISTENT_UNIQUE_ID=<persistent_unique_id#0002 len=6 shape=AAAAAA>, WPD_OBJECT_NAME=Apple iPhone, WPD_OBJECT_CONTENT_TYPE=WPD_CONTENT_TYPE_FUNCTIONAL_OBJECT, WPD_FUNCTIONAL_OBJECT_CATEGORY=WPD_FUNCTIONAL_CATEGORY_DEVICE, WPD_OBJECT_ORIGINAL_FILE_NAME=0x80070490 HRESULT_FROM_WIN32(ERROR_NOT_FOUND), WPD_OBJECT_PARENT_ID=, WPD_OBJECT_CONTAINER_FUNCTIONAL_OBJECT_ID=<container_functional_object_id#0003 len=6 shape=AAAAAA>, WPD_EVENT_PARAMETER_PNP_DEVICE_ID=\\?\usb#vid_05ac&pid_12a8&mi_00#<instance redacted>#{6ac27878-a6fa-4155-ba85-f98f491d4f33} |

### E4 - Automatic vs Keep Originals (same two test captures)

| Capture | Transfer setting | Name / reported size | Bytes read | Content signature (first bytes) | Re-read | SHA-256 prefix |
|---|---|---|---|---|---|---|
| Test capture 1 | Automatic | `IMG_….JPG` / 6,202,024 | 6,202,024 | JPEG, `FFD8FFE0 …JFIF` | identical | `f7cb513e5892` |
| Test capture 1 | Keep Originals | `IMG_….HEIC` / 3,101,012 | 3,101,012 | HEIF, `…ftypheic` | identical | `db97a9c4101a` |
| Test capture 2 | Automatic | `IMG_….JPG` / 6,061,874 | 6,061,874 | JPEG, `FFD8FFE0 …JFIF` | identical | `b9ae86d0d63e` |
| Test capture 2 | Keep Originals | `IMG_….HEIC` / 3,030,937 | 3,030,937 | HEIF, `…ftypheic` | identical | `8299f29c72ec` |

Four older resident photos also changed from `.JPG` (Automatic) to `.HEIC` (Keep Originals) at about half the size
(4,188,803 -> 1,821,219; 6,449,046 -> 3,224,523; 4,234,756 -> 2,117,378; 3,899,484 -> 1,695,428 bytes). Under
*Automatic* the device announces the converted name, size and the EXIF format code, so the size check, the
extension/signature check and the WPD metadata all agree with the converted bytes. HEIC objects are also reported
with the EXIF format code `0x3801`.

Changing the setting mid-connection did not change the listing, and reading the two listed `.JPG` objects then
failed: `0x8007001E` (ERROR_READ_FAULT) and `0x80042007` (consistent with PTP response 0x2007 Incomplete_Transfer).
No partial staging files were left behind.

### E5 - identifier stability

| Comparison | Entries matched by name+size+date | PUID unchanged | WPD object ID unchanged |
|---|---|---|---|
| Same connection, no changes | 16 | **16** | 16 |
| Same connection, after switching to Keep Originals | 16 | **16** | 16 |
| Across a reconnect, after new captures | 14 | **0** | 0 |
| Across a reconnect, unchanged items only | 10 | **0** | 0 |

PUIDs are GUID-shaped strings that are stable within a connection and different after every reconnect. Content
identity is stable: the original of the edited test photo had the same SHA-256 across two reconnects and after the
on-phone edit.

### E6 - Live Photo and edit

After capturing one Live Photo and cropping and filtering one test photo on the phone:

| # | Object (sanitized) | Ext | Reported size | Bytes read | Signature | Ext match | Size match | Re-read | MB/s | EXIF / GPS / DTO / Apple MakerNote | QT location / content-id key | Error |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | IMG_#0009.HEIC | HEIC | 2842342 | 2842342 | Heif | yes | yes | yes | 19.8 | yes / yes / yes / yes | NO / NO |  |
| 2 | IMG_#0009.MOV | MOV | 4386253 | 4386253 | QuickTime | yes | yes | yes | 25.6 | NO / NO / NO / NO | yes / yes |  |
| 3 | IMG_#0010.AAE | AAE | 1072 | 1072 | Xml | yes | yes | yes | 0.0 | NO / NO / NO / NO | NO / NO | AAE plist keys: adjustmentBaseVersion,adjustmentData,adjustmentEditorBundleID,adjustmentFormatIdentifier,adjustmentFormatVersion,adjustmentRenderTypes,adjustmentTimestamp; adjustmentFormatIdentifier=com.apple.photo |
| 4 | IMG_#0010.HEIC | HEIC | 3101012 | 3101012 | Heif | yes | yes | yes | 23.0 | yes / yes / yes / yes | NO / NO |  |
| 5 | IMG_E#0010.HEIC | HEIC | 1181147 | 1181147 | Heif | yes | yes | yes | 14.3 | yes / yes / yes / yes | NO / NO |  |
| 6 | IMG_#0011.HEIC | HEIC | 3030937 | 3030937 | Heif | yes | yes | yes | 23.2 | yes / yes / yes / yes | NO / NO |  |

- Live Photo candidate `IMG_#0009.HEIC + IMG_#0009.MOV`: motion file has `com.apple.quicktime.content.identifier` key = yes; still has Apple MakerNote = yes; **still and motion bytes share a UUID token = yes** (byte-level pairing evidence; values not shown).
- Related-by-name group `IMG_#0010.AAE + IMG_#0010.HEIC + IMG_E#0010.HEIC` (filename heuristic only).

### E11 - Windows.Media.Import grouping (find only, no import)

| Fact | Value |
|---|---|
| HasSucceeded | yes |
| Items usable before completion | no — FoundItems is only available after the operation completes (progress reports counts only) |
| Items with Sibling | 0 |
| Items with Sidecars | 2 |
| PhotoImportItem.Thumbnail readable (sampled) | 5/5 |
| SiblingsCount / SidecarsCount | 0 / 2 |
| TotalCount / PhotosCount / VideosCount | 18 / 17 / 1 |

| Name | ContentType | Size | Date | Sibling | Sidecars | VideoSegments |
|---|---|---|---|---|---|---|
| IMG_#0001.MOV | Video | 4386253 | 2026-10 (day/time redacted) |  | IMG_#0001.HEIC | 0 |
| IMG_#0002.HEIC | Image | 3101012 | 2026-10 (day/time redacted) |  | IMG_#0002.AAE | 0 |
| IMG_E#0002.HEIC | Image | 1181147 | 2026-10 (day/time redacted) |  |  | 0 |

Windows groups by file name: the Live Photo `.MOV` is the item and the still `.HEIC` its *sidecar*; the `.AAE` is a
sidecar of the original; the `IMG_E` render is a separate item.

## Not run in these sessions

- E8 (hidden / Recently Deleted / Shared Library visibility), E10 (physical unplug during a large transfer), and
  E11 import (`--import`).
- E12/E13 guarded delete/write experiments: not run. The device declares its storage read-only without deletion and
  the driver does not offer object creation.
- A library with iCloud Photos off (all originals resident): not available on this phone.
