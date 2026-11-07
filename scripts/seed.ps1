# Copies the deterministic seed dataset to the default data location used by all variants.
$src = Join-Path $PSScriptRoot '..\spec\data\items.json'
if (-not (Test-Path $src)) { node (Join-Path $PSScriptRoot '..\spec\gen-data.mjs') }
$dir = Join-Path $env:LOCALAPPDATA 'DesktopBench'
New-Item -ItemType Directory -Force $dir | Out-Null
Copy-Item $src (Join-Path $dir 'items.json') -Force
Write-Host "Seeded $dir\items.json"
