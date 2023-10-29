using System.Collections.Generic;
using HarmonyLib;
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
}
