using HarmonyLib;
using ValheimVRMod.Utilities;
using ValheimVRMod.Scripts;

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
            if (__instance != Player.m_localPlayer || TactsuitVR.suitDisabled)
            {
                return;
            }
            TactsuitVR.PlaybackHaptics("Eating");
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
            if (TactsuitVR.suitDisabled || __instance.m_character != Player.m_localPlayer)
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
                case "$se_freezing_name":
                    EffectName = "Freezing";
                    break;
            }
            if (EffectName != "")
            {
                TactsuitVR.StartThreadHaptic(EffectName);
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
            if (TactsuitVR.suitDisabled || __instance.m_character != Player.m_localPlayer)
            {
                return;
            }
            string name = "";
            switch (__instance.m_name)
            {
                case "$se_puke_name":
                    name = "Vomit";
                    break;
                case "$se_poison_name":
                    name = "Poison";
                    break;
                case "$se_burning_name":
                    name = "Flame";
                    break;
                case "$se_freezing_name":
                    name = "Freezing";
                    break;
            }
            if (name != "")
            {
                TactsuitVR.StopThreadHaptic(name);
            }
        }
    }

    /**
     * When player is using guardian power
     */
    [HarmonyPatch(typeof(Player), "StartGuardianPower")]
    class Player_GuardianPower_Patch
    {
        public static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer || TactsuitVR.suitDisabled)
            {
                return;
            }
            TactsuitVR.PlaybackHaptics("SuperPower");
        }
    }
    /**
    * on arrow release
    */
    [HarmonyPatch(typeof(Attack), "OnAttackTrigger")]
    class Attack_ArrowThrowing_Patch
    {
        public static void Postfix(Attack __instance)
        {
            if (__instance.m_character != Player.m_localPlayer || TactsuitVR.suitDisabled)
            {
                return;
            }
            if (EquipScript.getLeft() == EquipType.Bow)
            {
                TactsuitVR.PlaybackHaptics(VHVRConfig.LeftHanded() ? "ArrowThrowLeft" : "ArrowThrowRight");
            }
        }
    }
}
