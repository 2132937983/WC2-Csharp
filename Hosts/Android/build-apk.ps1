# Builds the Android APK and copies it to the repository root (WC2-android-arm64.apk).
# Usage: .\Hosts\Android\build-apk.ps1 [-Configuration Release] [-AndroidSdk C:\Android] [-JavaSdk "C:\Program Files\Java\jdk-17.0.2"]
# Requirements: .NET 9 SDK with the android workload (dotnet workload install android), Android SDK
# platform 35 + build-tools, JDK 17. The project file already points at C:\Android and the JDK 17
# folder when they exist; the parameters override that.
param(
    [string]$Configuration = "Release",
    [string]$AndroidSdk = "",
    [string]$JavaSdk = ""
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)   # the repository root
$project = Join-Path $PSScriptRoot 'WC2.Android.csproj'

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) { throw "dotnet was not found on PATH (install the .NET 9 SDK)" }
$workloads = & dotnet workload list 2>$null
if (-not ($workloads | Select-String -Pattern '^android' -Quiet)) {
    Write-Warning "The android workload is not installed: run 'dotnet workload install android'"
}

$props = @()
if ($AndroidSdk) { $props += "-p:AndroidSdkDirectory=$AndroidSdk" }
if ($JavaSdk) { $props += "-p:JavaSdkDirectory=$JavaSdk" }

Write-Host "== dotnet build -c $Configuration $project" -ForegroundColor Cyan
& dotnet build $project -c $Configuration @props
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE" }

$apk = Get-ChildItem (Join-Path $PSScriptRoot "bin\$Configuration\net9.0-android") -Filter '*-Signed.apk' | Select-Object -First 1
if (-not $apk) { throw "No signed APK found under bin\$Configuration\net9.0-android" }
$target = Join-Path $root 'WC2-android-arm64.apk'
Copy-Item $apk.FullName $target -Force
Write-Host ("== APK: {0} ({1:N0} bytes)" -f $target, (Get-Item $target).Length) -ForegroundColor Green
Write-Host "   install: adb install -r `"$target`""
