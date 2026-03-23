using System;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using ValheimVRMod.VRCore;
using ValheimVRMod.Scripts;
using ValheimVRMod.Utilities;
using Valve.VR;
using System.Collections.Generic;
using System.Reflection.Emit;


namespace ValheimVRMod.Patches
{
    enum HandItemPatchTarget
    {
        Both = 0,
        LeftHand = 1,
        RightHand = 2
    }

    [HarmonyPatch(typeof(Humanoid), "HideHandItems")]
    class PatchHideHandItems
    {
        
        private static MethodInfo setupVisEquipmentMethod = AccessTools.Method(typeof(Humanoid), "SetupVisEquipment");
        
        private static HandItemPatchTarget shouldPatch;

        public static void HideLocalPlayerHandItem(bool isRightHand) {
            shouldPatch = isRightHand ? HandItemPatchTarget.RightHand : HandItemPatchTarget.LeftHand;
            Player.m_localPlayer.HideHandItems();
            shouldPatch = HandItemPatchTarget.Both;      
        }

        static bool Prefix(ref Humanoid __instance,
            ref ItemDrop.ItemData ___m_leftItem, ref ItemDrop.ItemData ___m_rightItem, 
            ref ItemDrop.ItemData ___m_hiddenLeftItem, ref ItemDrop.ItemData ___m_hiddenRightItem, 
            ref VisEquipment ___m_visEquipment, ref ZSyncAnimation ___m_zanim) {

            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls()) {
                return true;
            }

            if (___m_leftItem == null && ___m_rightItem == null)
            {
                return false;
            }

            bool hideMainWeapon;
            bool hideSecondaryWeapon;
            if (shouldPatch == HandItemPatchTarget.Both)
            {
                hideMainWeapon = hideSecondaryWeapon = true;
            }
            else if (VRPlayer.isRightHandMainWeaponHand)
            {
                hideMainWeapon = (shouldPatch == HandItemPatchTarget.RightHand);
                hideSecondaryWeapon = (shouldPatch == HandItemPatchTarget.LeftHand);
            }
            else
            {
                hideMainWeapon = (shouldPatch == HandItemPatchTarget.LeftHand);
                hideSecondaryWeapon = (shouldPatch == HandItemPatchTarget.RightHand);
            }

            if (hideMainWeapon)
            {
                var itemToHide = ___m_rightItem != null ? ___m_rightItem : ___m_hiddenRightItem;
                __instance.UnequipItem(___m_rightItem);
                ___m_hiddenRightItem = itemToHide;
            }

            if (hideSecondaryWeapon)
            {
                var itemToHide = ___m_leftItem != null ? ___m_leftItem : ___m_hiddenLeftItem;
                __instance.UnequipItem(___m_leftItem);
                ___m_hiddenLeftItem = itemToHide;
            }

            setupVisEquipmentMethod.Invoke(__instance, new object[]{___m_visEquipment, false});
            ___m_zanim.SetTrigger("equip_hip");
    
            return false;
        }
    }
    
     
    [HarmonyPatch(typeof(Humanoid), "ShowHandItems")]
    class PatchShowHandItems
    {
        private static HandItemPatchTarget shouldPatch;
        public static bool IsEquipping { get; private set; }

        public static void ShowLocalPlayerHandItem(bool isRightHand)
        {
            shouldPatch = isRightHand ? HandItemPatchTarget.RightHand : HandItemPatchTarget.LeftHand;
            Player.m_localPlayer.ShowHandItems();
            shouldPatch = HandItemPatchTarget.Both;
        }

        static bool Prefix(ref Humanoid __instance,
            ref ItemDrop.ItemData ___m_hiddenLeftItem, ref ItemDrop.ItemData ___m_hiddenRightItem,
            ref ZSyncAnimation ___m_zanim)
        {  
            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls()) {
                return true;
            }
    
            var hiddenLeftItem = ___m_hiddenLeftItem;
            var hiddenRightItem = ___m_hiddenRightItem;
            if (hiddenLeftItem == null && hiddenRightItem == null)
            {
                return false;
            }

            bool showVanillaRightItem;
            bool showVanillaLeftItem;
            if (shouldPatch == HandItemPatchTarget.Both)
            {
                showVanillaRightItem = showVanillaLeftItem = true;
            }
            else
            {
                if (VRPlayer.isRightHandMainWeaponHand)
                {
                    showVanillaRightItem = (shouldPatch == HandItemPatchTarget.RightHand);
                    showVanillaLeftItem = (shouldPatch == HandItemPatchTarget.LeftHand);
                }
                else
                {
                    showVanillaRightItem = (shouldPatch == HandItemPatchTarget.LeftHand);
                    showVanillaLeftItem = (shouldPatch == HandItemPatchTarget.RightHand);
                }

                if (!VRPlayer.offHandWield)
                {
                    if (showVanillaRightItem && ___m_hiddenLeftItem != null && ___m_hiddenRightItem == null)
                    {
                        LogUtils.LogDebug("Unsheathing vanilla left item using dominant hand, entering temporary off-hand wield");
                        VRPlayer.offHandWield = true;
                        showVanillaRightItem = false;
                        showVanillaLeftItem = true;
                    }
                    else if (showVanillaLeftItem && ___m_hiddenLeftItem == null && ___m_hiddenRightItem != null)
                    {
                        LogUtils.LogDebug("Unsheathing vanilla right item using non-dominant hand, entering temporary off-hand wield");
                        VRPlayer.offHandWield = true;
                        showVanillaRightItem = true;
                        showVanillaLeftItem = false;
                    }
                }
            }

            IsEquipping = true;
            if (showVanillaRightItem && ___m_hiddenRightItem != null)
            {
                ___m_hiddenRightItem = null;
                __instance.EquipItem(hiddenRightItem);
                ___m_hiddenLeftItem = hiddenLeftItem;
                __instance.SetupVisEquipment(__instance.m_visEquipment, false);
            }
            if (showVanillaLeftItem && ___m_hiddenLeftItem != null)
            {
                ___m_hiddenLeftItem = null;
                __instance.EquipItem(hiddenLeftItem);
                ___m_hiddenRightItem = hiddenRightItem;
                __instance.SetupVisEquipment(__instance.m_visEquipment, false);
            }
            IsEquipping = false;

            ___m_zanim.SetTrigger("equip_hip");

            shouldPatch = HandItemPatchTarget.Both; // Patching is done.

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
