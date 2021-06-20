using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using ValheimVRMod.Scripts;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Patches {

     [HarmonyPatch(typeof(VisEquipment), "SetRightHandEquiped")]
     class PatchSetRightHandEquiped {
        static void Postfix(bool __result, string ___m_rightItem, ref GameObject ___m_rightItemInstance) {
            if (!__result || ___m_rightItemInstance == null || !VHVRConfig.UseVrControls()) {
                return;
            }

            MeshFilter meshFilter = ___m_rightItemInstance.GetComponentInChildren<MeshFilter>();

            if (meshFilter == null) {
                return;
            }

            Player player = ___m_rightItemInstance.GetComponentInParent<Player>();
            // only local player must trigger this
            if (player == null || Player.m_localPlayer != player) {
                return;
            }
            
            VrikCreator.resetVrikHandTransform();
            
            if (StaticObjects.quickSwitch != null) {
                StaticObjects.quickSwitch.GetComponent<QuickSwitch>().refreshItems();
            }
            

            switch (EquipScript.getRight()) {
                case EquipType.Fishing:
                    meshFilter.gameObject.AddComponent<FishingManager>();
                    return;
                    
                case EquipType.Spear:
                case EquipType.SpearChitin:
                    meshFilter.gameObject.AddComponent<SpearManager>();
                    // (no return, we want collider for spear also)
                    break;
            }
            
            StaticObjects.rightWeaponCollider().GetComponent<WeaponCollision>().setColliderParent(meshFilter.transform, ___m_rightItem, true);
            ParticleFix.maybeFix(___m_rightItemInstance);
        }
    }

    [HarmonyPatch(typeof(VisEquipment), "SetLeftHandEquiped")]
    class PatchSetLeftHandEquiped {
        static void Postfix(bool __result, string ___m_leftItem, GameObject ___m_leftItemInstance) {
            if (!__result || ___m_leftItemInstance == null || !VHVRConfig.UseVrControls()) {
                return;
            } 
                          
            MeshFilter meshFilter = ___m_leftItemInstance.GetComponentInChildren<MeshFilter>();

            if (meshFilter == null) {
                return;
            }

            Player player = ___m_leftItemInstance.GetComponentInParent<Player>();
            
            if (player == null) {
                return;
            }

            if (Player.m_localPlayer != player) {
                player.GetComponent<VRPlayerSync>().currentLeftWeapon = meshFilter.gameObject;
                return;
            }
            
            VrikCreator.resetVrikHandTransform();

            if (StaticObjects.quickSwitch != null) {
                StaticObjects.quickSwitch.GetComponent<QuickSwitch>().refreshItems();
            }

            switch (EquipScript.getLeft()) {
                
                case EquipType.Bow:
                    meshFilter.gameObject.AddComponent<BowLocalManager>();
                    return;
                
                case EquipType.Shield:
                    meshFilter.gameObject.AddComponent<ShieldManager>()._name = ___m_leftItem;
                    return;
            }
            
            StaticObjects.leftWeaponCollider().GetComponent<WeaponCollision>().setColliderParent(meshFilter.transform, ___m_leftItem, false);
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

    static class MeshHider {
        public static void hide(GameObject obj) {

            if (obj == null) {
                return;
            }
            
            Player player = obj.GetComponentInParent<Player>();
            if (player == null || Player.m_localPlayer != player) {
                return;
            }

            MeshRenderer meshRenderer = obj.GetComponentInChildren<MeshRenderer>();

            if (meshRenderer == null) {
                return;
            }

            meshRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }
    }
}