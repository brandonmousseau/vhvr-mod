using System;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts {
    public class LeftHandQuickMenu : QuickAbstract {

        public static LeftHandQuickMenu instance;

        protected override void Awake()
        {
            base.Awake();
            instance = this;
        }

        protected override void ExecuteHapticFeedbackOnHoverTo()
        {
            VRPlayer.leftHand.hapticAction.Execute(0, 0.1f, 40, 0.1f, SteamVR_Input_Sources.LeftHand);
        }

        protected override Transform handTransform { get { return VRPlayer.leftHand.transform; } }

        public override void UpdateWristBar()
        {
            // The wrist bar is on the other hand.
            if (wrist.transform.parent != VRPlayer.rightHand.transform)
            {
                wrist.transform.SetParent(VRPlayer.rightHand.transform);
            }
            wrist.transform.localPosition = VHVRConfig.RightWristQuickBarPos();
            wrist.transform.localRotation = VHVRConfig.RightWristQuickBarRot();
            wrist.SetActive(isInView() || IsInArea());
        }

        public override void refreshItems() {
            refreshRadialItems(/* isDominantHand= */ VHVRConfig.LeftHanded());

            if (VHVRConfig.QuickActionOnLeftHand())
            {
                RefreshWristQuickAction();
            }
            else
            {
                RefreshWristQuickSwitch();
            }
                
            reorderElements();
        }
    }
}
