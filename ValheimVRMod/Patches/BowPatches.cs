using System;
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
    
    [HarmonyPatch(typeof(Player), "FindHoverObject")]
    class PatchFindHoverObject {
        static bool Prefix(Player __instance, out GameObject hover, out Character hoverCreature,
            int ___m_interactMask, float ___m_maxInteractDistance, Transform ___m_eye) {
            
            hover = null;
            hoverCreature = null; 
            
            if (__instance != Player.m_localPlayer 
                || ! VRPlayer.isUsingBow()) {
                return true;
            }

            RaycastHit[] array = Physics.RaycastAll(BowManager.spawnPoint, BowManager.aimDir, 50f, ___m_interactMask);
            Array.Sort(array, (x, y) => x.distance.CompareTo(y.distance));
            foreach (RaycastHit raycastHit in array)
            {
                if (!(bool) (UnityEngine.Object) raycastHit.collider.attachedRigidbody || !raycastHit.collider.attachedRigidbody.gameObject != __instance.gameObject)
                {
                    if (hoverCreature == null)
                    {
                        Character character = (bool) (UnityEngine.Object) raycastHit.collider.attachedRigidbody ? raycastHit.collider.attachedRigidbody.GetComponent<Character>() : raycastHit.collider.GetComponent<Character>();
                        if (character != null)
                            hoverCreature = character;
                    }
                    if ((double) Vector3.Distance(___m_eye.position, raycastHit.point) >= ___m_maxInteractDistance)
                        break;
                    if (raycastHit.collider.GetComponent<Hoverable>() != null)
                    {
                        hover = raycastHit.collider.gameObject;
                        break;
                    }
                    if ((bool) (UnityEngine.Object) raycastHit.collider.attachedRigidbody)
                    {
                        hover = raycastHit.collider.attachedRigidbody.gameObject;
                        break;
                    }
                    hover = raycastHit.collider.gameObject;
                    break;
                }
            }

            return false;
        }
    }
}