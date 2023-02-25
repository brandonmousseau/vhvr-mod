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

        private bool isDominantHandWeapon;
        private TwoHandedStateProvider twoHandedStateSync;
        private Transform leftHandTransform;
        private Transform rightHandTransform;

        public void Initialize(ItemDrop.ItemData item, string itemName, bool isDominantHandWeapon, TwoHandedStateProvider twoHandedStateSync, Transform leftHandTransform, Transform rightHandTransform)
        {
            this.isDominantHandWeapon = isDominantHandWeapon;
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

        protected override Vector3 GetWeaponPointingDir()
        {
            if (!isDominantHandWeapon)
            {
                // This check is for crossbow.
                // TODO: figure out a better way to detect crossbow and adjust weapon up direction and dist accordingly.
                return transform.forward;
            }
            return base.GetWeaponPointingDir();
        }

        protected override Vector3 GetPreferredTwoHandedWeaponUp()
        {
            if (!isDominantHandWeapon)
            {
                // This check is for crossbow.
                // TODO: figure out a better way to detect crossbow and adjust weapon up direction and dist accordingly.
                return rearHandTransform.up;
            }
            return base.GetPreferredTwoHandedWeaponUp();
        }

        protected override void OnRenderObject()
        {
            if (twoHandedStateSync.IsVrEnabled())
            {
                base.OnRenderObject();
            }
        }
    }
}
