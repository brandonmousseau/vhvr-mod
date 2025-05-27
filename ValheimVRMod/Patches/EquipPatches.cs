using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ValheimVRMod.VRCore;
using ValheimVRMod.Scripts;
using ValheimVRMod.Scripts.Block;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Patches {

    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
    class PatchEquipItem
    {
        private static bool wasUsingKnife = false;
        private static ItemDrop.ItemData knife;

        static bool Prefix(Humanoid __instance, ItemDrop.ItemData item, bool triggerEquipEffects) {
            if (Player.m_localPlayer == null || __instance.gameObject != Player.m_localPlayer.gameObject || !VHVRConfig.UseVrControls())
            {
                return true;
            }

            if (EquipScript.getLeft() == EquipType.Knife && __instance.m_leftItem != null)
            {
                if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon ||
                    (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch && __instance.m_rightItem == null))
                {
                    knife = __instance.m_leftItem;
                    __instance.UnequipItem(__instance.m_leftItem, triggerEquipEffects);
                    wasUsingKnife = true;
                }
                else
                {
                    wasUsingKnife = false;
                }
                return true;
            }

            if (item.m_shared.m_attack.m_attackAnimation == "knife_stab")
            {
                if (__instance.m_leftItem != null)
                {
                    wasUsingKnife = false;
                    return true;
                }
                switch (EquipScript.getRight())
                {
                    case EquipType.Axe:
                    case EquipType.Club:
                    case EquipType.Knife:
                    case EquipType.Sword:
                        __instance.m_leftItem = item;
                        __instance.m_leftItem.m_equipped = true;
                        __instance.m_visEquipment.SetLeftItem(item.m_dropPrefab.name, item.m_variant);
                        if (triggerEquipEffects)
                        {
                            __instance.TriggerEquipEffect(item);
                        }
                        wasUsingKnife = false;
                        return false;
                    case EquipType.Torch:
                        wasUsingKnife = false;
                        return true;
                    default:
                        break;
                }
            }

            if (EquipScript.getRight() == EquipType.Knife && __instance.m_leftItem == null)
            {
                if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon)
                {
                    knife = __instance.m_rightItem;
                    __instance.UnequipItem(__instance.m_rightItem, triggerEquipEffects);
                    wasUsingKnife = true;
                }
                else
                {
                    wasUsingKnife = false;
                }
            }
            return true;
        }

        static void Postfix(Humanoid __instance, ItemDrop.ItemData item) {
            if (Player.m_localPlayer == null || __instance.gameObject != Player.m_localPlayer.gameObject)
            {
                return;
            }
            if (!wasUsingKnife) { 
                return;
            }
            wasUsingKnife = false;
            switch (EquipScript.getRight())
            {
                case EquipType.Axe:
                case EquipType.Club:
                case EquipType.Knife:
                case EquipType.Sword:
                case EquipType.Torch:
                    __instance.m_leftItem = knife;
                    __instance.m_leftItem.m_equipped = true;
                    __instance.m_visEquipment.SetLeftItem(knife.m_dropPrefab.name, knife.m_variant);
                    break;
                default:
                    return;
            }
        } 
    }

    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.GetCurrentBlocker))]
    class PatchGetCurrentBlocker
    {
        static void Postfix(Humanoid __instance, ref ItemDrop.ItemData __result)
        {
            if (Player.m_localPlayer == null || __instance.gameObject != Player.m_localPlayer.gameObject || !VHVRConfig.UseVrControls())
            {
                return;
            }

            if (__result == null)
            {
                return;
            }

            if (EquipScript.getLeft() == EquipType.Knife) {
                var currentWeapon = __instance.GetCurrentWeapon();
                if (currentWeapon != null)
                {
                    __result = currentWeapon;
                }
            }
        }
    }

    [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetRightHandEquipped))]
    class PatchSetRightHandEquipped {
        static void Postfix(VisEquipment __instance, bool __result, string ___m_rightItem, ref GameObject ___m_rightItemInstance) {
            if (!__result) {
                return;
            }

            Player player = __instance.GetComponentInParent<Player>();
            
            if (player == null) {
                return;
            }

            if (player == Player.m_localPlayer && !VHVRConfig.NonVrPlayer())
            {
                if (VHVRConfig.UseVrControls())
                {
                    FistBlock.instance?.updateBlockBoxShape();
                }
                if (___m_rightItemInstance)
                {
                    EquipBoundingBoxFix.GetInstanceForPlayer(player)?.RequestBoundingBoxFix(___m_rightItemInstance);
                }
            }

            MeshFilter meshFilter = ___m_rightItemInstance == null ? null : ___m_rightItemInstance.GetComponentInChildren<MeshFilter>();
            var vrPlayerSync = player.GetComponent<VRPlayerSync>();
            
            if (vrPlayerSync != null) {
                UpdateDualWieldWeapon(vrPlayerSync, ___m_rightItemInstance, meshFilter, player == Player.m_localPlayer);
                if (vrPlayerSync.currentDualWieldWeapon == null)
                {
                    if (vrPlayerSync.IsLeftHanded())
                    {
                        if (meshFilter == null)
                        {
                            vrPlayerSync.currentLeftWeapon = null;
                        }
                        else
                        {
                            vrPlayerSync.currentLeftWeapon = meshFilter.gameObject;
                            vrPlayerSync.currentLeftWeapon.name = ___m_rightItem;
                        }
                    }
                    else
                    {
                        if (meshFilter == null)
                        {
                            vrPlayerSync.currentRightWeapon = null;
                        }
                        else
                        {
                            vrPlayerSync.currentRightWeapon = meshFilter.gameObject;
                            vrPlayerSync.currentRightWeapon.name = ___m_rightItem;
                        }
                    }
                }

                VrikCreator.resetVrikHandTransform(player);   
            }

            if (!___m_rightItemInstance || meshFilter == null)
            {
                return;
            }

            if (Player.m_localPlayer != player)
            {
                if (vrPlayerSync != null && vrPlayerSync.hasReceivedData)
                {
                    // TODO: figure out away to get item name for non-local players (GetRightItem() returns null for non-local players and ___m_rightItem is empty).
                    WeaponWieldSync weaponWieldSync = ___m_rightItemInstance.AddComponent<WeaponWieldSync>();
                    weaponWieldSync.Initialize(player.GetRightItem(), ___m_rightItem, isDominantHandWeapon: true, vrPlayerSync, vrPlayerSync.leftHand.transform, vrPlayerSync.rightHand.transform);
                }
                return;
            }

            ParticleFix.maybeFix(___m_rightItemInstance, EquipScript.getRight());

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
                    break;
                case EquipType.Fishing:
                    meshFilter.gameObject.transform.localPosition = new Vector3(0, 0, -0.4f);
                    meshFilter.gameObject.AddComponent<FishingManager>();
                    break;
            }

            if (EquipScript.getLeft() == EquipType.None && EquipScript.getRight() == EquipType.None)
            {
                return;
            }

            LocalWeaponWield weaponWield = EquipScript.isSpearEquipped() ? ___m_rightItemInstance.AddComponent<SpearWield>() : ___m_rightItemInstance.AddComponent<LocalWeaponWield>();
            weaponWield.Initialize(Player.m_localPlayer.GetRightItem(), ___m_rightItem, isDominantHandWeapon: true);

            if (MagicWeaponManager.IsSwingLaunchEnabled())
            {
                meshFilter.gameObject.AddComponent<SwingLaunchManager>();
            }

            if (EquipScript.isThrowable(player.GetRightItem()) || EquipScript.isSpearEquipped() || EquipScript.getRight() == EquipType.ThrowObject)
            {
                (meshFilter.gameObject.AddComponent<ThrowableManager>()).weaponWield = weaponWield;
            }

            var weaponCol = StaticObjects.rightWeaponCollider().GetComponent<WeaponCollision>();
            weaponCol.setColliderParent(
                meshFilter, handPosition: ___m_rightItemInstance.transform.parent.position, ___m_rightItem, true);
            switch (EquipScript.getRight())
            {
                case EquipType.Cultivator:
                case EquipType.Hammer:
                case EquipType.Hoe:
                case EquipType.Sledge:
                case EquipType.Tankard:
                    weaponCol.gameObject.layer = LayerUtils.CHARACTER;
                    break;
                default:
                    // Use this layer to make sure the weapon collides with all targets including soft building pieces and plants.
                    weaponCol.gameObject.layer = LayerUtils.VHVR_WEAPON;
                    break;
            }
            weaponCol.weaponWield = weaponWield;
            meshFilter.gameObject.AddComponent<ButtonSecondaryAttackManager>().Initialize(meshFilter.transform, ___m_rightItem, true);

            if (___m_rightItem == "StaffLightning")
            {
                WeaponUtils.AlignLoadedMeshToUnloadedMesh(
                    loaded: ___m_rightItemInstance.transform.Find("Loaded").gameObject,
                    unloaded: meshFilter.gameObject);
                ___m_rightItemInstance.AddComponent<WeaponBlock>().weaponWield = weaponWield;
            }
            else
            {
                meshFilter.gameObject.AddComponent<WeaponBlock>().weaponWield = weaponWield;
            }
        }

        private static void UpdateDualWieldWeapon(VRPlayerSync sync, GameObject itemInstance, MeshFilter meshFilter, bool isLocalPlayer)
        {
            if (itemInstance == null)
            {
                sync.currentDualWieldWeapon = null;
                return;
            }

            // Dual wield weapon has to be a skinned mesh as opposed to a mesh filter.
            if (meshFilter != null)
            {
                sync.currentDualWieldWeapon = null;
                return;
            }

            if (isLocalPlayer && EquipScript.getRight() == EquipType.Claws)
            {
                // For the local player, do not consider claw as a dual wielding weapon for synchronization purposes.
                // For remote users, it is hard to know whether the item is claws.
                sync.currentLeftWeapon = sync.currentRightWeapon = sync.currentDualWieldWeapon = null;
                return;
            }

            SkinnedMeshRenderer skinnedMeshRenderer = itemInstance.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skinnedMeshRenderer == null)
            {
                // Dual wield weapon has to be a skinned mesh.
                sync.currentDualWieldWeapon = null;
                return;
            }

            sync.currentLeftWeapon = sync.currentRightWeapon = null;
            sync.currentDualWieldWeapon = skinnedMeshRenderer.gameObject;
        }
    }

    [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetLeftHandEquipped))]
    class PatchSetLeftHandEquipped {
        static void Postfix(VisEquipment __instance, bool __result, string ___m_leftItem, GameObject ___m_leftItemInstance) {
            if (!__result)
            {
                return;
            }

            Player player = __instance.GetComponentInParent<Player>();

            if (player == null) {
                return;
            }

            if (player == Player.m_localPlayer && !VHVRConfig.NonVrPlayer())
            {
                if (VHVRConfig.UseVrControls())
                {
                    FistBlock.instance?.updateBlockBoxShape();
                }
                if (__result && ___m_leftItemInstance != null)
                {
                    EquipBoundingBoxFix.GetInstanceForPlayer(player)?.RequestBoundingBoxFix(___m_leftItemInstance);
                }
            }

            MeshFilter meshFilter = ___m_leftItemInstance == null ? null : ___m_leftItemInstance.GetComponentInChildren<MeshFilter>();
            var vrPlayerSync = player.GetComponent<VRPlayerSync>();
            if (vrPlayerSync != null)
            {
                if (vrPlayerSync.hasReceivedData || (Player.m_localPlayer == player && VHVRConfig.UseVrControls()))
                {
                    if (vrPlayerSync.IsLeftHanded())
                    {
                        vrPlayerSync.currentRightWeapon = meshFilter == null ? null : meshFilter.gameObject;
                    }
                    else
                    {
                        vrPlayerSync.currentLeftWeapon = meshFilter == null ? null : meshFilter.gameObject;
                    }

                    VrikCreator.resetVrikHandTransform(player);
                }
            }

            if (!___m_leftItemInstance || meshFilter == null)
            {
                return;
            }

            if (Player.m_localPlayer != player)
            {
                if (vrPlayerSync != null && vrPlayerSync.hasReceivedData)
                {
                    // TODO: figure out away to get item name for non-local players (GetLeftItem() returns null for non-local players and ___m_leftItem is empty).
                    WeaponWieldSync weaponWieldSync = ___m_leftItemInstance.AddComponent<WeaponWieldSync>();
                    weaponWieldSync.Initialize(player.GetLeftItem(), ___m_leftItem, isDominantHandWeapon: false, vrPlayerSync, vrPlayerSync.leftHand.transform, vrPlayerSync.rightHand.transform);
                }
                return;
            }

            ParticleFix.maybeFix(___m_leftItemInstance, EquipScript.getLeft());

            if (!VHVRConfig.UseVrControls()) {
                return;
            }

            if (MagicWeaponManager.CanSummonWithOppositeHand())
            {
                ___m_leftItemInstance.AddComponent<MagicWeaponManager.SummonByMovingHandUpward>();
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
                    crossbowManager.Initialize(Player.m_localPlayer.GetLeftItem(), ___m_leftItem, isDominantHandWeapon: false);
                    crossbowManager.gameObject.AddComponent<WeaponBlock>().weaponWield = crossbowManager;
                    EquipScript.equipAmmo();
                    return;
                case EquipType.Knife:
                    ___m_leftItemInstance.AddComponent<SecondaryWeaponRotator>();
                    break;
                case EquipType.Lantern:
                    // TODO: implement a component that makes dverger lantern hangs downward regardless of hand orientation.
                    return;
                case EquipType.Shield:
                    meshFilter.gameObject.AddComponent<ShieldBlock>().itemName = ___m_leftItem;
                    return;
            }

            meshFilter.gameObject.AddComponent<ButtonSecondaryAttackManager>().Initialize(meshFilter.transform, ___m_leftItem, false);
        }
    }

    class SecondaryWeaponRotator : MonoBehaviour
    {
        private Quaternion originalRotation;
        private Quaternion inversedRotation;

        void Awake()
        {
            originalRotation = transform.localRotation;
            inversedRotation = transform.localRotation * Quaternion.Euler(180, 0, 0);
        }

        void Update()
        {
            transform.localRotation = FistCollision.ShouldSecondaryKnifeHoldInverse ? inversedRotation : originalRotation;
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

    [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetShoulderEquipped))]
    class PatchBack
    {
        static void Postfix(bool __result, List<GameObject> ___m_shoulderItemInstances)
        {

            if (!__result || !VHVRConfig.UseVrControls() || ___m_shoulderItemInstances == null)
            {
                return;
            }

            if (Player.m_localPlayer?.m_shoulderItem == null)
            {
                return;
            }

            if (Player.m_localPlayer.m_shoulderItem.m_shared.m_name == "$item_cape_ash")
            {
                foreach (var item in ___m_shoulderItemInstances)
                {
                    item.AddComponent<HeadEquipVisibiltiyUpdater>();
                }
            }
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
                EquipBoundingBoxFix.GetInstanceForPlayer(player)?.RequestArmorBoundingBoxFixIfNeeded(itemInstance, ___m_chestItem);
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
        Dictionary<GameObject, int> originalLayers = new Dictionary<GameObject, int>();
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
                        if (!originalLayers.ContainsKey(renderer.gameObject))
                        {
                            originalLayers.Add(renderer.gameObject, renderer.gameObject.layer);
                        }
                        if (VHVRConfig.UseThirdPersonCameraOnFlatscreen())
                        {
                            // Borrow the UI layer to hide the equipment from the VR camera but keep them shown to the follow camera.
                            renderer.gameObject.layer = LayerUtils.CHARARCTER_TRIGGER;
                        }
                        else
                        {
                            renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                        }
                    }
                    isHidden = true;
                }
            }
            else if (isHidden)
            {
                foreach (Renderer renderer in gameObject.GetComponentsInChildren<Renderer>())
                {
                    if (originalLayers.ContainsKey(renderer.gameObject))
                    {
                        renderer.gameObject.layer = originalLayers[renderer.gameObject];
                    }
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
