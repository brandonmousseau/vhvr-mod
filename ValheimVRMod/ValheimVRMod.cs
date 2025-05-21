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
    [BepInPlugin("org.bepinex.plugins.valheimvrmod", "ValheimVR Mod", "0.9.20")]
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
            if (VHVRConfig.NonVrPlayer()) {
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
            bool vrInitialized = false;
            if (!VHVRConfig.NonVrPlayer())
            {
                vrInitialized = VRManager.InitializeVR();
                if (!vrInitialized)
                {
                    LogError("Could not initialize VR.");
                    failedToInitializeVR = true;
                }
            }

            HarmonyPatcher.DoPatching();

            if (!VRAssetManager.Initialize())
            {
                LogError("Problem initializing VR Assets");
                vrInitialized = false;
            }

            if (!vrInitialized)
            {
                LogDebug("Non VR Mode Patching Complete.");
                return;
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
