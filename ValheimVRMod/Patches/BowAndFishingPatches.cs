using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Patches {

    /**
     * Set the Draw Percentage to how much the Bow String is Pulled
     */
    [HarmonyPatch(typeof(Humanoid), "GetAttackDrawPercentage")]
    class PatchGetAttackDrawPercentage {
        static bool Prefix(Humanoid __instance, ref float __result) {

            if (__instance != Player.m_localPlayer) {
                return true;
            }

            if (VRPlayer.isUsingFishingRod()) {
                __result = FishingManager.attackDrawPercentage;
                return false;
            }
            
            if (VRPlayer.isUsingBow()) {
                __result = BowManager.attackDrawPercentage;
                return false;
            }

            return true;
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
            
            if (___m_character != Player.m_localPlayer) {
                return true;
            }

            if (VRPlayer.isUsingBow()) {
                spawnPoint = BowManager.spawnPoint;
                aimDir = BowManager.aimDir;
                return false;
            }

            if (VRPlayer.isUsingFishingRod()) {
                spawnPoint = FishingManager.spawnPoint;
                aimDir = FishingManager.aimDir;
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
        static void Prefix(Player player, ref float bowDrawPercentage) {
            bowDrawPercentage = 0;
        }
    }
    
    /**
     * Remove shoot animation (its not working tho i guess)
     */
    [HarmonyPatch(typeof(Player), "PlayerAttackInput")]
    class PatchPlayerAttackInput {
        
        private static MethodInfo SetBoolCall =
            AccessTools.Method(typeof(ZSyncAnimation), nameof(ZSyncAnimation.SetBool), new []{typeof(string), typeof(bool)});
        
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var original = new List<CodeInstruction>(instructions);
            var patched = new List<CodeInstruction>();
            
            foreach (var instruction in original) {
    
                if (instruction.Calls(SetBoolCall)) {
                    patched[patched.Count - 1].opcode = OpCodes.Ldc_I4_0;
                }
    
                patched.Add(instruction);
                
            }
            return patched;
        }
    }
    
    /**
    * remove character facing and inaccuracy for projectile stuff
    */
    [HarmonyPatch(typeof(Attack), "FireProjectileBurst")]
    class PatchFireProjectileBurst {
        
        static void Prefix(ref Attack __instance, ref ItemDrop.ItemData ___m_ammoItem) {
           
            __instance.m_useCharacterFacing = false;
            __instance.m_launchAngle = 0; //maybe adjust this for fishing rod
            __instance.m_projectileAccuracyMin = 0;
            ___m_ammoItem.m_shared.m_attack.m_projectileAccuracyMin = 0;
        }
    }
    
    /**
    * Fix RodTop to get the VR's transform instead of original model's transform
    */
    [HarmonyPatch(typeof(FishingFloat), "GetRodTop")]
    class PatchGetRodTop {
        
        static bool Prefix(ref Transform __result, Character owner) {

            if (owner != Player.m_localPlayer
                || FishingManager.fixedRodTop == null) {
                return true;
            }

            __result = FishingManager.fixedRodTop.transform;
            return false;
        }
    }
}