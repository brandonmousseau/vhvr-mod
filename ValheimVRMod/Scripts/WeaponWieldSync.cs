using UnityEngine;

namespace ValheimVRMod.Scripts
{
    // Synchronizes weapon orientation of other (i. e. non-local) players.
    public class WeaponWieldSync : MonoBehaviour
    {
        public interface TwoHandedStateProvider
        {
            WeaponWield.TwoHandedState GetTwoHandedState();
            bool IsLeftHanded();
            bool IsVrEnabled();

            bool InverseHold();
        }

        private bool isDominantHandWeapon;
        private TwoHandedStateProvider twoHandedStateSync;
        private Transform leftHandTransform;
        private Transform rightHandTransform;
        private bool recalculatedDirectionOffset = false;
        private VRPlayerSync playerSync { get { return _playerSync == null ? (_playerSync = GetComponentInParent<VRPlayerSync>()) : _playerSync; } }
        private VRPlayerSync _playerSync;

        public void Initialize(ItemDrop.ItemData item, string itemName, bool isDominantHandWeapon, TwoHandedStateProvider twoHandedStateSync, Transform leftHandTransform, Transform rightHandTransform)
        {
            // TODO: remove unused variables
            this.isDominantHandWeapon = isDominantHandWeapon;
            this.twoHandedStateSync = twoHandedStateSync;
            this.leftHandTransform = leftHandTransform;
            this.rightHandTransform = rightHandTransform;
        }


        protected void OnRenderObject()
        {
            if (playerSync == null)
            {
                return;
            }
            transform.SetPositionAndRotation(playerSync.weaponPosition, playerSync.weaponRotation);
        }
    }
}
