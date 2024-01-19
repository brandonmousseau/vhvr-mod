using System;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using ValheimVRMod.Scripts;
using ValheimVRMod.Utilities;
using System.Collections.Generic;
using System.Reflection.Emit;
using Pose = ValheimVRMod.Utilities.Pose;


namespace ValheimVRMod.Patches
{
    
    [HarmonyPatch(typeof(Humanoid), "HideHandItems")]
    class PatchHideHandItems
    {
        
        private static MethodInfo setupVisEquipmentMethod = AccessTools.Method(typeof(Humanoid), "SetupVisEquipment");
        private static bool allowHidingDominantHandItem = true;
        private static bool allowHidingNonDominantHandItem = true;

        public static void HideLocalPlayerHandItem(bool isDominantHand) {
            allowHidingDominantHandItem = isDominantHand;
            allowHidingNonDominantHandItem = !isDominantHand;
            Player.m_localPlayer.HideHandItems();
        }

        static bool Prefix(ref Humanoid __instance,
            ref ItemDrop.ItemData ___m_leftItem, ref ItemDrop.ItemData ___m_rightItem, 
            ref ItemDrop.ItemData ___m_hiddenLeftItem, ref ItemDrop.ItemData ___m_hiddenRightItem, 
            ref VisEquipment ___m_visEquipment, ref ZSyncAnimation ___m_zanim) {

            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls()) {
                return true;
            }
            
            bool hideDominantHandItem = allowHidingDominantHandItem;
            bool hidingNonDominantHandItem = allowHidingNonDominantHandItem;

            allowHidingDominantHandItem = true;
            allowHidingNonDominantHandItem = true;
    
            if (___m_leftItem == null && ___m_rightItem == null) {
                return false;   
            }
    
            if (hidingNonDominantHandItem) {
                ItemDrop.ItemData leftItem = ___m_leftItem;
                __instance.UnequipItem(___m_leftItem);
                ___m_hiddenLeftItem = leftItem;
            }
    
            if (hideDominantHandItem) {
                ItemDrop.ItemData rightItem = ___m_rightItem;
                __instance.UnequipItem(___m_rightItem);
                ___m_hiddenRightItem = rightItem;                
            }
            
            setupVisEquipmentMethod.Invoke(__instance, new object[]{___m_visEquipment, false});
            ___m_zanim.SetTrigger("equip_hip");
    
            return false;
        }
    }
    
     
    [HarmonyPatch(typeof(Humanoid), "ShowHandItems")]
    class PatchShowHandItems
    {
        private static bool allowShowingDominantHandItem = true;
        private static bool allowShowingNonDominantHandItem = true;

        public static void ShowLocalPlayerHandItem(bool isDominantHand)
        {
            allowShowingDominantHandItem = isDominantHand;
            allowShowingNonDominantHandItem = !isDominantHand;
            Player.m_localPlayer.ShowHandItems();
        }

        static bool Prefix(ref Humanoid __instance,
            ref ItemDrop.ItemData ___m_hiddenLeftItem, ref ItemDrop.ItemData ___m_hiddenRightItem,
            ref ZSyncAnimation ___m_zanim)
        {
            
            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls()) {
                return true;
            }
    
            bool showDominantHandItem = allowShowingDominantHandItem;
            bool showNonDominantHandItem = allowShowingNonDominantHandItem;

            allowShowingDominantHandItem = true;
            allowShowingNonDominantHandItem = true;
            
            if (___m_hiddenLeftItem == null && ___m_hiddenRightItem == null) {
                return false;   
            }
    
            if (showNonDominantHandItem) {
                ItemDrop.ItemData hiddenLeftItem =___m_hiddenLeftItem;
                ___m_hiddenLeftItem = null;
                if (hiddenLeftItem != null) {
                    var item = ___m_hiddenRightItem;
                    __instance.EquipItem(hiddenLeftItem);
                    ___m_hiddenRightItem = item;
                    __instance.SetupVisEquipment(__instance.m_visEquipment, false);
                }
            }
    
            if (showDominantHandItem)
            {
                ItemDrop.ItemData hiddenRightItem = ___m_hiddenRightItem;
                ___m_hiddenRightItem = null;
                if (hiddenRightItem != null) {
                    var item = ___m_hiddenLeftItem;
                    __instance.EquipItem(hiddenRightItem);
                    ___m_hiddenLeftItem = item;
                    __instance.SetupVisEquipment(__instance.m_visEquipment, false);
                }
            }
            
            ___m_zanim.SetTrigger("equip_hip");
            
            return false;
        }
    }

    [HarmonyPatch(typeof(Inventory), "Changed")]
    class PatchInventoryChanged {

        static void Postfix() {
            if (StaticObjects.rightHandQuickMenu != null && VHVRConfig.UseVrControls()) {
                StaticObjects.rightHandQuickMenu.GetComponent<RightHandQuickMenu>()?.refreshItems();
                StaticObjects.leftHandQuickMenu.GetComponent<LeftHandQuickMenu>()?.refreshItems();
            }
        }
    }
    
    [HarmonyPatch(typeof(InventoryGui), "OnSelectedItem")]
    class PatchOnSelectedItem {

        static void Postfix() {
            if (StaticObjects.rightHandQuickMenu != null && VHVRConfig.UseVrControls()) {
                StaticObjects.rightHandQuickMenu.GetComponent<RightHandQuickMenu>().refreshItems();
                StaticObjects.leftHandQuickMenu.GetComponent<LeftHandQuickMenu>().refreshItems();
            }
        }
    }

    [HarmonyPatch(typeof(Humanoid), "UnequipItem")]
    class PatchUnEquipItem {

        static void Prefix(Humanoid __instance, ItemDrop.ItemData item, ItemDrop.ItemData ___m_leftItem, ItemDrop.ItemData ___m_rightItem) {

            if (__instance.GetType() != typeof(Player)) {
                return;
            }

            var vrPlayerSync = __instance.GetComponent<VRPlayerSync>();
            
            if (vrPlayerSync != null && (__instance != Player.m_localPlayer || VHVRConfig.UseVrControls())) {
                if (item == ___m_leftItem) {
                    if (VHVRConfig.LeftHanded()) {
                        vrPlayerSync.currentRightWeapon = null;
                    }
                    else {
                        vrPlayerSync.currentLeftWeapon = null;   
                    }
                }

                if (item == ___m_rightItem) {
                    if (VHVRConfig.LeftHanded()) {
                        vrPlayerSync.currentLeftWeapon = null;
                    }
                    else {
                        vrPlayerSync.currentRightWeapon = null;   
                    }
                }

                VrikCreator.resetVrikHandTransform(__instance);
            }
        }

        static void Postfix() {
            if (StaticObjects.rightHandQuickMenu != null) {
                StaticObjects.rightHandQuickMenu.GetComponent<RightHandQuickMenu>().refreshItems();
                StaticObjects.leftHandQuickMenu.GetComponent<LeftHandQuickMenu>().refreshItems();
            }
        }
    }
        
    [HarmonyPatch(typeof(Player), "SetGuardianPower")]
    class PatchSetGuardianPower {

        static void Postfix(Humanoid __instance) {

            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls()) {
                return;
            }
            
            if (StaticObjects.leftHandQuickMenu != null) {
                StaticObjects.leftHandQuickMenu.GetComponent<LeftHandQuickMenu>().refreshItems();
                StaticObjects.rightHandQuickMenu.GetComponent<RightHandQuickMenu>().refreshItems();
            }
        }
    }

    // This swaps the Raycast camera from the original game camera to the VR camera
    // so that the interaction aligns with the player's head orientation. This
    // only impacts M&KB players since interaction is done using Hands with VR controls enabled
    [HarmonyPatch(typeof(Player), nameof(Player.FindHoverObject))]
    class KBandMouse_FindHoverObjectPatch
    {

        private static MethodInfo GameCamera_get_instance = AccessTools.Method(typeof(GameCamera), "get_instance", new Type[] { });

        private static Camera vrCam = null;

        private static Vector3 GetVRCameraPosition()
        {
            if (!vrCam)
            {
                vrCam = CameraUtils.getCamera(CameraUtils.VR_CAMERA);
            }
            if (!vrCam)
            {
                // Default to original
                return GameCamera.instance.transform.position;
            }
            return vrCam.transform.position;
        }

        private static Vector3 GetVRCameraForward()
        {
            if (!vrCam)
            {
                vrCam = CameraUtils.getCamera(CameraUtils.VR_CAMERA);
            }
            if (!vrCam)
            {
                // Default to original
                return GameCamera.instance.transform.forward;
            }
            return vrCam.transform.forward;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            if (VHVRConfig.UseVrControls() || VHVRConfig.NonVrPlayer())
            {
                LogUtils.LogDebug("Skipping KBandMouse_FindHoverObjectPatch patch.");
                return instructions;
            }
            var original = new List<CodeInstruction>(instructions);
            var patched = new List<CodeInstruction>();
            int gameCameraCount = 0;
            for (int i = 0; i < original.Count; i++)
            {
                var instruction = original[i];
                if (instruction.opcode == OpCodes.Call && instruction.Calls(GameCamera_get_instance))
                {
                    if (gameCameraCount == 0)
                    {
                        LogUtils.LogDebug("Patching FindHoverObject Raycast Starting Position");
                        patched.Add(CodeInstruction.Call(typeof(KBandMouse_FindHoverObjectPatch), nameof(GetVRCameraPosition)));
                        i++;
                        i++;
                    } else if (gameCameraCount == 1)
                    {
                        LogUtils.LogDebug("Patching FindHoverObject Raycast Direction");
                        patched.Add(CodeInstruction.Call(typeof(KBandMouse_FindHoverObjectPatch), nameof(GetVRCameraForward)));
                        i++;
                        i++;
                    } else
                    {
                        LogUtils.LogWarning("Unexpected use of GameCamera instance in FindHoverObject");
                        patched.Add(instruction);
                        continue;
                    }
                    gameCameraCount++;
                } else
                {
                    patched.Add(instruction);
                }
            }
            return patched;
        }
    }
}