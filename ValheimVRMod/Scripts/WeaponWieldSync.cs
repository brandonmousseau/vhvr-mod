using UnityEngine;

namespace ValheimVRMod.Scripts
{
    // Synchronizes weapon orientation of other (i. e. non-local) players.
    public class WeaponWieldSync : MonoBehaviour
    {
        private bool isDominantHandWeapon;
        private bool recalculatedDirectionOffset = false;
        private VRPlayerSync playerSync { get { return _playerSync == null ? (_playerSync = GetComponentInParent<VRPlayerSync>()) : _playerSync; } }
        private VRPlayerSync _playerSync;
        
        public void Initialize(ItemDrop.ItemData item, string itemName, bool isDominantHandWeapon)
        {
            this.isDominantHandWeapon = isDominantHandWeapon;
        }

        void OnRenderObject()
        {
            if (playerSync == null)
            {
                return;
            }
            transform.SetPositionAndRotation(playerSync.weaponPosition, playerSync.weaponRotation);

        }
    }
}
