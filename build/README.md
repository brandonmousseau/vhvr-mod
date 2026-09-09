# Build without installing

The normal post-build script copies files into the game installation. To compile
without running that script, pass `-p:SkipPostBuild=true`:

```sh
dotnet build ValheimVRMod/ValheimVRMod.csproj -c Release -p:SkipPostBuild=true
```

The project follows upstream's `ValheimGameLibz` 1.0 and Unity reference
packages. For a staged build, `GameManagedDir` and `UnityBuildManagedDir` can
point to the installed game's managed assemblies and staged mod dependencies.
Use absolute directory paths with trailing separators. The previous
`PublicizedGameManagedDir` override is no longer needed.

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
