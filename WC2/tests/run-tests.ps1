# Runs the audio self-test, the data and asset checks and every scripted test, then compares the
# rule-state dumps and the reference screenshots with the goldens.
# Usage: cd WC2; dotnet build; .\tests\run-tests.ps1 [-Filter conquest] [-Record] [-RecordShots] [-RealTime]
#   -Record       store the dumps of this run as the new goldens (tests\golden\<script>\<dump>.json)
#   -RecordShots  store this run's screenshots as the new reference screenshots (320 px wide JPEG)
#   -RealTime     run with the real clock instead of the deterministic mode (no golden comparison)
# Deterministic mode: --seed $Seed --fixed-step and a fresh documents folder per script, so a script
# replays the same game every time and the dumps can be compared byte for byte.
# A script whose first line is "#! ARGS" gets ARGS on the command line (window size, layout).
param([string]$Filter = "", [switch]$Record, [switch]$RecordShots, [switch]$RealTime, [int]$Seed = 20261106)
$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $PSScriptRoot 'out'
$golden = Join-Path $PSScriptRoot 'golden'
$config = 'Debug'
$tools = Join-Path $root "..\WC2.Tools\bin\$config\net9.0\WC2.Tools.dll"
$pack = Join-Path $root "bin\$config\net9.0\pack"
New-Item -ItemType Directory -Force $out | Out-Null
$failures = 0

# audio self test: decodes every music file and loads every sound effect, output kept in tests/out/audio.txt
Write-Host "== audio" -ForegroundColor Cyan
$audioLog = Join-Path $out 'audio.txt'
& dotnet run --project $root --no-build -- --test-audio | Out-File -Encoding utf8 $audioLog
$audioProblems = Select-String -Path $audioLog -Pattern 'FAILED|MISMATCH|, [1-9][0-9]* bad'
if ($audioProblems) { $failures++; Write-Host "   problems:" -ForegroundColor Yellow; $audioProblems | ForEach-Object { Write-Host "   $($_.Line)" } }
else { Write-Host "   ok ($((Select-String -Path $audioLog -Pattern '\.mp3 ').Count) music files, $((Select-String -Path $audioLog -Pattern 'wav effects').Line))" -ForegroundColor Green }

# data check: every JSON data file must load into the same definitions as the original XML it was
# converted from (an intentional data change shows up here; re-convert or accept it knowingly)
Write-Host "== data" -ForegroundColor Cyan
$dataLog = Join-Path $out 'verify-data.txt'
& dotnet run --project $root --no-build -- --verify-data | Out-File -Encoding utf8 $dataLog
if (Select-String -Path $dataLog -Pattern 'verify-data: all identical' -Quiet) {
    Write-Host "   ok ($((Select-String -Path $dataLog -Pattern ': identical').Count) data types identical to the original)" -ForegroundColor Green
}
else { $failures++; Write-Host "   problems:" -ForegroundColor Yellow; Select-String -Path $dataLog -Pattern 'differ|missing|exception|invalid|^   ' | ForEach-Object { Write-Host "   $($_.Line)" } }

# asset check: the built package (every original reachable by its old name, every index entry present,
# every file referenced by the data present)
Write-Host "== assets" -ForegroundColor Cyan
$assetLog = Join-Path $out 'assets.txt'
& dotnet $tools validate --pack $pack --source (Join-Path $root 'assets') | Out-File -Encoding utf8 $assetLog
if ($LASTEXITCODE -eq 0) { Write-Host "   ok ($((Get-Content $assetLog | Select-Object -Last 1)))" -ForegroundColor Green }
else { $failures++; Write-Host "   problems:" -ForegroundColor Yellow; Get-Content $assetLog | Select-Object -First 20 | ForEach-Object { Write-Host "   $_" } }

Get-ChildItem (Join-Path $PSScriptRoot '*.txt') | Where-Object { $_.BaseName -like "*$Filter*" -and $_.BaseName -ne 'README' } | ForEach-Object {
    $name = $_.BaseName
    Write-Host "== $name" -ForegroundColor Cyan
    $runDir = Join-Path $out $name
    if (Test-Path $runDir) { Remove-Item -Recurse -Force $runDir }
    New-Item -ItemType Directory -Force $runDir | Out-Null
    $log = Join-Path $out "$name.log"
    if (Test-Path $log) { Remove-Item $log }
    $extra = @(); if ($name -like 'ipad*') { $extra += '--ipad' }   # ipad_*.txt scripts run in the 1024x768 tablet layout
    $firstLine = Get-Content $_.FullName -TotalCount 1
    if ($firstLine -like '#!*') { $extra += @($firstLine.Substring(2).Trim() -split '\s+' | Where-Object { $_ }) }
    if (-not $RealTime) { $extra += @('--seed', "$Seed", '--fixed-step', '--docs', (Join-Path $runDir 'docs')) }
    & dotnet run --project $root --no-build -- @extra --exit-after 1500 --screenshot (Join-Path $runDir "$name.png") --script $_.FullName --log $log | Out-Null
    $problems = @(Select-String -Path $log -Pattern 'failed|xception' | Where-Object { $_.Line -notmatch 'Sound missing' } | ForEach-Object { $_.Line })
    if (-not (Select-String -Path $log -Pattern 'Music playing' -Quiet)) { $problems += 'no "Music playing" line in the log' }
    $hitTests = @(Select-String -Path $_.FullName -Pattern '^\s*hittest\b').Count
    if ($hitTests -gt 0) {
        $ran = @(Select-String -Path $log -Pattern 'hittest \[').Count
        if ($ran -lt $hitTests) { $problems += "only $ran of $hitTests hittest commands ran" }
    }

    if (-not $RealTime) {
        $dumps = Get-ChildItem $runDir -Filter '*.json' -ErrorAction SilentlyContinue
        $goldDir = Join-Path $golden $name
        if ($Record -and $dumps) {
            New-Item -ItemType Directory -Force $goldDir | Out-Null
            $dumps | ForEach-Object { Copy-Item $_.FullName (Join-Path $goldDir $_.Name) -Force }
            Write-Host "   recorded $($dumps.Count) golden dump(s)" -ForegroundColor DarkCyan
        }
        elseif (Test-Path $goldDir) {
            Get-ChildItem $goldDir -Filter '*.json' | ForEach-Object {
                $actual = Join-Path $runDir $_.Name
                if (-not (Test-Path $actual)) { $problems += "missing dump $($_.Name)" }
                else {
                    $a = Get-Content $actual; $g = Get-Content $_.FullName
                    $diff = Compare-Object $g $a
                    if ($diff) {
                        $first = ($diff | Select-Object -First 4 | ForEach-Object { "$($_.SideIndicator) $($_.InputObject.Trim())" }) -join ' | '
                        $problems += "dump $($_.Name) differs from the golden: $first"
                    }
                }
            }
        }

        # reference screenshots: tests\golden\<script>\<shot>.jpg (320 px wide)
        if ($RecordShots) {
            $shots = Get-ChildItem $runDir -Filter '*.png' | Where-Object { $_.BaseName -ne $name -and $_.BaseName -notlike '*.diff' }
            foreach ($shot in $shots) {
                & dotnet $tools compare-image --record --golden (Join-Path $goldDir ($shot.BaseName + '.jpg')) --actual $shot.FullName | Out-Null
            }
            if ($shots) { Write-Host "   recorded $(@($shots).Count) reference screenshot(s)" -ForegroundColor DarkCyan }
        }
        elseif (Test-Path $goldDir) {
            Get-ChildItem $goldDir -Filter '*.jpg' | ForEach-Object {
                $actual = Join-Path $runDir ($_.BaseName + '.png')
                $result = & dotnet $tools compare-image --golden $_.FullName --actual $actual --diff (Join-Path $runDir ($_.BaseName + '.diff.png'))
                if ($LASTEXITCODE -ne 0) { $problems += "screenshot $result" }
            }
        }
    }

    if ($problems.Count -gt 0) { $failures++; Write-Host "   problems:" -ForegroundColor Yellow; $problems | ForEach-Object { Write-Host "   $_" } }
    else { Write-Host "   ok" -ForegroundColor Green }
}

if ($failures -gt 0) { Write-Host "$failures test(s) with problems" -ForegroundColor Red; exit 1 }
Write-Host "all tests ok" -ForegroundColor Green
