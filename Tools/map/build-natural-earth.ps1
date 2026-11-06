# Downloads the Natural Earth source data (public domain, naturalearthdata.com; GeoJSON from the
# nvkelso/natural-earth-vector repository) and builds the map pack assets/data/maps/natural-earth and
# the playable map mod mods/map-natural-earth (map 2 with the conquest conquest_world.xml).
# Usage: .\Tools\map\build-natural-earth.ps1 [-Source DIR] [-SkipDownload]
# The downloads (about 50 MB) go to Tools\map\source (not in git). See MAP_PIPELINE.md.
param([string]$Source = "", [switch]$SkipDownload)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not $Source) { $Source = Join-Path $PSScriptRoot 'source' }
New-Item -ItemType Directory -Force $Source | Out-Null

$base = 'https://raw.githubusercontent.com/nvkelso/natural-earth-vector/master/geojson'
$files = @('ne_10m_admin_1_states_provinces.geojson', 'ne_50m_populated_places.geojson', 'ne_10m_ports.geojson', 'ne_10m_airports.geojson')
if (-not $SkipDownload) {
    foreach ($f in $files) {
        $target = Join-Path $Source $f
        if (Test-Path $target) { Write-Host "have $f"; continue }
        Write-Host "downloading $f"
        Invoke-WebRequest -Uri "$base/$f" -OutFile $target -UseBasicParsing
    }
}

& dotnet build (Join-Path $repo 'Tools\WC2.Tools.csproj') -v q --nologo | Out-Null
& dotnet (Join-Path $repo 'Tools\bin\Debug\net9.0\WC2.Tools.dll') build-map `
    --config (Join-Path $PSScriptRoot 'natural-earth.json') --source $Source --out (Join-Path $repo 'assets\data\maps\natural-earth') `
    --classic-out (Join-Path $repo 'mods\map-natural-earth')
if ($LASTEXITCODE -ne 0) { throw "build-map failed" }
