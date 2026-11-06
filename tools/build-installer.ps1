# Tests, publishes the single-file exe, then builds the NSIS installer into ./publish
# Usage: powershell -ExecutionPolicy Bypass -File tools/build-installer.ps1
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')

& (Join-Path $PSScriptRoot 'publish.ps1')

# Version comes from the app's csproj so the installer always matches the exe.
[xml]$proj = Get-Content (Join-Path $root 'src\SiegeServerChanger.App\SiegeServerChanger.App.csproj')
$version = ($proj.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version

$makensis = (Get-Command makensis -ErrorAction SilentlyContinue).Source
if (-not $makensis) { $makensis = 'C:\Program Files (x86)\NSIS\makensis.exe' }
if (-not (Test-Path $makensis)) { throw 'NSIS not found. Install it: winget install NSIS.NSIS' }

& $makensis /V2 "/DVERSION=$version" (Join-Path $root 'installer\setup.nsi')
if ($LASTEXITCODE -ne 0) { throw 'makensis failed.' }

Get-ChildItem (Join-Path $root 'publish') -Filter '*Setup*.exe' |
    Select-Object Name, @{ n = 'MB'; e = { [Math]::Round($_.Length / 1MB, 1) } }
