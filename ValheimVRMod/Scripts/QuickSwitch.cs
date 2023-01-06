using System;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts {
    public class QuickSwitch : QuickAbstract {

        public static QuickSwitch instance;

        protected override void Awake()
        {
            base.Awake();
            instance = this;
        }

        protected override void ExecuteHapticFeedbackOnHoverTo()
        {
            VRPlayer.dominantHand.hapticAction.Execute(0, 0.1f, 40, 0.1f, VRPlayer.dominantHandInputSource);
        }

        public override void UpdateWristBar()
        {
            if (wrist.transform.parent != VRPlayer.dominantHand.otherHand.transform)
            {
                wrist.transform.SetParent(VRPlayer.dominantHand.otherHand.transform);
            }
            wrist.transform.localPosition = VHVRConfig.NonDominantHandWristQuickBarPos();
            wrist.transform.localRotation = VHVRConfig.NonDominantHandWristQuickBarRot();
            wrist.SetActive(isInView() || IsInArea());
        }
        /**
         * loop the inventory hotbar and set corresponding item icons + activate equipped layers
         */
        public override void refreshItems() {
            refreshRadialItems(/* isDominantHand= */ true);

            //Extra
            if (VHVRConfig.QuickActionOnLeftHand() ^ VHVRConfig.LeftHanded())
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
