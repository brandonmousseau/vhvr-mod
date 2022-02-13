using HarmonyLib;
using System.Threading;
using BhapticsTactsuit;
using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.Patches
{
    /**
     * When player is eating food
     */
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

    /**
     * When a status Effect starts, creates and starts thread
     */
    [HarmonyPatch(typeof(StatusEffect), "TriggerStartEffects")]
    class StatusEffect_Start_Patch
    {
        public static void Postfix(StatusEffect __instance)
        {
            if (TactsuitVR.Instance.suitDisabled)
            {
                return;
            }
            string EffectName = "";
            switch (__instance.m_name)
            {
                case "$se_puke_name":
                    EffectName = "Vomit";
                    break;
                case "$se_poison_name":
                    EffectName = "Poison";
                    break;
                case "$se_burning_name":
                    EffectName = "Flame";
                    break;
            }
            if (EffectName != "")
            {
                TactsuitVR.Instance.StartThreadHaptic(EffectName);
            } 
        }
    }

    /**
     * When a statusEffect stops, stops thread corresponding to effect name
     */
    [HarmonyPatch(typeof(StatusEffect), "Stop")]
    class StatusEffect_Stop_Patch
    {
        public static void Postfix(StatusEffect __instance)
        {
            if (TactsuitVR.Instance.suitDisabled)
            {
                return;
            }
            switch (__instance.m_name)
            {
                case "$se_puke_name":
                    TactsuitVR.Instance.StopThreadHaptic("Vomit");
                    break;
                case "$se_poison_name":
                    TactsuitVR.Instance.StopThreadHaptic("Poison");
                    break;
                case "$se_burning_name":
                    TactsuitVR.Instance.StopThreadHaptic("Flame");
                    break;
            }
        }
    }
}
