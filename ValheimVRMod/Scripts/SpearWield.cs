using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts
{
    class SpearWield : LocalWeaponWield
    {
        private float harpoonHidingTimer = 0;

        void FixedUpdate()
        {

            var throwableManager = GetComponentInChildren<ThrowableManager>();

            if (harpoonHidingTimer > 0)
            {
                harpoonHidingTimer -= Time.fixedDeltaTime;
            }

            if (EquipScript.getRight() == EquipType.SpearChitin)
            {
                MeshRenderer spearRenderer = throwableManager.GetComponent<MeshRenderer>();
                spearRenderer.shadowCastingMode = (harpoonHidingTimer > 0 && !ThrowableManager.isAiming) ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        public void HideHarpoon()
        {
            harpoonHidingTimer = 1;
        }

        protected override Vector3 GetDesiredSingleHandedPosition(Vector3 originalPosition)
        {
            return VHVRConfig.SpearInverseWield() && !ThrowableManager.isAiming ?
                originalPosition + 0.5f * GetWeaponPointingDir() :
                base.GetDesiredSingleHandedPosition(originalPosition);
        }

        protected override Quaternion GetDesiredSingleHandedRotation(Quaternion originalRotation)
        {
            if (ThrowableManager.isAiming)
            {
                var pointing = ThrowableManager.aimDir.normalized;

                if (VHVRConfig.SpearThrowType() != "Classic")
                {
                    return PointAtWeaponAtDirection(pointing);
                }

                Vector3 staticPointing =
                    VHVRConfig.LeftHanded() ?
                    (VRPlayer.leftHandBone.up - VRPlayer.leftHandBone.right) :
                    (VRPlayer.rightHandBone.up + VRPlayer.rightHandBone.right);
                staticPointing = staticPointing.normalized;

                Vector3 handVelocity =
                    (VHVRConfig.LeftHanded() ? VRPlayer.leftHandPhysicsEstimator : VRPlayer.rightHandPhysicsEstimator).GetVelocity();
                float weight = Vector3.Dot(staticPointing, pointing) * handVelocity.magnitude;
                if (weight < 0)
                {
                    pointing = -pointing;
                    weight = -weight;
                }

                // Use a weight to avoid direction flickering when the hand speed 
                return PointAtWeaponAtDirection(
                    Vector3.RotateTowards(staticPointing, pointing, Mathf.Max(weight * 8 - 1, 0), Mathf.Infinity));

            }

            return EquipScript.isSpearEquippedUlnarForward() ?
                originalRotation :
                originalRotation * Quaternion.Euler(180, 0, 0);
        }

        protected override bool TemporaryDisableTwoHandedWield()
        {
            return ThrowableManager.isAiming || ThrowableManager.isThrowing;
        }

        protected override Vector3 GetWeaponPointingDir()
        {
            Vector3 roughDirection = -transform.forward;
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
