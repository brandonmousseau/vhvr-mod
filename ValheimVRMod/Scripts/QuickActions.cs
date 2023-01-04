using System;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts {
    public class QuickActions : QuickAbstract {

        public static QuickActions instance;

        protected override void InitializeWrist()
        {
            currentHand = VRPlayer.rightHand;
            instance = this;
        }

        public override void UpdateWristBar()
        {
            if(wrist.transform.parent != VRPlayer.rightHand.transform)
            {
                wrist.transform.SetParent(VRPlayer.rightHand.transform);
            }
            wrist.transform.localPosition = VHVRConfig.RightWristQuickActionPos();
            wrist.transform.localRotation = VHVRConfig.RightWristQuickActionRot();
            wrist.SetActive(isInView() || IsInArea());
        }

        public override void refreshItems() {
            refreshRadialItems();

            if (VHVRConfig.QuickActionOnLeftHand())
            {
                RefreshQuickAction();
            }
            else
            {
                RefreshQuickSwitch();
            }
                
            reorderElements();
            
        }
    }
}
