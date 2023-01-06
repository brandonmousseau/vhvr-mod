using System;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts {
    public class RightHandQuickMenu : QuickAbstract {

        public static RightHandQuickMenu instance;

        protected override void Awake()
        {
            base.Awake();
            instance = this;
        }

        protected override void ExecuteHapticFeedbackOnHoverTo()
        {
            VRPlayer.rightHand.hapticAction.Execute(0, 0.1f, 40, 0.1f, SteamVR_Input_Sources.RightHand);
        }

        protected override Transform handTransform { get { return VRPlayer.rightHand.transform; } }

        public override void UpdateWristBar()
        {
            // The wrist bar is on the other hand.
            if (wrist.transform.parent != VRPlayer.leftHand.transform)
            {
                wrist.transform.SetParent(VRPlayer.leftHand.transform);
            }
            wrist.transform.localPosition = VHVRConfig.LeftWristQuickBarPos();
            wrist.transform.localRotation = VHVRConfig.LeftWristQuickBarRot();
            wrist.SetActive(isInView() || IsInArea());
        }

        /**
         * loop the inventory hotbar and set corresponding item icons + activate equipped layers
         */
        public override void refreshItems() {
            refreshRadialItems(/* isDominantHand= */ !VHVRConfig.LeftHanded());

            //Extra
            if (VHVRConfig.QuickActionOnLeftHand())
            {
                RefreshWristQuickSwitch();
            }
            else
            {
                RefreshWristQuickAction();
            }

            reorderElements();
        }
    }
}
