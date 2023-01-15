using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ValheimVRMod.Scripts;
using ValheimVRMod.Scripts.Block;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Patches {

     [HarmonyPatch(typeof(VisEquipment), "SetRightHandEquiped")]
     class PatchSetRightHandEquiped {
        static void Postfix(bool __result, string ___m_rightItem, ref GameObject ___m_rightItemInstance) {
            if (!__result || ___m_rightItemInstance == null) {
                return;
            }

            Player player = ___m_rightItemInstance.GetComponentInParent<Player>();
            
            if (player == null) {
                return;
            }

            if (player == Player.m_localPlayer && VHVRConfig.UseVrControls())
            {
                EquipBoundingBoxFix.GetInstanceForPlayer(player)?.RequestBoundingBoxFix(___m_rightItem, ___m_rightItemInstance);
            }

            MeshFilter meshFilter = ___m_rightItemInstance.GetComponentInChildren<MeshFilter>();
            if (meshFilter == null)
            {
                return;
            }

            var vrPlayerSync = player.GetComponent<VRPlayerSync>();
            
            if (vrPlayerSync != null && meshFilter != null) {
                if (VHVRConfig.LeftHanded()) {
                    player.GetComponent<VRPlayerSync>().currentLeftWeapon = meshFilter.gameObject;
                    player.GetComponent<VRPlayerSync>().currentLeftWeapon.name = ___m_rightItem;    
                }
                else {
                    player.GetComponent<VRPlayerSync>().currentRightWeapon = meshFilter.gameObject;
                    player.GetComponent<VRPlayerSync>().currentRightWeapon.name = ___m_rightItem;
                }
                
                VrikCreator.resetVrikHandTransform(player);   
            }

            if (Player.m_localPlayer != player || !VHVRConfig.UseVrControls()) {
                return;
            }

            if (StaticObjects.rightHandQuickMenu != null) {
                StaticObjects.rightHandQuickMenu.GetComponent<RightHandQuickMenu>().refreshItems();
                StaticObjects.leftHandQuickMenu.GetComponent<LeftHandQuickMenu>().refreshItems();
            }

            switch (EquipScript.getRight()) {
                case EquipType.Hammer:
                    meshFilter.gameObject.AddComponent<BuildingManager>();
                    return;
                case EquipType.Fishing:
                    meshFilter.gameObject.transform.localPosition = new Vector3(0, 0, -0.4f);
                    meshFilter.gameObject.AddComponent<FishingManager>();
                    break;
            }
            WeaponWield weaponWield = EquipScript.isSpearEquipped() ? ___m_rightItemInstance.AddComponent<SpearWield>() : ___m_rightItemInstance.AddComponent<WeaponWield>();
            weaponWield.itemName = ___m_rightItem;
            weaponWield.Initialize(false);

            if (EquipScript.isThrowable(player.GetRightItem()) || EquipScript.isSpearEquipped() || EquipScript.getRight() == EquipType.ThrowObject)
            {
                // TODO: rename this to ThrowableManager
                (meshFilter.gameObject.AddComponent<ThrowableManager>()).weaponWield = weaponWield;
            }

            var weaponCol = StaticObjects.rightWeaponCollider().GetComponent<WeaponCollision>();
            weaponCol.setColliderParent(meshFilter.transform, ___m_rightItem, true);
            weaponCol.weaponWield = weaponWield;
            meshFilter.gameObject.AddComponent<WeaponSecondaryManager>().Initialize(meshFilter.transform, ___m_rightItem, true);
            meshFilter.gameObject.AddComponent<WeaponBlock>().weaponWield = weaponWield;

            ParticleFix.maybeFix(___m_rightItemInstance);
        }
    }

    [HarmonyPatch(typeof(VisEquipment), "SetLeftHandEquiped")]
    class PatchSetLeftHandEquiped {
        static void Postfix(bool __result, string ___m_leftItem, GameObject ___m_leftItemInstance) {
            if (!__result || ___m_leftItemInstance == null) {
                return;
            }

            Player player = ___m_leftItemInstance.GetComponentInParent<Player>();
            
            if (player == null) {
                return;
            }

            if (player == Player.m_localPlayer && VHVRConfig.UseVrControls())
            {
                EquipBoundingBoxFix.GetInstanceForPlayer(player)?.RequestBoundingBoxFix(___m_leftItem, ___m_leftItemInstance);
            }

            MeshFilter meshFilter = ___m_leftItemInstance.GetComponentInChildren<MeshFilter>();
            if (meshFilter == null)
            {
                return;
            }

            var vrPlayerSync = player.GetComponent<VRPlayerSync>();

            if (vrPlayerSync != null) {
                if (VHVRConfig.LeftHanded()) {
                    player.GetComponent<VRPlayerSync>().currentRightWeapon = meshFilter.gameObject;    
                }
                else {
                    player.GetComponent<VRPlayerSync>().currentLeftWeapon = meshFilter.gameObject;
                }
                
                VrikCreator.resetVrikHandTransform(player);
            }

            if (Player.m_localPlayer != player || !VHVRConfig.UseVrControls()) {
                return;
            }

            if (StaticObjects.rightHandQuickMenu != null) {
                StaticObjects.rightHandQuickMenu.GetComponent<RightHandQuickMenu>().refreshItems();
                StaticObjects.leftHandQuickMenu.GetComponent<LeftHandQuickMenu>().refreshItems();
            }

            WeaponWield weaponWield;
            switch (EquipScript.getLeft()) {
                
                case EquipType.Bow:
                    meshFilter.gameObject.AddComponent<BowLocalManager>();
                    var bow = Player.m_localPlayer.GetLeftItem();
                    if (!Attack.HaveAmmo(Player.m_localPlayer, bow))
                    {
                        return;
                    }
                    Attack.EquipAmmoItem(Player.m_localPlayer, bow);
                    
                    return;
                case EquipType.Crossbow:
                    CrossbowManager crossbowManager = ___m_leftItemInstance.AddComponent<CrossbowManager>();
                    crossbowManager.Initialize(true);
                    crossbowManager.itemName = ___m_leftItem;
                    crossbowManager.gameObject.AddComponent<WeaponBlock>().weaponWield = crossbowManager;
                    return;
                case EquipType.Lantern:
                    weaponWield = ___m_leftItemInstance.AddComponent<WeaponWield>().Initialize(true);
                    weaponWield.itemName = ___m_leftItem;
                    break;
                case EquipType.Shield:
                    meshFilter.gameObject.AddComponent<ShieldBlock>().itemName = ___m_leftItem;
                    return;
            }

            StaticObjects.leftWeaponCollider().GetComponent<WeaponCollision>().setColliderParent(meshFilter.transform, ___m_leftItem, false);
            meshFilter.gameObject.AddComponent<WeaponSecondaryManager>().Initialize(meshFilter.transform, ___m_leftItem, false);
            ParticleFix.maybeFix(___m_leftItemInstance);
        }
    }
    
    [HarmonyPatch(typeof(VisEquipment), "SetHelmetEquiped")]
    class PatchHelmet {
        static void Postfix(bool __result, GameObject ___m_helmetItemInstance) {
            
            if (!__result || !VHVRConfig.UseVrControls()) {
                return;
            }
            
            MeshHider.hide(___m_helmetItemInstance);
        }
    }
    
    [HarmonyPatch(typeof(VisEquipment), "SetHairEquiped")]
    class PatchHair {
        static void Postfix(bool __result, GameObject ___m_hairItemInstance) {
            
            if (!__result || !VHVRConfig.UseVrControls()) {
                return;
            }
            
            MeshHider.hide(___m_hairItemInstance);
        }
    }
    
    [HarmonyPatch(typeof(VisEquipment), "SetBeardEquiped")]
    class PatchBeard {
        static void Postfix(bool __result, GameObject ___m_beardItemInstance) {
            
            if (!__result || !VHVRConfig.UseVrControls()) {
                return;
            }
            
            MeshHider.hide(___m_beardItemInstance);
        }
    }

    [HarmonyPatch(typeof(VisEquipment), "SetChestEquiped")]
    class PatchSetChestEquiped
    {
        static void Postfix(bool __result, string ___m_chestItem, List<GameObject> ___m_chestItemInstances)
        {
            if (!__result || ___m_chestItemInstances == null || ___m_chestItemInstances.Count == 0 || !VHVRConfig.UseVrControls())
            {
                return;
            }

            Player player = ___m_chestItemInstances[0].GetComponentInParent<Player>();

            if (player == null || player != Player.m_localPlayer)
            {
                return;
            }
             
            foreach (GameObject itemInstance in ___m_chestItemInstances)
            {
                EquipBoundingBoxFix.GetInstanceForPlayer(player)?.RequestBoundingBoxFix(___m_chestItem, itemInstance);
            }
        }
    }

    [HarmonyPatch(typeof(VisEquipment), "AttachItem")]
    class PatchAttachItem {
        
        /// <summary>
        /// For Left Handed mode, switch left with right items
        /// </summary>
        static void Prefix(VisEquipment __instance, ref Transform joint) {

            if (joint.GetComponentInParent<Player>() != Player.m_localPlayer
                || !VHVRConfig.UseVrControls() 
                || !VHVRConfig.LeftHanded()) {
                return;
            }

            if (joint == __instance.m_rightHand) {
                joint = __instance.m_leftHand;
            }
            else if (joint == __instance.m_leftHand) {
                joint = __instance.m_rightHand;
            }
        }

        /// <summary>
        /// For Left Handed mode we need to mirror models of shields and tankard 
        /// </summary>
        static void Postfix(GameObject __result) {

            if (Player.m_localPlayer == null 
                || __result == null
                || __result.GetComponentInParent<Player>() != Player.m_localPlayer
                || !VHVRConfig.UseVrControls() 
                || !VHVRConfig.LeftHanded()
                || EquipScript.getLeft() != EquipType.Shield
                && EquipScript.getRight() != EquipType.Tankard) {
                return;
            }
            
            __result.transform.localScale = new Vector3 (__result.transform.localScale.x, __result.transform.localScale.y * -1 , __result.transform.localScale.z);

        }
    }

    static class MeshHider {
        public static void hide(GameObject obj) {

            if (obj == null) {
                return;
            }
            
            Player player = obj.GetComponentInParent<Player>();
            if (player == null || Player.m_localPlayer != player) {
                return;
            }

            foreach (Renderer renderer in obj.GetComponentsInChildren<Renderer>()) {
                renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;    
            }
        }
    }

    [HarmonyPatch(typeof(Player),nameof(Player.ToggleEquiped))]
    class PatchEquipActionQueue
    {
        static bool Prefix(Player __instance, ref bool __result)
        {
            if(__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls())
            {
                return true;
            }

            if (WeaponSecondaryManager.wasSecondaryAttack)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}
