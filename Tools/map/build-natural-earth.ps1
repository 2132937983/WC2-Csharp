# Downloads the source data (Natural Earth, public domain, naturalearthdata.com, GeoJSON from the
# nvkelso/natural-earth-vector repository; CShapes 2.0 historical borders, icr.ethz.ch/data/cshapes)
# and builds the map pack assets/data/maps/natural-earth and the playable map mod
# mods/map-natural-earth (map 2 with the conquest conquest_10.xml, WORLD 1939).
# Usage: .\Tools\map\build-natural-earth.ps1 [-Source DIR] [-SkipDownload]
# The downloads (about 75 MB) go to Tools\map\source (not in git). See MAP_PIPELINE.md.
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
    # historical borders 1886-2019: CShapes 2.0 (ETH Zurich, CC BY-NC-SA 4.0), about 26 MB
    $cshapes = Join-Path $Source 'CShapes-2.0.geojson'
    if (Test-Path $cshapes) { Write-Host "have CShapes-2.0.geojson" }
    else {
        Write-Host "downloading CShapes-2.0.geojson"
        Invoke-WebRequest -Uri 'https://icr.ethz.ch/data/cshapes/CShapes-2.0.geojson' -OutFile $cshapes -UseBasicParsing
    }
}

& dotnet build (Join-Path $repo 'Tools\WC2.Tools.csproj') -v q --nologo | Out-Null
& dotnet (Join-Path $repo 'Tools\bin\Debug\net9.0\WC2.Tools.dll') build-map `
    --config (Join-Path $PSScriptRoot 'natural-earth.json') --source $Source --out (Join-Path $repo 'assets\data\maps\natural-earth') `
    --classic-out (Join-Path $repo 'mods\map-natural-earth')
if ($LASTEXITCODE -ne 0) { throw "build-map failed" }
