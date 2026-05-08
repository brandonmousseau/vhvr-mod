using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ValheimVRMod.VRCore;
using ValheimVRMod.Scripts;
using ValheimVRMod.Scripts.Block;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Patches
{

    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
    class PatchEquipItem
    {
        private static bool wasUsingKnife = false;
        private static ItemDrop.ItemData knife;
        public static bool isLocalPlayerEquipping { get; private set; }

        static bool Prefix(Humanoid __instance, ItemDrop.ItemData item, bool triggerEquipEffects)
        {
            if (Player.m_localPlayer == null || __instance.gameObject != Player.m_localPlayer.gameObject)
            {
                return true;
            }

            isLocalPlayerEquipping = true;

            if (!VHVRConfig.UseVrControls())
            {
                return true;
            }

            if (EquipScript.IsDualWeapon(item))
            {
                VRPlayer.offHandWield = false;
            }

            if (EquipScript.CurrentOffHandEquipType() == EquipType.Knife && __instance.m_leftItem != null)
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
                if (EquipScript.IsCompatibleWithParryingKnife())
                {
                    __instance.m_leftItem = item;
                    __instance.m_leftItem.m_equipped = true;
                    item.m_shared.m_equipEffect.Create(__instance.m_visEquipment.m_leftHand.position, __instance.m_visEquipment.m_leftHand.rotation, null, 1f, -1);
                    __instance.m_hiddenRightItem = null;
                    __instance.m_hiddenLeftItem = null;
                    if (__instance.IsItemEquiped(item))
                    {
                        item.m_equipped = true;
                    }
                    __instance.SetupEquipment();
                    if (triggerEquipEffects)
                    {
                        __instance.TriggerEquipEffect(item);
                    }
                    wasUsingKnife = false;
                    return false;
                }
                if (EquipScript.CurrentMainHandEquipType() == EquipType.Torch || EquipScript.CurrentMainHandEquipType() == EquipType.Lantern)
                {
                    wasUsingKnife = false;
                    return true;
                }
            }

            if (EquipScript.CurrentMainHandEquipType() == EquipType.Knife && __instance.m_leftItem == null)
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

        static void Postfix(Humanoid __instance, ItemDrop.ItemData item)
        {
            if (Player.m_localPlayer == null || __instance.gameObject != Player.m_localPlayer.gameObject)
            {
                return;
            }
            isLocalPlayerEquipping = false;
            if (!wasUsingKnife)
            {
                return;
            }
            wasUsingKnife = false;
            switch (EquipScript.CurrentMainHandEquipType())
            {
                case EquipType.Axe:
                case EquipType.Club:
                case EquipType.Knife:
                case EquipType.Sword:
                case EquipType.Torch:
                case EquipType.Lantern:
                    __instance.m_leftItem = knife;
                    __instance.m_leftItem.m_equipped = true;
                    __instance.SetupEquipment();
                    break;
                default:
                    return;
            }
        }
    }

    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UnequipItem))]
    class PatchUnequipItem
    {

        // Patch to reset off-hand wield when the player is unequipped
        static void Postfix(Humanoid __instance, ItemDrop.ItemData item)
        {
            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls() || PatchEquipItem.isLocalPlayerEquipping)
            {
                return;
            }

            if (__instance.m_leftItem != null && __instance.m_leftItem.m_equipped)
            {
                return;
            }

            if (__instance.m_rightItem != null && __instance.m_rightItem.m_equipped)
            {
                return;
            }

            // TODO: consider adding a timeout before resettin off-hand wield            
            VRPlayer.offHandWield = false;
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

            if (EquipScript.CurrentOffHandEquipType() == EquipType.Knife)
            {
                var currentWeapon = __instance.GetCurrentWeapon();
                if (currentWeapon != null)
                {
                    __result = currentWeapon;
                }
            }
        }
    }

    [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetRightHandEquipped))]
    class PatchSetRightHandEquipped
    {
        static void Postfix(VisEquipment __instance, bool __result, ref GameObject ___m_rightItemInstance, int hash)
        {
            if (!__result)
            {
                return;
            }

            Player player = __instance.GetComponentInParent<Player>();

            if (player == null)
            {
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

            if (vrPlayerSync != null)
            {
                // Since VisEquipment#m_leftItem and VisEquipment#m_rightItem are emtpy for remote players and
                // Player#getLeftItem() and Player#getRightItem() return null for remote players,
                // we need to figure out the equip type purely from the item hash.
                vrPlayerSync.mainHandEquipType = EquipScript.GetEquipTypeFromHash(hash);
                if (vrPlayerSync.hasReceivedData || (Player.m_localPlayer == player && VHVRConfig.UseVrControls()))
                {
                    VrikCreator.resetVrikHandTransform(player);
                }
            }

            if (!___m_rightItemInstance || meshFilter == null)
            {
                return;
            }

            if (Player.m_localPlayer != player)
            {
                if (vrPlayerSync != null && vrPlayerSync.hasReceivedData)
                {
                    switch (vrPlayerSync.mainHandEquipType)
                    {
                        case EquipType.Axe:
                        case EquipType.BattleAxe:
                        case EquipType.Club:
                        case EquipType.Cultivator:
                        case EquipType.Fishing:
                        case EquipType.Hoe:
                        case EquipType.Knife:
                        case EquipType.Magic:
                        case EquipType.Pickaxe:
                        case EquipType.Polearms:
                        case EquipType.Scythe:
                        case EquipType.Sledge:
                        case EquipType.Spear:
                        case EquipType.SpearChitin:
                        case EquipType.Sword:
                        case EquipType.Torch:
                            if (!vrPlayerSync.MaybeAddClientWeaponSync(___m_rightItemInstance))
                            {
                                //  TODO: remove this once weapon sync is fully supported
                                WeaponWieldSync weaponWieldSync = ___m_rightItemInstance.AddComponent<WeaponWieldSync>();
                                weaponWieldSync.Initialize(player.GetRightItem(), hash, isDominantHandWeapon: true, vrPlayerSync, vrPlayerSync.leftHand.transform, vrPlayerSync.rightHand.transform);
                            }
                            return;
                        default:
                            return;
                    }
                }
                return;
            }

            if (VHVRConfig.NonVrPlayer())
            {
                return;
            }

            ParticleFix.maybeFix(___m_rightItemInstance, EquipScript.CurrentMainHandEquipType());

            if (!VHVRConfig.UseVrControls())
            {
                return;
            }

            if (StaticObjects.rightHandQuickMenu != null)
            {
                StaticObjects.rightHandQuickMenu.GetComponent<RightHandQuickMenu>().refreshItems();
                StaticObjects.leftHandQuickMenu.GetComponent<LeftHandQuickMenu>().refreshItems();
            }

            switch (EquipScript.CurrentMainHandEquipType())
            {
                case EquipType.Hammer:
                case EquipType.Hoe:
                case EquipType.Tray:
                    meshFilter.gameObject.AddComponent<BuildingManager>();
                    break;
                case EquipType.Fishing:
                    meshFilter.gameObject.transform.localPosition = new Vector3(0, 0, -0.4f);
                    meshFilter.gameObject.AddComponent<FishingManager>();
                    break;
            }

            if (EquipScript.CurrentOffHandEquipType() == EquipType.None && EquipScript.CurrentMainHandEquipType() == EquipType.None)
            {
                return;
            }

            if (EquipScript.CurrentMainHandEquipType() == EquipType.Lantern)
            {
                return;
            }

            var weaponCol = StaticObjects.rightWeaponCollider().GetComponent<WeaponCollision>();
            // Weapon collider should be estimated before weapon wield initialization since
            // the latter may move the weapon and interfere with collider estimation.
            weaponCol.setColliderParent(
                meshFilter, handPosition: ___m_rightItemInstance.transform.parent.position, hash, true);

            LocalWeaponWield weaponWield = EquipScript.IsSpearEquipped() ? ___m_rightItemInstance.AddComponent<SpearWield>() : ___m_rightItemInstance.AddComponent<LocalWeaponWield>();
            weaponWield.Initialize(Player.m_localPlayer.GetRightItem(), hash, isDominantHandWeapon: true);

            if (MagicWeaponManager.IsSwingLaunchEnabled())
            {
                meshFilter.gameObject.AddComponent<SwingLaunchManager>();
            }

            if (EquipScript.IsThrowable(player.GetRightItem()) || EquipScript.IsSpearEquipped() || EquipScript.CurrentMainHandEquipType() == EquipType.ThrowObject)
            {
                (meshFilter.gameObject.AddComponent<ThrowableManager>()).weaponWield = weaponWield;
            }

            switch (EquipScript.CurrentMainHandEquipType())
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
            meshFilter.gameObject.AddComponent<ButtonSecondaryAttackManager>().Initialize(meshFilter.transform, VRPlayer.isRightHandMainWeaponHand);

            if (EquipScript.IsDundr(hash))
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
    }

    [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetLeftHandEquipped))]
    class PatchSetLeftHandEquipped
    {
        static void Postfix(VisEquipment __instance, bool __result, GameObject ___m_leftItemInstance, int hash)
        {
            if (!__result)
            {
                return;
            }

            Player player = __instance.GetComponentInParent<Player>();

            if (player == null)
            {
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
                // Since VisEquipment#m_leftItem and VisEquipment#m_rightItem are emtpy for remote players and
                // Player#getLeftItem() and Player#getRightItem() return null for remote players,
                // we need to figure out the equip type purely from the item hash.
                vrPlayerSync.offHandEquipType = EquipScript.GetEquipTypeFromHash(hash);
                if (vrPlayerSync.hasReceivedData || (Player.m_localPlayer == player && VHVRConfig.UseVrControls()))
                {
                    VrikCreator.resetVrikHandTransform(player);
                }
            }

            if (!___m_leftItemInstance || meshFilter == null)
            {
                return;
            }

            if (Player.m_localPlayer != player)
            {
                if (vrPlayerSync != null && vrPlayerSync.hasReceivedData && vrPlayerSync.offHandEquipType == EquipType.Crossbow)
                {
                    if (!vrPlayerSync.MaybeAddClientWeaponSync(___m_leftItemInstance))
                    {
                        //  TODO: remove this once weapon sync is fully supported
                        WeaponWieldSync weaponWieldSync = ___m_leftItemInstance.AddComponent<WeaponWieldSync>();
                        weaponWieldSync.Initialize(player.GetLeftItem(), hash, isDominantHandWeapon: false, vrPlayerSync, vrPlayerSync.leftHand.transform, vrPlayerSync.rightHand.transform);
                    }
                }
                return;
            }

            if (VHVRConfig.NonVrPlayer())
            {
                return;
            }

            ParticleFix.maybeFix(___m_leftItemInstance, EquipScript.CurrentOffHandEquipType());

            if (!VHVRConfig.UseVrControls())
            {
                return;
            }

            if (MagicWeaponManager.CanSummonWithOppositeHand())
            {
                ___m_leftItemInstance.AddComponent<MagicWeaponManager.SummonByMovingHandUpward>();
            }

            if (StaticObjects.rightHandQuickMenu != null)
            {
                StaticObjects.rightHandQuickMenu.GetComponent<RightHandQuickMenu>().refreshItems();
                StaticObjects.leftHandQuickMenu.GetComponent<LeftHandQuickMenu>().refreshItems();
            }

            switch (EquipScript.CurrentOffHandEquipType())
            {
                case EquipType.Bow:
                    meshFilter.gameObject.AddComponent<BowLocalManager>();
                    EquipScript.EquipAmmo();
                    return;
                case EquipType.Crossbow:
                    CrossbowManager crossbowManager = ___m_leftItemInstance.AddComponent<CrossbowManager>();
                    crossbowManager.Initialize(Player.m_localPlayer.GetLeftItem(), hash, isDominantHandWeapon: false);
                    crossbowManager.gameObject.AddComponent<WeaponBlock>().weaponWield = crossbowManager;
                    EquipScript.EquipAmmo();
                    return;
                case EquipType.Knife:
                    ___m_leftItemInstance.AddComponent<SecondaryWeaponRotator>();
                    break;
                case EquipType.Lantern:
                    return;
                case EquipType.Shield:
                    return;
            }

            meshFilter.gameObject.AddComponent<ButtonSecondaryAttackManager>().Initialize(meshFilter.transform, !VRPlayer.isRightHandMainWeaponHand);
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
    class PatchHelmet
    {
        static void Postfix(bool __result, GameObject ___m_helmetItemInstance)
        {

            if (!__result || !VHVRConfig.UseVrControls() || !___m_helmetItemInstance)
            {
                return;
            }

            ___m_helmetItemInstance.AddComponent<HeadEquipVisibiltiyUpdater>();
        }
    }

    [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetHairEquipped))]
    class PatchHair
    {
        static void Postfix(bool __result, GameObject ___m_hairItemInstance)
        {

            if (!__result || !VHVRConfig.UseVrControls() || !___m_hairItemInstance)
            {
                return;
            }

            ___m_hairItemInstance.AddComponent<HeadEquipVisibiltiyUpdater>();
        }
    }

    [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetBeardEquipped))]
    class PatchBeard
    {
        static void Postfix(bool __result, GameObject ___m_beardItemInstance)
        {

            if (!__result || !VHVRConfig.UseVrControls() || !___m_beardItemInstance)
            {
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
        static void Postfix(bool __result, List<GameObject> ___m_chestItemInstances, int hash)
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
                EquipBoundingBoxFix.GetInstanceForPlayer(player)?.RequestArmorBoundingBoxFixIfNeeded(itemInstance, EquipScript.GetItemName(hash));
            }
        }
    }

    [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.AttachItem))]
    class PatchAttachItem
    {
        static void Prefix(VisEquipment __instance, ref Transform joint, int itemHash)
        {
            if (joint == null)
            {
                return;
            }
            Player player = joint.GetComponentInParent<Player>();
            if (player == null)
            {
                return;
            }

            VRPlayerSync vrPlayerSync = player.GetComponent<VRPlayerSync>();
            bool isLocalPlayer = (player == Player.m_localPlayer);
            if (isLocalPlayer ? !VHVRConfig.UseVrControls() : vrPlayerSync == null)
            {
                // Not using VR controls
                return;
            }

            bool isLeftHanded =
                (isLocalPlayer ? !VRPlayer.isRightHandMainWeaponHand : vrPlayerSync.isLeftHanded);

            if (EquipScript.GetEquipTypeFromHash(itemHash) == EquipType.Lantern)
            {
                // Lantern must be reparented to the VR controller otherwise VRIK and vanilla animation fighting
                // to set character hand position/rotation will cause lantern physics to flicker
                // TODO: should this be applied to most weapons in general?
                var leftController = isLocalPlayer ? VRPlayer.leftHand.transform : vrPlayerSync.leftHand.transform;
                var rightController = isLocalPlayer ? VRPlayer.rightHand.transform : vrPlayerSync.rightHand.transform;
                if (joint == __instance.m_rightHand)
                {
                    joint = isLeftHanded ? leftController : rightController;
                }
                else if (joint == __instance.m_leftHand)
                {
                    joint = isLeftHanded ? rightController : leftController;
                }
                return;
            }

            if (!isLeftHanded)
            {
                return;
            }

            /// For Left Handed mode, switch left with right items
            if (joint == __instance.m_rightHand)
            {
                joint = __instance.m_leftHand;
            }
            else if (joint == __instance.m_leftHand)
            {
                joint = __instance.m_rightHand;
            }
        }

        /// <summary>
        /// For Left Handed mode we need to mirror models of shields and tankard 
        /// </summary>
        static void Postfix(GameObject __result, int itemHash)
        {
            if (!__result)
            {
                return;
            }

            var sync = __result.GetComponentInParent<VRPlayerSync>();
            if (sync == null || !sync.isLeftHanded)
            {
                return;
            }

            // TODO: consider fixing orietantion for dead raiser too.
            var equipType = EquipScript.GetEquipTypeFromHash(itemHash);
            if (equipType == EquipType.Tankard)
            {
                __result.transform.localScale = new Vector3(__result.transform.localScale.x, __result.transform.localScale.y * -1, __result.transform.localScale.z);
            }
            else if (equipType == EquipType.Shield)
            {
                __result.transform.localRotation =
                    __result.transform.localRotation * Quaternion.Euler(0, 0, 180);
            }
        }
    }

    class HeadEquipVisibiltiyUpdater : MonoBehaviour
    {
        private bool isLocalPlayer;
        Dictionary<GameObject, int> originalLayers = new Dictionary<GameObject, int>();
        private bool isHidden = false;

        void Awake()
        {
            Player player = gameObject.GetComponentInParent<Player>();
            isLocalPlayer = (player != null && player == Player.m_localPlayer);
        }

        void OnRenderObject()
        {
            if (shouldHide())
            {
                if (!isHidden)
                {
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

        private bool shouldHide()
        {
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
            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls())
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
