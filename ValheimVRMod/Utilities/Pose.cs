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
            if (Player.m_localPlayer == null || !VRControls.mainControlsActive)
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
        
        private static void checkHandOverShoulder(bool isDominantHand, bool isDualWielding)
        {
            var isRightHand = isDominantHand ^ VHVRConfig.LeftHanded();
            var inputSource = isRightHand ? SteamVR_Input_Sources.RightHand : SteamVR_Input_Sources.LeftHand;
            var otherHandInputSource = isRightHand? SteamVR_Input_Sources.LeftHand : SteamVR_Input_Sources.RightHand;
            var hand = isRightHand ? VRPlayer.rightHand : VRPlayer.leftHand;
            var camera = CameraUtils.getCamera(CameraUtils.VR_CAMERA).transform;
            var action = SteamVR_Actions.valheim_Grab;

            var isHandBehindBack =
                isDualWielding ?
                isBehindBack(camera, hand.transform) && isBehindBack(camera, hand.otherHand.transform) :
                isBehindBack(camera, hand.transform);

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

        private static bool isBehindBack(Transform headCameraTransform, Transform handTransform)
        {
            return headCameraTransform.InverseTransformPoint(handTransform.position).y > -0.4f &&
                headCameraTransform.InverseTransformPoint(handTransform.position).z < 0;
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
