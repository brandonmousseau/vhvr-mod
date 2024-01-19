using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ValheimVRMod.VRCore;
using ValheimVRMod.Scripts;
using ValheimVRMod.Scripts.Block;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Patches {

    [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetRightHandEquipped))]
    class PatchSetRightHandEquipped {
        static void Postfix(bool __result, string ___m_rightItem, ref GameObject ___m_rightItemInstance) {
            if (!__result || !___m_rightItemInstance) {
                return;
            }

            Player player = ___m_rightItemInstance.GetComponentInParent<Player>();
            
            if (player == null) {
                return;
            }

            if (player == Player.m_localPlayer && !VHVRConfig.NonVrPlayer())
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

            if (Player.m_localPlayer != player)
            {
                if (vrPlayerSync != null)
                {
                    // TODO: figure out away to get item name for non-local players (GetRightItem() returns null for non-local players and ___m_rightItem is empty).
                    WeaponWieldSync weaponWieldSync = ___m_rightItemInstance.AddComponent<WeaponWieldSync>();
                    weaponWieldSync.Initialize(player.GetRightItem(), ___m_rightItem, isDominantHandWeapon: true, vrPlayerSync, vrPlayerSync.leftHand.transform, vrPlayerSync.rightHand.transform);
                }
                return;
            }

            if (!VHVRConfig.UseVrControls()) {
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
            LocalWeaponWield weaponWield = EquipScript.isSpearEquipped() ? ___m_rightItemInstance.AddComponent<SpearWield>() : ___m_rightItemInstance.AddComponent<LocalWeaponWield>();
            weaponWield.Initialize(Player.m_localPlayer.GetRightItem(), ___m_rightItem);

            if (MagicWeaponManager.IsSwingLaunchEnabled())
            {
                meshFilter.gameObject.AddComponent<SwingLaunchManager>();
            }

            if (EquipScript.isThrowable(player.GetRightItem()) || EquipScript.isSpearEquipped() || EquipScript.getRight() == EquipType.ThrowObject)
            {
                (meshFilter.gameObject.AddComponent<ThrowableManager>()).weaponWield = weaponWield;
            }

            var weaponCol = StaticObjects.rightWeaponCollider().GetComponent<WeaponCollision>();
            weaponCol.setColliderParent(meshFilter.transform, ___m_rightItem, true);
            weaponCol.weaponWield = weaponWield;
            meshFilter.gameObject.AddComponent<ButtonSecondaryAttackManager>().Initialize(meshFilter.transform, ___m_rightItem, true);
            meshFilter.gameObject.AddComponent<WeaponBlock>().weaponWield = weaponWield;

            ParticleFix.maybeFix(___m_rightItemInstance);
        }
    }

    [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetLeftHandEquipped))]
    class PatchSetLeftHandEquipped {
        static void Postfix(bool __result, string ___m_leftItem, GameObject ___m_leftItemInstance) {
            if (!__result || !___m_leftItemInstance) {
                return;
            }

            Player player = ___m_leftItemInstance.GetComponentInParent<Player>();
            
            if (player == null) {
                return;
            }

            if (player == Player.m_localPlayer && !VHVRConfig.NonVrPlayer())
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

            if (Player.m_localPlayer != player)
            {
                if (vrPlayerSync != null)
                {
                    // TODO: figure out away to get item name for non-local players (GetLeftItem() returns null for non-local players and ___m_leftItem is empty).
                    WeaponWieldSync weaponWieldSync = ___m_leftItemInstance.AddComponent<WeaponWieldSync>();
                    weaponWieldSync.Initialize(player.GetLeftItem(), ___m_leftItem, isDominantHandWeapon: false, vrPlayerSync, vrPlayerSync.leftHand.transform, vrPlayerSync.rightHand.transform);
                }
                return;
            }
            
            if (!VHVRConfig.UseVrControls()) {
                return;
            }

            if (StaticObjects.rightHandQuickMenu != null) {
                StaticObjects.rightHandQuickMenu.GetComponent<RightHandQuickMenu>().refreshItems();
                StaticObjects.leftHandQuickMenu.GetComponent<LeftHandQuickMenu>().refreshItems();
            }

            switch (EquipScript.getLeft()) {
                case EquipType.Bow:
                    meshFilter.gameObject.AddComponent<BowLocalManager>();
                    EquipScript.equipAmmo();                   
                    return;
                case EquipType.Crossbow:
                    CrossbowManager crossbowManager = ___m_leftItemInstance.AddComponent<CrossbowManager>();
                    crossbowManager.Initialize(Player.m_localPlayer.GetLeftItem(), ___m_leftItem);
                    crossbowManager.gameObject.AddComponent<WeaponBlock>().weaponWield = crossbowManager;
                    EquipScript.equipAmmo();
                    return;
                case EquipType.Lantern:
                    // TODO: implement a component that makes dverger lantern hangs downward regardless of hand orientation.
                    return;
                case EquipType.Shield:
                    meshFilter.gameObject.AddComponent<ShieldBlock>().itemName = ___m_leftItem;
                    return;
            }

            meshFilter.gameObject.AddComponent<ButtonSecondaryAttackManager>().Initialize(meshFilter.transform, ___m_leftItem, false);
            ParticleFix.maybeFix(___m_leftItemInstance);
        }
    }

    [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetHelmetEquipped))]
    class PatchHelmet {
        static void Postfix(bool __result, GameObject ___m_helmetItemInstance) {

            if (!__result || !VHVRConfig.UseVrControls() || !___m_helmetItemInstance) {
                return;
            }

            ___m_helmetItemInstance.AddComponent<HeadEquipVisibiltiyUpdater>();
        }
    }

    [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetHairEquipped))]
    class PatchHair {
        static void Postfix(bool __result, GameObject ___m_hairItemInstance) {
            
            if (!__result || !VHVRConfig.UseVrControls() || !___m_hairItemInstance) {
                return;
            }
            
            ___m_hairItemInstance.AddComponent<HeadEquipVisibiltiyUpdater>();
        }
    }

    [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetBeardEquipped))]
    class PatchBeard {
        static void Postfix(bool __result, GameObject ___m_beardItemInstance) {
            
            if (!__result || !VHVRConfig.UseVrControls() || !___m_beardItemInstance) {
                return;
            }
            
            ___m_beardItemInstance.AddComponent<HeadEquipVisibiltiyUpdater>();
        }
    }

    [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetChestEquipped))]
    class PatchSetChestEquiped
    {
        static void Postfix(bool __result, string ___m_chestItem, List<GameObject> ___m_chestItemInstances)
        {
            if (!__result || ___m_chestItemInstances == null || ___m_chestItemInstances.Count == 0 || VHVRConfig.NonVrPlayer())
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

    [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.AttachItem))]
    class PatchAttachItem {
        
        /// <summary>
        /// For Left Handed mode, switch left with right items
        /// </summary>
        static void Prefix(VisEquipment __instance, ref Transform joint) {
            if (joint == null)
            {
                return;
            }
            Player player = joint.GetComponentInParent<Player>();
            if (player == null)
            {
                return;
            }

            if (player == Player.m_localPlayer)
            {
                if (!VHVRConfig.UseVrControls() || !VHVRConfig.LeftHanded())
                {
                    return;
                }
            } else
            {
                VRPlayerSync vrPlayerSync = player.GetComponent<VRPlayerSync>();
                if (vrPlayerSync == null)
                {
                    return;
                }
                // Since VisEquipment#m_leftItem and VisEquipment#m_rightItem are emtpy for remote players and
                // Player#getLeftItem() and Player#getRightItem() return null for remote players,
                // we need to record the item hash to figure out what items a remote player is equipped with.
                // TODO: implement item-specific logic in WeaponWieldSync using the item hash.
                vrPlayerSync.remotePlayerNonDominantHandItemHash = __instance.m_currentLeftItemHash;
                vrPlayerSync.remotePlayerDominantHandItemHash = __instance.m_currentRightItemHash;
                if (!vrPlayerSync.IsLeftHanded())
                {
                    return;
                }
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
        static void Postfix(GameObject __result)
        {
            // TODO: consider fixing orietantion for dead raiser too.
            // TODO: figure out a way to fix oriention for non-local players (e. g. using vrPlayerSync.remotePlayerNonDominantHandItemHash).
            if (Player.m_localPlayer == null 
                || !__result
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

    class HeadEquipVisibiltiyUpdater : MonoBehaviour
    {
        private bool isLocalPlayer;

        private bool isHidden = false;

        void Awake() {
            Player player = gameObject.GetComponentInParent<Player>();
            isLocalPlayer = (player != null && player == Player.m_localPlayer);
        }

        void OnRenderObject()
        {
            if (shouldHide())
            {
                if (!isHidden) {
                    foreach (Renderer renderer in gameObject.GetComponentsInChildren<Renderer>())
                    {
                        renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                    }
                    isHidden = true;
                }
            } else if (isHidden)
            {
                foreach (Renderer renderer in gameObject.GetComponentsInChildren<Renderer>())
                {
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                }
                isHidden = false;
            }
        }

        private bool shouldHide() { 
            if (!isLocalPlayer || !VRPlayer.attachedToPlayer)
            {
                return false;
            }
            if (!Menu.IsVisible())
            {
                return true;
            }
            Vector3 cameraPos = CameraUtils.getCamera(CameraUtils.VR_CAMERA).transform.position;
            Vector3 characterHeadPos = Player.m_localPlayer.m_head.transform.position;
            // When the user is in the menu, show head equipments when the camera moves away from the character so that the full character is visible to the user.
            return Vector3.Distance(cameraPos, characterHeadPos) < 0.25f;
        }
    }

    [HarmonyPatch(typeof(Player), "ToggleEquipped")]
    class PatchEquipActionQueue
    {
        static bool Prefix(Player __instance, ref bool __result)
        {
            if(__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls())
            {
                return true;
            }

            if (ButtonSecondaryAttackManager.isSecondaryAttackStarted)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}
