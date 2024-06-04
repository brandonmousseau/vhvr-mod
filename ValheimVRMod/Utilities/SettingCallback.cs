using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using ValheimVRMod.VRCore;
using ValheimVRMod.VRCore.UI;
using Valve.VR;

namespace ValheimVRMod.Utilities {
    public class SettingCallback : MonoBehaviour {

        private static SteamVR_Input_Sources inputHand;
        private static SteamVR_Action_Boolean inputAction;
        private static UnityAction<Vector3, Quaternion> action;
        private static UnityAction<Vector3> action3Axis;
        private static Transform target;
        private static Transform sourceHand;
        private static GameObject notification;

        public static bool configRunning;


        public static bool CameraLocked(UnityAction<Vector3> pAction)
        {
            action3Axis = pAction;
            return createSettingObj3Axis(VHVRConfig.CameraLockedPos(), "Camera HUD", CameraUtils.getCamera(CameraUtils.VR_UI_CAMERA).transform);
        }
        public static bool CameraLockedDefault(UnityAction<Vector3> pAction)
        {
            pAction(VHVRConfig.DefaultCameraLockedPos());
            return true;
        }

        public static bool CameraLocked2(UnityAction<Vector3> pAction)
        {
            action3Axis = pAction;
            return createSettingObj3Axis(VHVRConfig.CameraLocked2Pos(), "Camera HUD 2", CameraUtils.getCamera(CameraUtils.VR_UI_CAMERA).transform);
        }
        public static bool CameraLocked2Default(UnityAction<Vector3> pAction)
        {
            pAction(VHVRConfig.DefaultCameraLocked2Pos());
            return true;
        }
        /**
         * called by ConfigSettings.createTransformButton()
         */
        public static bool LeftWrist(UnityAction<Vector3, Quaternion> pAction) {
            action = pAction;
            return createSettingObj(VHVRConfig.LeftWristPos(), VHVRConfig.LeftWristRot(), "Left Wrist", false);
        }

        public static bool LeftWristDefault(UnityAction<Vector3, Quaternion> pAction) {

            pAction(VHVRConfig.DefaultLeftWristPos(), VHVRConfig.DefaultLeftWristRot());
            return true;
        }

        public static bool RightWristDefault(UnityAction<Vector3, Quaternion> pAction)
        {
            pAction(VHVRConfig.DefaultRightWristPos(), VHVRConfig.DefaultRightWristRot());
            return true;
        }

        public static bool LeftWristQuickSwitchDefault(UnityAction<Vector3, Quaternion> pAction)
        {
            pAction(VHVRConfig.DefaultLeftWristQuickBarPos(), VHVRConfig.DefaultLeftWristQuickBarRot());
            return true;
        }

        public static bool RightWristQuickActionDefault(UnityAction<Vector3, Quaternion> pAction)
        {
            pAction(VHVRConfig.DefaultRightWristQuickBarPos(), VHVRConfig.DefaultRightWristQuickBarRot());
            return true;
        }

        /**
         * called by ConfigSettings.createTransformButton()
         */
        public static bool RightWrist(UnityAction<Vector3, Quaternion> pAction) {
            action = pAction;
            return createSettingObj(VHVRConfig.RightWristPos(), VHVRConfig.RightWristRot(), "Right Wrist", true);
        }

        public static bool LeftWristQuickSwitch(UnityAction<Vector3, Quaternion> pAction)
        {
            action = pAction;
            return createSettingObj(VHVRConfig.LeftWristQuickBarPos(), VHVRConfig.LeftWristQuickBarRot(), "Left Wrist Quick Bar", false);
        }     

        public static bool RightWristQuickAction(UnityAction<Vector3, Quaternion> pAction)
        {
            action = pAction;
            return createSettingObj(VHVRConfig.RightWristQuickBarPos(), VHVRConfig.RightWristQuickBarRot(), "Right Wrist Quick Bar", true);
        }

        private static bool createSettingObj(Vector3 pos, Quaternion rot, string panel, bool isRightWrist) {

            if (configRunning) {
                LogUtils.LogWarning("Trying to set HUD when config is not running.");
                return false;
            }

            if (isRightWrist) {
                inputAction = SteamVR_Actions.valheim_UseLeft;
                inputHand = SteamVR_Input_Sources.LeftHand;
                target = VRPlayer.rightHand.transform;
                sourceHand = VRPlayer.leftHand.transform;
            }
            else {
                inputAction = SteamVR_Actions.valheim_Use;
                inputHand = SteamVR_Input_Sources.RightHand;
                target = VRPlayer.leftHand.transform;
                sourceHand = VRPlayer.rightHand.transform;
            }
            
            VHVRConfig.config.SaveOnConfigSet = false;
            var settingObj = new GameObject();
            settingObj.AddComponent<SettingCallback>();
            settingObj.transform.SetParent(target, false);
            settingObj.transform.localPosition = pos;
            settingObj.transform.localRotation = rot;
            configRunning = true;
            showNotification(panel);
            
            return true;
        }

        private static bool createSettingObj3Axis(Vector3 pos, string panel, Transform targetParent)
        {
            if (configRunning)
            {
                LogUtils.LogWarning("Trying to set HUD when config is not running.");
                return false;
            }
            if (!target)
            {
                LogUtils.LogWarning("Target does not exist");
            }
            inputAction = SteamVR_Actions.valheim_Use;
            inputHand = SteamVR_Input_Sources.RightHand;
            sourceHand = VRPlayer.rightHand.transform;
            target = targetParent;

            VHVRConfig.config.SaveOnConfigSet = false;
            var settingObj = new GameObject();
            settingObj.AddComponent<SettingCallback>();
            settingObj.transform.SetParent(targetParent, false);
            settingObj.transform.localPosition = pos;
            configRunning = true;
            showNotification(panel);

            return true;
        }

        private static void showNotification(string panel) {
            
            var rect = new Vector2(400, 70);
            notification = new GameObject();
            var image = notification.AddComponent<Image>();
            Color black = Color.black;
            image.color = black;
            image.transform.SetParent(Hud.instance.m_rootObject.transform, false);
            image.rectTransform.sizeDelta = rect;

            var text = new GameObject().AddComponent<Text>();
            text.text = "Configuring " + panel + ". Hold Front Trigger of other Hand to position panel, Press Jump to save.";
            text.fontSize = 20;
            text.color = Color.red;
            text.font =  Resources.GetBuiltinResource(typeof(Font), "Arial.ttf") as Font;
            text.gameObject.AddComponent<ContentSizeFitter>();
            text.transform.SetParent(notification.transform, false);
            text.rectTransform.sizeDelta = rect;

        }
        
        private void OnRenderObject() {
            
            if (SteamVR_Actions.valheim_Jump.GetState(SteamVR_Input_Sources.Any)) {
                VHVRConfig.config.Save();
                VHVRConfig.config.SaveOnConfigSet = true;
                configRunning = false;
                Destroy(notification);
                Destroy(gameObject);
                action = null;
                action3Axis = null;
                return;
            }
            
            if (inputAction.GetStateUp(inputHand)) {
                transform.SetParent(target);
            }

            if (! inputAction.GetState(inputHand)) {
                return;
            }
            
            transform.SetParent(target);
            if(action != null)
                action(transform.localPosition, transform.localRotation);
            if(action3Axis != null)
                action3Axis(transform.localPosition);
            transform.SetParent(sourceHand);
        }
    }
}
