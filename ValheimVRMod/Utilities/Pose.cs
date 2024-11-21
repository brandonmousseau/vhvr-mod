using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.Patches;
using ValheimVRMod.Scripts;
using ValheimVRMod.VRCore;
using ValheimVRMod.VRCore.UI;
using Valve.VR;

namespace ValheimVRMod.Utilities {
    public static class Pose {
        private static Dictionary<bool, bool> isUnsheathing =
            new Dictionary<bool, bool>() { {true, false}, { false, false} };

        public static void checkInteractions()
        {
            if (Player.m_localPlayer == null || !VRControls.mainControlsActive || Player.m_localPlayer.IsSwimming())
            {
                return;
            }

            var isDualWielding = FistCollision.hasDualWieldingWeaponEquipped() || EquipScript.localPlayerHasDualWieldingWeaponHolstered();
            checkHandOverShoulder(isDominantHand: true, isDualWielding);
            if (!isDualWielding)
            {
                checkHandOverShoulder(isDominantHand: false, isDualWielding);
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
            if (verticalOffset < -0.4f || verticalOffset > 0.25f)
            {
                return false;
            }

            Vector3 facing = Vector3.ProjectOnPlane(vrCam.transform.forward, roomUp).normalized;

            return Vector3.Dot(facing, offset) < -0.05f;
        }

        private static void checkHandOverShoulder(bool isDominantHand, bool isDualWielding)
        {
            var isRightHand = isDominantHand ^ VHVRConfig.LeftHanded();
            var inputSource = isRightHand ? SteamVR_Input_Sources.RightHand : SteamVR_Input_Sources.LeftHand;
            var otherHandInputSource = isRightHand? SteamVR_Input_Sources.LeftHand : SteamVR_Input_Sources.RightHand;
            var hand = isRightHand ? VRPlayer.rightHand : VRPlayer.leftHand;
            var action = SteamVR_Actions.valheim_Grab;

            var isHandBehindBack =
                isDualWielding ?
                isBehindBack(hand.transform) && isBehindBack(hand.otherHand.transform) :
                isBehindBack(hand.transform);

            if (isHandBehindBack)
            {
                var isGripping =
                    isDualWielding ?
                    (action.GetStateDown(inputSource) && action.GetStateDown(otherHandInputSource)) ||
                    (action.GetStateDown(inputSource) && action.GetState(otherHandInputSource)) ||
                    (action.GetState(inputSource) && action.GetStateDown(otherHandInputSource)) :
                    action.GetStateDown(inputSource);

                if (isGripping)
                {
                    hand.hapticAction.Execute(0, 0.2f, 100, 0.3f, inputSource);
                    if (isDualWielding)
                    {
                        hand.otherHand.hapticAction.Execute(0, 0.2f, 100, 0.3f, inputSource);
                    }

                    if (isDominantHand && EquipScript.getLeft() == EquipType.Bow) {
                        BowLocalManager.instance.toggleArrow();
                    } else if (isDominantHand && EquipScript.getLeft() == EquipType.Crossbow) {
                        CrossbowMorphManager.instance.toggleBolt();
                    } else if (!isHoldingItem(isDominantHand)) {
                        PatchShowHandItems.ShowLocalPlayerHandItem(isDominantHand);
                        isUnsheathing[isDominantHand] = true;
                    }
                }
                else if (!isUnsheathing[isDominantHand] && isHoldingItem(isDominantHand)) {
                    var isUngripping =
                        isDualWielding ?
                        (action.GetStateUp(inputSource) && action.GetStateUp(otherHandInputSource)) ||
                        (action.GetStateUp(inputSource) && action.GetState(otherHandInputSource)) ||
                        (action.GetState(inputSource) && action.GetStateUp(otherHandInputSource)) :
                        action.GetStateUp(inputSource);

                    if (isUngripping) {
                        PatchHideHandItems.HideLocalPlayerHandItem(isDominantHand);
                    }
                }
            }

            if (isUnsheathing[isDominantHand]) {
                var finishedUnsheathing =
                    isDualWielding ?
                    !action.GetState(inputSource) && !action.GetState(otherHandInputSource) :
                    !action.GetState(inputSource);
                isUnsheathing[isDominantHand] = !finishedUnsheathing;
            }
        }
        
        private static bool isHoldingItem(bool isDominantHand) {
            if (isDominantHand && Player.m_localPlayer.GetRightItem() != null)
            {
                return true;
            }

            if (!isDominantHand && Player.m_localPlayer.GetLeftItem() != null)
            {
                return true;
            }

            return FistCollision.hasDualWieldingWeaponEquipped();
        }
    }
}
