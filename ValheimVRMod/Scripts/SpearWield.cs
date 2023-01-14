using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts
{
    class SpearWield : WeaponWield
    {
        void Awake()
        {
            MeshFilter meshFilter = gameObject.GetComponentInChildren<MeshFilter>();
            if (EquipScript.isSpearEquippedRadialForward())
            {
                meshFilter.gameObject.transform.localPosition = Quaternion.AngleAxis(180, Vector3.right) * meshFilter.gameObject.transform.localPosition;
                meshFilter.gameObject.transform.localRotation *= Quaternion.AngleAxis(180, Vector3.right);
            }
        }

        protected override Quaternion GetSingleHandedRotation(Quaternion originalRotation)
        {
            // TODO: consider use this instead of the rotating the mesh filter for inversed spear wield:
            // return EquipScript.isSpearEquippedUlnarForward() ? originalRotation : originalRotation * Quaternion.euler(180, 0, 0);
            return base.GetSingleHandedRotation(originalRotation);
        }

        protected override bool TemporaryDisableTwoHandedWield()
        {
            return SpearManager.IsAiming() || SpearManager.isThrowing;
        }

        protected override Vector3 GetSingleHandedWeaponPointingDir()
        {
            Vector3 roughDirection = EquipScript.isSpearEquippedRadialForward() ? transform.forward : -transform.forward;
            return Vector3.Project(roughDirection, base.GetSingleHandedWeaponPointingDir()).normalized;
        }
        
        protected override float GetPreferredOffsetFromRearHand(float handDist)
        {
            return 0.09f / Mathf.Max(handDist, 0.09f) + 0.2f;
        }        
    }
}
