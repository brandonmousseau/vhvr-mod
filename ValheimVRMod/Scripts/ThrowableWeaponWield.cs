using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts {
    class ThrowableWeaponWield : WeaponWield
    {
        private SpearManager spearManager;
        void Awake()
        {
            MeshFilter meshFilter = gameObject.GetComponentInChildren<MeshFilter>();
            if (EquipScript.isSpearEquippedRadialForward())
            {
                meshFilter.gameObject.transform.localRotation *= Quaternion.AngleAxis(180, Vector3.right);
                // LogUtils.LogWarning("Spear item: " + itemName);
                switch (itemName) // TODO: is this the right property to check?
                {
                    case "SpearChitin":
                        meshFilter.gameObject.transform.localPosition = new Vector3(0, 0, -0.2f);
                        break;
                    case "SpearElderbark":
                    case "SpearBronze":
                    case "SpearCarapace":
                        meshFilter.gameObject.transform.localPosition = new Vector3(0, 0, -1.15f);
                        break;
                }
            }

            // TODO: consider renaming this ThrowableManager
            spearManager = meshFilter.gameObject.AddComponent<SpearManager>();
            spearManager.weaponWield = this;
        }
        
        protected override GetSingleHandedRotation(Quaternion originalRotation) {
            // TODO: consider use this instead of the rotating the mesh filter for inversed spear wield:
            // return EquipScript.isSpearEquippedUlnarForward() ? originalRotation : originalRotation * Quaternion.euler(180, 0, 0);
            return base.GetSingleHandedRotation(originalRotation);
        }
        
        protected override bool TemporaryDisableTwoHandedWield()
        {
            return EquipScript.isSpearEquipped() && (SpearManager.IsAiming() || SpearManager.isThrowing);
        }
        
        protected override Vector3 GetSingleHandedWeaponForward() {
            Vector3 roughDirection = EquipScript.isSpearEquippedRadialForward() ? VRPlayer.dominantHand.up : -VRPlayer.dominantHand.up;
            return Vector3.Project(roughDirection, base.GetSingleHandedWeaponForward()).normalized;
        }
    }
}
