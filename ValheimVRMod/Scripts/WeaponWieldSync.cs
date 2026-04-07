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

            bool InverseHold();
        }

        private bool isDominantHandWeapon;
        private TwoHandedStateProvider twoHandedStateSync;
        private Transform leftHandTransform;
        private Transform rightHandTransform;
        private bool recalculatedDirectionOffset = false;

        public void Initialize(ItemDrop.ItemData item, string itemName, bool isDominantHandWeapon, TwoHandedStateProvider twoHandedStateSync, Transform leftHandTransform, Transform rightHandTransform)
        {
            this.isDominantHandWeapon = isDominantHandWeapon;
            this.twoHandedStateSync = twoHandedStateSync;
            this.leftHandTransform = leftHandTransform;
            this.rightHandTransform = rightHandTransform;
            // TODO: figure out a better way to detect crossbow.
            base.Initialize(item, itemName, isDominantHandWeapon, twoHandedStateSync);
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
            if (!twoHandedStateSync.IsVrEnabled())
            {
                return;
            }

            if (!recalculatedDirectionOffset && twoHandedStateSync.InverseHold())
            {
                // The offset might have been calculated not knowing the weapon is a spear so it can be stale and needs to be recalculated
                offsetFromPointingDir =
                    Quaternion.Inverse(Quaternion.LookRotation(GetWeaponPointingDirection(), transform.up)) *
                    transform.rotation;
                recalculatedDirectionOffset = true;
            }
            base.OnRenderObject();

            if (twoHandedState == TwoHandedState.SingleHanded)
            {
                transform.SetPositionAndRotation(
                    geometryProvider.GetDesiredSingleHandedPosition(this),
                    geometryProvider.GetDesiredSingleHandedRotation(this));
            }
        }
    }
}
