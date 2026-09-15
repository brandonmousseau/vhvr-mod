using System;
using System.Collections;
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
    [BepInPlugin("org.bepinex.plugins.valheimvrmod", "ValheimVR Mod", "0.10.0")]
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

        void StartValheimVR()
        {
            HarmonyPatcher.DoPatching();

            bool assetsInitialized = VRAssetManager.Initialize();
            if (!assetsInitialized)
            {
                LogError("Problem initializing VR Assets");
            }

            if (VHVRConfig.NonVrPlayer())
            {
                LogDebug("Non VR Mode Patching Complete.");
                return;
            }

            if (!assetsInitialized)
            {
                FallBackToFlatScreenMode();
                return;
            }

            // See StartupCinematicPatch for why VR is not initialized until the startup cinematic is over.
            StartCoroutine(InitializeVRAfterStartupCinematic());
        }

        private static void FallBackToFlatScreenMode()
        {
            failedToInitializeVR = true;
            LogDebug("Non VR Mode Patching Complete.");
        }

        private IEnumerator InitializeVRAfterStartupCinematic()
        {
            LogInfo("Waiting for the startup cinematic to finish before initializing VR...");
            // The intro cinematic that plays on first startup swaps the game over to its own camera:
            // CinematicsManager.Play() disables Utils.GetMainCamera() and CinematicsManager.Stop() enables it
            // again. Once VR is running that resolves to the VR camera (VHVR keeps the vanilla "Main Camera"
            // disabled), which leaves the start menu fighting VRPlayer.enableCameras() over who owns the
            // camera, and the video is not rendered in stereo either. So ValheimVRMod waits for the intro to
            // finish before initializing VR, letting it play on the flat screen with the vanilla camera.
            // FejdStartup.Start() starts the intro coroutine, whose first step already calls
            // CinematicsManager.Play(), so once Start() has returned CinematicsManager.IsStartedPlaying()
            // tells whether the intro is still playing. A finalizer is used so that an exception thrown from
            // Start() cannot leave VR waiting forever.
            // TODO: m_introOnNewWorld plays the same intro video via Game when a new world is created, after
            // VR is running, and breaks the VR camera the same way.
            while (!StartupCinematicPatch.hasFejdStartupStarted || CinematicsManager.IsStartedPlaying())
            {
                yield return null;
            }

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
