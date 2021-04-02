# VHVR-Mod
This is an experimental mod for the PC game Valheim that adds in native VR support using Unity and SteamVR with OpenVR.

## What's in this package?
### Unity Project
This project exists primarily as an asset generator to produce necessary AssetBundles used by the mod as well as a way to build SteamVR and Unity XR libraries necessary in the proper environment.

There are several assets being generated int he Unity package, but the most important include:
* SteamVR Player prefab: This prefab is a Unity GameObject hierarchy that includes allt he needed scripts to properly use SteamVR. It includes a Camera in the hierarchy that will be swapped out for the main game's camera. The camera is configured to use stereoscopic 3D displayed in the HMD.
* Unity and OpenVR assets: These are assets required to properly bootstrap Unity's XR engine when the game starts up.
* Amplify Occlusion graphics post-processor: Used as a higher performance substitute to in game SSAO processor

### ValheimVRMod C# Project
This project contains the bulk of the code for the mod. It includes classes/Unity MonoBehaviour scripts that the following functionality:
* initialize Unity's/OpenVR XR engine
* instantiate the SteamVR prefabs from the AssetBundles
* swap out the game's main camera with the VR camera and position it appropriately
* translates the game's GUI into VR
* implements motion controls (WIP)

### Requirements
This mod requires [BepInExPack Valheim](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/). BepInEx is a mod framework for Unity games that allows modders to inject their code into the game's runtime. It also includes [Harmony](https://harmony.pardeike.net/articles/intro.html), which is a tools used to patch existing methods in C# libraries.

Additionally, you need an HMD that supports OpenVR/SteamVR. I've only tested it using a Valve Index, but it should theoretically work on anything that meets those requirements. Please feel free to create issues if you find incompatibilities and I'll work on any problems related to specific headsets if possible.

### Other Info
This is an experimental mod and almost certainly will contain a bunch of bugs and glitches. Additionally, Valheim is currently an early access game, so there is a high probability that patches will be released for the game that break this mod. Please be patient as fixes are worked on and feel free to report any issues you find :)

It should only be played seated and with a mouse and keyboard (motion controls WIP!!!).

As Valheim was not made for VR, the implementation isn't going to be as comfortable as a built-for-VR game. If VR tends to make you feel queasy, then this mod probably isn't for you.
