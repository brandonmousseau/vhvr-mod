# Build without installing

The normal post-build script copies files into the game installation. To compile
without running that script, pass `-p:SkipPostBuild=true`:

```sh
dotnet build ValheimVRMod/ValheimVRMod.csproj -c Release -p:SkipPostBuild=true
```

The project follows upstream's `ValheimGameLibz` 1.0 and Unity reference
packages. For a staged build, `GameManagedDir` and `UnityBuildManagedDir` can
point to the installed game's managed assemblies and staged mod dependencies.
Use absolute directory paths with trailing separators.

### Build against an installed game

If the upstream `ValheimGameLibz` package is unavailable, use the opt-in local
reference mode. It keeps the normal upstream dependency choices unchanged and
does not copy anything into the game. Install Python 3 and .NET 8, then run:

```sh
python3 build/prepare-game-references.py \
  "/absolute/path/to/Valheim/valheim_Data/Managed" \
  "/absolute/path/to/build-references"

dotnet build ValheimVRMod/ValheimVRMod.csproj -c Release \
  -p:SkipPostBuild=true -p:UseInstalledGameAssemblies=true \
  -p:GameManagedDir="/absolute/path/to/Valheim/valheim_Data/Managed/" \
  -p:PublicizedGameManagedDir="/absolute/path/to/build-references/" \
  -p:UnityBuildManagedDir="/absolute/path/to/staged/Valheim_Data/Managed/"
```

On Windows use `py -3` instead of `python3` if needed. The preparation script
pins BepInEx AssemblyPublicizer 0.4.3 and strips method bodies from the references.
These DLLs are for compilation only; never install or distribute them. Regenerate
them when the game updates. The staged mod dependencies must include the matching
SteamVR DLL described below; an existing VHVR installation can supply those files.

### Regression checks

```sh
dotnet run --project tests/MaintenanceRegression -- /absolute/path/to/vhvr-mod
```

These tests compile the actual input patch and exercise the real asset lookup,
attack cooldown, slider logic, haptic scheduler, configuration parser, packet
layout, angular velocity and enemy-HUD/TAA/bow rewrites. Small Unity doubles make them runnable without game files. They do
not test rendering, headset tracking, Unity object lifetime, or physical haptics.

### SteamVR dependency from source

The v0.9.21 release's `SteamVR.dll` predates the ankle tracker roles used by the
current source. If reusing that release's other Unity dependencies, first build
the matching SteamVR assembly:

```sh
dotnet build build/SteamVR/SteamVR.csproj -c Release \
  -p:GameManagedDir="/absolute/path/to/Valheim/valheim_Data/Managed/" \
  -p:UnityBuildManagedDir="/absolute/path/to/staged/Valheim_Data/Managed/"
```

This compiles the checked-in SteamVR runtime sources against Unity 2019.4.21
reference assemblies, matching the asset project's Unity version. The helper
project also needs the staged XR, spatial-tracking, and Valve JSON dependencies,
and the game's `UnityEngine.UI.dll`. Copy the resulting
`build/SteamVR/bin/Release/net46/SteamVR.dll` into the staged dependency directory
before building VHVR with that directory as `UnityBuildManagedDir`.

A package for this branch needs that rebuilt `SteamVR.dll` plus the current
`Assets/StreamingAssets/SteamVR` bindings and the current asset bundles, alongside
the newly built `ValheimVRMod.dll`. Do not include the old `amplify_occlusion.dll`:
Valheim 1.0 supplies `AmplifyOcclusionEffect` itself. A successful build does not
validate the remaining in-headset HUD issues described in PR #720.
