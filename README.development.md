# Setting up a development environment

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
5. Navigate to the `vhvr-mod\Unity` folder inside your mod checkout.
6. Create a new subdirectory there named `build` and navigate into it.
7. Select that folder for the build. The Unity project's build output should appear in `vhvr-mod\Unity\build`.
8. Close Unity.

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
