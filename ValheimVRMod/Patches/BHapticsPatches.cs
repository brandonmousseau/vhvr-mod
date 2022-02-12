using HarmonyLib;
using System.Threading;
using BhapticsTactsuit;
using static ValheimVRMod.Utilities.LogUtils;

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
    [HarmonyPatch(typeof(StatusEffect), "TriggerStartEffects")]
    class StatusEffect_PukeStart_Patch
    {
        public static void Postfix(StatusEffect __instance)
        {
            if (TactsuitVR.Instance.suitDisabled)
            {
                return;
            }
            LogInfo("Status Effect Name Start: " + __instance.m_name);
            if (__instance.m_name == "__SE_Puke__")
            {
                Thread PukeThread = new Thread(() => TactsuitVR.Instance.ThreadHapticFunc("Vomit"));
                PukeThread.Start();
            }
                
        }
    }
    [HarmonyPatch(typeof(StatusEffect), "Stop")]
    class StatusEffect_PukeStop_Patch
    {
        public static void Postfix(StatusEffect __instance)
        {
            if (TactsuitVR.Instance.suitDisabled)
            {
                return;
            }
            LogInfo("Status Effect Name Stop: " + __instance.m_name);
            if (__instance.m_name == "__SE_Puke__")
            {
                TactsuitVR.Instance.StopThreadHaptic();
            }

        }
    }
}
