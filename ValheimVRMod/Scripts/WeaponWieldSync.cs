using UnityEngine;

namespace ValheimVRMod.Scripts
{
    // Synchronizes weapon orientation of other (i. e. non-local) players.
    public class WeaponWieldSync : WeaponWield
    {
        public interface TwoHandedStateProvider
        {
            TwoHandedState GetTwoHandedState();
            bool IsLeftHanded();
            bool IsVrEnabled();
        }

        private TwoHandedStateProvider twoHandedStateSync;
        private Transform leftHandTransform;
        private Transform rightHandTransform;

        public void Initialize(ItemDrop.ItemData item, string itemName, TwoHandedStateProvider twoHandedStateSync, Transform leftHandTransform, Transform rightHandTransform)
        {
            this.twoHandedStateSync = twoHandedStateSync;
            this.leftHandTransform = leftHandTransform;
            this.rightHandTransform = rightHandTransform;
            base.Initialize(item, itemName);
        }

        protected override bool IsPlayerLeftHanded()
        {
            return twoHandedStateSync.IsLeftHanded();
        }

        protected override Transform GetLeftHandTransform()
        {
            return leftHandTransform;
        }

        protected override Transform GetRightHandTransform()
        {
            return rightHandTransform;
        }

        protected override TwoHandedState GetDesiredTwoHandedState(bool wasTwoHanded)
        {
            return twoHandedStateSync.GetTwoHandedState();
        }

        protected override void OnRenderObject()
        {
            if (twoHandedStateSync.IsVrEnabled()) {
                base.OnRenderObject();
            }
        }
    }
}
