# Runs every scripted smoke test and collects screenshots + logs under tests\out.
# Usage: cd WC2; dotnet build; .\tests\run-tests.ps1 [-Filter conquest]
param([string]$Filter = "")
$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $PSScriptRoot 'out'
New-Item -ItemType Directory -Force $out | Out-Null
# input self test: viewport mapping (centring, cutout, round trip) and the touch router, no window
Write-Host "== input" -ForegroundColor Cyan
$inputLog = Join-Path $out 'input.txt'
& dotnet run --project $root --no-build -- --test-input | Out-File -Encoding utf8 $inputLog
$inputProblems = Select-String -Path $inputLog -Pattern '^FAIL'
$inputSummary = (Select-String -Path $inputLog -Pattern 'input self test:').Line
if ($inputProblems -or -not $inputSummary) { Write-Host "   problems:" -ForegroundColor Yellow; $inputProblems | ForEach-Object { Write-Host "   $($_.Line)" }; if (-not $inputSummary) { Write-Host "   no summary line" } }
else { Write-Host "   ok ($inputSummary)" -ForegroundColor Green }
# audio self test: decodes every music file and loads every sound effect, output kept in tests/out/audio.txt
Write-Host "== audio" -ForegroundColor Cyan
$audioLog = Join-Path $out 'audio.txt'
& dotnet run --project $root --no-build -- --test-audio | Out-File -Encoding utf8 $audioLog
$audioProblems = Select-String -Path $audioLog -Pattern 'FAILED|MISMATCH|, [1-9][0-9]* bad'
if ($audioProblems) { Write-Host "   problems:" -ForegroundColor Yellow; $audioProblems | ForEach-Object { Write-Host "   $($_.Line)" } }
else { Write-Host "   ok ($((Select-String -Path $audioLog -Pattern '\.mp3 ').Count) music files, $((Select-String -Path $audioLog -Pattern 'wav effects').Line))" -ForegroundColor Green }

Get-ChildItem (Join-Path $PSScriptRoot '*.txt') | Where-Object { $_.BaseName -like "*$Filter*" } | ForEach-Object {
    $name = $_.BaseName
    Write-Host "== $name" -ForegroundColor Cyan
    $log = Join-Path $out "$name.log"
    if (Test-Path $log) { Remove-Item $log }
    $extra = @(); if ($name -like 'ipad*') { $extra += '--ipad' }   # ipad_*.txt scripts run in the 1024x768 tablet layout
    # a first line "#! ARGS" adds command line options for the script (window size, --density, --insets)
    $first = Get-Content $_.FullName -TotalCount 1
    if ($first -match '^#!\s*(.+)$') { $extra += ($Matches[1] -split '\s+' | Where-Object { $_ }) }
    & dotnet run --project $root --no-build -- @extra --exit-after 460 --screenshot (Join-Path $out "$name.png") --script $_.FullName --log $log | Out-Null
    $problems = Select-String -Path $log -Pattern 'failed|xception' | Where-Object { $_.Line -notmatch 'Sound missing' }
    if (-not (Select-String -Path $log -Pattern 'Music playing' -Quiet)) { $problems = @($problems) + 'no "Music playing" line in the log' }
    if ($problems) { Write-Host "   problems:" -ForegroundColor Yellow; $problems | ForEach-Object { Write-Host "   $($_.Line)" } }
    else { Write-Host "   ok" -ForegroundColor Green }
}
