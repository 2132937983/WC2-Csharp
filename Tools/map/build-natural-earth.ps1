# Downloads the source data (Natural Earth, public domain, naturalearthdata.com, GeoJSON from the
# nvkelso/natural-earth-vector repository; CShapes 2.0 historical borders, icr.ethz.ch/data/cshapes)
# and builds the map pack assets/data/maps/natural-earth and the playable map mod
# mods/map-natural-earth (map 2 with the conquest conquest_10.xml, WORLD 1939).
# Usage: .\Tools\map\build-natural-earth.ps1 [-Source DIR] [-SkipDownload]
# The downloads (about 190 MB; the relief picture unpacked: 175 MB) go to Tools\map\source (not in git). See MAP_PIPELINE.md.
param([string]$Source = "", [switch]$SkipDownload)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not $Source) { $Source = Join-Path $PSScriptRoot 'source' }
New-Item -ItemType Directory -Force $Source | Out-Null

$base = 'https://raw.githubusercontent.com/nvkelso/natural-earth-vector/master/geojson'
$files = @('ne_10m_admin_1_states_provinces.geojson', 'ne_50m_populated_places.geojson', 'ne_10m_populated_places.geojson', 'ne_10m_ports.geojson', 'ne_10m_airports.geojson', 'ne_10m_lakes.geojson', 'ne_10m_lakes_historic.geojson')
if (-not $SkipDownload) {
    foreach ($f in $files) {
        $target = Join-Path $Source $f
        if (Test-Path $target) { Write-Host "have $f"; continue }
        Write-Host "downloading $f"
        Invoke-WebRequest -Uri "$base/$f" -OutFile $target -UseBasicParsing
    }
    # the relief picture: Natural Earth II with shaded relief and water, 1:50m (public domain), about 89 MB
    $relief = Join-Path $Source 'ne2\NE2_50M_SR_W\NE2_50M_SR_W.tif'
    if (Test-Path $relief) { Write-Host "have NE2_50M_SR_W.tif" }
    else {
        Write-Host "downloading NE2_50M_SR_W.zip"
        $zip = Join-Path $Source 'NE2_50M_SR_W.zip'
        Invoke-WebRequest -Uri 'https://naciscdn.org/naturalearth/50m/raster/NE2_50M_SR_W.zip' -OutFile $zip -UseBasicParsing
        Expand-Archive -Path $zip -DestinationPath (Join-Path $Source 'ne2') -Force
        Remove-Item $zip
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
