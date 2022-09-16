using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using ValheimVRMod.Scripts;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Patches {

    /**
     * Set the Draw Percentage to how much the Bow String is Pulled
     */
    [HarmonyPatch(typeof(Humanoid), "GetAttackDrawPercentage")]
    class PatchGetAttackDrawPercentage {
        static bool Prefix(Humanoid __instance, ref float __result) {

            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls()) {
                return true;
            }

            if (EquipScript.getLeft() == EquipType.Bow && !VHVRConfig.RestrictBowDrawSpeed()) {
                __result = BowLocalManager.realLifePullPercentage;
                return false;
            }

            if (EquipScript.getRight() == EquipType.Fishing) {
                __result = FishingManager.attackDrawPercentage;
                return false;
            }

            return true;
        }

        static void Postfix(Humanoid __instance, ref float __result) {
            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls()) {
                return;
            }

            if (EquipScript.getLeft() == EquipType.Bow && VHVRConfig.RestrictBowDrawSpeed()) {
                // Since the attack draw percentage is not patched in the prefix, we need to clamp it here in case the real life pull percentage is smaller than the unpatched attack draw percentage.
                __result = Math.Min(__result, BowManager.realLifePullPercentage);
            }
        }
    }

    /**
        * Manipulate Position and Direction of the Arrow SpawnPoint
        */
    [HarmonyPatch(typeof(Attack), "GetProjectileSpawnPoint")]
    class PatchGetProjectileSpawnPoint {
        static bool Prefix(out Vector3 spawnPoint, out Vector3 aimDir, Humanoid ___m_character) {

            spawnPoint = Vector3.zero;
            aimDir = Vector3.zero;
            
            if (___m_character != Player.m_localPlayer || !VHVRConfig.UseVrControls()) {
                return true;
            }

            if (EquipScript.getLeft() == EquipType.Bow) {
                spawnPoint = BowLocalManager.spawnPoint;
                aimDir = BowLocalManager.aimDir;
                return false;
            }
            
            switch (EquipScript.getRight()) {

                case EquipType.Fishing:
                    spawnPoint = FishingManager.spawnPoint;
                    aimDir = FishingManager.aimDir;
                    return false;
                case EquipType.Spear:
                case EquipType.SpearChitin:
                case EquipType.ThrowObject:
                    spawnPoint = SpearManager.spawnPoint;
                    aimDir = SpearManager.aimDir;
                    return false;
                case EquipType.RuneSkyheim:
                    spawnPoint = VRPlayer.rightHand.transform.position;
                    aimDir = VRPlayer.rightPointer.rayDirection * Vector3.forward;
                    return false;
            }

            return true;

        }
    }

    /**
     * Remove Crosshair 
     */
    [HarmonyPatch(typeof(Hud), "UpdateCrosshair")]
    class PatchUpdateCrosshair {
        static void Prefix(ref float bowDrawPercentage) {
            if (VHVRConfig.UseVrControls())
            {
                bowDrawPercentage = 0;
            }
        }
    }
    
    /**
     * Remove attack animation by speeding it up. It only applies to attack moves,
     * because the original method switches it back to normal for other animations
     */
    [HarmonyPatch(typeof(CharacterAnimEvent), "FixedUpdate")]
    class PatchFixedUpdate {
        static void Prefix(Character ___m_character, ref Animator ___m_animator) {
            
            if (___m_character != Player.m_localPlayer || !VHVRConfig.UseVrControls()) {
                return;
            }
            
            ___m_animator.speed = 1000f;
        }
    }  
    
    /**
    * remove character facing and inaccuracy for projectile stuff
    */
    [HarmonyPatch(typeof(Attack), "FireProjectileBurst")]
    class PatchFireProjectileBurst {
        
        static void Prefix(ref Attack __instance, ref ItemDrop.ItemData ___m_ammoItem, Humanoid ___m_character) {
           
            if (___m_character != Player.m_localPlayer || !VHVRConfig.UseVrControls()) {
                return;
            }
            
            __instance.m_useCharacterFacing = false;
            __instance.m_launchAngle = 0;
            
            if (VHVRConfig.RestrictBowDrawSpeed()) {
                return;
            }

            __instance.m_projectileAccuracyMin = 0;
            if (___m_ammoItem != null) {
                ___m_ammoItem.m_shared.m_attack.m_projectileAccuracyMin = 0;   
            }
        }
    }
    
    /**
    * Fix RodTop to get the VR's transform instead of original model's transform
    */
    [HarmonyPatch(typeof(FishingFloat), "GetRodTop")]
    class PatchGetRodTop {
        
        static bool Prefix(ref Transform __result, Character owner) {

            if (owner != Player.m_localPlayer
                || FishingManager.fixedRodTop == null || !VHVRConfig.UseVrControls()) {
                return true;
            }

            __result = FishingManager.fixedRodTop.transform;
            return false;
        }
    }
    
        
    /**
     * Remove bow pulling animation
     */
    [HarmonyPatch(typeof(Player), "PlayerAttackInput")]
    class PatchPlayerAttackInput {
        
        private static MethodInfo SetBoolCall =
            AccessTools.Method(typeof(ZSyncAnimation), nameof(ZSyncAnimation.SetBool), new []{typeof(string), typeof(bool)});
        
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var original = new List<CodeInstruction>(instructions);
            var patched = new List<CodeInstruction>();
            if (!VHVRConfig.UseVrControls())
            {
                return original;
            }
            
            foreach (var instruction in original) {
    
                if (instruction.Calls(SetBoolCall)) {
                    patched[patched.Count - 1].opcode = OpCodes.Ldc_I4_0;
                }
    
                patched.Add(instruction);
                
            }
            return patched;
        }
    }
}
