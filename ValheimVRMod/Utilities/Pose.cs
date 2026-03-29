using UnityEngine;
using ValheimVRMod.Patches;
using ValheimVRMod.Scripts;
using ValheimVRMod.VRCore;
using ValheimVRMod.VRCore.UI;
using Valve.VR;

namespace ValheimVRMod.Utilities {
    public static class Pose {
        private static bool isLeftHandUnsheathing = false;
        private static bool isRightHandUnsheathing = false;
        private static SteamVR_Action_Boolean grabAction { get { return SteamVR_Actions.valheim_Grab;} }

        private enum BackReachLocation
        {
            None,
            LeftShoulderRadialUp,        // left hand, right (opposite) shoulder
            LeftShoulderRadialDown,      // left hand, right (opposite) shoulder
            LeftShoulderRadialMedial,    // left hand, left (same) shoulder
            LeftShoulderRadialLateral,   // left hand, left (same) shoulder
            RightShoulderRadialUp,       // right hand, left (opposite) shoulder
            RightShoulderRadialDown,     // right hand, left (opposite) shoulder
            RightShoulderRadialMedial,   // right hand, right (same) shoulder
            RightShoulderRadialLateral,  // right hand, right (same) shoulder
            LeftWaistRadialForward,      // left hand, right (opposite) waist
            LeftWaistRadialBackward,     // left hand, right (opposite) waist
            LeftWaistRadialMedial,       // left hand, left (same) waist
            LeftWaistRadialLateral,      // left hand, left (same) waist
            RightWaistRadialForward,     // right hand, left (opposite) waist
            RightWaistRadialBackward,    // right hand, left (opposite) waist
            RightWaistRadialMedial,      // right hand, right (same) waist
            RightWaistRadialLateral,     // right hand, right (same) waist
        }

        public static void checkInteractions()
        {
            if (Player.m_localPlayer == null || !VRControls.mainControlsActive || Player.m_localPlayer.IsSwimming())
            {
                return;
            }

            var leftHandStartsGripping = grabAction.GetStateDown(SteamVR_Input_Sources.LeftHand);
            var rightHandStartsGripping = grabAction.GetStateDown(SteamVR_Input_Sources.RightHand);
            var leftHandGripping = grabAction.GetState(SteamVR_Input_Sources.LeftHand);
            var rightHandGripping = grabAction.GetState(SteamVR_Input_Sources.RightHand);
            var isDoubleGripping =
                (leftHandStartsGripping && rightHandGripping) ||
                (leftHandGripping && rightHandStartsGripping);

            var leftHandBackReach = getBackReachLocation(VRPlayer.leftHand.transform, isRightHand: false);
            var rightHandBackReach = getBackReachLocation(VRPlayer.rightHand.transform, isRightHand: true);

            checkHolster(leftHandBackReach, rightHandBackReach);

            var doubleGrabbedSlot =
                twoHandBackReachToInventory(
                    leftHandBackReach, rightHandBackReach, out bool doubleGrabbedWeaponAttachToRightHand);
            if (doubleGrabbedSlot >= 0)
            {
                if (isDoubleGripping && checkEquippingWeapon(doubleGrabbedSlot, doubleGrabbedWeaponAttachToRightHand))
                {
                    isLeftHandUnsheathing = isRightHandUnsheathing = true;
                    playEquippingHaptic(true, true);
                }
            }
            else if (isDoubleGripping && checkUnsheathingHolsteredDualWieldWeapon(leftHandBackReach, rightHandBackReach))
            {
                isLeftHandUnsheathing = isRightHandUnsheathing = true;
                playEquippingHaptic(true, true);
            }
            else {
                if (rightHandStartsGripping && checkOnehandedEquipping(isRightHand: true, rightHandBackReach))
                {
                    isRightHandUnsheathing = true;
                    playEquippingHaptic(false, true);
                }
                if (leftHandStartsGripping && checkOnehandedEquipping(isRightHand: false, leftHandBackReach))
                {   
                    isLeftHandUnsheathing = true;
                    playEquippingHaptic(true, false);
                }
            }

            if (FistCollision.hasDualWieldingWeaponEquipped())
            {
                if (!(leftHandGripping || rightHandGripping))
                {
                    isLeftHandUnsheathing = isRightHandUnsheathing = false;
                }
            }
            else {
                if (!leftHandGripping)
                {
                    isLeftHandUnsheathing = false;
                }
                if (!rightHandGripping)
                {
                    isRightHandUnsheathing = false;
                }
            }
        }

        public static bool isBehindBack(Transform handTransform)
        {
            Camera vrCam = VRPlayer.vrCam;
            if (vrCam == null)
            {
                return false;
            }

            Vector3 roomUp = vrCam.transform.parent.up;
            Vector3 offset = handTransform.position - vrCam.transform.position;
            float verticalOffset = Vector3.Dot(roomUp, offset);
            if (verticalOffset < -0.75f || verticalOffset > 0.25f)
            {
                return false;
            }

            Vector3 facing = Vector3.ProjectOnPlane(vrCam.transform.forward, roomUp).normalized;

            return Vector3.Dot(facing, offset) < -0.05f;
        }

        private static void checkHolster(BackReachLocation leftHandBackReach, BackReachLocation rightHandBackReach)
        {
            if (FistCollision.hasDualWieldingWeaponEquipped())
            {
                if (isLeftHandUnsheathing || isRightHandUnsheathing)
                {
                    return;
                }
                if (leftHandBackReach == BackReachLocation.None || rightHandBackReach == BackReachLocation.None)
                {
                    return;
                }
                if ((grabAction.GetStateUp(SteamVR_Input_Sources.LeftHand) && grabAction.GetStateUp(SteamVR_Input_Sources.RightHand)) ||
                    (grabAction.GetStateUp(SteamVR_Input_Sources.LeftHand) && grabAction.GetState(SteamVR_Input_Sources.RightHand)) ||
                    (grabAction.GetState(SteamVR_Input_Sources.LeftHand) && grabAction.GetStateUp(SteamVR_Input_Sources.RightHand)))
                {
                    PatchHideHandItems.HideLocalPlayerHandItem(true);
                }
                return;
            }

            if (!isLeftHandUnsheathing &&
                isHoldingItem(isRightHand: false) &&
                leftHandBackReach != BackReachLocation.None &&
                grabAction.GetStateUp(SteamVR_Input_Sources.LeftHand))
            {
                PatchHideHandItems.HideLocalPlayerHandItem(!VRPlayer.isRightHandMainWeaponHand);
            }

            if (!isRightHandUnsheathing &&
                isHoldingItem(isRightHand: true) &&
                rightHandBackReach != BackReachLocation.None &&
                grabAction.GetStateUp(SteamVR_Input_Sources.RightHand))
            {
                PatchHideHandItems.HideLocalPlayerHandItem(VRPlayer.isRightHandMainWeaponHand);
            }
        }

        private static bool checkUnsheathingHolsteredDualWieldWeapon(BackReachLocation leftHandBackReach, BackReachLocation rightHandBackReach)
        {
            if (!EquipScript.localPlayerHasDualWieldingWeaponHolstered())
            {
                return false;
            }

            if (leftHandBackReach == BackReachLocation.None || rightHandBackReach == BackReachLocation.None)
            {
                return false;
            }

            VRPlayer.offHandWield = false;
           
            PatchShowHandItems.ShowLocalPlayerHandItem(isMainHandItem: true);

            return true;
        }

        private static bool checkOnehandedEquipping(bool isRightHand, BackReachLocation backReachLocation)
        {
            if (backReachLocation == BackReachLocation.None)
            {
                return false;
            }

            bool isBowHand = VRPlayer.isRightHandMainWeaponHand ^ isRightHand;
            if (!isBowHand && EquipScript.getLeft() == EquipType.Bow)
            {
                BowLocalManager.instance.toggleArrow();
                return true;
            }
            if (!isBowHand && EquipScript.getLeft() == EquipType.Crossbow)
            {
                CrossbowMorphManager.instance.toggleBolt();
                return true;
            }

            switch (EquipScript.getRight())
            {
                case EquipType.Polearms:
                case EquipType.BattleAxe:
                    return false;
            }

            var inventorySlot =
                isRightHand ?
                rightHandBackReachToInventory(backReachLocation) :
                leftHandBackReachToInventory(backReachLocation);

            if (checkEquippingWeapon(inventorySlot, isRightHand))
            {
                return true;
            }

            return checkUnsheathingHolsteredNonDualWieldItem(isRightHand, backReachLocation);
        }

        private static bool checkUnsheathingHolsteredNonDualWieldItem(
            bool isRightHand, BackReachLocation backReachLocation)
        {
            if (isHoldingItem(isRightHand))
            {
                return false;
            }

            switch (backReachLocation)
            {
                case BackReachLocation.RightShoulderRadialMedial:
                case BackReachLocation.RightShoulderRadialLateral:
                case BackReachLocation.RightShoulderRadialUp:
                case BackReachLocation.RightShoulderRadialDown:
                    VRPlayer.offHandWield = !isRightHand;
                    PatchShowHandItems.ShowLocalPlayerHandItem(!VHVRConfig.LeftHanded());
                    return true;
                case BackReachLocation.LeftShoulderRadialUp:
                case BackReachLocation.LeftShoulderRadialDown:
                case BackReachLocation.LeftShoulderRadialMedial:
                case BackReachLocation.LeftShoulderRadialLateral:
                    VRPlayer.offHandWield = isRightHand;
                    PatchShowHandItems.ShowLocalPlayerHandItem(VHVRConfig.LeftHanded());
                    return true;
                default:
                    return false;
            }
        }

        private static bool checkEquippingWeapon(int inventorySlot, bool isRightHand)
        {
            var inventory = Player.m_localPlayer.GetInventory();

            if (inventory == null || inventorySlot < 0)
            {
                return false;
            }

            ItemDrop.ItemData item = inventory?.GetItemAt(inventorySlot % 8, inventorySlot / 8);
            if (item == null || item.m_equipped)
            {
                return false;
            }

            if (EquipScript.getEquippedItem(item) == EquipType.None)
            {
                return false;
            }

            var isDualWieldItem = EquipScript.isDualWeapon(item);
            var isMainHandItem = IsMainHandItem(item);

            if (EquipScript.isDualWeapon(item))
            {
                if (isHoldingNonDualWieldingItem(isRightHand: true) ||
                    isHoldingNonDualWieldingItem(isRightHand: false))
                {
                    return false;
                }
                VRPlayer.offHandWield = false;
            }
            else if (isHoldingNonDualWieldingItem(isRightHand))
            {
                return false;
            }
            else
            {
                bool isLeftHanded = isMainHandItem ^ isRightHand;
                VRPlayer.offHandWield = isLeftHanded ^ VHVRConfig.LeftHanded();
            }

            if (isMainHandItem && Player.m_localPlayer.m_hiddenRightItem == item)
            {
                PatchShowHandItems.ShowLocalPlayerHandItem(isMainHandItem: true);
            }
            else if (!isMainHandItem && Player.m_localPlayer.m_hiddenLeftItem == item)
            {
                PatchShowHandItems.ShowLocalPlayerHandItem(isMainHandItem: false);
            }
            else
            {
                Player.m_localPlayer.UseItem(inventory, item, false);
            }

            return true;
        }

        private static bool IsMainHandItem(ItemDrop.ItemData item)
        {
            if (EquipScript.getEquippedItem(item) == EquipType.Knife)
            {
                return !EquipScript.isCompatibleWithParryingKnife();
            }

            if (item == Player.m_localPlayer.m_hiddenLeftItem)
            {
                return false;
            }

            return item == Player.m_localPlayer.m_hiddenRightItem || EquipScript.IsDominantHandItem(item);
        }

        private static int rightHandBackReachToInventory(BackReachLocation backReachLocation)
        {
            switch (backReachLocation)
            {
                case BackReachLocation.LeftShoulderRadialDown:
                    return 0;
                case BackReachLocation.LeftShoulderRadialUp:
                    return 3;
                case BackReachLocation.LeftWaistRadialForward:
                    return 2;
                case BackReachLocation.LeftWaistRadialBackward:
                    return 1;
                // case BackReachLocation.RightShoulderRadialMedial:
                //    // TODO: enable when legacy equip is disabled
                //    return 4;
                case BackReachLocation.RightShoulderRadialLateral:
                    return 5;
                case BackReachLocation.RightWaistRadialLateral:
                    return 6;
                case BackReachLocation.RightWaistRadialMedial:
                    return 7;
                default:
                    return -1;
            }
        }

        private static int leftHandBackReachToInventory(BackReachLocation backReachLocation)
        {
            switch (backReachLocation)
            {
                // case BackReachLocation.LeftShoulderRadialMedial:
                // TODO: enable when legacy equip is disabled
                //    return 0;
                case BackReachLocation.LeftShoulderRadialLateral:
                    return 3;
                case BackReachLocation.LeftWaistRadialLateral:
                    return 2;
                case BackReachLocation.LeftWaistRadialMedial:
                    return 1;
                case BackReachLocation.RightShoulderRadialUp:
                    return 4;
                case BackReachLocation.RightShoulderRadialDown:
                    return 5;
                case BackReachLocation.RightWaistRadialForward:
                    return 6;
                case BackReachLocation.RightWaistRadialBackward:
                    return 7;
                default:
                    return -1;
            }
        }

        private static int twoHandBackReachToInventory(
            BackReachLocation leftHandBackReach,
            BackReachLocation rightHandBackReach,
            out bool attachToRightHand)
        {
            if (leftHandBackReach == BackReachLocation.LeftWaistRadialMedial &&
                rightHandBackReach == BackReachLocation.RightWaistRadialMedial)
            {
                attachToRightHand = true;
                return 14;
            }

            if (leftHandBackReach == BackReachLocation.LeftWaistRadialLateral &&
                rightHandBackReach == BackReachLocation.RightWaistRadialLateral)
            {
                attachToRightHand = true;
                return 15;
            }

            attachToRightHand = false;
            return -1;
        }

        private static BackReachLocation getBackReachLocation(Transform handTransform, bool isRightHand)
        {
            Camera vrCam = VRPlayer.vrCam;
            if (vrCam == null)
            {
                return BackReachLocation.None;
            }

            Vector3 roomUp = vrCam.transform.parent.up;
            Vector3 playerRight = vrCam.transform.right;
            Vector3 facing = Vector3.ProjectOnPlane(vrCam.transform.forward, roomUp).normalized;

            // Vertical offset always relative to head
            Vector3 offsetFromHead = handTransform.position - vrCam.transform.position;
            float verticalOffset = Vector3.Dot(roomUp, offsetFromHead);

            bool reachingShoulder = verticalOffset >= -0.125f && verticalOffset <= 0.25f;
            bool reachingWaist = verticalOffset >= -0.75f && verticalOffset < -0.25f;

            if (!reachingShoulder && !reachingWaist)
            {
                return BackReachLocation.None;
            }

            float lateralOffset = Vector3.Dot(playerRight, offsetFromHead);
            bool reachingRight =
                reachingShoulder ?
                lateralOffset > 0 :
                isRightHand ? lateralOffset > -0.1f : lateralOffset > 0.1f;
            bool ipsilateral = (isRightHand ^ !reachingRight);
            float sagittalOffset = Vector3.Dot(facing, offsetFromHead);

            float behindThreshold = ipsilateral ? -0.0625f : 0.125f;
            if (sagittalOffset >= behindThreshold)
            {
                return BackReachLocation.None;
            }

            if (isRightHand ? lateralOffset > 0.5f : lateralOffset < -0.5f)
            {
                return BackReachLocation.None;
            }

            if (reachingShoulder)
            {
                if (ipsilateral)
                {
                    bool radialPointingRight = (Vector3.Dot(handTransform.forward, playerRight) > 0);
                    if (reachingRight)
                    {
                        return radialPointingRight ? BackReachLocation.RightShoulderRadialLateral : BackReachLocation.RightShoulderRadialMedial;
                    }
                    else
                    {
                        return radialPointingRight ? BackReachLocation.LeftShoulderRadialMedial : BackReachLocation.LeftShoulderRadialLateral;
                    }
                }
                else
                {
                    if (reachingRight)
                    {
                        return Vector3.Dot(handTransform.forward, roomUp + playerRight) > 0 ?
                            BackReachLocation.RightShoulderRadialUp :
                            BackReachLocation.RightShoulderRadialDown;
                    }
                    else
                    {
                        return Vector3.Dot(handTransform.forward, roomUp - playerRight) > 0 ?
                            BackReachLocation.LeftShoulderRadialUp :
                            BackReachLocation.LeftShoulderRadialDown;
                    }
                }
            }
            else // reaching waist
            {
                if (ipsilateral)
                {
                    if (reachingRight)
                    {
                        return
                            Vector3.Dot(handTransform.forward, facing + playerRight) > 0 ?
                            BackReachLocation.RightWaistRadialLateral :
                            BackReachLocation.RightWaistRadialMedial;
                    }
                    else
                    {
                        return Vector3.Dot(handTransform.forward, facing - playerRight) > 0 ?
                            BackReachLocation.LeftWaistRadialLateral :
                            BackReachLocation.LeftWaistRadialMedial;
                    }
                }
                else
                {
                    bool radialPointingForward = (Vector3.Dot(handTransform.forward, facing) > -0.125f);
                    if (reachingRight)
                    {
                        return radialPointingForward ? BackReachLocation.RightWaistRadialForward : BackReachLocation.RightWaistRadialBackward;
                    }
                    else
                    {
                        return radialPointingForward ? BackReachLocation.LeftWaistRadialForward : BackReachLocation.LeftWaistRadialBackward;
                    }
                }
            }
        }

        private static bool isHoldingItem(bool isRightHand) {
            bool isMainWeaponHand = VRPlayer.isRightHandMainWeaponHand ^ !isRightHand;
            if (
                isMainWeaponHand ?
                Player.m_localPlayer.GetRightItem() != null :
                Player.m_localPlayer.GetLeftItem() != null)
            {
                return true;
            }

            return FistCollision.hasDualWieldingWeaponEquipped();
        }

        private static bool isHoldingNonDualWieldingItem(bool isRightHand)
        {
            if (FistCollision.hasDualWieldingWeaponEquipped())
            {
                return false;
            }

            bool isMainWeaponHand = VRPlayer.isRightHandMainWeaponHand ^ !isRightHand;
            return isMainWeaponHand ?
                Player.m_localPlayer.GetRightItem() != null :
                Player.m_localPlayer.GetLeftItem() != null;
        }

        private static void playEquippingHaptic(bool leftHand, bool rightHand) {
            if (leftHand)
            {
                VRPlayer.leftHand.hapticAction.Execute(0, 0.2f, 100, 0.3f, SteamVR_Input_Sources.LeftHand);
            }
            if (rightHand)
            {
                VRPlayer.rightHand.hapticAction.Execute(0, 0.2f, 100, 0.3f, SteamVR_Input_Sources.RightHand);
            }
        }
    }
}
