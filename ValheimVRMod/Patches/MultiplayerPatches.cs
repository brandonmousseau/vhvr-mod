using HarmonyLib;
using UnityEngine;
using ValheimVRMod.Scripts;

namespace ValheimVRMod.Patches {

    [HarmonyPatch(typeof(Player), "Awake")]
    class PatchAwake {
        static void Postfix(Player __instance) {
            if (__instance == Player.m_localPlayer) {
                return;
            }
            Debug.Log("PATCHING VR RPLAYER !");
            __instance.gameObject.AddComponent<VRPlayerSync>();
        }
    }
}