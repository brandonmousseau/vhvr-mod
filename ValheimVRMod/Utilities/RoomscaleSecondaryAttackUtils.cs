using System;
using UnityEngine;
using ValheimVRMod.Scripts;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Utilities
{
    public static class RoomscaleSecondaryAttackUtils
    {
        public static bool IsSecondaryAttack(PhysicsEstimator collisionPhysicsEstimator, PhysicsEstimator handPhysicsEstimator)
        {
            foreach (SecondaryAttackCheck check in secondaryAttackChecks)
            {
                if (check(collisionPhysicsEstimator, handPhysicsEstimator))
                {
                    return true;
                }
            }
            return false;
        }

        private delegate bool SecondaryAttackCheck(PhysicsEstimator collisionPhysicsEstimator, PhysicsEstimator handPhysicsEstimator);

        private static SecondaryAttackCheck[] secondaryAttackChecks = new SecondaryAttackCheck[] {

            delegate(PhysicsEstimator collisionPhysicsEstimator, PhysicsEstimator handPhysicsEstimator) // Sword check
            {
                if (EquipScript.getRight() != EquipType.Sword)
                {
                    return false;
                }

                const float MIN_STAB_SPEED = 5f;
                const float MIN_THRUST_DISTANCE = 1f;

                if (!IsStab(handPhysicsEstimator))
                {
                    return false;
                }

                // When wielding with both hands, use the sum of both hands' velocities to make secondary attack easier to trigger.
                Vector3 handVelocity =
                    LocalWeaponWield.isCurrentlyTwoHanded() ?
                    GetHandVelocitySum() :
                    handPhysicsEstimator.GetAverageVelocityInSnapshots();

                float stabSpeed = Vector3.Dot(handVelocity, LocalWeaponWield.weaponForward);
                if (stabSpeed < MIN_STAB_SPEED)
                {
                    return false;
                }

                if (LocalWeaponWield.isCurrentlyTwoHanded())
                {
                    // When holding sword with both hands, waive requirement on thrust distance.
                    return true;
                }

                Vector3 thrust = handPhysicsEstimator == null ? Vector3.zero : handPhysicsEstimator.GetLongestLocomotion(1f);
                return Vector3.Dot(thrust, LocalWeaponWield.weaponForward) >= MIN_THRUST_DISTANCE;
            },

            delegate (PhysicsEstimator collisionPhysicsEstimator, PhysicsEstimator handPhysicsEstimator) // Two-handed axe check
            {
                if (!EquipScript.isTwoHandedAxeEquiped())
                {
                    return false;
                }

                return !IsTwoHandedWithDominantHandInFront() || IsStab(handPhysicsEstimator);
            },

            delegate (PhysicsEstimator collisionPhysicsEstimator, PhysicsEstimator handPhysicsEstimator) // Single-handed axe and club check
            {
                if (EquipScript.getRight() != EquipType.Axe && EquipScript.getRight() != EquipType.Club) {
                    return false;
                }
                
                if (EquipScript.isTwoHandedAxeEquiped() || EquipScript.isTwoHandedClubEquiped() || IsStab(handPhysicsEstimator))
                {
                    return false;
                }

                // TODO: consider adjusting these constants to account for weapon length variation.
                const float MIN_SPEED = 8f;
                const float MIN_THRUST_DISTANCE = 1.5f;
         
                // When wielding with both hands, use the sum of both hands' velocities to make secondary attack easier to trigger.
                Vector3 handVelocity =
                    LocalWeaponWield.isCurrentlyTwoHanded() ?
                    GetHandVelocitySum() :
                    handPhysicsEstimator.GetAverageVelocityInSnapshots();

                if (GetSagittalComponent(handVelocity).magnitude < MIN_SPEED)
                {
                    return false;
                }

                if (LocalWeaponWield.isCurrentlyTwoHanded())
                {
                    // When holding the weapon with both hands, waive requirement on thrust distance.
                    return true;
                }

                Vector3 thrust = collisionPhysicsEstimator.GetLongestLocomotion(0.5f);
                return GetSagittalComponent(thrust).magnitude >= MIN_THRUST_DISTANCE;
            },

            delegate (PhysicsEstimator collisionPhysicsEstimator, PhysicsEstimator handPhysicsEstimator) // Polearms check
            {
                if (EquipScript.getRight() != EquipType.Polearms) {
                    return false;
                }

                return IsTwoHandedWithDominantHandInFront() && !IsStab(handPhysicsEstimator);
            },

            delegate (PhysicsEstimator collisionPhysicsEstimator, PhysicsEstimator handPhysicsEstimator) // One-handed knife check
            {
                if (EquipScript.getRight() != EquipType.Knife || !SteamVR_Actions.valheim_Grab.GetState(VRPlayer.dominantHandInputSource)) {
                    return false;
                }

                const float MIN_SPEED = 6f;
                const float MIN_THRUST_DISTANCE = 1f;

                Vector3 velocity = handPhysicsEstimator.GetAverageVelocityInSnapshots();
                if (velocity.magnitude < MIN_SPEED)
                {
                    return false;
                }

                Vector3 thrust = handPhysicsEstimator.GetLongestLocomotion(0.5f);

                return thrust.magnitude >= MIN_THRUST_DISTANCE;
            },

            delegate (PhysicsEstimator collisionPhysicsEstimator, PhysicsEstimator handPhysicsEstimator) // Fist and claw check
            {
                var leftEquipType = EquipScript.getLeft();
                var rightEquipType = EquipScript.getRight();
                if (!(leftEquipType == EquipType.None && rightEquipType == EquipType.None) && rightEquipType != EquipType.Claws)
                {
                    return false;
                }
                const float MIN_HOOK_SPEED = 6f;
                const float MIN_HOOK_DISTANCE = 1f;
                const float MAX_HOOK_ALIGNMENT_ANGLE = 30f;
                Vector3 v = handPhysicsEstimator.GetAverageVelocityInSnapshots();
                if (v.magnitude < MIN_HOOK_SPEED) {
                    return false;
                }
                Vector3 thrust = handPhysicsEstimator.GetLongestLocomotion(1f);
                return Vector3.Dot(thrust, v.normalized) >= MIN_HOOK_DISTANCE && Vector3.Angle(thrust, v) <= MAX_HOOK_ALIGNMENT_ANGLE;
            },

            delegate (PhysicsEstimator collisionPhysicsEstimator, PhysicsEstimator handPhysicsEstimator) // Dual knife and axe check
            {
                var equipType = EquipScript.getRight();
                if (equipType != EquipType.DualAxes && equipType != EquipType.DualKnives)
                {
                    return false;
                }

                const float MIN_HEW_SPEED = 1f;
                const float MIN_TOTAL_HEW_SPEED = 4f;
                const float MAX_BLADE_DISTANCE = 0.75f;

                var leftHandHewSpeed =
                    Vector3.Dot(VRPlayer.leftHandPhysicsEstimator.GetAverageVelocityInSnapshots(), VRPlayer.leftHand.transform.forward);
                if (leftHandHewSpeed < MIN_HEW_SPEED)
                {
                    return false;
                }

                var rightHandHewSpeed =
                    Vector3.Dot(VRPlayer.rightHandPhysicsEstimator.GetAverageVelocityInSnapshots(), VRPlayer.rightHand.transform.forward);
                if (rightHandHewSpeed < MIN_HEW_SPEED)
                {
                    return false;
                }

                if (leftHandHewSpeed + rightHandHewSpeed < MIN_TOTAL_HEW_SPEED)
                {
                    return false;
                }

                return Vector3.Distance(StaticObjects.leftFist().transform.position, StaticObjects.rightFist().transform.position) < MAX_BLADE_DISTANCE;
            }
        };

        private static bool IsTwoHandedWithDominantHandInFront()
        {
            return LocalWeaponWield.isCurrentlyTwoHanded() && !LocalWeaponWield.IsDominantHandBehind;
        }

        private static Vector3 GetHandVelocitySum()
        {
            return VRPlayer.leftHandPhysicsEstimator.GetAverageVelocityInSnapshots() + VRPlayer.rightHandPhysicsEstimator.GetAverageVelocityInSnapshots();
        }

        private static bool IsStab(PhysicsEstimator physicsEstimator)
        {
            return WeaponUtils.IsStab(
                physicsEstimator.GetAverageVelocityInSnapshots(),
                LocalWeaponWield.weaponForward,
                LocalWeaponWield.isCurrentlyTwoHanded());
        }

        private static Vector3 GetSagittalComponent(Vector3 v)
        {
            return Vector3.ProjectOnPlane(v, Player.m_localPlayer.transform.right);
        }
    }
}
