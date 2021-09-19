using UnityEngine;
using UnityEngine.Events;
using ValheimVRMod.VRCore;
using ValheimVRMod.VRCore.UI;
using Valve.VR;

namespace ValheimVRMod.Utilities {
    public class SettingCallback : MonoBehaviour {

        private static SteamVR_Input_Sources inputHand = SteamVR_Input_Sources.RightHand;
        private static SteamVR_Action_Boolean inputAction = SteamVR_Actions.valheim_Use;
        private static UnityAction<Vector3, Quaternion> action;
        private static Transform target;
        private static Transform sourceHand;

        public static bool configRunning;
        
        /**
         * called by ConfigSettings.createTransformButton()
         */
        public static bool HealthPanel(UnityAction<Vector3, Quaternion> pAction) {
            action = pAction;
            return createSettingObj(VHVRConfig.HealthPanelPlacement(), VHVRConfig.HealthPanelPos(), VHVRConfig.HealthPanelRot());
        }

        /**
         * called by ConfigSettings.createTransformButton()
         */
        public static bool StaminaPanel(UnityAction<Vector3, Quaternion> pAction) {
            action = pAction;
            return createSettingObj(VHVRConfig.StaminaPanelPlacement(), VHVRConfig.StaminaPanelPos(), VHVRConfig.StaminaPanelRot());
        }
        
        private static bool createSettingObj(string position, Vector3 pos, Quaternion rot) {

            if (configRunning) {
                //TODO show message ?
                return false;
            }
            
            switch (position) {
                
                case VRHud.CAMERA_LOCKED:
                    return false;
                
                case VRHud.LEFT_WRIST:
                    inputAction = SteamVR_Actions.valheim_Use;
                    inputHand = SteamVR_Input_Sources.RightHand;
                    target = VRPlayer.vrikRef.references.leftHand;
                    sourceHand = VRPlayer.rightHand.transform;
                    break;
                
                case VRHud.RIGHT_WRIST:
                    inputAction = SteamVR_Actions.valheim_UseLeft;
                    inputHand = SteamVR_Input_Sources.LeftHand;
                    target = VRPlayer.vrikRef.references.rightHand;
                    sourceHand = VRPlayer.leftHand.transform;
                    break;
            }
            
            VHVRConfig.config.SaveOnConfigSet = false;
            var settingObj = new GameObject();
            settingObj.AddComponent<SettingCallback>();
            settingObj.transform.SetParent(target, false);
            settingObj.transform.localPosition = pos;
            settingObj.transform.localRotation = rot;
            configRunning = true;
            return true;
        }

        private void OnRenderObject() {
            
            if (SteamVR_Actions.valheim_Jump.GetState(SteamVR_Input_Sources.Any)) {
                VHVRConfig.config.Save();
                VHVRConfig.config.SaveOnConfigSet = true;
                configRunning = false;
                Destroy(gameObject);
                return;
            }
            
            if (inputAction.GetStateUp(inputHand)) {
                transform.SetParent(target);
            }

            if (! inputAction.GetState(inputHand)) {
                return;
            }
            
            transform.SetParent(target);
            action(transform.localPosition, transform.localRotation);
            transform.SetParent(sourceHand);
        }
    }
}