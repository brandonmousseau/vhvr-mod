using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using ValheimVRMod.VRCore;
using ValheimVRMod.Utilities;
using Valve.VR.InteractionSystem;

namespace ValheimVRMod.Patches {
    [HarmonyPatch(typeof(Hand), "FixedUpdate")]
    class PatchDebug {

        static bool Prefix(Hand __instance, ref List<Hand.AttachedObject> ___attachedObjects) {
            if (VHVRConfig.NonVrPlayer())
            {
                return true;
            }
            if (__instance.currentAttachedObject == null) {
                return false;
            }
            
            if (__instance.currentAttachedObjectInfo.Value.interactable == null) {
                ___attachedObjects.RemoveAt(___attachedObjects.Count - 1);
                return false;   
            }
            
            return true;
        }
    }
    
    [HarmonyPatch(typeof(Character), "SetVisible")]
    class PatchFixVanishing {

        static bool Prefix(Player __instance) {
            if (VHVRConfig.NonVrPlayer()) {
                return true;
            }
            return __instance != Player.m_localPlayer;
        }
    }

    [HarmonyPatch(typeof(VirtualFrameBuffer), nameof(VirtualFrameBuffer.UpdateCurrentRenderScale))]
    class PatchUpdateCurrentRenderScale
    {
        static bool Prefix()
        {
            if (VHVRConfig.NonVrPlayer())
            {
                return true;
            }
            // A render scale less than 1 would cause the game world disappear in VR. Force it to be 1 since we do not support any other value.
            VirtualFrameBuffer.m_global3DRenderScale = 1f;
            return false;
        }
    }

    [HarmonyPatch(typeof(WaterVolume), nameof(WaterVolume.OnDestroy))]
    class WaterVolumeOnDestroyPatch
    {
        public static void Prefix(WaterVolume __instance, List<IWaterInteractable> ___m_inWater)
        {
            if (Player.m_localPlayer == null || VHVRConfig.NonVrPlayer())
            {
                return;
            }

            if (!___m_inWater.Contains((IWaterInteractable) Player.m_localPlayer)) {
                return;
            }

            // Make sure the player is removed from the water in VR mode
            // TODO: find out why this vanilla game logic is failing in VR sometimes and the player is stuck in swimming state in dungeons.
            if (Player.m_localPlayer.Decrement(LiquidType.Water) == 0)
            {
                Player.m_localPlayer.SetLiquidLevel(-10000f, LiquidType.Water, __instance);
            }
            ___m_inWater.Remove((IWaterInteractable)Player.m_localPlayer);
        }
    }

    [HarmonyPatch(typeof(Character), nameof(Character.SetLiquidLevel))]
    class LiquidLevelReporter
    {
        public static void Postfix(Character __instance, float level, LiquidType type, Component liquidObj)
        {
            if (Player.m_localPlayer == null)
            {
                return;
            }

            if (__instance == null)
            {
                LogUtils.LogError("Setting liquid level " + level + " " + type + " on null character");
                return;
            }

            if (__instance.gameObject != Player.m_localPlayer.gameObject)
            {
                return;
            }
        }
    }

    /**
     * Remove attack animation by speeding it up. It only applies to attack moves,
     * because the original method switches it back to normal for other animations
     */
    [HarmonyPatch(typeof(CharacterAnimEvent), nameof(CharacterAnimEvent.CustomFixedUpdate))]
    class PatchFixedUpdate
    {

        public static float lastSpeedUp = 1f;
        static void Prefix(Character ___m_character, ref Animator ___m_animator)
        {
            if (___m_character != Player.m_localPlayer || !VHVRConfig.UseVrControls())
            {
                return;
            }

            if (!EquipScript.shouldSkipAttackAnimation() || ___m_character.IsStaggering() || !VRPlayer.attachedToPlayer)
            {
                ___m_animator.speed = 1f;
                return;
            }

            if (___m_character.IsSitting() && !___m_character.m_attack && !___m_character.m_attackHold)
            {
                ___m_animator.speed = 1f;
                return;
            }

            if (___m_animator.speed != 1 && ___m_animator.speed != 1000)
            {
                lastSpeedUp = ___m_animator.speed;
            }
            ___m_animator.speed = 1000f;
        }
    }
}
