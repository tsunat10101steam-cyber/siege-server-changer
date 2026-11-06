# Builds a single self-contained SiegeServerChanger.exe into ./publish
# Usage: powershell -ExecutionPolicy Bypass -File tools/publish.ps1
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$out = Join-Path $root 'publish'

dotnet test (Join-Path $root 'SiegeServerChanger.slnx')
if ($LASTEXITCODE -ne 0) { throw 'Tests failed; not publishing.' }

dotnet publish (Join-Path $root 'src\SiegeServerChanger.App\SiegeServerChanger.App.csproj') -c Release -o $out -p:DebugType=none
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

Get-ChildItem $out | Select-Object Name, @{ n = 'MB'; e = { [Math]::Round($_.Length / 1MB, 1) } }
