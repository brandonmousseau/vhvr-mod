# OpenXR plugin files

The files the mod ships for its OpenXR runtime (`VRRuntime = OpenXR`), checked in so that building the mod needs
neither the network nor a rebuild. `ValheimVRMod/make-release.cmd` copies them into the game and the release, and the
mod project references `Managed/Unity.XR.OpenXR.dll`.

From Unity's `com.unity.xr.openxr` package **1.19.0** (tarball SHA-1 `55edf9b26dd2bdec9d513fbcb0b9e4a9e5d790e9`),
licensed as described in `LICENSE.md` (copied from the package):

| File | Source |
| --- | --- |
| `Managed/Unity.XR.OpenXR.dll` | built from the package's `Runtime` sources against Valheim's DLLs and the mod's XR Management 4.0.1 |
| `Plugins/x86_64/UnityOpenXR.dll` | `Runtime/windows/x64/UnityOpenXR.dll`, as it is |
| `Plugins/x86_64/openxr_loader.dll` | `RuntimeLoaders/windows/x64/openxr_loader.dll` (Khronos loader), as it is |
| `UnitySubsystems/UnityOpenXR/UnitySubsystemsManifest.json` | `Runtime/UnitySubsystemsManifest.json`, as it is |

To refresh them (a new package version, or a Unity upgrade of the game), update the version and SHA-1 in
`scripts/Build-OpenXRPlugin.ps1` and run it; it writes here.
