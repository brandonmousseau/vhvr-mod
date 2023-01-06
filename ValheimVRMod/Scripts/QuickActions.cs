using System;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts {
    public class QuickActions : QuickAbstract {

        public static QuickActions instance;

        protected override void Awake()
        {
            base.Awake();
            instance = this;
        }

        protected override void ExecuteHapticFeedbackOnHoverTo()
        {
            VRPlayer.dominantHand.otherHand.hapticAction.Execute(0, 0.1f, 40, 0.1f, VRPlayer.nonDominantHandInputSource);
        }

        public override void UpdateWristBar()
        {
            if(wrist.transform.parent != VRPlayer.dominantHand.transform)
            {
                wrist.transform.SetParent(VRPlayer.dominantHand.transform);
            }
            wrist.transform.localPosition = VHVRConfig.DominantHandWristQuickBarPos();
            wrist.transform.localRotation = VHVRConfig.DominantHandWristQuickBarRot();
            wrist.SetActive(isInView() || IsInArea());
        }

        public override void refreshItems() {
            refreshRadialItems(/* isDominantHand= */ false);

            if (VHVRConfig.QuickActionOnLeftHand() ^ VHVRConfig.LeftHanded())
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
