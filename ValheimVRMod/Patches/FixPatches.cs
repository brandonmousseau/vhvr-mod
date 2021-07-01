using System.Collections.Generic;
using HarmonyLib;
using ValheimVRMod.Utilities;
using Valve.VR.InteractionSystem;

namespace ValheimVRMod.Patches {
    [HarmonyPatch(typeof(Hand), "FixedUpdate")]
    class PatchDebug {

        static bool Prefix(Hand __instance, ref List<Hand.AttachedObject> ___attachedObjects) {
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
}