using UnityEngine;
using ValheimVRMod.Patches;
using ValheimVRMod.Scripts;
using ValheimVRMod.VRCore;
using ValheimVRMod.VRCore.UI;
using Valve.VR;
using Valve.VR.InteractionSystem;

namespace ValheimVRMod.Utilities {
    public static class Pose {
        private const bool ALWAYS_USE_UNPRESS_SHEATH = true;

        private static bool justUnsheathed;

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

                    if(isDominantHand && isHoldingItem(isDominantHand) && isUnpressSheath()) {
                        return;
                    } 
                 
                    hand.hapticAction.Execute(0, 0.2f, 100, 0.3f, inputSource);
                    
                    if (isDominantHand && EquipScript.getLeft() == EquipType.Bow) {
                        BowLocalManager.instance.toggleArrow();
                    } else if (isDominantHand && EquipScript.getLeft() == EquipType.Crossbow) {
                        CrossbowMorphManager.instance.toggleBolt();
                    } else if (isHoldingItem(isDominantHand)) {
                        PatchHideHandItems.HideLocalPlayerHandItem(isDominantHand);
                    } else {
                        PatchShowHandItems.ShowLocalPlayerHandItem(isDominantHand);
                        justUnsheathed = true;
                    }
                }
                else if (!justUnsheathed && isDominantHand && action.GetStateUp(inputSource)) {
                    if (isHoldingItem(isDominantHand) && isUnpressSheath()) {
                        PatchHideHandItems.HideLocalPlayerHandItem(isDominantHand);
                    }
                }
            }
            if (justUnsheathed && isDominantHand && action.GetStateUp(inputSource) && isUnpressSheath()) {
                justUnsheathed = false;
            }
        }
        
        private static bool isHoldingItem(bool isDominantHand) {
            return isDominantHand && Player.m_localPlayer.GetRightItem() != null
                   || !isDominantHand && Player.m_localPlayer.GetLeftItem() != null;
        }

        private static bool isUnpressSheath() {
            // TODO: remove this method and clean up if it always returns true.
            return ALWAYS_USE_UNPRESS_SHEATH
                   || EquipScript.getRight() == EquipType.Spear 
                   || EquipScript.getRight() == EquipType.SpearChitin
                   || EquipScript.getRight() == EquipType.ThrowObject
                   || EquipScript.getRight() == EquipType.Fishing
                   || EquipScript.getRight() == EquipType.Tankard
                   || EquipScript.getRight() == EquipType.Hammer 
                   || EquipScript.getRight() == EquipType.Hoe 
                   || EquipScript.getRight() == EquipType.Cultivator;
        }
    }
}
