using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts
{
    public static class TwoHandedGeometry
    {
        public class DefaultGeometryProvider : WeaponWield.TwoHandedGeometryProvider
        {
            protected float distanceBetweenGripAndRearEnd { get; private set; }

            public DefaultGeometryProvider(float distanceBetweenGripAndRearEnd)
            {
                this.distanceBetweenGripAndRearEnd = distanceBetweenGripAndRearEnd;
            }

            public virtual Vector3 GetWeaponPointingDirection(Transform weaponTransform, Vector3 longestLocalExtrusion)
            {
                return longestLocalExtrusion;
            }

            public virtual Vector3 GetDesiredSingleHandedPosition(WeaponWield weaponWield)
            {
                return weaponWield.originalPosition;
            }

            public virtual Quaternion GetDesiredSingleHandedRotation(WeaponWield weaponWield)
            {
                return weaponWield.originalRotation;
            }

            public virtual Vector3 GetPreferredTwoHandedWeaponUp(WeaponWield weaponWield)
            {
                return weaponWield.transform.up;
            }

            public virtual float GetPreferredOffsetFromRearHand(float handDist, bool rearHandIsDominant)
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

            public virtual bool ShouldRotateHandForOneHandedWield()
            {
                return false;
            }

            protected Vector3 GetSingleHandedPointingDirection(WeaponWield weaponWield)
            {
                return GetDesiredSingleHandedRotation(weaponWield) * Quaternion.Inverse(weaponWield.offsetFromPointingDir) * Vector3.forward;
            }
        }

        public class ArmpitAnchoredGeometryProvider : DefaultGeometryProvider
        {
            public ArmpitAnchoredGeometryProvider(float distanceBetweenGripAndRearEnd) : base(distanceBetweenGripAndRearEnd)
            {
            }

            public override Quaternion GetDesiredSingleHandedRotation(WeaponWield weaponWield)
            {
                Vector3 armpitAnchor = (VHVRConfig.LeftHanded() ? VRPlayer.vrikRef.references.leftUpperArm.position : VRPlayer.vrikRef.references.rightUpperArm.position);
                armpitAnchor -= VRPlayer.vrikRef.references.chest.up * 0.25f;
                return Quaternion.LookRotation(VRPlayer.dominantHand.transform.position - armpitAnchor, weaponWield.originalRotation * Vector3.up);
            }

            public override bool ShouldRotateHandForOneHandedWield()
            {
                return true;
            }
        }

        public class AtgeirGeometryProvider : ArmpitAnchoredGeometryProvider
        {
            // Atgeir wield rotation fix: the tip of the atgeir is pointing at (0.328, -0.145, 0.934) in local coordinates.
            private static readonly Quaternion ROTATION_ADJUST = Quaternion.Euler(353, 340, 0);

            public AtgeirGeometryProvider(float distanceBetweenGripAndRearEnd) : base(distanceBetweenGripAndRearEnd * 0.7f)
            {
            }

            public override Vector3 GetDesiredSingleHandedPosition(WeaponWield weaponWield)
            {
                return weaponWield.originalPosition - distanceBetweenGripAndRearEnd * GetSingleHandedPointingDirection(weaponWield);
            }

            public override Quaternion GetDesiredSingleHandedRotation(WeaponWield weaponWield)
            {
                return base.GetDesiredSingleHandedRotation(weaponWield) * ROTATION_ADJUST;
            }

            public override float GetPreferredOffsetFromRearHand(float handDist, bool rearHandIsDominant)
            {
                if (rearHandIsDominant)
                {
                    return distanceBetweenGripAndRearEnd * 0.5f - WeaponWield.HAND_CENTER_OFFSET;
                }

                return base.GetPreferredOffsetFromRearHand(handDist, rearHandIsDominant);
            }
        }

        public class BattleaxeGeometryProvider : ArmpitAnchoredGeometryProvider
        {
            public BattleaxeGeometryProvider(float distanceBetweenGripAndRearEnd) : base(distanceBetweenGripAndRearEnd * 0.3f)
            {
            }

            public override Vector3 GetDesiredSingleHandedPosition(WeaponWield weaponWield)
            {
                return ShouldRotateHandForOneHandedWield() ?
                    weaponWield.originalPosition - 0.37f * GetSingleHandedPointingDirection(weaponWield) :
                    weaponWield.originalPosition - 0.63f * GetSingleHandedPointingDirection(weaponWield);
            }

            public override Quaternion GetDesiredSingleHandedRotation(WeaponWield weaponWield)
            {
                return ShouldRotateHandForOneHandedWield() ? base.GetDesiredSingleHandedRotation(weaponWield) : weaponWield.originalRotation;
            }

            public override bool ShouldRotateHandForOneHandedWield()
            {
                return !SteamVR_Actions.valheim_Grab.GetState(VRPlayer.dominantHandInputSource);
            }
        }

        public class SledgeGeometryProvider : DefaultGeometryProvider
        {
            public SledgeGeometryProvider(float distanceBetweenGripAndRearEnd) : base(distanceBetweenGripAndRearEnd)
            {
            }

            public override Vector3 GetDesiredSingleHandedPosition(WeaponWield weaponWield)
            {
                return weaponWield.originalPosition - 0.3f * GetSingleHandedPointingDirection(weaponWield);
            }
        }

        public class StaffGeometryProvider : DefaultGeometryProvider
        {
            private float twoHandedGripOffset;
            public StaffGeometryProvider(float distanceBetweenGripAndRearEnd) : base(distanceBetweenGripAndRearEnd)
            {
                twoHandedGripOffset = distanceBetweenGripAndRearEnd * 0.25f;
            }

            public override float GetPreferredOffsetFromRearHand(float handDist, bool rearHandIsDominant)
            {
                if (rearHandIsDominant)
                {
                    return twoHandedGripOffset - WeaponWield.HAND_CENTER_OFFSET;
                }

                return base.GetPreferredOffsetFromRearHand(handDist, rearHandIsDominant);

            }
        }

        public class DundrGeometryProvider : WeaponWield.TwoHandedGeometryProvider
        {
            private static readonly Vector3 DUNDR_OFFSET = new Vector3(-0.1f, 0, 0.2f);
            private static readonly Quaternion DUNDR_OFFSET_ANGLE = Quaternion.Euler(0, 55f, 0);

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

            public bool ShouldRotateHandForOneHandedWield()
            {
                return false;
            }
        }

        public abstract class InversibleGeometryProvider : DefaultGeometryProvider
        {
            private const float SPEAR_HANDLE_LENGTH_BEHIND_CENTER = 1.25f;
            private const float SPEAR_FRONT_HAND_DOMINANT_OFFSET = 0.625f;
            private const float SPEAR_SINGLE_HAND_OFFSET = 0.5625f;

            public InversibleGeometryProvider(float distanceBetweenGripAndRearEnd) : base(distanceBetweenGripAndRearEnd) { }

            public override Vector3 GetWeaponPointingDirection(Transform weaponTransform, Vector3 longestExtrusion)
            {
                if (IsSpear())
                {
                    return Vector3.Project(-weaponTransform.forward, longestExtrusion).normalized;
                }
                return base.GetWeaponPointingDirection(weaponTransform, longestExtrusion);
            }

            public override Vector3 GetDesiredSingleHandedPosition(WeaponWield weaponWield)
            {
                if (IsSpear() && !IsAiming())
                {
                    return weaponWield.originalPosition + SPEAR_SINGLE_HAND_OFFSET * GetSingleHandedPointingDirection(weaponWield);
                }
                return weaponWield.originalPosition;
            }

            public override Quaternion GetDesiredSingleHandedRotation(WeaponWield weaponWield)
            {
                return InverseHold() ?
                    weaponWield.originalRotation * Quaternion.Euler(180, 0, 0) :
                    weaponWield.originalRotation;
            }

            public override float GetPreferredOffsetFromRearHand(float handDist, bool rearHandIsDominant)
            {
                if (!IsSpear())
                {
                    return base.GetPreferredOffsetFromRearHand(handDist, rearHandIsDominant);
                }

                if (rearHandIsDominant)
                {
                    // When the dominant hand is in the back, anchor the very end of the spear handle in it to allow longer attack range.
                    return SPEAR_HANDLE_LENGTH_BEHIND_CENTER - WeaponWield.HAND_CENTER_OFFSET;
                }
                else if (handDist > SPEAR_HANDLE_LENGTH_BEHIND_CENTER - SPEAR_FRONT_HAND_DOMINANT_OFFSET)
                {
                    // The hands are far away from each other, anchor the end of the spear handle in the rear/non-dominant hand.
                    return SPEAR_HANDLE_LENGTH_BEHIND_CENTER - WeaponWield.HAND_CENTER_OFFSET;
                }
                else
                {
                    // Anchor the center of the spear in the front/dominant hand to allow shorter attack range.
                    return handDist - WeaponWield.HAND_CENTER_OFFSET + SPEAR_FRONT_HAND_DOMINANT_OFFSET;
                }
            }

            protected abstract bool IsSpear();
            protected abstract bool InverseHold();
            protected virtual bool IsAiming() {  return false; }
        }

        public class LocalSpearGeometryProvider : InversibleGeometryProvider
        {
            public LocalSpearGeometryProvider() : base(0) { }

            protected override bool IsSpear()
            {
                return true;
            }

            protected override bool InverseHold()
            {
                return SpearWield.isSingleHandedWieldCurrentlyInversed && !IsAiming();
            }

            protected override bool IsAiming()
            {
                return ThrowableManager.isAiming || ThrowableManager.preAimingInTwoStagedThrow;
            }

            public override Quaternion GetDesiredSingleHandedRotation(WeaponWield weaponWield)
            {
                var staticRotation = base.GetDesiredSingleHandedRotation(weaponWield);

                if (!ThrowableManager.isAiming)
                {
                    return staticRotation;
                }

                var pointing = SpearWield.lastFixedUpdatedAimDir.normalized;

                if (VHVRConfig.SpearThrowType() != "Classic")
                {
                    return weaponWield.getAimingRotation(pointing, GetPreferredTwoHandedWeaponUp(weaponWield));
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
                return weaponWield.getAimingRotation(
                      Vector3.RotateTowards(staticPointing, pointing, Mathf.Max(weight * 8 - 1, 0), Mathf.Infinity),
                      GetPreferredTwoHandedWeaponUp(weaponWield));
            }
        }

        public class LocalKnifeGeometryProvider : InversibleGeometryProvider
        {
            public static bool shouldInverseHold { get { return SteamVR_Actions.valheim_Grab.GetState(VRPlayer.dominantHandInputSource); } }

            public LocalKnifeGeometryProvider(float distanceBetweenGripAndRearEnd) : base(distanceBetweenGripAndRearEnd) { }

            protected override bool IsSpear()
            {
                return false;
            }

            protected override bool InverseHold()
            {
                return shouldInverseHold;
            }
        }

        public class ScytheGeometryProvider : DefaultGeometryProvider
        {
            bool isPlayerLeftHanded;

            public ScytheGeometryProvider(bool isPlayerLeftHanded, float distanceBetweenGripAndRearEnd) : base(distanceBetweenGripAndRearEnd)
            {
                this.isPlayerLeftHanded = isPlayerLeftHanded;
            }

            public override Vector3 GetWeaponPointingDirection(Transform weaponTransform, Vector3 longestLocalExtrusion)
            {
                return -longestLocalExtrusion;
            }

            public override Quaternion GetDesiredSingleHandedRotation(WeaponWield weaponWield)
            {
                return isPlayerLeftHanded ? weaponWield.originalRotation * Quaternion.AngleAxis(180, Vector3.forward) : weaponWield.originalRotation;
            }

            public override Vector3 GetPreferredTwoHandedWeaponUp(WeaponWield weaponWield)
            {
                return weaponWield.twoHandedState == WeaponWield.TwoHandedState.LeftHandBehind ?
                    weaponWield.frontHandTransform.up:
                    -weaponWield.frontHandTransform.up;
            }

            public override float GetPreferredOffsetFromRearHand(float handDist, bool rearHandIsDominant)
            {
                if (handDist > distanceBetweenGripAndRearEnd)
                {
                    // Anchor the rear end of the weapon in the rear/non-dominant hand.
                    return distanceBetweenGripAndRearEnd - WeaponWield.HAND_CENTER_OFFSET;
                }

                // Anchor the grip of the weapon in the front/dominant hand instead.
                return handDist - WeaponWield.HAND_CENTER_OFFSET;
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

            public bool ShouldRotateHandForOneHandedWield()
            {
                return false;
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

        public class RemoteGeometryProvider : InversibleGeometryProvider
        {
            private WeaponWieldSync.TwoHandedStateProvider twoHandedStateProvider;
            public RemoteGeometryProvider(float distanceBetweenGripAndRearEnd, WeaponWieldSync.TwoHandedStateProvider twoHandedStateProvider) :
                base(distanceBetweenGripAndRearEnd) {
                this.twoHandedStateProvider = twoHandedStateProvider;
            }

            protected override bool IsSpear()
            {
                // Since the item name is null for remote player's weapons,
                // it is hard to conclusively infer whether the weapon is a spear.
                // If it is inversed currently and its handle length is too long to be of a knife,
                // we can confidently infer that it is a spear and should treat it like one.
                // If it is not currently inversed, we cannot be conclusive. But if it is not currently inversed,
                // we can treat it as a non-spear without causing any visual difference.
                return distanceBetweenGripAndRearEnd > 0.5f && InverseHold();
            }

            protected override bool InverseHold()
            {
                return twoHandedStateProvider.InverseHold();
            }
        }
    }
}
