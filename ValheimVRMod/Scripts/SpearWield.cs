using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts
{
    class SpearWield : LocalWeaponWield
    {
        private float harpoonHidingTimer = 0;
        private Vector3 defaultMeshFilterLocalPosition;
        // Local position of mesh filter shifted forward to better single-handed melee wield.
        private Vector3 shiftedMeshFilterLocalPosition;
        private MeshFilter meshFilter;


        protected override void Awake()
        {
            base.Awake();
            meshFilter = gameObject.GetComponentInChildren<MeshFilter>();
            if (EquipScript.isSpearEquippedRadialForward())
            {
                meshFilter.gameObject.transform.localPosition = Quaternion.AngleAxis(180, Vector3.right) * meshFilter.gameObject.transform.localPosition;
                meshFilter.gameObject.transform.localRotation *= Quaternion.AngleAxis(180, Vector3.right);
                defaultMeshFilterLocalPosition = meshFilter.transform.localPosition;
                meshFilter.gameObject.transform.position = meshFilter.gameObject.transform.position + GetWeaponPointingDir() * 0.5f;
                shiftedMeshFilterLocalPosition = meshFilter.transform.localPosition;
            }
            else
            {
                defaultMeshFilterLocalPosition = shiftedMeshFilterLocalPosition = meshFilter.transform.localPosition;
            }
        }

        protected override void OnRenderObject()
        {
            base.OnRenderObject();
            bool shiftMeshFilter = false;
            if (ThrowableManager.isThrowing) {
                harpoonHidingTimer = 1;
            }
            else if (ThrowableManager.isAiming)
            {
                harpoonHidingTimer = 0;
            }
            else if (!isCurrentlyTwoHanded())
            {
                shiftMeshFilter = true;
            }

            meshFilter.transform.localPosition =
                shiftMeshFilter ? shiftedMeshFilterLocalPosition : defaultMeshFilterLocalPosition;
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
            bool rearHandIsDominant = (IsPlayerLeftHanded() == (twoHandedState == TwoHandedState.LeftHandBehind));
            float handleLengthBehindCenter = 1.25f;
            if (rearHandIsDominant)
            {
                // When the dominant hand is in the back, anchor the very end of the spear handle in it to allow longer attack range.
                return handleLengthBehindCenter - HAND_CENTER_OFFSET;
            }
            else if (handDist > handleLengthBehindCenter)
            {
                // The hands are far away from each other, anchor the end of the spear handle in the rear/non-dominant hand.
                return handleLengthBehindCenter - HAND_CENTER_OFFSET;
            }
            else
            {
                // Anchor the center of the spear in the front/dominant hand to allow shorter attack range.
                return handDist - HAND_CENTER_OFFSET;
            }
        }        
    }
}
