[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$ReportPath
)

$ErrorActionPreference = 'Stop'

if ($env:OS -ne 'Windows_NT') {
    throw 'The WinUI gallery benchmark must run on Windows.'
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$project = Join-Path $PSScriptRoot 'PhotoArchive.M000.GallerySpike\PhotoArchive.M000.GallerySpike.csproj'

if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Join-Path $repoRoot 'artifacts\gallery-winui-benchmark-local.json'
}
$ReportPath = [System.IO.Path]::GetFullPath($ReportPath)
New-Item -ItemType Directory -Force -Path (Split-Path $ReportPath -Parent) | Out-Null

$os = Get-CimInstance Win32_OperatingSystem
$cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
$system = Get-CimInstance Win32_ComputerSystem
$gpu = Get-CimInstance Win32_VideoController | Select-Object -First 1

$env:GALLERY_SPIKE_WINDOWS = "$($os.Caption) $($os.Version) build $($os.BuildNumber)"
$env:GALLERY_SPIKE_CPU = $cpu.Name
$env:GALLERY_SPIKE_RAM = "$([math]::Round($system.TotalPhysicalMemory / 1GB, 1)) GiB"
$env:GALLERY_SPIKE_GPU = $gpu.Name

Write-Host "Building gallery spike ($Configuration, x64)..."
dotnet build $project --configuration $Configuration -p:Platform=x64
if ($LASTEXITCODE -ne 0) {
    throw "Gallery spike build failed with exit code $LASTEXITCODE."
}

$binRoot = Join-Path (Split-Path $project -Parent) "bin\x64\$Configuration"
$exe = Get-ChildItem -Path $binRoot -Filter 'PhotoArchive.M000.GallerySpike.exe' -Recurse | Select-Object -First 1
if ($null -eq $exe) {
    throw "Gallery spike executable was not found under $binRoot."
}

Write-Host "Running: $($exe.FullName)"
Write-Host "Report: $ReportPath"
$process = Start-Process -FilePath $exe.FullName -ArgumentList '--benchmark', "--report=$ReportPath" -PassThru -Wait

if ($process.ExitCode -ne 0) {
    $trace = Join-Path (Split-Path $ReportPath -Parent) 'startup-trace.log'
    throw "Gallery spike exited with code $($process.ExitCode). Inspect $trace and Windows Application events."
}

if (-not (Test-Path $ReportPath)) {
    throw "Gallery spike exited without writing $ReportPath."
}

Get-Content $ReportPath
