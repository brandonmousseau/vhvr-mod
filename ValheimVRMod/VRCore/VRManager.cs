using static ValheimVRMod.Utilities.LogUtils;

using UnityEngine;
using UnityEngine.XR;
using Unity.XR.OpenVR;
using Valve.VR;
using System.Collections.Generic;
using System.IO;
using UnityEngine.XR.Management;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.VRCore
{
    /**
     * VRManager is responsible for initializing/starting the OpenVR XRSDK and the SteamVR
     * interactions system for HMD/controller input.
     */
    class VRManager
    {
        public static bool InitializeVR()
        {
            // Need to PreInitialize actions before XRSDK
            // to ensure SteamVR_Input is enabled.
            LogDebug("PreInitializing SteamVR Actions...");
            SteamVR_Actions.PreInitialize();
            LogInfo("Initializing VR...");
            if (!InitXRSDK())
            {
                LogError("Failed to initialize VR!.");
                return false;
            }
            if (!InitializeSteamVR())
            {
                LogError("Problem initializing SteamVR");
                return false;
            }
            return true;
        }

        public static bool StartVR()
        {
            LogInfo("Starting VR...");
            return StartXRSDK();
        }

        public static bool InitializeSteamVR()
        {
            LogInfo("Initializing SteamVR...");
            SteamVR.Initialize();
            if (!(SteamVR.initializedState == SteamVR.InitializedStates.InitializeSuccess))
            {
                LogError("Problem Initializing SteamVR");
                return false;
            }
            if (!SteamVR_Input.initialized)
            {
                LogError("Problem Initializing SteamVR_Input");
                return false;
            }

            ApplicationManifestHelper.UpdateManifest(Path.Combine(Application.streamingAssetsPath, "valheim.vrmanifest"),
                                                    "steam.app.892970",
                                                    "https://steamcdn-a.akamaihd.net/steam/apps/892970/header.jpg",
                                                    "Valheim VR",
                                                    "VR mod for Valheim",
                                                    steamBuild: true,
                                                    steamAppId: 892970);
            return true;
        }

        private static bool StartXRSDK()
        {
            LogInfo("Starting XRSDK!");
            if (XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null
                && XRGeneralSettings.Instance.Manager.activeLoader != null)
            {
                XRGeneralSettings.Instance.Manager.StartSubsystems();
            } else
            {
                LogError("Error Starting XRSDK!");
                if (XRGeneralSettings.Instance == null)
                {
                    LogError("XRGeneralSettings Instance is null!");
                    return false;
                } else if (XRGeneralSettings.Instance.Manager == null)
                {
                    LogError("XRManager instance is null!");
                    return false;
                } else if (XRGeneralSettings.Instance.Manager.activeLoader == null)
                {
                    LogError("Active loader is null!");
                    return false;
                }
            }
            return true;
        }

        private static bool InitXRSDK()
        {
            XRGeneralSettings xrGeneralSettings = LoadXRSettingsFromAssetBundle();
            if (xrGeneralSettings == null)
            {
                LogInfo("XRGeneralSettings Instance is null!");
                return false;
            }
            LogDebug("Loaded XRGeneralSettings!");
            return InitializeXRSDKLoaders(xrGeneralSettings.Manager);
        }

        private static XRGeneralSettings LoadXRSettingsFromAssetBundle()
        {
            string xrManagerAssetPath = Path.Combine(Application.streamingAssetsPath, "xrmanager");
            LogDebug("Loading XR Settings from AssetBundle: " + xrManagerAssetPath);
            var assetBundle = AssetBundle.LoadFromFile(xrManagerAssetPath);
            foreach (var a in assetBundle.LoadAllAssets())
            {
                LogDebug("XRManagement Asset Loaded: " + a.name);
            }
            XRGeneralSettings instance = XRGeneralSettings.Instance;
            if (instance == null)
            {
                LogError("XRGeneralSettings Instance is null!");
                return null;
            }
            LogDebug("XRGeneralSettings Instance is non-null.");
            return instance;
        }

        private static bool InitializeXRSDKLoaders(XRManagerSettings managerSettings)
        {
            LogDebug("Initializing XRSDK Loaders...");
            if (managerSettings == null)
            {
                LogError("XRManagerSettings instance is null, cannot initialize loader.");
                return false;
            }
            int tries = 0;
            do
            {
                managerSettings.InitializeLoaderSync();
                tries++;
            } while ((managerSettings.activeLoader == null) && (tries < VHVRConfig.MaxVRInitializationTries()));
            if (managerSettings.activeLoader == null)
            {
                LogError("managerSettings.activeLoader is null after " + tries + " tries.");
                return false;
            }
            OpenVRSettings openVrSettings = OpenVRSettings.GetSettings(false);
            if (openVrSettings != null)
            {
                OpenVRSettings.MirrorViewModes mirrorMode = VHVRConfig.GetMirrorViewMode();
                LogInfo("Mirror View Mode: " + mirrorMode);
                openVrSettings.SetMirrorViewMode(mirrorMode);
            }
            LogDebug("Got non-null Active Loader.");
            return true;
        }

        public static void tryRecenter()
        {
            if (VRPlayer.ShouldPauseMovement)
            {
                return;
            }

            List<XRInputSubsystem> inputSubsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetInstances(inputSubsystems);
            foreach (var subsystem in inputSubsystems)
            {
                LogDebug("Recentering Input Subsystem: " + subsystem);
                subsystem.TryRecenter();
            }

            // Trigger recentering head position on player body
            VRPlayer.RequestRecentering();
            VRPlayer.RequestPelvisCaliberation();
            VRPlayer.vrPlayerInstance?.ResetRoomscaleCamera();
        }

        private static void PrintSteamVRSettings()
        {
            SteamVR_Settings settings = SteamVR_Settings.instance;
            if (settings == null)
            {
                LogWarning("SteamVR Settings are null.");
                return;
            }
            LogDebug("SteamVR Settings:");
            LogDebug("  actionsFilePath: " + settings.actionsFilePath);
            LogDebug("  editorAppKey: " + settings.editorAppKey);
            LogDebug("  activateFirstActionSetOnStart: " + settings.activateFirstActionSetOnStart);
            LogDebug("  autoEnableVR: " + settings.autoEnableVR);
            LogDebug("  inputUpdateMode: " + settings.inputUpdateMode);
            LogDebug("  legacyMixedRealityCamera: " + settings.legacyMixedRealityCamera);
            LogDebug("  mixedRealityCameraPose: " + settings.mixedRealityCameraPose);
            LogDebug("  lockPhysicsUpdateRateToRenderFrequency: " + settings.lockPhysicsUpdateRateToRenderFrequency);
            LogDebug("  mixedRealityActionSetAutoEnable: " + settings.mixedRealityActionSetAutoEnable);
            LogDebug("  mixedRealityCameraInputSource: " + settings.mixedRealityCameraInputSource);
            LogDebug("  mixedRealityCameraPose: " + settings.mixedRealityCameraPose);
            LogDebug("  pauseGameWhenDashboardVisible: " + settings.pauseGameWhenDashboardVisible);
            LogDebug("  poseUpdateMode: " + settings.poseUpdateMode);
            LogDebug("  previewHandLeft: " + settings.previewHandLeft);
            LogDebug("  previewHandRight: " + settings.previewHandRight);
            LogDebug("  steamVRInputPath: " + settings.steamVRInputPath);
        }

        private static void PrintOpenVRSettings()
        {
            OpenVRSettings settings = OpenVRSettings.GetSettings(false);
            if (settings == null)
            {
                LogWarning("OpenVRSettings are null.");
                return;
            }
            LogDebug("OpenVR Settings:");
            LogDebug("  StereoRenderingMode: " + settings.StereoRenderingMode);
            LogDebug("  InitializationType: " + settings.InitializationType);
            LogDebug("  ActionManifestFileRelativeFilePath: " + settings.ActionManifestFileRelativeFilePath);
            LogDebug("  MirrorView: " + settings.MirrorView);

        }

        private static void PrintUnityXRSettings()
        {
            LogDebug("Unity.XR.XRSettings: ");
            LogDebug("  enabled: " + XRSettings.enabled);
            LogDebug("  deviceEyeTextureDimension: " + XRSettings.deviceEyeTextureDimension);
            LogDebug("  eyeTextureDesc: " + XRSettings.eyeTextureDesc);
            LogDebug("  eyeTextureHeight: " + XRSettings.eyeTextureHeight);
            LogDebug("  eyeTextureResolutionScale: " + XRSettings.eyeTextureResolutionScale);
            LogDebug("  eyeTextureWidth: " + XRSettings.eyeTextureWidth);
            LogDebug("  gameViewRenderMode: " + XRSettings.gameViewRenderMode);
            LogDebug("  isDeviceActive: " + XRSettings.isDeviceActive);
            LogDebug("  loadedDeviceName: " + XRSettings.loadedDeviceName);
            LogDebug("  occlusionMaskScale: " + XRSettings.occlusionMaskScale);
            LogDebug("  renderViewportScale: " + XRSettings.renderViewportScale);
            LogDebug("  showDeviceView: " + XRSettings.showDeviceView);
            LogDebug("  stereoRenderingMode: " + XRSettings.stereoRenderingMode);
            LogDebug("  supportedDevices: " + XRSettings.supportedDevices);
            LogDebug("  useOcclusionMesh: " + XRSettings.useOcclusionMesh);
        }

    }
}
