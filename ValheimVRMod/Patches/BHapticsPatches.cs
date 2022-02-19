using System;
using HarmonyLib;
using ValheimVRMod.Utilities;
using ValheimVRMod.Scripts;
using UnityEngine;

using static ValheimVRMod.Utilities.LogUtils;
using System.Collections.Generic;

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
     * When any player is using guardian power
     */
    [HarmonyPatch(typeof(Player), "ActivateGuardianPower")]
    class Player_GuardianPower_Patch
    {
        public static void Postfix(Player __instance)
        {
            if (TactsuitVR.suitDisabled)
            {
                return;
            }

            List<Player> list = new List<Player>();
            Player.GetPlayersInRange( __instance.transform.position, 10f, list);
            foreach (Player item in list)
            {
                if (item == Player.m_localPlayer)
                {
                    TactsuitVR.PlaybackHaptics("SuperPower");
                }
            }
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
                TactsuitVR.PlaybackHaptics(VHVRConfig.LeftHanded() ? "ArrowThrowLeft" : "ArrowThrowRight", 1.5f);
            }
        }
    }
    /**
    * on bow string pull
    */
    [HarmonyPatch(typeof(BowManager), "pullString")]
    class BowManager_pullString_Patch
    {
        public static void Postfix(float ___realLifePullPercentage)
        {
            if (TactsuitVR.suitDisabled)
            {
                return;
            }
            if (___realLifePullPercentage == 0)
            {
                return;
            }
            TactsuitVR.StartThreadHaptic(VHVRConfig.LeftHanded() ? "BowStringLeft" : "BowStringRight",
                ___realLifePullPercentage * 1.5f, true);
            // TODO ARMS TACTOSY
        }
    }
    /**
    * on bow string stop
    */
    [HarmonyPatch(typeof(BowLocalManager), "OnRenderObject")]
    class BowLocalManager_OnRenderObject_Patch
    {
        public static void Postfix(bool ___isPulling)
        {
            if (TactsuitVR.suitDisabled)
            {
                return;
            }
            if (!___isPulling)
            {
                TactsuitVR.StopThreadHaptic(VHVRConfig.LeftHanded() ? "BowStringLeft" : "BowStringRight");
            }
        }
    }
    /**
    * on getting arrow from your back
    */
    [HarmonyPatch(typeof(BowLocalManager), "toggleArrow")]
    class BowLocalManager_toggleArrow_Patch
    {
        public static void Prefix(GameObject ___arrow)
        {
            if (TactsuitVR.suitDisabled)
            {
                return;
            }
            if (___arrow != null)
            {
                TactsuitVR.PlaybackHaptics(VHVRConfig.LeftHanded() ?
                    "HolsterArrowLeftShoulder" : "HolsterArrowRightShoulder");
                return;
            }
            var ammoItem = Player.m_localPlayer.GetAmmoItem();
            if (ammoItem == null || ammoItem.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Ammo)
            {
                // out of ammo
                return;
            }
            TactsuitVR.PlaybackHaptics(VHVRConfig.LeftHanded() ?
               "UnholsterArrowLeftShoulder" : "UnholsterArrowRightShoulder");
        }
    }

    /**
     * Player low Health
     */
    [HarmonyPatch(typeof(Character), "SetHealth")]
    class Character_LowHealth_Patch
    {
        public static void Postfix(Character __instance)
        {
            if (__instance != Player.m_localPlayer || TactsuitVR.suitDisabled)
            {
                return;
            }
            int hlth = Convert.ToInt32(__instance.GetHealth() * 100 / __instance.GetMaxHealth());
            switch (hlth)
            {
                case int n when hlth < 20 && hlth > 15:
                    TactsuitVR.StopThreadHaptic("HeartBeatFast");
                    TactsuitVR.StartThreadHaptic("HeartBeat");
                    break;
                case int n when hlth <= 15 && hlth > 0:
                    TactsuitVR.StopThreadHaptic("HeartBeat");
                    TactsuitVR.StartThreadHaptic("HeartBeatFast", 1.0f, false, 3.0f);
                    break;
                case int n when hlth <= 0:
                    TactsuitVR.StopThreadHaptic("HeartBeat");
                    TactsuitVR.StopThreadHaptic("HeartBeatFast");
                    TactsuitVR.PlaybackHaptics("Dying");
                    break;
                default:
                    TactsuitVR.StopThreadHaptic("HeartBeat");
                    TactsuitVR.StopThreadHaptic("HeartBeatFast");
                    break;
            }
        }
    }

    /**
     * On blocking with shield
     */
    [HarmonyPatch(typeof(Humanoid), "BlockAttack")]
    class Humanoid_BlockAttack_Patch
    {
        public static void Postfix(Humanoid __instance, bool __result)
        {

            if (__instance != Player.m_localPlayer || EquipScript.getLeft() != EquipType.Shield || !VHVRConfig.UseVrControls())
            {
                return;
            }
            if (__result)
            {
                TactsuitVR.PlaybackHaptics(VHVRConfig.LeftHanded() ?
                    "BlockVest_R" : "BlockVest_L");
                // TODO ARMS TACTOSY
            }
        }
    }

    /**
     * OnDeath player, kill all haptics threads just in case
     */
    [HarmonyPatch(typeof(Player), "OnDeath")]
    class Player_OnDeath_Patch
    {
        public static void Prefix(Player __instance)
        {

            if (__instance != Player.m_localPlayer || TactsuitVR.suitDisabled)
            {
                return;
            }
            TactsuitVR.PlaybackHaptics("Death");
        }
        public static void Postfix(Player __instance)
        {

            if (__instance != Player.m_localPlayer || TactsuitVR.suitDisabled)
            {
                return;
            }
            TactsuitVR.StopThreads();
        }
    }

    /**
     * OnRespawn, turns manual event on just in case
     */
    [HarmonyPatch(typeof(Player), "OnRespawn")]
    class Player_OnRespawn_Patch
    {
        public static void Postfix(Player __instance)
        {

            if (__instance != Player.m_localPlayer || TactsuitVR.suitDisabled)
            {
                return;
            }
            TactsuitVR.StartManuelResetEvent();
        }
    }

    /**
     * DoMeleeAttack
     */
    /*[HarmonyPatch(typeof(Attack), "DoMeleeAttack")]
    class Attack_DoMeleeAttack_Patch
    {
        public static void Postfix(Attack __instance)
        {

            if (__instance != Attack.m_localPlayer || TactsuitVR.suitDisabled)
            {
                return;
            }
            TactsuitVR.StartManuelResetEvent();
        }
    }*/

    /**
     * OnTriggerEnter Attack succeded against any object with any weapon
     */
    [HarmonyPatch(typeof(WeaponCollision), "OnTriggerEnter")]
    class WeaponCollision_OnTriggerEnter_Patch
    {
        public static void Postfix(WeaponCollision __instance, bool ___hasAttackedSuccess)
        {

            if ( !___hasAttackedSuccess || TactsuitVR.suitDisabled)
            {
                return;
            }
            TactsuitVR.SwordRecoil(!VHVRConfig.LeftHanded());
        }
    }
}