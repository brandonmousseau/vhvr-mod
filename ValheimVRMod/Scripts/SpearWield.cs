using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts
{
    class SpearWield : LocalWeaponWield
    {
        private class MeshFilterRepositioner
        {
            private Transform transform;
            private Quaternion defaultLocalRotation;
            private Vector3 defaultLocalPosition;
            private Vector3 shiftedLocalPosition;

            public MeshFilterRepositioner(Transform transform, Vector3 weaponPointing, bool shouldInvertSpear)
            {
                this.transform = transform;

                if (shouldInvertSpear)
                {
                    transform.localPosition = Quaternion.AngleAxis(180, Vector3.right) * transform.localPosition;
                    transform.localRotation *= Quaternion.AngleAxis(180, Vector3.right);
                    defaultLocalPosition = transform.localPosition;
                    transform.position = transform.position + weaponPointing * 0.5f;
                } 
                else
                {
                    defaultLocalPosition = transform.localPosition;
                }

                defaultLocalRotation = transform.localRotation;
                shiftedLocalPosition = transform.localPosition;
            }

            public void reposition(bool shoudShift, ThrowableManager throwableManager)
            {

                if (ThrowableManager.isAiming)
                {
                    if (transform.GetComponentInChildren<ThrowableManager>() == null && throwableManager != null)
                    {
                        // If this mesh filter has no throwable manager on it, we need to explicitly update
                        // its rotation here to match the throwing direction.
                        transform.SetPositionAndRotation(throwableManager.transform.position, throwableManager.transform.rotation);
                    }
                }
                else
                {
                    transform.localRotation = defaultLocalRotation;
                    transform.localPosition = shoudShift ? shiftedLocalPosition : defaultLocalPosition;
                }
            }
        }

        private float harpoonHidingTimer = 0;
        // Local position of mesh filter shifted forward to better single-handed melee wield.
        private List<MeshFilterRepositioner> meshFilterRepositioners = new List<MeshFilterRepositioner>();

        protected override void Awake()
        {
            base.Awake();
            bool shouldInvertSpear = EquipScript.isSpearEquippedRadialForward();
            Vector3 weaponPointing = GetWeaponPointingDir();
            foreach (Transform childTransform in gameObject.transform)
            {
                if (childTransform.GetComponentInChildren<MeshFilter>() != null)
                {
                    meshFilterRepositioners.Add(
                        new MeshFilterRepositioner(childTransform, weaponPointing, shouldInvertSpear));
                }
            }
        }

        void FixedUpdate()
        {

            bool shouldShiftMeshFilter = !isCurrentlyTwoHanded();
            var throwableManager = GetComponentInChildren<ThrowableManager>();
            foreach (var repositioner in meshFilterRepositioners)
            {
                repositioner.reposition(shouldShiftMeshFilter, throwableManager);
            }

            if (ThrowableManager.isAiming)
            {
                harpoonHidingTimer = 1;
            } 
            else if (harpoonHidingTimer > 0)
            {
                harpoonHidingTimer -= Time.fixedDeltaTime;
            }

            if (EquipScript.getRight() == EquipType.SpearChitin)
            {
                MeshRenderer spearRenderer = throwableManager.GetComponent<MeshRenderer>();
                spearRenderer.shadowCastingMode = (harpoonHidingTimer > 0 && !ThrowableManager.isAiming) ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
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
