using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts
{
    class SpearWield : LocalWeaponWield
    {
        private float harpoonHidingTimer = 0;

        protected override void Awake()
        {
            base.Awake();
            MeshFilter meshFilter = gameObject.GetComponentInChildren<MeshFilter>();
            if (EquipScript.isSpearEquippedRadialForward())
            {
                meshFilter.gameObject.transform.localPosition = Quaternion.AngleAxis(180, Vector3.right) * meshFilter.gameObject.transform.localPosition;
                meshFilter.gameObject.transform.localRotation *= Quaternion.AngleAxis(180, Vector3.right);
            }
        }

        protected override void OnRenderObject()
        {
            base.OnRenderObject();
            if (ThrowableManager.isThrowing) {
                harpoonHidingTimer = 1;
            }
            else if (ThrowableManager.isAiming)
            {
                harpoonHidingTimer = 0;
            }
        }

        void FixedUpdate()
        {
            if (harpoonHidingTimer > 0)
            {
                harpoonHidingTimer -= Time.fixedDeltaTime;
            }

            if (EquipScript.getRight() == EquipType.SpearChitin)
            {
                MeshRenderer spearRenderer = gameObject.GetComponentInChildren<ThrowableManager>().gameObject.GetComponent<MeshRenderer>();
                spearRenderer.shadowCastingMode = harpoonHidingTimer > 0 ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
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
            return ThrowableManager.isAiming || ThrowableManager.isThrowing;
        }

        protected override Vector3 GetWeaponPointingDir()
        {
            Vector3 roughDirection = EquipScript.isSpearEquippedUlnarForward() ? -transform.forward : transform.forward;
            return Vector3.Project(roughDirection, base.GetWeaponPointingDir()).normalized;
        }
        
        protected override float GetPreferredOffsetFromRearHand(float handDist)
        {
            return 0.09f / Mathf.Max(handDist, 0.09f) + 0.2f;
        }        
    }
}
