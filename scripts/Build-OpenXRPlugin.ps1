# Refreshes the checked-in OpenXR plugin files the mod ships for its OpenXR runtime (VRRuntime = OpenXR) in
# Unity\OpenXR, where make-release.cmd copies them from. Building the mod does not need this; run it only to move to
# another package version (update $package below and Unity\OpenXR\README.md), then commit the changed files:
#   Managed\Unity.XR.OpenXR.dll                                     built from the package sources (see OpenXRPlugin\)
#   Plugins\x86_64\UnityOpenXR.dll, Plugins\x86_64\openxr_loader.dll taken from the package as they are
#   UnitySubsystems\UnityOpenXR\UnitySubsystemsManifest.json         taken from the package as it is
# The package is fetched from Unity's package registry at a pinned version and checked against its SHA-1. Needs the
# game installed (the assembly is built against its DLLs) and the mod's Unity project built (for XR Management).
param(
    [string]$ValheimDir = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim',
    [switch]$Force
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$project = Join-Path $PSScriptRoot 'OpenXRPlugin'
$packagesDir = Join-Path $project 'packages'
$outDir = Join-Path $repo 'Unity\OpenXR'

# OpenXR 1.19 is a line made for Unity 6000.0, which Valheim runs. It is built against the XR Management the mod
# already ships (4.0.1), not the 4.4 it asks for: it uses nothing newer. Of its other dependencies only the Input
# System is referenced by the runtime assembly, and Valheim has its own (1.19).
$package = @{ Name = 'com.unity.xr.openxr'; Version = '1.19.0'; Sha1 = '55edf9b26dd2bdec9d513fbcb0b9e4a9e5d790e9' }

$managed = Join-Path $ValheimDir 'valheim_Data\Managed'
if (-not (Test-Path -LiteralPath (Join-Path $managed 'UnityEngine.CoreModule.dll'))) {
    throw "Valheim's managed DLLs are not in $managed; pass -ValheimDir."
}
$xrManagement = Join-Path $repo 'Unity\build\ValheimVR_Data\Managed\Unity.XR.Management.dll'
if (-not (Test-Path -LiteralPath $xrManagement)) {
    throw "$xrManagement is missing: build the Unity project first (see README.development.md)."
}

New-Item -ItemType Directory -Force $packagesDir | Out-Null
$dir = Join-Path $packagesDir $package.Name
$stamp = Join-Path $dir '.version'
if ($Force -or -not (Test-Path -LiteralPath $stamp) -or (Get-Content -LiteralPath $stamp) -ne $package.Version) {
    $tarball = Join-Path $packagesDir "$($package.Name)-$($package.Version).tgz"
    $url = "https://download.packages.unity.com/$($package.Name)/-/$($package.Name)-$($package.Version).tgz"
    Write-Output "Downloading $url"
    Invoke-WebRequest -Uri $url -OutFile $tarball -UseBasicParsing
    $sha1 = (Get-FileHash -LiteralPath $tarball -Algorithm SHA1).Hash.ToLowerInvariant()
    if ($sha1 -ne $package.Sha1) {
        Remove-Item -LiteralPath $tarball
        throw "$($package.Name) $($package.Version): SHA-1 $sha1, expected $($package.Sha1)."
    }
    if (Test-Path -LiteralPath $dir) { Remove-Item -Recurse -Force -LiteralPath $dir }
    New-Item -ItemType Directory -Force $dir | Out-Null
    & tar -xzf $tarball -C $dir
    if ($LASTEXITCODE) { throw "Could not unpack $tarball." }
    Remove-Item -LiteralPath $tarball
    Set-Content -LiteralPath $stamp -Value $package.Version
}

# Directory properties end in '/': a '\' before the closing quote of a quoted argument would escape the quote.
$managedDirArg = $managed.TrimEnd('\') + '/'
& dotnet build (Join-Path $project 'Unity.XR.OpenXR.csproj') -c Release --nologo -v:q -clp:ErrorsOnly `
    "-p:ValheimManagedDir=$managedDirArg" "-p:PackagesDir=$packagesDir/" "-p:XRManagementDll=$xrManagement"
if ($LASTEXITCODE) { throw 'Building the OpenXR plugin failed.' }

$openXr = Join-Path $dir 'package'
$files = @(
    @{ From = Join-Path $project 'bin\Unity.XR.OpenXR.dll'; To = 'Managed' },
    @{ From = Join-Path $openXr 'Runtime\windows\x64\UnityOpenXR.dll'; To = 'Plugins\x86_64' },
    @{ From = Join-Path $openXr 'RuntimeLoaders\windows\x64\openxr_loader.dll'; To = 'Plugins\x86_64' },
    @{ From = Join-Path $openXr 'Runtime\UnitySubsystemsManifest.json'; To = 'UnitySubsystems\UnityOpenXR' }
)
foreach ($file in $files) {
    $to = Join-Path $outDir $file.To
    New-Item -ItemType Directory -Force $to | Out-Null
    Copy-Item -LiteralPath $file.From -Destination $to -Force
}
Write-Output "OpenXR plugin files are in $outDir"
