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

            if (__instance != Player.m_localPlayer
                || !VRPlayer.isUsingBow()) {
                return true;
            }
            
            __result = BowManager.attackDrawPercentage;
            return false;

        }
    }
    
    /**
     * Manipulate Position and Direction of the Arrow SpawnPoint
     */
    [HarmonyPatch(typeof(Attack), "GetProjectileSpawnPoint")]
    class PatchGetProjectileSpawnPoint {
        static bool Prefix(out Vector3 spawnPoint, out Vector3 aimDir, Humanoid ___m_character) {

            if (___m_character != Player.m_localPlayer 
                || !VRPlayer.isUsingBow()) {
                spawnPoint = Vector3.zero;
                aimDir = Vector3.zero;
                return true;
            }
            
            spawnPoint = BowManager.spawnPoint;
            aimDir = BowManager.aimDir;
            return false;

        }
    }

    /**
     * Remove Crosshair for Bow
     */
    [HarmonyPatch(typeof(Hud), "UpdateCrosshair")]
    class PatchUpdateCrosshair {
        static void Prefix(Player player, ref float bowDrawPercentage) {

            if (player == Player.m_localPlayer 
                && VRPlayer.isUsingBow()) {
                bowDrawPercentage = 0;
            }
        }
    }
}