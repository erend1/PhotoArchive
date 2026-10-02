<#
.SYNOPSIS
    Collects a sanitized Windows-side environment snapshot for the M000 Apple device capability spike.

.DESCRIPTION
    Records Windows version, Apple software (Store packages and classic installs), Apple driver packages,
    and every present Apple USB device node (VID_05AC) with the driver bound to it. Device instance IDs embed
    the iPhone serial/UDID and the WPD friendly name is user-assigned (e.g. "<name>'s iPhone"); both are
    redacted unless -NoSanitize is given. Read-only: changes nothing on the PC or the phone.

    Compatible with Windows PowerShell 5.1 and PowerShell 7+.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\scripts\Collect-Environment.ps1
    powershell -ExecutionPolicy Bypass -File .\scripts\Collect-Environment.ps1 -OutFile "$env:TEMP\m000-env.md"
#>
[CmdletBinding()]
param(
    [string] $OutFile,
    [switch] $NoSanitize
)

$ErrorActionPreference = 'Stop'
$lines = New-Object System.Collections.Generic.List[string]

function Add-Line([string] $text) { $lines.Add($text) | Out-Null }

function Protect-InstanceId([string] $id) {
    if ($NoSanitize -or [string]::IsNullOrEmpty($id)) { return $id }
    $parts = $id.Split('\')
    if ($parts.Length -ge 3) { return ($parts[0..1] -join '\') + '\<instance redacted>' }
    return '<redacted>'
}

function Protect-Name([string] $name, [string] $class) {
    if ($NoSanitize -or [string]::IsNullOrEmpty($name)) { return $name }
    $possessive = "'s |" + [char]0x2019 + 's '
    if ($class -eq 'WPD' -or $name -match $possessive) { return '<user-assigned device name redacted>' }
    return $name
}

function Get-DeviceProperty([string] $instanceId, [string] $key) {
    try {
        $value = (Get-PnpDeviceProperty -InstanceId $instanceId -KeyName $key -ErrorAction Stop).Data
        if ($null -eq $value) { return '' }
        return [string]$value
    } catch { return '' }
}

$cv = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
$build = [int]$cv.CurrentBuildNumber
$edition = if ($build -ge 22000) { 'Windows 11' } else { 'Windows 10' }

Add-Line '# M000 Apple device spike - Windows environment snapshot'
Add-Line ''
Add-Line ("Captured: {0:yyyy-MM-dd} | Sanitized: {1}" -f (Get-Date), (-not $NoSanitize))
Add-Line ''
Add-Line '## Windows'
Add-Line ''
Add-Line ("- Edition (derived from build): {0}" -f $edition)
Add-Line ("- DisplayVersion: {0}" -f $cv.DisplayVersion)
Add-Line ("- Build: {0}.{1}" -f $cv.CurrentBuildNumber, $cv.UBR)
Add-Line ("- PowerShell: {0}" -f $PSVersionTable.PSVersion)
Add-Line ''

Add-Line '## Apple software'
Add-Line ''
$appx = @(Get-AppxPackage -ErrorAction SilentlyContinue | Where-Object { $_.Name -like 'AppleInc.*' -or $_.Publisher -match 'Apple' })
if ($appx.Count -eq 0) { Add-Line '- Store packages: (none)' }
foreach ($p in $appx) { Add-Line ("- Store package: {0} {1}" -f $p.Name, $p.Version) }

$uninstallRoots = @(
    'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
    'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*',
    'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*')
$classic = @(Get-ItemProperty $uninstallRoots -ErrorAction SilentlyContinue |
    Where-Object { $_.DisplayName -and ($_.Publisher -match 'Apple' -or $_.DisplayName -match 'Apple|iTunes|Bonjour|iCloud') } |
    Sort-Object DisplayName -Unique)
if ($classic.Count -eq 0) { Add-Line '- Classic installs: (none)' }
foreach ($c in $classic) { Add-Line ("- Classic install: {0} {1}" -f $c.DisplayName, $c.DisplayVersion) }

$services = @(Get-CimInstance Win32_Service -ErrorAction SilentlyContinue | Where-Object { $_.Name -match 'Apple|Bonjour|iPod|AppleMobileDevice' -or $_.DisplayName -match 'Apple|Bonjour|Mobile Device' })
if ($services.Count -eq 0) { Add-Line '- Services: (none)' }
foreach ($s in $services) { Add-Line ("- Service: {0} ({1}) state={2} start={3}" -f $s.Name, $s.DisplayName, $s.State, $s.StartMode) }
Add-Line ''

Add-Line '## Apple driver packages in the driver store'
Add-Line ''
$drivers = @()
try { $drivers = @(Get-WindowsDriver -Online -ErrorAction Stop | Where-Object { $_.ProviderName -match 'Apple' }) } catch { $drivers = @() }
if ($drivers.Count -eq 0) {
    # Get-WindowsDriver needs elevation; fall back to the driver store folder names.
    $folders = @(Get-ChildItem "$env:WINDIR\System32\DriverStore\FileRepository" -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^(appleusb|usbaapl|netaapl|applekmdfrt|appleccd)' })
    if ($folders.Count -eq 0) { Add-Line '- (none found)' }
    foreach ($f in $folders) { Add-Line ("- {0}" -f $f.Name) }
} else {
    foreach ($d in $drivers) { Add-Line ("- {0} ({1}) {2} {3:yyyy-MM-dd} class={4}" -f $d.OriginalFileName.Split('\')[-1], $d.Driver, $d.Version, $d.Date, $d.ClassName) }
}
Add-Line ''

Add-Line '## Present Apple USB device nodes (VID_05AC) and bound drivers'
Add-Line ''
$apple = @(Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | Where-Object { $_.InstanceId -match 'VID_05AC' })
if ($apple.Count -eq 0) {
    Add-Line '- No Apple USB device is currently connected (connect, unlock and trust the iPhone, then re-run).'
} else {
    Add-Line '| Class | Name | Status | Service | Driver INF | Provider | Version | Problem | Instance |'
    Add-Line '|---|---|---|---|---|---|---|---|---|'
    foreach ($d in $apple) {
        Add-Line ("| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} | {8} |" -f
            $d.Class,
            (Protect-Name $d.FriendlyName $d.Class),
            $d.Status,
            (Get-DeviceProperty $d.InstanceId 'DEVPKEY_Device_Service'),
            (Get-DeviceProperty $d.InstanceId 'DEVPKEY_Device_DriverInfPath'),
            (Get-DeviceProperty $d.InstanceId 'DEVPKEY_Device_DriverProvider'),
            (Get-DeviceProperty $d.InstanceId 'DEVPKEY_Device_DriverVersion'),
            (Get-DeviceProperty $d.InstanceId 'DEVPKEY_Device_ProblemCode'),
            (Protect-InstanceId $d.InstanceId))
    }
}
Add-Line ''

Add-Line '## Present WPD (Portable Devices) nodes'
Add-Line ''
$wpd = @(Get-PnpDevice -PresentOnly -Class WPD -ErrorAction SilentlyContinue)
if ($wpd.Count -eq 0) { Add-Line '- (none)' }
foreach ($w in $wpd) {
    Add-Line ("- {0} | status={1} | service={2} | {3}" -f (Protect-Name $w.FriendlyName 'WPD'), $w.Status,
        (Get-DeviceProperty $w.InstanceId 'DEVPKEY_Device_Service'), (Protect-InstanceId $w.InstanceId))
}

$text = $lines -join [Environment]::NewLine
if ($OutFile) {
    Set-Content -Path $OutFile -Value $text -Encoding UTF8
    Write-Host "Written: $OutFile"
}
$text
