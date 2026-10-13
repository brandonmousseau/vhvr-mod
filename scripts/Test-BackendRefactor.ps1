param([Parameter(Mandatory)][string]$ValheimDir)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$ValheimDir = (Resolve-Path -LiteralPath $ValheimDir).Path
$checks = Join-Path $repo 'tests/BackendRefactor'
$gameplay = Join-Path $checks 'Build/VHVR.Gameplay/bin/Release/net472/ValheimVRMod.dll'
$sdk = Join-Path $checks 'Build/SteamVR.Actions/bin/Release/net472'

# Build source against owned game/VR reference assemblies. No game files are written,
# no Unity editor post-build command is invoked, and no runtime is started.
& python (Join-Path $checks 'check_boundary.py')
if ($LASTEXITCODE) { throw 'Backend source boundary checks failed.' }
& dotnet build (Join-Path $checks 'Build/VHVR.Gameplay/VHVR.Gameplay.csproj') -c Release "-p:ValheimDir=$ValheimDir" "-p:VHVRSourceRoot=$repo" --nologo -v:q -clp:ErrorsOnly
if ($LASTEXITCODE) { throw 'Gameplay and original SteamVR source build failed.' }
& dotnet build (Join-Path $checks 'BackendRefactor.csproj') -c Release "-p:ValheimDir=$ValheimDir" "-p:GameplayAssembly=$gameplay" "-p:SteamVRManagedDir=$sdk" --nologo -v:q -clp:ErrorsOnly
if ($LASTEXITCODE) { throw 'Backend check runner build failed.' }
& dotnet (Join-Path $checks 'bin/Release/net8.0/BackendRefactor.dll') $ValheimDir $gameplay $sdk
if ($LASTEXITCODE) { throw 'Backend differential checks failed.' }
& git -C $repo diff --check
if ($LASTEXITCODE) { throw 'Whitespace errors in the refactor.' }
Write-Output ('Gameplay SHA-256: ' + (Get-FileHash -LiteralPath $gameplay -Algorithm SHA256).Hash)
