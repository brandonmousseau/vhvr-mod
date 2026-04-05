using UnityEngine;
using ValheimVRMod.Patches;
using ValheimVRMod.Scripts;
using ValheimVRMod.VRCore;
using ValheimVRMod.VRCore.UI;
using Valve.VR;

namespace ValheimVRMod.Utilities {
    public static class Pose {
        private static bool isLeftHandDrawingWeapon = false;
        private static bool isRightHandDrawingWeapon = false;
        private static BackReachLocation rightHandGrabbedBackLocation = BackReachLocation.None;
        private static BackReachLocation leftHandGrabbedBackLocation = BackReachLocation.None;

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
            // TODO: consider making this class extends Monobehaviour instead of having VRPlayer call this method every frame

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

            updateGrabbedBackReachLocation(leftHandBackReach, false);
            updateGrabbedBackReachLocation(rightHandBackReach, true);

            if (checkGripDraw(leftHandBackReach, rightHandBackReach)) {
                isLeftHandDrawingWeapon = isRightHandDrawingWeapon = true;
            }
            else if (isDoubleGripping && checkUnsheathingHolsteredDualWieldWeapon(leftHandBackReach, rightHandBackReach))
            {
                isLeftHandDrawingWeapon = isRightHandDrawingWeapon = true;
            }
            else {
                if (rightHandStartsGripping)
                {
                    if (VRPlayer.isRightHandMainWeaponHand)
                    {
                        checkArrowToggle(rightHandBackReach);
                    }
                    if (checkUnsheathingHolsteredNonDualWieldItem(isRightHand: true, rightHandBackReach))
                    {
                        isRightHandDrawingWeapon = true;
                    }
                }
                if (leftHandStartsGripping)
                {
                    if (VRPlayer.isLeftHandMainWeaponHand)
                    {
                        checkArrowToggle(leftHandBackReach);
                    }
                    if (checkUnsheathingHolsteredNonDualWieldItem(isRightHand: false, leftHandBackReach))
                    {
                        isLeftHandDrawingWeapon = true;
                    }
                }
            }

            if (FistCollision.hasDualWieldingWeaponEquipped())
            {
                if (!(leftHandGripping || rightHandGripping))
                {
                    isLeftHandDrawingWeapon = isRightHandDrawingWeapon = false;
                }
            }
            else {
                if (!leftHandGripping)
                {
                    isLeftHandDrawingWeapon = false;
                }
                if (!rightHandGripping)
                {
                    isRightHandDrawingWeapon = false;
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
                if (isLeftHandDrawingWeapon || isRightHandDrawingWeapon)
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

            if (!isLeftHandDrawingWeapon &&
                VRPlayer.leftHandItem != null &&
                leftHandBackReach != BackReachLocation.None &&
                grabAction.GetStateUp(SteamVR_Input_Sources.LeftHand))
            {
                PatchHideHandItems.HideLocalPlayerHandItem(VRPlayer.isLeftHandMainWeaponHand);
            }

            if (!isRightHandDrawingWeapon &&
                VRPlayer.rightHandItem != null &&
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

            if (leftHandBackReach != BackReachLocation.LeftShoulderRadialMedial ||
                rightHandBackReach != BackReachLocation.RightShoulderRadialMedial)
            {
                return false;
            }

            VRPlayer.offHandWield = false;

            playEquippingHaptic(true, true);

            return PatchShowHandItems.ShowLocalPlayerHandItem(isMainHandItem: true);
        }

        private static bool checkArrowToggle(BackReachLocation backReachLocation)
        {
            if (backReachLocation == BackReachLocation.None)
            {
                return false;
            }
            if (EquipScript.getLeft() == EquipType.Bow)
            {
                BowLocalManager.instance.toggleArrow();
                return true;
            }
            if (EquipScript.getLeft() == EquipType.Crossbow)
            {
                CrossbowMorphManager.instance.toggleBolt();
                return true;
            }
            return false;
        }

        private static bool checkUnsheathingHolsteredNonDualWieldItem(
            bool isRightHand, BackReachLocation backReachLocation)
        {
            if (!canGrabNewWeapon(isRightHand) || EquipScript.localPlayerHasDualWieldingWeaponHolstered())
            {
                return false;
            }

            switch (backReachLocation)
            {
                case BackReachLocation.RightShoulderRadialMedial:
                    if (isRightHand && PatchShowHandItems.ShowLocalPlayerHandItem(!VHVRConfig.LeftHanded()))
                    {
                        VRPlayer.offHandWield = false;
                        playEquippingHaptic(false, true);
                        return true;
                    }
                    return false;
                case BackReachLocation.LeftShoulderRadialMedial:
                    if (!isRightHand && PatchShowHandItems.ShowLocalPlayerHandItem(VHVRConfig.LeftHanded())) {
                        VRPlayer.offHandWield = false;
                        playEquippingHaptic(true, false);
                        return true;
                    }
                    return false;
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
                if (!(canGrabNewWeapon(isRightHand: true) && canGrabNewWeapon(isRightHand: false)))
                {
                    return false;
                }
                VRPlayer.offHandWield = false;
            }
            else if (!canGrabNewWeapon(isRightHand))
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

        private static void updateGrabbedBackReachLocation(BackReachLocation backReach, bool isRightHand)
        {
            var inputSource = isRightHand ? SteamVR_Input_Sources.RightHand : SteamVR_Input_Sources.LeftHand;

            if (!grabAction.GetState(inputSource))
            {
                if (isRightHand)
                {
                    rightHandGrabbedBackLocation = BackReachLocation.None;
                }
                else
                {
                    leftHandGrabbedBackLocation = BackReachLocation.None;
                }
                return;
            }

            // Only record on grip press down
            if (!grabAction.GetStateDown(inputSource))
            {
                return;
            }

            if (!canGrabNewWeapon(isRightHand))
            {
                return;
            }

            // Record the location and give haptic feedback
            if (isRightHand)
            {
                rightHandGrabbedBackLocation = canGrabNewWeapon(isRightHand) ? backReach : BackReachLocation.None;
            }
            else
            {
                leftHandGrabbedBackLocation = canGrabNewWeapon(isRightHand) ? backReach : BackReachLocation.None;
            }

            playEquippingHaptic(!isRightHand, isRightHand);
        }

        private static bool checkGripDraw(BackReachLocation leftHandBackReach, BackReachLocation rightHandBackReach)
        {
            if (leftHandGrabbedBackLocation == BackReachLocation.None &&
                rightHandGrabbedBackLocation == BackReachLocation.None)
            {
                return false;
            }

            bool leftHandGripping = grabAction.GetState(SteamVR_Input_Sources.LeftHand);
            bool rightHandGripping = grabAction.GetState(SteamVR_Input_Sources.RightHand);

            // Check dual grip first
            if (leftHandGrabbedBackLocation != BackReachLocation.None &&
                rightHandGrabbedBackLocation != BackReachLocation.None)
            {
                if (isDrawingWeapon(leftHandBackReach, VRPlayer.leftHand.transform, VRPlayer.leftFootPhysicsEstimator.GetVelocity()) ||
                    isDrawingWeapon(rightHandBackReach, VRPlayer.rightHand.transform, VRPlayer.rightHandPhysicsEstimator.GetVelocity()))
                {
                    var leftLocation = leftHandGrabbedBackLocation;
                    var rightLocation = rightHandGrabbedBackLocation;
                    leftHandGrabbedBackLocation = BackReachLocation.None;
                    rightHandGrabbedBackLocation = BackReachLocation.None;
                    return onDualGripDraw(leftLocation, rightLocation);
                }

                return false;
            }

            if (rightHandGrabbedBackLocation != BackReachLocation.None &&
                isDrawingWeapon(rightHandBackReach, VRPlayer.rightHand.transform, VRPlayer.rightHandPhysicsEstimator.GetVelocity()))
            {
                var location = rightHandGrabbedBackLocation;
                rightHandGrabbedBackLocation = BackReachLocation.None;
                return onSingleGripDraw(isRightHand: true, location);
            }

            if (leftHandGrabbedBackLocation != BackReachLocation.None &&
                isDrawingWeapon(leftHandBackReach, VRPlayer.leftHand.transform, VRPlayer.leftFootPhysicsEstimator.GetVelocity()))
            {
                var location = leftHandGrabbedBackLocation;
                leftHandGrabbedBackLocation = BackReachLocation.None;
                return onSingleGripDraw(isRightHand: false, location);
            }

            return false;
        }

        private static bool isDrawingWeapon(
            BackReachLocation backReach, Transform controller, Vector3 velocity)
        {
            if (backReach == BackReachLocation.None)
            {
                // Hand leaving back area, consider this as a weapon draw
                return true;
            }

            return Mathf.Abs(Vector3.Dot(controller.forward, velocity)) > 1;
        }

        private static bool onSingleGripDraw(bool isRightHand, BackReachLocation backReach)
        {
            var inventorySlot =
                isRightHand ?
                rightHandBackReachToInventory(backReach) :
                leftHandBackReachToInventory(backReach);

            if (!checkEquippingWeapon(inventorySlot, isRightHand))
            {
                return false;
            }

            if (isRightHand)
            {
                isRightHandDrawingWeapon = true;
                playEquippingHaptic(false, true);
            }
            else
            {
                isLeftHandDrawingWeapon = true;
                playEquippingHaptic(true, false);
            }

            return true;
        }

        private static bool onDualGripDraw(BackReachLocation leftHandBackReach, BackReachLocation rightHandBackReach)
        {
            var inventorySlot = twoHandBackReachToInventory(leftHandBackReach, rightHandBackReach, out bool attachToRightHand);
            if  (!checkEquippingWeapon(inventorySlot, attachToRightHand))
            {
                return false;
            }
            isLeftHandDrawingWeapon = isRightHandDrawingWeapon = true;
            playEquippingHaptic(true, true);
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

            bool trackPelvis = (VHVRConfig.IsHipTrackingEnabled() && VRPlayer.pelvis != null);

            Vector3 playerUp =
                trackPelvis ?
                (vrCam.transform.position - VRPlayer.pelvis.transform.position).normalized :
                vrCam.transform.parent.up;

            Vector3 offsetFromHead = handTransform.position - vrCam.transform.position;

            float verticalOffset = Vector3.Dot(playerUp, offsetFromHead);

            bool reachingShoulder = verticalOffset >= -0.25f && verticalOffset <= 0.25f;
            bool reachingWaist = verticalOffset >= -0.75f && verticalOffset < -0.375f;
            if (!reachingShoulder && !reachingWaist)
            {
                return BackReachLocation.None;
            }

            Vector3 facing =
                Vector3.ProjectOnPlane(trackPelvis ? VRPlayer.pelvis.transform.forward : vrCam.transform.forward, playerUp).normalized;
            Vector3 playerRight = Vector3.Cross(playerUp, facing);

            float lateralOffset = Vector3.Dot(offsetFromHead, playerRight);
            float sagittalOffset = Vector3.Dot(offsetFromHead, facing);

            bool reachingRight =
                reachingShoulder ?
                lateralOffset > 0 :
                lateralOffset > (isRightHand ? 0.5f * sagittalOffset : -0.5f * sagittalOffset);

            bool contralateral = (isRightHand ^ reachingRight);

            if (contralateral ?
                sagittalOffset > 0.0625f :
                sagittalOffset > (reachingShoulder ? -0.0625f : -0.125f))
            {
                return BackReachLocation.None;
            }

            if ((reachingShoulder || contralateral) ?
                Mathf.Abs(lateralOffset) > 0.5f :
                Mathf.Abs(lateralOffset) > 0.33f)
            {
                return BackReachLocation.None;
            }

            if (reachingShoulder)
            {
                if (reachingRight)
                {
                    if (Vector3.Dot(handTransform.forward, playerUp + playerRight) > 0)
                    {
                        return contralateral ?
                            BackReachLocation.RightShoulderRadialUp :
                            BackReachLocation.RightShoulderRadialLateral;
                    }
                    else
                    {
                        return contralateral ?
                            BackReachLocation.RightShoulderRadialDown :
                            BackReachLocation.RightShoulderRadialMedial;
                    }
                }
                else
                {
                    if (Vector3.Dot(handTransform.forward, playerUp - playerRight) > 0)
                    {
                        return contralateral ?
                            BackReachLocation.LeftShoulderRadialUp :
                            BackReachLocation.LeftShoulderRadialLateral;
                    }
                    else
                    {
                        return contralateral ?
                            BackReachLocation.LeftShoulderRadialDown :
                            BackReachLocation.LeftShoulderRadialMedial;
                    }
                }
            }

            // Reaching waist
            if (reachingRight)
            {
                if (Vector3.Dot(handTransform.forward, facing + playerRight * 0.5f) > 0)
                {
                    return contralateral ?
                        BackReachLocation.RightWaistRadialForward :
                        BackReachLocation.RightWaistRadialLateral;
                }
                else
                {
                    return contralateral ?
                        BackReachLocation.RightWaistRadialBackward :
                        BackReachLocation.RightWaistRadialMedial;
                }
            }
            else
            {
                if (Vector3.Dot(handTransform.forward, facing - playerRight * 0.5f) > 0)
                {
                    return contralateral ?
                        BackReachLocation.LeftWaistRadialForward :
                        BackReachLocation.LeftWaistRadialLateral;
                }
                else
                {
                    return contralateral ?
                        BackReachLocation.LeftWaistRadialBackward :
                        BackReachLocation.LeftWaistRadialMedial;
                }
            }
        }

        private static bool canGrabNewWeapon(bool isRightHand)
        {
            if (EquipScript.getLeft() == EquipType.Bow
                || EquipScript.getLeft() == EquipType.Crossbow
                || EquipScript.getRight() == EquipType.Polearms
                || EquipScript.getRight() == EquipType.BattleAxe
                || FistCollision.hasDualWieldingWeaponEquipped()
                || Player.m_localPlayer == null
                || Player.m_localPlayer.m_inCraftingStation)
            {
                return false;
            }

            var item = isRightHand ? VRPlayer.rightHandItem : VRPlayer.leftHandItem;
            return item == null ||
                EquipScript.getEquippedItem(item) == EquipType.None ||
                EquipScript.getEquippedItem(item) == EquipType.Hammer;
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
