using HarmonyLib;
using ValheimVRMod.Scripts;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Patches {

    [HarmonyPatch(typeof(Player), "Start")]
    class PatchPlayerAwake {
        public static void Postfix(Player __instance) {
            if (__instance == Player.m_localPlayer && !VHVRConfig.UseVrControls()) {
                return;
            }
            __instance.gameObject.AddComponent<VRPlayerSync>();
        }
    }
}