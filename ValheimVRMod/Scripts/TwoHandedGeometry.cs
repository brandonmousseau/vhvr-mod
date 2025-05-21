using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;
using static ValheimVRMod.Scripts.WeaponWield;

namespace ValheimVRMod.Scripts
{
    public static class TwoHandedGeometry
    {
        public class DefaultGeometryProvider : TwoHandedGeometryProvider
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
                return InverseHoldForDominantHand() ?
                    weaponWield.originalRotation * Quaternion.Euler(180, 0, 0) :
                    weaponWield.originalRotation;
            }

            public virtual Vector3 GetPreferredTwoHandedWeaponUp(WeaponWield weaponWield)
            {
                return weaponWield.transform.up;
            }

            public virtual float GetPreferredOffsetFromRearHand(float handDist, bool rearHandIsDominant)
            {
                if (handDist > distanceBetweenGripAndRearEnd)
                {
                    // Anchor the rear end of the weapon in the rear/non-dominant hand.
                    return distanceBetweenGripAndRearEnd - WeaponWield.HAND_CENTER_OFFSET;
                }

                // Anchor the grip of the weapon in the front/dominant hand instead.
                return handDist - WeaponWield.HAND_CENTER_OFFSET;
            }

            public virtual bool InverseHoldForDominantHand()
            {
                return false;
            }

            public virtual bool ShouldRotateHandForOneHandedWield()
            {
                return false;
            }

            protected Vector3 GetSingleHandedPointingDirection(WeaponWield weaponWield)
            {
                return GetDesiredSingleHandedRotation(weaponWield) * Quaternion.Inverse(weaponWield.offsetFromPointingDir) * Vector3.forward;
            }

            protected static Quaternion GetArmpitAnchoredDominantHandedRotation(WeaponWield weaponWield)
            {
                Vector3 armpitAnchor = (VHVRConfig.LeftHanded() ? VRPlayer.vrikRef.references.leftUpperArm.position : VRPlayer.vrikRef.references.rightUpperArm.position);
                armpitAnchor -= VRPlayer.vrikRef.references.chest.up * 0.25f;
                return Quaternion.LookRotation(VRPlayer.dominantHand.transform.position - armpitAnchor, weaponWield.originalRotation * Vector3.up);
            }

        }


        public class AtgeirGeometryProvider : DefaultGeometryProvider
        {
            // Atgeir wield rotation fix: the tip of the atgeir is pointing at (0.328, -0.145, 0.934) in local coordinates.
            protected static readonly Quaternion ROTATION_ADJUST = Quaternion.Euler(353, 340, 0);

            private const float SHORT_GRIP_REAR_HAND_DOMINANT_OFFSET = 0.2f;
            private const float SHORT_GRIP_FRONT_HAND_DOMINANT_OFFSET = 1.2f;

            private LongGripStateProvider longGripStateProvider;
            private float longGripOffset;
            private float shortGripMaxOffset;
            private float singleHandGripOffset;

            public AtgeirGeometryProvider(float distanceBetweenGripAndRearEnd, LongGripStateProvider longGripStateProvider) :
                base(distanceBetweenGripAndRearEnd)
            {
                this.longGripStateProvider = longGripStateProvider;
                longGripOffset = distanceBetweenGripAndRearEnd * 0.375f - WeaponWield.HAND_CENTER_OFFSET;
                shortGripMaxOffset = distanceBetweenGripAndRearEnd * 0.75f - WeaponWield.HAND_CENTER_OFFSET;
                singleHandGripOffset = -0.875f * distanceBetweenGripAndRearEnd;
            }

            public override Vector3 GetDesiredSingleHandedPosition(WeaponWield weaponWield)
            {
                return weaponWield.originalPosition + singleHandGripOffset * GetSingleHandedPointingDirection(weaponWield);
            }

            public override Quaternion GetDesiredSingleHandedRotation(WeaponWield weaponWield)
            {
                return base.GetDesiredSingleHandedRotation(weaponWield) * ROTATION_ADJUST;
            }

            public override float GetPreferredOffsetFromRearHand(float handDist, bool rearHandIsDominant)
            {
                if (longGripStateProvider.ShouldUseLongGrip())
                {
                    return longGripOffset;
                }

                if (rearHandIsDominant)
                {
                    return -SHORT_GRIP_REAR_HAND_DOMINANT_OFFSET - WeaponWield.HAND_CENTER_OFFSET;
                }

                if (handDist > SHORT_GRIP_FRONT_HAND_DOMINANT_OFFSET)
                {
                    return shortGripMaxOffset;
                }

                // Anchor the center of the spear in the front/dominant hand
                return handDist - SHORT_GRIP_FRONT_HAND_DOMINANT_OFFSET + shortGripMaxOffset;
            }
        }

        public class LocalAtgeirGeometryProvider : AtgeirGeometryProvider
        {
            public static bool UsingArmpitAnchor { get { return !SteamVR_Actions.valheim_Grab.GetState(VRPlayer.dominantHandInputSource); } }

            public LocalAtgeirGeometryProvider(float distanceBetweenGripAndRearEnd, LongGripStateProvider longGripStateProvider) :
                base(distanceBetweenGripAndRearEnd, longGripStateProvider) { }

            public override Quaternion GetDesiredSingleHandedRotation(WeaponWield weaponWield)
            {
                return ShouldRotateHandForOneHandedWield() ?
                    GetArmpitAnchoredDominantHandedRotation(weaponWield) * ROTATION_ADJUST :
                    weaponWield.originalRotation * ROTATION_ADJUST;
            }

            public override bool ShouldRotateHandForOneHandedWield()
            {
                return UsingArmpitAnchor;
            }
        }

        public class BattleaxeGeometryProvider : DefaultGeometryProvider
        {
            private const float SHORT_GRIP_REAR_HAND_DOMINANT_OFFSET = 0.3f;
            private const float SHORT_GRIP_FRONT_HAND_DOMINANT_OFFSET = 0.7f;

            private LongGripStateProvider longGripStateProvider;

            public BattleaxeGeometryProvider(float distanceBetweenGripAndRearEnd, LongGripStateProvider longGripStateProvider) :
                base(distanceBetweenGripAndRearEnd)
            {
                this.longGripStateProvider = longGripStateProvider;
            }

            public override Vector3 GetDesiredSingleHandedPosition(WeaponWield weaponWield)
            {
                return weaponWield.originalPosition - 0.4f * GetSingleHandedPointingDirection(weaponWield);
            }

            public override float GetPreferredOffsetFromRearHand(float handDist, bool rearHandIsDominant)
            {
                if (longGripStateProvider.ShouldUseLongGrip())
                {
                    return base.GetPreferredOffsetFromRearHand(handDist, rearHandIsDominant);
                }

                if (rearHandIsDominant)
                {
                    return -SHORT_GRIP_REAR_HAND_DOMINANT_OFFSET - WeaponWield.HAND_CENTER_OFFSET;
                }

                if (handDist > SHORT_GRIP_FRONT_HAND_DOMINANT_OFFSET)
                {
                    return distanceBetweenGripAndRearEnd - WeaponWield.HAND_CENTER_OFFSET;
                }

                // Anchor the center of the spear in the front/dominant hand
                return handDist - SHORT_GRIP_FRONT_HAND_DOMINANT_OFFSET + distanceBetweenGripAndRearEnd - WeaponWield.HAND_CENTER_OFFSET;
            }
        }

        public class LocalBattleaxeGeometryProvider : BattleaxeGeometryProvider
        {
            public static bool UsingArmpitAnchor { get { return !SteamVR_Actions.valheim_Grab.GetState(VRPlayer.dominantHandInputSource); } }

            public LocalBattleaxeGeometryProvider(float distanceBetweenGripAndRearEnd, LongGripStateProvider longGripStateProvider) :
                base(distanceBetweenGripAndRearEnd, longGripStateProvider) { }

            public override Vector3 GetDesiredSingleHandedPosition(WeaponWield weaponWield)
            {
                return ShouldRotateHandForOneHandedWield() ?
                    weaponWield.originalPosition - 0.37f * GetSingleHandedPointingDirection(weaponWield) :
                    weaponWield.originalPosition - 0.63f * GetSingleHandedPointingDirection(weaponWield);
            }

            public override Quaternion GetDesiredSingleHandedRotation(WeaponWield weaponWield)
            {
                return ShouldRotateHandForOneHandedWield() ? GetArmpitAnchoredDominantHandedRotation(weaponWield) : weaponWield.originalRotation;
            }

            public override bool ShouldRotateHandForOneHandedWield()
            {
                return UsingArmpitAnchor;
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

        public class LocalSledgeGeometryProvider : SledgeGeometryProvider
        {
            private const float MIN_SWING_SPEED = 1;
            private const float SWING_SPEED_CAP = 4;

            public LocalSledgeGeometryProvider(float distanceBetweenGripAndRearEnd) : base(distanceBetweenGripAndRearEnd)
            {
            }

            public override Vector3 GetDesiredSingleHandedPosition(WeaponWield weaponWield)
            {
                if (SteamVR_Actions.valheim_Grab.GetState(VRPlayer.dominantHandInputSource))
                {
                    return base.GetDesiredSingleHandedPosition(weaponWield);
                }

                if (!VHVRConfig.TwoHandedWield())
                {
                    return weaponWield.originalPosition;
                }

                var physicsEstimator = VHVRConfig.LeftHanded() ? VRPlayer.leftHandPhysicsEstimator : VRPlayer.rightHandPhysicsEstimator;
                var speed = physicsEstimator.GetVelocity().magnitude;

                if (speed > SWING_SPEED_CAP)
                {
                    return weaponWield.originalPosition;
                }

                if (speed < MIN_SWING_SPEED)
                {
                    return base.GetDesiredSingleHandedPosition(weaponWield);
                }

                return Vector3.Lerp(
                    base.GetDesiredSingleHandedPosition(weaponWield), 
                    weaponWield.originalPosition,
                    Mathf.InverseLerp(MIN_SWING_SPEED, SWING_SPEED_CAP, speed));
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

        public class DundrGeometryProvider : TwoHandedGeometryProvider
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

            public bool InverseHoldForDominantHand()
            {
                return false;
            }

            public bool ShouldRotateHandForOneHandedWield()
            {
                return false;
            }
        }

        public abstract class SpearGeometryProvider : DefaultGeometryProvider
        {
            private const float SPEAR_HANDLE_LENGTH_BEHIND_CENTER = 1.25f;
            private const float SPEAR_SINGLE_HAND_OFFSET = 0.5625f;
            private const float SHORT_GRIP_REAR_HAND_DOMINANT_OFFSET = -0.625f;
            private const float SHORT_GRIP_FRONT_HAND_DOMINANT_OFFSET = 1f;

            private LongGripStateProvider longGripStateProvider;

            public SpearGeometryProvider(LongGripStateProvider longGripStateProvider) : base(0) {
                this.longGripStateProvider = longGripStateProvider;
            }

            public override Vector3 GetWeaponPointingDirection(Transform weaponTransform, Vector3 longestExtrusion)
            {
                return Vector3.Project(-weaponTransform.forward, longestExtrusion).normalized;
            }

            public override Vector3 GetDesiredSingleHandedPosition(WeaponWield weaponWield)
            {
                return weaponWield.originalPosition + SPEAR_SINGLE_HAND_OFFSET * GetSingleHandedPointingDirection(weaponWield);
            }

            public override float GetPreferredOffsetFromRearHand(float handDist, bool rearHandIsDominant)
            {
                if (longGripStateProvider.ShouldUseLongGrip())
                {
                    // Anchor the very end of the spear handle in it to allow longer attack range.
                    return SPEAR_HANDLE_LENGTH_BEHIND_CENTER - WeaponWield.HAND_CENTER_OFFSET;
                }

                if (rearHandIsDominant)
                {
                    return -SHORT_GRIP_REAR_HAND_DOMINANT_OFFSET - WeaponWield.HAND_CENTER_OFFSET;
                }

                if (handDist > SHORT_GRIP_FRONT_HAND_DOMINANT_OFFSET)
                {
                    // The hands are far away from each other, anchor the end of the spear handle in the rear/non-dominant hand.
                    return SPEAR_HANDLE_LENGTH_BEHIND_CENTER - WeaponWield.HAND_CENTER_OFFSET;
                }

                // Anchor the center of the spear in the front/dominant hand to allow shorter attack range.
                return handDist - SHORT_GRIP_FRONT_HAND_DOMINANT_OFFSET + SPEAR_HANDLE_LENGTH_BEHIND_CENTER - WeaponWield.HAND_CENTER_OFFSET;
            }

        }

        public class LocalSpearGeometryProvider : SpearGeometryProvider
        {
            public LocalSpearGeometryProvider(LongGripStateProvider longGripStateProvider) : base(longGripStateProvider) { }

            public override bool InverseHoldForDominantHand()
            {
                return !SpearWield.IsWeaponPointingUlnar && !IsAiming();
            }

            public override Vector3 GetDesiredSingleHandedPosition(WeaponWield weaponWield)
            {
                return IsAiming() ? weaponWield.originalPosition : base.GetDesiredSingleHandedPosition(weaponWield);
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

            private bool IsAiming()
            {
                return ThrowableManager.isAiming || ThrowableManager.preAimingInTwoStagedThrow;
            }
        }

        public class LocalKnifeGeometryProvider : DefaultGeometryProvider
        {
            public LocalKnifeGeometryProvider(float distanceBetweenGripAndRearEnd) : base(distanceBetweenGripAndRearEnd) { }

            public override bool InverseHoldForDominantHand()
            {
                return LocalWeaponWield.IsWeaponPointingUlnar;
            }
        }

        public class LocalSwordGeometryProvider : DefaultGeometryProvider
        {
            public LocalSwordGeometryProvider(float distanceBetweenGripAndRearEnd) : base(distanceBetweenGripAndRearEnd) { }

            public override bool InverseHoldForDominantHand()
            {
                return LocalWeaponWield.IsWeaponPointingUlnar;
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
                return Vector3.Cross(weaponWield.frontHandTransform.forward, weaponWield.frontHandTransform.position - weaponWield.rearHandTransform.position);
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

        public class CrossbowGeometryProvider : TwoHandedGeometryProvider
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

            public bool InverseHoldForDominantHand()
            {
                return false;
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

        public class RemoteGeometryProvider : DefaultGeometryProvider
        {
            private WeaponWieldSync.TwoHandedStateProvider twoHandedStateProvider;
            public RemoteGeometryProvider(float distanceBetweenGripAndRearEnd, WeaponWieldSync.TwoHandedStateProvider twoHandedStateProvider) :
                base(distanceBetweenGripAndRearEnd) {
                this.twoHandedStateProvider = twoHandedStateProvider;
            }

            public override bool InverseHoldForDominantHand()
            {
                return twoHandedStateProvider.InverseHold();
            }
        }

        public class RemoteSpearGeometryProvider : SpearGeometryProvider
        {
            private WeaponWieldSync.TwoHandedStateProvider twoHandedStateProvider;
            public RemoteSpearGeometryProvider(WeaponWieldSync.TwoHandedStateProvider twoHandedStateProvider, LongGripStateProvider longGripStateProvider) :
                base(longGripStateProvider)
            {
                this.twoHandedStateProvider = twoHandedStateProvider;
            }

            public override bool InverseHoldForDominantHand()
            {
                return twoHandedStateProvider.InverseHold();
            }
        }
    }
}
