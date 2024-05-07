using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts
{
    public static class TwoHandedGeometry
    {
        public class DefaultGeometryProvider : WeaponWield.TwoHandedGeometryProvider
        {
            private float distanceBetweenGripAndRearEnd;

            public DefaultGeometryProvider(float distanceBetweenGripAndRearEnd)
            {
                this.distanceBetweenGripAndRearEnd = distanceBetweenGripAndRearEnd;
            }

            public Vector3 GetWeaponPointingDirection(Transform weaponTransform, Vector3 longestLocalExtrusion)
            {
                return longestLocalExtrusion;
            }

            public Vector3 GetDesiredSingleHandedPosition(WeaponWield weaponWield)
            {
                return weaponWield.originalPosition;
            }
            public virtual Quaternion GetDesiredSingleHandedRotation(WeaponWield weaponWield)
            {
                return weaponWield.originalRotation;
            }

            public Vector3 GetPreferredTwoHandedWeaponUp(WeaponWield weaponWield)
            {
                return weaponWield.transform.up;
            }

            public float GetPreferredOffsetFromRearHand(float handDist, bool rearHandIsDominant)
            {
                if (rearHandIsDominant)
                {
                    // Anchor the grip of the weapon in the rear/dominant hand.
                    return -WeaponWield.HAND_CENTER_OFFSET;
                }

                if (handDist > distanceBetweenGripAndRearEnd)
                {
                    // Anchor the rear end of the weapon in the rear/non-dominant hand.
                    return distanceBetweenGripAndRearEnd - WeaponWield.HAND_CENTER_OFFSET;
                }

                // Anchor the grip of the weapon in the front/dominant hand instead.
                return handDist - WeaponWield.HAND_CENTER_OFFSET;
            }
        }

        public class AtgeirGeometryProvider : DefaultGeometryProvider
        {
            public AtgeirGeometryProvider(float distanceBetweenGripAndRearEnd) :
                base(distanceBetweenGripAndRearEnd)
            {
            }

            public override Quaternion GetDesiredSingleHandedRotation(WeaponWield weaponWield)
            {
                // Atgeir wield rotation fix: the tip of the atgeir is pointing at (0.328, -0.145, 0.934) in local coordinates.
                return weaponWield.originalRotation * Quaternion.AngleAxis(-20, Vector3.up) * Quaternion.AngleAxis(-7, Vector3.right);
            }
        }

        public class DundrGeometryProvider : WeaponWield.TwoHandedGeometryProvider
        {
            private static readonly Vector3 DUNDR_OFFSET = new Vector3(-0.1f, 0, 0.2f);
            private static readonly Quaternion DUNDR_OFFSET_ANGLE = Quaternion.Euler(4.28f, 54.29f, 0);

            public Vector3 GetWeaponPointingDirection(Transform weaponTransform, Vector3 longestLocalExtrusion)
            {
                return longestLocalExtrusion;
            }

            public Vector3 GetDesiredSingleHandedPosition(WeaponWield weaponWield)
            {
                return weaponWield.originalPosition + weaponWield.originalRotation * DUNDR_OFFSET_ANGLE * DUNDR_OFFSET;
            }

            public Quaternion GetDesiredSingleHandedRotation(WeaponWield weaponWield)
            {
                return weaponWield.originalRotation * DUNDR_OFFSET_ANGLE;
            }

            public Vector3 GetPreferredTwoHandedWeaponUp(WeaponWield weaponWield)
            {
                return weaponWield.transform.up;
            }

            public float GetPreferredOffsetFromRearHand(float handDist, bool rearHandIsDominant)
            {
                return 0;
            }
        }

        public class LocalSpearGeometryProvider : WeaponWield.TwoHandedGeometryProvider
        {
            private const float HANDLE_LENGTH_BEHIND_CENTER = 1.25f;

            public Vector3 GetWeaponPointingDirection(Transform weaponTransform, Vector3 longestExtrusion)
            {
                return Vector3.Project(-weaponTransform.forward, longestExtrusion).normalized;
            }

            public Vector3 GetDesiredSingleHandedPosition(WeaponWield weaponWield)
            {
                if (!VHVRConfig.SpearInverseWield() || ThrowableManager.isAiming)
                {
                    return weaponWield.originalPosition;
                }
                return weaponWield.originalPosition - 0.5f * (weaponWield.originalRotation * Quaternion.Inverse(weaponWield.offsetFromPointingDir) * Vector3.forward);
            }

            public Quaternion GetDesiredSingleHandedRotation(WeaponWield weaponWield)
            {
                if (ThrowableManager.isAiming)
                {
                    return GetAimingRotation(weaponWield);
                }

                return EquipScript.isSpearEquippedUlnarForward() ?
                    weaponWield.originalRotation :
                    weaponWield.originalRotation * Quaternion.Euler(180, 0, 0);
            }

            public Vector3 GetPreferredTwoHandedWeaponUp(WeaponWield weaponWield)
            {
                return weaponWield.transform.up;
            }

            public float GetPreferredOffsetFromRearHand(float handDist, bool rearHandIsDominant)
            {
                if (rearHandIsDominant)
                {
                    // When the dominant hand is in the back, anchor the very end of the spear handle in it to allow longer attack range.
                    return HANDLE_LENGTH_BEHIND_CENTER - WeaponWield.HAND_CENTER_OFFSET;
                }
                else if (handDist > HANDLE_LENGTH_BEHIND_CENTER)
                {
                    // The hands are far away from each other, anchor the end of the spear handle in the rear/non-dominant hand.
                    return HANDLE_LENGTH_BEHIND_CENTER - WeaponWield.HAND_CENTER_OFFSET;
                }
                else
                {
                    // Anchor the center of the spear in the front/dominant hand to allow shorter attack range.
                    return handDist - WeaponWield.HAND_CENTER_OFFSET;
                }
            }

            private Quaternion GetAimingRotation(WeaponWield weaponWield)
            {
                var pointing = SpearWield.lastFixedUpdatedAimDir.normalized;

                if (VHVRConfig.SpearThrowType() != "Classic")
                {
                    return PointWeaponAtDirection(weaponWield, pointing);
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

                // Use a weight to avoid direction flickering when the hand speed is low.
                return PointWeaponAtDirection(
                    weaponWield, Vector3.RotateTowards(staticPointing, pointing, Mathf.Max(weight * 8 - 1, 0), Mathf.Infinity));
            }

            private Quaternion PointWeaponAtDirection(WeaponWield weaponWield, Vector3 direction)
            {
                return Quaternion.LookRotation(direction, GetPreferredTwoHandedWeaponUp(weaponWield)) * weaponWield.offsetFromPointingDir;
            }
        }

        public class CrossbowGeometryProvider : WeaponWield.TwoHandedGeometryProvider
        {
            bool isPlayerLeftHanded;

            public CrossbowGeometryProvider(bool isPlayerLeftHanded)
            {
                this.isPlayerLeftHanded = isPlayerLeftHanded;
            }

            public Vector3 GetWeaponPointingDirection(Transform weaponTransform, Vector3 longestLocalExtrusion)
            {
                return weaponTransform.forward;
            }
            public Vector3 GetDesiredSingleHandedPosition(WeaponWield weaponWield)
            {
                return weaponWield.originalPosition;
            }
            public Quaternion GetDesiredSingleHandedRotation(WeaponWield weaponWield)
            {
                return isPlayerLeftHanded ? weaponWield.originalRotation * Quaternion.AngleAxis(180, Vector3.forward) : weaponWield.originalRotation;
            }

            public virtual Vector3 GetPreferredTwoHandedWeaponUp(WeaponWield weaponWield)
            {
                return weaponWield.twoHandedState == WeaponWield.TwoHandedState.LeftHandBehind ?
                    -weaponWield.frontHandTransform.right - weaponWield.frontHandTransform.up * 0.5f + weaponWield.frontHandTransform.forward * 0.5f :
                    weaponWield.frontHandTransform.right;
            }

            public float GetPreferredOffsetFromRearHand(float handDist, bool rearHandIsDominant)
            {
                return CrossbowManager.INTERGRIP_DISTANCE;
            }
        }

        public class LocalCrossbowGeometryProvider : CrossbowGeometryProvider
        {
            public LocalCrossbowGeometryProvider() : base(VHVRConfig.LeftHanded()) { }

            public override Vector3 GetPreferredTwoHandedWeaponUp(WeaponWield weaponWield)
            {
                Vector3 rearHandleUp =
                    Vector3.Cross(
                        weaponWield.frontHandTransform.position - weaponWield.rearHandTransform.position,
                        weaponWield.rearHandTransform.right).normalized;
                switch (VHVRConfig.CrossbowSaggitalRotationSource())
                {
                    case "RearHand":
                        return rearHandleUp;
                    case "BothHands":
                        Vector3 frontHandPalmar =
                            weaponWield.twoHandedState == WeaponWield.TwoHandedState.LeftHandBehind ?
                            -weaponWield.frontHandTransform.right :
                            weaponWield.frontHandTransform.right;
                        Vector3 frontHandRadial = weaponWield.frontHandTransform.up;
                        Vector3 frontHandleUp = (frontHandPalmar * 1.73f + frontHandRadial).normalized;
                        return frontHandleUp + rearHandleUp;
                    default:
                        LogUtils.LogWarning("WeaponWield: unknown CrossbowSaggitalRotationSource");
                        return rearHandleUp;
                }
            }
        }
    }
}
