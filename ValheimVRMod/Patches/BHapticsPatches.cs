using HarmonyLib;
using BhapticsTactsuit;

namespace ValheimVRMod.Patches
{
    [HarmonyPatch(typeof(Player), "EatFood")]
    class Player_EatingFood_Patch
    {
        public static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer || TactsuitVR.Instance.suitDisabled)
            {
                return;
            }
            TactsuitVR.Instance.PlaybackHaptics("Eating");
        }
    }
}
