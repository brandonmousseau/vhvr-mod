# Setting up a development environment

Build VHVR's Unity assets and plugin by following the
[environment setup](#setting-up-the-environment) and [mod build](#build-the-mod)
steps below.

## Run automated checks

Install Git, Python 3 (available as `python`), and .NET SDK 8 or newer with the
.NET 8 runtime. Use an owned Valheim installation with BepInEx and the managed
VHVR reference libraries, including FinalIK, OpenVR XR and bHaptics, available in
`Valheim_Data\Managed`. Keep the tracked `Unity/ValheimVR/Assets/SteamVR` sources
and `Unity/ValheimVR/Assets/StreamingAssets/SteamVR` in the checkout, including when
using a sparse checkout.

From the repository root, run this in PowerShell with your Valheim path:

```powershell
./scripts/Test-BackendRefactor.ps1 -ValheimDir 'C:\Program Files (x86)\Steam\steamapps\common\Valheim'
```

The projects in `tests/BackendRefactor` are the build harness and automated test
suite. The command builds the gameplay and bundled SteamVR SDK, creates the
SteamVR actions from the action catalog, then checks provider boundaries, the
catalog against `actions.json`, managed input snapshots, callbacks and haptic
action identity. It uses a .NET Framework 4.7.2 diagnostic
build; the production plugin's .NET Framework 4.6 build remains described below.
Outputs stay in ignored `bin`/`obj` directories; nothing is deployed to the game
or included in release packaging. Tracking, rendering and hardware haptics need
separate testing in SteamVR.

### Adding an input action

Input actions are listed once, in `ValheimVRMod/VRCore/Backends/VRInputActions.cs`,
and the SteamVR backend creates them from that list at startup. Adding one takes:

1. an entry in `VRInputActions.Actions` (and a property, if the mod reads it);
2. the same action in `Unity/ValheimVR/Assets/StreamingAssets/SteamVR/actions.json`
   (type, requirement, English name) and in the default bindings that should bind it.

No Unity rebuild is needed: the generated `SteamVR_Actions.dll` is no longer used.
The mod logs an error at startup for any difference between the list and
`actions.json`, and the test suite above fails on one.

## Who is this for?

## Software you will need
* Valheim (obviously)
* [Microsoft Visual Studio 2019](https://visualstudio.microsoft.com/vs/) ([Details](#vs-install))
* [Unity](https://unity3d.com/get-unity/download) ([Details](#unity-install))
* [BepInExPack Valheim](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/). Already installed if you installed the mod.
* [Bepinex Publicizer](https://github.com/MrPurple6411/Bepinex-Tools/releases) ([Details](#publicizer-install))

## Setting up the environment
### Prerequisites
1. Make sure all the software above is installed (see the Details for tips).
2. Run Valheim once.
   * This will generate the publicized DLLs using Bepinex Publicizer. In your Valheim install folder, it should create `valehim_Data\Managed\publicized_assemblies`.

### Checkout and configure the source
1. Open Visual Studio, and choose `Clone a Repository`, then enter this repo's checkout URL, e.g.
   https://github.com/brandonmousseau/vhvr-mod.git
2. Update the CommonDir to point to your Valheim install:
    1. In the Visual Studio Solution Explorer, browse to `vhvr-mod\ValheimVRMod`.
    2. Right-click `ValheimVRMod.csproj`, and Open With -> Source Code (Text) Editor. 
    3. Find the following line and update the path to point to your Steam library folder containing Valheim:
    ```
     <CommonDir>C:\Program Files (x86)\Steam\steamapps\common\</CommonDir>
    ```
    3. Save the file and close Visual Studio.

### Build the Unity assets
1. In Unity Hub, go to Projects -> Add. 
2. Navigate to wherever you checked the source out above, and choose the `Unity\ValheimVR` folder.
3. Click the newly added project to open it in Unity.
4. Go to File -> Build Settings, then click Build.
5. Navigate to the `vhvr-mod\Unity\build` folder inside your mod checkout.
6. Select the folder for the build. The Unity project's build output should appear in `vhvr-mod\Unity\build`.
7. Close Unity.

## Build the mod
1. Open Visual Studio, and choose "Open a project or solution"
2. Navigate to the the mod source folder, then `ValheimVRMod\ValheimVRMod.sln`.
3. Make sure the release settings in the toolbar show: "Debug" and "Any CPU".
4. Click Build -> Build Solution.
    * The mod will be built and installed to your Valheim directory. Check for the presence of `BepInEx\plugins\ValheimVRMod.dll`

<hr>

## Installation details

### Visual Studio Installation Details {#vs-install}
Note: this isn't meant to be comprehensive, and ideally you've installed and worked with other IDEs before. I find
it useful to have some reminders though especially when I'm not in my default environement.

1. Install [Visual Studio 2019 Community Edition](https://visualstudio.microsoft.com/vs/).
2. During install, it will prompt to choose some Workloads. I chose `.NET
   desktop development`, `Desktop development with C++`, and `Game development
   with Unity`. You can also add any of these later by redownloading/rerunning the installer and choosing "Modify".

### Unity Installation Details {#unity-install}
1. You'll need a [Unity ID](https://id.unity.com/account/new). This requires
   email verification and so forth, so best to get it out of the way first.
2. Download [Unity Hub](https://unity3d.com/get-unity/download), and log in.
3. Install [Unity.2019.4.21](unityhub://2019.4.21f1/b76dac84db26). If clicking that doesn't work, go to the
   [Unity Archive](https://unity3d.com/get-unity/download/archive), then choose the `Unity 2019.x` tab at the
   top, then `Unity.2019.4.21` and click the `Unity Hub` button.

### Bepinex Publicizer Details {#publicizer-install}

This is a easier-to-use take on [Assembly Publicizer](https://github.com/CabbageCrow/AssemblyPublicizer),
but feel free to go that route if you're more comfortable with it. This has the advantage of re-publicizing the assemblies
when Valheim is updated.

1. Download the zip of the [most recent release](https://github.com/MrPurple6411/Bepinex-Tools/releases/latest).
2. Unzip into your Valheim install folder; it should create a folder `BepInEx\plugins\Bepinx-Publicizer` with a couple of
   DLLs in it.
