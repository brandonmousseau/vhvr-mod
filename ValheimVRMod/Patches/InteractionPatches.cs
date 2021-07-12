using System.Reflection;
using HarmonyLib;
using UnityEngine;
using ValheimVRMod.Scripts;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Patches
{
    
    [HarmonyPatch(typeof(Humanoid), "HideHandItems")]
    class PatchHideHandItems
    {
        
        private static MethodInfo setupVisEquipmentMethod = AccessTools.Method(typeof(Humanoid), "SetupVisEquipment");
        
        static bool Prefix(ref Humanoid __instance,
            ref ItemDrop.ItemData ___m_leftItem, ref ItemDrop.ItemData ___m_rightItem, 
            ref ItemDrop.ItemData ___m_hiddenLeftItem, ref ItemDrop.ItemData ___m_hiddenRightItem, 
            ref VisEquipment ___m_visEquipment, ref ZSyncAnimation ___m_zanim) {

            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls()) {
                return true;
            }
            
            bool leftHand = VRPlayer.toggleShowLeftHand;
            bool rightHand = VRPlayer.toggleShowRightHand;
            
            VRPlayer.toggleShowRightHand = true;
            VRPlayer.toggleShowLeftHand = true;
    
            if (___m_leftItem == null && ___m_rightItem == null) {
                return false;   
            }
    
            if (leftHand) {
                ItemDrop.ItemData leftItem = ___m_leftItem;
                __instance.UnequipItem(___m_leftItem);
                ___m_hiddenLeftItem = leftItem;
            }
    
            if (rightHand) {
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
    
        static bool Prefix(ref Humanoid __instance,
            ref ItemDrop.ItemData ___m_hiddenLeftItem, ref ItemDrop.ItemData ___m_hiddenRightItem,
            ref ZSyncAnimation ___m_zanim)
        {
            
            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls()) {
                return true;
            }
    
            bool leftHand = VRPlayer.toggleShowLeftHand;
            bool rightHand = VRPlayer.toggleShowRightHand;
            
            VRPlayer.toggleShowRightHand = true;
            VRPlayer.toggleShowLeftHand = true;
            
            if (___m_hiddenLeftItem == null && ___m_hiddenRightItem == null) {
                return false;   
            }
    
            if (leftHand) {
                ItemDrop.ItemData hiddenLeftItem =___m_hiddenLeftItem;
                ___m_hiddenLeftItem = null;
                if (hiddenLeftItem != null) {
                    var item = ___m_hiddenRightItem;
                    __instance.EquipItem(hiddenLeftItem);
                    ___m_hiddenRightItem = item;
                    __instance.SetupVisEquipment(__instance.m_visEquipment, false);
                }
            }
    
            if (rightHand)
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
            if (StaticObjects.quickSwitch != null && VHVRConfig.UseVrControls()) {
                StaticObjects.quickSwitch.GetComponent<QuickSwitch>()?.refreshItems();
                StaticObjects.quickActions.GetComponent<QuickActions>()?.refreshItems();
            }
        }
    }
    
    [HarmonyPatch(typeof(InventoryGui), "OnSelectedItem")]
    class PatchOnSelectedItem {

        static void Postfix() {
            if (StaticObjects.quickSwitch != null && VHVRConfig.UseVrControls()) {
                StaticObjects.quickSwitch.GetComponent<QuickSwitch>().refreshItems();
                StaticObjects.quickActions.GetComponent<QuickActions>().refreshItems();
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
                    vrPlayerSync.currentLeftWeapon = null;
                }

                if (item == ___m_rightItem) {
                    vrPlayerSync.currentRightWeapon = null;
                }

                VrikCreator.resetVrikHandTransform(__instance);
            }
        }

        static void Postfix() {
            if (StaticObjects.quickSwitch != null) {
                StaticObjects.quickSwitch.GetComponent<QuickSwitch>().refreshItems();
                StaticObjects.quickActions.GetComponent<QuickActions>().refreshItems();
            }
        }
    }
        
    [HarmonyPatch(typeof(Player), "SetGuardianPower")]
    class PatchSetGuardianPower {

        static void Postfix(Humanoid __instance) {

            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls()) {
                return;
            }
            
            if (StaticObjects.quickActions != null) {
                StaticObjects.quickActions.GetComponent<QuickActions>().refreshItems();
            }
        }
    }
}