using System;
using System.Collections;
using System.Runtime.CompilerServices;
using BepInEx;
using UnityEngine;
using ValheimVRMod.VRCore;
using ValheimVRMod.VRCore.UI;
using ValheimVRMod.Utilities;
using ValheimVRMod.Scripts;
using ValheimVRMod.Patches;

using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod
{
    [BepInPlugin("org.bepinex.plugins.valheimvrmod", "ValheimVR Mod", "0.10.5")]
    public class ValheimVRMod : BaseUnityPlugin
    {

        public static System.Version PLUGIN_VERSION { get { return _version; } }
        private static System.Version _version = null;
        public static bool failedToInitializeVR { get; private set; } = false;

        private GameObject vrPlayer;
        private GameObject vrGui;
        private GameObject BhapticsTactsuit;

        void Awake() {
            _version = Info.Metadata.Version;
            VHVRConfig.InitializeConfiguration(Config);
            LogInfo("Pre-release VHVR");
            if (!VHVRConfig.ModEnabled())
            {
                LogInfo("ValheimVRMod is disabled via configuration.");
                enabled = false;
                return;
            }
            Game.isModded = true;
            LogInfo("ValheimVR Mod Awakens!");
#if NONVRMODE
            LogInfo("Running non-VR mode companion mod!");
#endif
        }

        void Start()
        {
            StartValheimVR();
        }

        void Update()
        {
            // vrPlayer is only created once VR has been initialized and started.
            if (vrPlayer == null) {
                return;
            }

            if (Input.GetKeyDown(VHVRConfig.GetRecenterKey()))
            {
                VRManager.tryRecenter();
            }
#if DEBUG
            if (Input.GetKeyDown(KeyCode.Backslash))
            {
              //  dumpall();
            }
#endif
        }

        // Nothing here may mention a type from the VR assemblies, including in a branch that is not taken:
        // Mono compiles the whole method body on the first call and resolves the types it references, so
        // the VR half lives in StartVr(), which flat screen mode never calls and therefore never compiles.
        void StartValheimVR()
        {
            // Resolved before anything is patched so that the patch set and the session mode cannot disagree.
            bool nonVrPlayer = VHVRConfig.NonVrPlayer();

            HarmonyPatcher.DoFlatScreenSafePatching();

            if (nonVrPlayer)
            {
                if (!VRAssetManager.InitializeFlatScreenAssets())
                {
                    LogError("Problem initializing flat screen assets");
                }
                LogDebug("Non VR Mode Patching Complete.");
                return;
            }

            StartVr();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void StartVr()
        {
            HarmonyPatcher.DoVrPatching();

            if (!VRAssetManager.InitializeVrAssets())
            {
                LogError("Problem initializing VR Assets");
                FallBackToFlatScreenMode();
                return;
            }

            StartCoroutine(InitializeVRAndCreateRig());
        }

        // Reached only when the VR assemblies are installed but VR could not be started, so the VR patches
        // are already in place by now. They are left installed and neutralized by their own
        // VHVRConfig.NonVrPlayer() checks, which is why those runtime guards must stay even though the
        // patch set is now chosen up front.
        private static void FallBackToFlatScreenMode()
        {
            failedToInitializeVR = true;
            LogDebug("Non VR Mode Patching Complete.");
        }

        // VR startup comes in two phases: Initializing the XR SDK and SteamVR is slow but invisible to the
        // game: it creates no cameras and disables nothing, so it can run as early as possible. Creating the
        // VRPlayer rig is the half that takes over rendering, disabling the vanilla Main Camera that
        // Utils.GetMainCamera() resolves to, so it must not land in the middle of a cinematic.
        // There is deliberately nothing to wait for before initializing: this runs during the plugin's Start(),
        // which is well before the start scene and therefore FejdStartup are loaded, and the intro is held back
        // until the rig exists by IntroCinematicVrDelayPatch rather than by delaying initialization here.
        private IEnumerator InitializeVRAndCreateRig()
        {
            bool vrInitialized;
            try
            {
                vrInitialized = VRManager.InitializeVR();
            }
            catch (Exception e)
            {
                LogError("Exception while initializing VR: " + e);
                vrInitialized = false;
            }
            if (!vrInitialized)
            {
                LogError("Could not initialize VR.");
                FallBackToFlatScreenMode();
                yield break;
            }

            VRManager.StartVR();

            // A cinematic that is already playing captured the vanilla camera in CinematicsManager.m_mainCamera
            // and its Stop() would enable that camera again behind VRPlayer's back, while VRPlayer.enableCameras()
            // would meanwhile rebuild the VR camera it finds disabled. IntroCinematicVrDelayPatch normally holds
            // the startup intro until the rig exists so the intro is shown on the VRGUI instead, so this only
            // waits when that hold gave up and let the intro play flat.
            while (CinematicsManager.IsStartedPlaying())
            {
                yield return null;
            }

            // VRPlayer has to come first: VRGUI.Awake() installs its input module on EventSystem.current, and
            // before the start scene is loaded the only EventSystem in existence is the one on the SteamVR
            // player prefab that VRPlayer.Awake() instantiates.
            vrPlayer = new GameObject("VRPlayer");
            DontDestroyOnLoad(vrPlayer);
            vrPlayer.AddComponent<VRPlayer>();
            vrGui = new GameObject("VRGui");
            DontDestroyOnLoad(vrGui);
            vrGui.AddComponent<VRGUI>();
            if (VHVRConfig.RecenterOnStart())
            {
                VRManager.tryRecenter();
            }
            if (VHVRConfig.BhapticsEnabled())
            {
                BhapticsTactsuit = new GameObject("BhapticsTactsuit");
                DontDestroyOnLoad(BhapticsTactsuit);
                BhapticsTactsuit.AddComponent<BhapticsTactsuit>();
            }
        }

#if DEBUG
        void dumpall()
        {
            foreach (var o in GameObject.FindObjectsOfType<GameObject>())
            {
                LogDebug("Name + " + o.name + "   Layer = " + o.layer);
            }
        }
#endif
    }
}
