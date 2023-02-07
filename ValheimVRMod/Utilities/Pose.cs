using UnityEngine;
using ValheimVRMod.Scripts;
using ValheimVRMod.VRCore;
using ValheimVRMod.VRCore.UI;
using Valve.VR;
using Valve.VR.InteractionSystem;

namespace ValheimVRMod.Utilities {
    public static class Pose {
        public const bool ALWAYS_USE_UNPRESS_SHEATH = true;
        public static bool toggleShowLeftHand = true;
        public static bool toggleShowRightHand = true;
        public static bool justUnsheathed;

        public static void checkInteractions()
        {
            
            if (Player.m_localPlayer == null || !VRControls.mainControlsActive)
            {
                return;
            }

            checkHandOverShoulder(true, ref toggleShowLeftHand);
            checkHandOverShoulder(false, ref toggleShowRightHand);

        }
        
        private static void checkHandOverShoulder(bool isRightHand, ref bool toggleShowHand)
        {
            var isMainHand = isRightHand ^ VHVRConfig.LeftHanded();
            var inputSource = isMainHand ? SteamVR_Input_Sources.RightHand : SteamVR_Input_Sources.LeftHand;
            var hand = isMainHand ? VRPlayer.rightHand : VRPlayer.leftHand;
            
            var camera = CameraUtils.getCamera(CameraUtils.VR_CAMERA).transform;
            var action = SteamVR_Actions.valheim_Grab;
            if (camera.InverseTransformPoint(hand.transform.position).y > -0.4f &&
                camera.InverseTransformPoint(hand.transform.position).z < 0)
            {

                if (action.GetStateDown(inputSource))
                {

                    if(isRightHand && isHoldingItem(isRightHand) && isUnpressSheath()) {
                        return;
                    } 
                 
                    toggleShowHand = false;
                    hand.hapticAction.Execute(0, 0.2f, 100, 0.3f, inputSource);
                    
                    if (isRightHand && EquipScript.getLeft() == EquipType.Bow) {
                        BowLocalManager.instance.toggleArrow();
                    } else if (isHoldingItem(isRightHand)) {
                        Player.m_localPlayer.HideHandItems();
                    } else {
                        Player.m_localPlayer.ShowHandItems();
                        justUnsheathed = true;
                    }
                }
                else if (!justUnsheathed && isRightHand && action.GetStateUp(inputSource)) {
                    if (isHoldingItem(isRightHand) && isUnpressSheath()) {
                        Player.m_localPlayer.HideHandItems();
                    }
                }
            }
            if (justUnsheathed && isRightHand && action.GetStateUp(inputSource)&& isUnpressSheath()) {
                justUnsheathed = false;
            }
        }
        
        private static bool isHoldingItem(bool isRightHand) {
            return isRightHand && Player.m_localPlayer.GetRightItem() != null
                   || !isRightHand && Player.m_localPlayer.GetLeftItem() != null;
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
