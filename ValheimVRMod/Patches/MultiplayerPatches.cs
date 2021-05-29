using HarmonyLib;
using ValheimVRMod.Scripts;

namespace ValheimVRMod.Patches {

    // [HarmonyPatch(typeof(Player), "Update")]
    // class PatchAwake {
    //     static void Postfix(Player __instance) {
    //         
    //         if (__instance == Player.m_localPlayer) {
    //             return; // only patch other players
    //         }
    //
    //         __instance.
    //         
    //         ZNetView netView = __instance.GetComponent<ZNetView>();
    //         netView.GetZDO().
    //         
    //         if ()
    //         __instance.gameObject.AddComponent<VrikCreator>();
    //     }
    // }
}