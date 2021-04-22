using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RootMotion.FinalIK;
using Valve.VR;

namespace RootMotion.Demos
{
    // Simple avatar scale calibration.
    public class VRIKAvatarScaleCalibrationSteamVR : MonoBehaviour
    {
        public VRIK ik;
        public float scaleMlp = 1f;

        public SteamVR_Action_Boolean calibrationAction;
        public SteamVR_Input_Sources inputSource = SteamVR_Input_Sources.Any;

        private bool calibrateFlag;

        void OnEnable()
        {
            if (calibrationAction != null)
            {
                calibrationAction.AddOnChangeListener(OnTriggerPressedOrReleased, inputSource);
            }
        }

        private void OnDisable()
        {
            if (calibrationAction != null)
            {
                calibrationAction.RemoveOnChangeListener(OnTriggerPressedOrReleased, inputSource);
            }
        }

        private void OnTriggerPressedOrReleased(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource, bool newState)
        {
            // Return if trigger released
            if (!newState) return;
            
            // You can calibrate directly here only if you have "Input Update Mode" set to "OnLateUpdate" in the SteamVR_Settings file.
            calibrateFlag = true;
        }

        private void LateUpdate()
        {
            // Making sure calibration is done in LateUpdate
            if (!calibrateFlag) return;
            calibrateFlag = false;

            // Compare the height of the head target to the height of the head bone, multiply scale by that value.
            float sizeF = (ik.solver.spine.headTarget.position.y - ik.references.root.position.y) / (ik.references.head.position.y - ik.references.root.position.y);
            ik.references.root.localScale *= sizeF * scaleMlp;
        }
    }
}
