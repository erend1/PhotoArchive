# Evidence: development machine, no iPhone attached (2026-10-01)

**Evidence level: EXECUTED — Windows-side environment and API availability only.**
No iPhone was connected to this machine during the spike session (no `VID_05AC` USB node and no WPD device present;
the only Apple records on the machine were Bluetooth pairings). Therefore nothing in this file proves or disproves any
iPhone capability; every device row of the capability matrix remains `LOCAL_EXECUTION_REQUIRED` until the experiments
in `../README.md` are run with a real, trusted iPhone.

What this run does establish:

- The probe builds and runs on Windows 11 25H2 (build 26200.8246) with .NET 10.0.12 (x64).
- The documented WPD COM API is callable from an unpackaged .NET 10 process: `IPortableDeviceManager::GetDevices`
  succeeded and returned 0 devices (no device attached).
- The documented `Windows.Media.Import` WinRT API is callable from an unpackaged .NET 10 process:
  `PhotoImportManager.IsSupportedAsync()` returned `true`; `FindAllSourcesAsync()` returned 0 sources.
- No-device behaviour is explicit: `inspect` exits with code 3 and the troubleshooting text below; `report` still
  records the environment and the Windows.Media.Import result.
- Apple software on this machine: no Apple Devices / iTunes Store package, no Apple Mobile Device service,
  an `appleusb.inf` (Apple USB driver, Apple Inc., 538.0.0.0, dated 2023-06-14) package in the driver store,
  and the iCloud Outlook add-in (15.0.0.215).

Commands (from `spikes/M000.AppleDevice`, Release build):

```text
apple-device-probe report   -> exit 0
apple-device-probe inspect  -> exit 3 (no Apple device visible through WPD)
powershell -File scripts/Collect-Environment.ps1 -> exit 0
```

---

## `apple-device-probe report` (sanitized output, verbatim)

### M000 Apple device probe — report

#### Run

| Fact | Value |
|---|---|
| Command line (options only) | --out |
| Sanitized | yes |
| Test conditions (fill in manually) | iPhone model/iOS: ? \| iCloud Photos: on/off \| Optimize iPhone Storage: on/off \| Transfer to Mac or PC: Automatic/Keep Originals \| cable/port: ? |


#### Environment (Windows side)

| Fact | Value |
|---|---|
| .NET runtime | .NET 10.0.12 |
| Apple Store packages (current user) | (none) |
| Apple USB driver package (appleusb.inf) in driver store | yes (1 package folder(s)) |
| Apple classic installs (uninstall registry) | iCloud Outlook 15.0.0.215 |
| OS description | Microsoft Windows 10.0.26200 |
| PortableDeviceApi.dll version | 10.0.26100.8875 (WinBuild.160101.0800) |
| PortableDeviceTypes.dll version | 10.0.26100.5074 (WinBuild.160101.0800) |
| Probe version | 1.0.0.0 |
| Process architecture | X64 |
| WPD name table size (keys/GUIDs) | 110/47 |
| Windows build | 26200.8246 |
| Windows display version | 25H2 |
| Windows edition (derived from build; registry ProductName still says 'Windows 10' on Windows 11) | Windows 11 |
| Windows product (registry ProductName) | Windows 10 Pro |
| Windows.Media.Import PhotoImportManager.IsSupportedAsync() | yes |
| WpdShext.dll version | 10.0.26100.1710 (WinBuild.160101.0800) |
| wpdmtp.dll version | 10.0.26100.8115 (WinBuild.160101.0800) |
| wpdmtpdr.dll version | (not present) |

Evidence level for this section: EXECUTED on the machine that produced this report.

#### WPD devices

| Fact | Value |
|---|---|
| API | IPortableDeviceManager::GetDevices / GetPrivateDevices / GetDeviceFriendlyName / GetDeviceManufacturer / GetDeviceDescription |
| Apple-looking devices | 0 |
| Devices visible | 0 |

| # | Friendly name | Manufacturer | Description | Apple? | PnP id (sanitized) |
|---|---|---|---|---|---|


#### Apple device

| Fact | Value |
|---|---|
| Result | No Apple device visible through WPD on this run |

No Apple device is visible through Windows Portable Devices. Check: (1) the iPhone is connected by a data-capable USB cable, (2) it is UNLOCKED, (3) you tapped Trust on the 'Trust This Computer?' prompt, (4) Apple Devices (Microsoft Store) is installed, (5) 'Apple iPhone' appears under Portable Devices in Device Manager. Run `devices` to list what Windows sees.

#### Windows.Media.Import sources

| Fact | Value |
|---|---|
| API | PhotoImportManager.IsSupportedAsync / FindAllSourcesAsync; PhotoImportSource; PhotoImportStorageMedium.SupportedAccessMode |
| PhotoImportManager.IsSupportedAsync() | yes |
| Sources found | 0 |

| # | Display name | Manufacturer | Model | Type | Protocol | Transport | Mass storage | Locked | Storage media (type/access mode) |
|---|---|---|---|---|---|---|---|---|---|



## `apple-device-probe inspect` console output

```text
[ERROR] No Apple device is visible through Windows Portable Devices. Check: (1) the iPhone is connected by a data-capable USB cable, (2) it is UNLOCKED, (3) you tapped Trust on the 'Trust This Computer?' prompt, (4) Apple Devices (Microsoft Store) is installed, (5) 'Apple iPhone' appears under Portable Devices in Device Manager. Run `devices` to list what Windows sees.
```

## `scripts/Collect-Environment.ps1` (sanitized output, verbatim)

### M000 Apple device spike - Windows environment snapshot

Captured: 2026-10-01 | Sanitized: True

#### Windows

- Edition (derived from build): Windows 11
- DisplayVersion: 25H2
- Build: 26200.8246
- PowerShell: 7.5.5

#### Apple software

- Store packages: (none)
- Classic install: iCloud Outlook 15.0.0.215
- Services: (none)

#### Apple driver packages in the driver store

- appleusb.inf_amd64_58854158183af679

#### Present Apple USB device nodes (VID_05AC) and bound drivers

- No Apple USB device is currently connected (connect, unlock and trust the iPhone, then re-run).

#### Present WPD (Portable Devices) nodes

- (none)
