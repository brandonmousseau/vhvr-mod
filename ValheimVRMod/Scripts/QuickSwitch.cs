using System;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts {
    public class QuickSwitch : QuickAbstract {

        public static QuickSwitch instance;

        protected override void InitializeWrist()
        {
            currentHand = VRPlayer.leftHand;
            instance = this;
        }

        public override void UpdateWristBar()
        {
            if (wrist.transform.parent != VRPlayer.leftHand.transform)
            {
                wrist.transform.SetParent(VRPlayer.leftHand.transform);
            }
            wrist.transform.localPosition = VHVRConfig.LeftWristQuickSwitchPos();
            wrist.transform.localRotation = VHVRConfig.LeftWristQuickSwitchRot();
            wrist.SetActive(isInView() || IsInArea());
        }
        /**
         * loop the inventory hotbar and set corresponding item icons + activate equipped layers
         */
        public override void refreshItems() {
            refreshRadialItems();

            //Extra
            if (VHVRConfig.QuickActionOnLeftHand())
            {
                RefreshQuickSwitch();
            }
            else
            {
                RefreshQuickAction();
            }

            reorderElements();
        }
    }
}
