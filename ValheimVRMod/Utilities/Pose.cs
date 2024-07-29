using System.Collections.Generic;
using ValheimVRMod.Patches;
using ValheimVRMod.Scripts;
using ValheimVRMod.VRCore;
using ValheimVRMod.VRCore.UI;
using Valve.VR;

namespace ValheimVRMod.Utilities {
    public static class Pose {
        private static Dictionary<bool, bool> justUnsheathed =
            new Dictionary<bool, bool>() { {true, false}, { false, false} };

        public static void checkInteractions()
        {
            if (Player.m_localPlayer == null || !VRControls.mainControlsActive)
            {
                return;
            }

            checkHandOverShoulder(isDominantHand: true);
            checkHandOverShoulder(isDominantHand: false);
        }
        
        private static void checkHandOverShoulder(bool isDominantHand)
        {
            var isRightHand = isDominantHand ^ VHVRConfig.LeftHanded();
            var inputSource = isRightHand ? SteamVR_Input_Sources.RightHand : SteamVR_Input_Sources.LeftHand;
            var hand = isRightHand ? VRPlayer.rightHand : VRPlayer.leftHand;
            
            var camera = CameraUtils.getCamera(CameraUtils.VR_CAMERA).transform;
            var action = SteamVR_Actions.valheim_Grab;
            if (camera.InverseTransformPoint(hand.transform.position).y > -0.4f &&
                camera.InverseTransformPoint(hand.transform.position).z < 0)
            {
                if (action.GetStateDown(inputSource))
                {

                    hand.hapticAction.Execute(0, 0.2f, 100, 0.3f, inputSource);
                    
                    if (isDominantHand && EquipScript.getLeft() == EquipType.Bow) {
                        BowLocalManager.instance.toggleArrow();
                    } else if (isDominantHand && EquipScript.getLeft() == EquipType.Crossbow) {
                        CrossbowMorphManager.instance.toggleBolt();
                    } else if (!isHoldingItem(isDominantHand)) {
                        PatchShowHandItems.ShowLocalPlayerHandItem(isDominantHand);
                        justUnsheathed[isDominantHand] = true;
                    }
                }
                else if (!justUnsheathed[isDominantHand] && action.GetStateUp(inputSource) && isHoldingItem(isDominantHand)) {
                     PatchHideHandItems.HideLocalPlayerHandItem(isDominantHand);
                }
            }
            if (justUnsheathed[isDominantHand] && action.GetStateUp(inputSource)) {
                justUnsheathed[isDominantHand] = false;
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
