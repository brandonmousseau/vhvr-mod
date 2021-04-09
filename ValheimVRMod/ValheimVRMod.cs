using static ValheimVRMod.Utilities.LogUtils;

using BepInEx;
using UnityEngine;
using ValheimVRMod.VRCore;
using ValheimVRMod.VRCore.UI;
using ValheimVRMod.Utilities;
using ValheimVRMod.Patches;

namespace ValheimVRMod
{
    [BepInPlugin("org.bepinex.plugins.valheimvrmod", "ValheimVR Mod", "0.0.0.1")]
    public class ValheimVRMod : BaseUnityPlugin
    {
        // Instance of VRPlayer
        private GameObject vrPlayer;
        private GameObject vrGui;

        void Awake()
        {
            VVRConfig.InitializeConfiguration(Config);
            if (!VVRConfig.ModEnabled())
            {
                LogInfo("ValheimVRMod is disabled via configuration.");
                enabled = false;
                return;
            }
            LogInfo("ValheimVR Mod Awakens!");
            LogInfo("VHVR v0.1.6");
            StartValheimVR();
        }

        void Update()
        {
            if (Input.GetKeyDown(VVRConfig.GetRecenterKey()))
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
            if (VRManager.InitializeVR())
            {
                VRManager.StartVR();
                vrPlayer = new GameObject("VRPlayer");
                DontDestroyOnLoad(vrPlayer);
                VRPlayer playerComponent = vrPlayer.AddComponent<VRPlayer>();
                vrGui = new GameObject("VRGui");
                DontDestroyOnLoad(vrGui);
                vrGui.AddComponent<VRGUI>();
                if (VVRConfig.RecenterOnStart())
                {
                    VRManager.tryRecenter();
                }
            }
            else
            {
                LogError("Could not initialize VR.");
                enabled = false;
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
