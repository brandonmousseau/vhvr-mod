using HarmonyLib;
using ValheimVRMod.Scripts;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Patches {

    [HarmonyPatch(typeof(Player), "Start")]
    [FlatScreenSafe]
    class PatchPlayerAwake {
        public static void Postfix(Player __instance) {
            if (VHVRConfig.NonVrPlayer() && __instance == Player.m_localPlayer) {
                return;
            }
            __instance.gameObject.AddComponent<VRPlayerSync>();
        }
    }
}