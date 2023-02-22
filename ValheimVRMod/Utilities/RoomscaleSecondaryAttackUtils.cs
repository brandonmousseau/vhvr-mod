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

        private const float MAX_STAB_ANGLE = 30;

        private static SecondaryAttackCheck[] secondaryAttackChecks = new SecondaryAttackCheck[] {

            delegate(PhysicsEstimator collisionPhysicsEstimator, PhysicsEstimator handPhysicsEstimator) // Sword check
            {
                if (EquipScript.getRight() != EquipType.Sword)
                {
                    return false;
                }

                const float MIN_STAB_SPEED = 3.5f;
                const float MIN_THRUST_DISTANCE = 1f;

                if (!IsStab(handPhysicsEstimator))
                {
                    return false;
                }

                float stabSpeed = Vector3.Dot(handPhysicsEstimator.GetVelocity(), LocalWeaponWield.weaponForward);
                if (stabSpeed < MIN_STAB_SPEED)
                {
                    return false;
                }

                Vector3 thrust = handPhysicsEstimator == null ? Vector3.zero : handPhysicsEstimator.GetLongestLocomotion(1f);
                if (Vector3.Dot(thrust, LocalWeaponWield.weaponForward) < MIN_THRUST_DISTANCE)
                {
                    return false;
                }

                return Vector3.Angle(thrust, LocalWeaponWield.weaponForward) <= MAX_STAB_ANGLE;
            },

            delegate (PhysicsEstimator collisionPhysicsEstimator, PhysicsEstimator handPhysicsEstimator) // Single-handed axe check
            {
                if (EquipScript.getRight() != EquipType.Axe || EquipScript.isTwoHandedAxeEquiped())
                {
                    return false;
                }

                // TODO: consider adjusting these constants to account for weapon length.
                const float MIN_SPEED = 10f;
                const float MIN_THRUST_DISTANCE = 1.8f;
                const float MAX_ANGLE = 60;

                if (IsStab(handPhysicsEstimator))
                {
                    return false;
                }

                Vector3 v = collisionPhysicsEstimator.GetAverageVelocityInSnapshots();
                Vector3 sagittalSpeed = GetSagittalComponentOfVector(v);
                float speedDownAndForward = new Vector2(Mathf.Min(sagittalSpeed.y, 0), Mathf.Max(sagittalSpeed.z, 0)).magnitude;
                if (speedDownAndForward < MIN_SPEED)
                {
                    return false;
                }

                Vector3 thrust = collisionPhysicsEstimator.GetLongestLocomotion(0.5f);
                Vector3 sagittalThrust = GetSagittalComponentOfVector(thrust);
                float thrustDownAndFoward = new Vector2(Mathf.Min(sagittalThrust.y, 0), Mathf.Max(sagittalThrust.z, 0)).magnitude;
                if (thrustDownAndFoward < MIN_THRUST_DISTANCE)
                {
                    return false;
                }

                return Vector3.Angle(thrust, v) <= MAX_ANGLE;
            },

            delegate (PhysicsEstimator collisionPhysicsEstimator, PhysicsEstimator handPhysicsEstimator) // Two-handed axe check
            {
                if (!EquipScript.isTwoHandedAxeEquiped())
                {
                    return false;
                }
                return !LocalWeaponWield.isCurrentlyTwoHanded() || IsStab(handPhysicsEstimator);
            },

            delegate (PhysicsEstimator collisionPhysicsEstimator, PhysicsEstimator handPhysicsEstimator) // Single-handed club check
            {
                if (EquipScript.getRight() != EquipType.Club || EquipScript.isTwoHandedClubEquiped() || IsStab(handPhysicsEstimator))
                {
                    return false;
                }

                // TODO: consider adjusting these constants to account for weapon length variation.
                const float MIN_SPEED = 10f;
                const float MIN_THRUST_DISTANCE = 1.5f;
                const float MAX_ANGLE = 60;

                Vector3 v = collisionPhysicsEstimator.GetAverageVelocityInSnapshots();
                Vector3 sagittalVelocity = GetSagittalComponentOfVector(v);
                float sagittalSpeed = new Vector2(sagittalVelocity.y, Mathf.Max(sagittalVelocity.z, 0)).magnitude;
                if (sagittalSpeed < MIN_SPEED)
                {
                    return false;
                }

                Vector3 thrust = collisionPhysicsEstimator.GetLongestLocomotion(0.5f);
                Vector3 sagittalThrust = GetSagittalComponentOfVector(thrust);
                float sagittalSpeedThrust = new Vector2(sagittalThrust.y, Mathf.Max(sagittalThrust.z, 0)).magnitude;
                if (sagittalSpeedThrust < MIN_THRUST_DISTANCE)
                {
                    return false;
                }

                return Vector3.Angle(thrust, v) <= MAX_ANGLE;
            },

            delegate (PhysicsEstimator collisionPhysicsEstimator, PhysicsEstimator handPhysicsEstimator) // Polearms check
            {
                if (EquipScript.getRight() != EquipType.Polearms || !LocalWeaponWield.isCurrentlyTwoHanded()) {
                    return false;
                }
                    
                const float MIN_SWIPING_SPEED = 2.5f;
                float swipingSpeed = Vector3.ProjectOnPlane(collisionPhysicsEstimator.GetVelocity(), LocalWeaponWield.weaponForward).magnitude;
                return !IsStab(handPhysicsEstimator) && swipingSpeed >= MIN_SWIPING_SPEED;
            },

            delegate (PhysicsEstimator collisionPhysicsEstimator, PhysicsEstimator handPhysicsEstimator) // One-handed knife check
            {
                if (EquipScript.getRight() != EquipType.Knife || !SteamVR_Actions.valheim_Grab.GetState(VRPlayer.dominantHandInputSource)) {
                    return false;
                }

                const float MIN_SPEED = 4f;
                const float MIN_THRUST_DISTANCE = 1f;

                Vector3 velocity = handPhysicsEstimator.GetAverageVelocityInSnapshots();
                if (velocity.magnitude < MIN_SPEED)
                {
                    return false;
                }

                Vector3 thrust = handPhysicsEstimator.GetLongestLocomotion(0.5f);

                return thrust.magnitude >= MIN_THRUST_DISTANCE;
            },

            delegate (PhysicsEstimator collisionPhysicsEstimator, PhysicsEstimator handPhysicsEstimator) // Fist and dual knife check
            {
                if (EquipScript.getRight() != EquipType.DualKnives && (EquipScript.getLeft() != EquipType.None || EquipScript.getRight() != EquipType.None))
                {
                    return false;
                }
                const float MIN_HOOK_SPEED = 5f;
                const float MIN_HOOK_DISTANCE = 1f;
                const float MAX_HOOK_ALIGNMENT_ANGLE = 30f;
                Vector3 v = handPhysicsEstimator.GetAverageVelocityInSnapshots();
                if (v.magnitude < MIN_HOOK_SPEED) {
                    return false;
                }
                Vector3 thrust = handPhysicsEstimator.GetLongestLocomotion(1f);
                return Vector3.Dot(thrust, v.normalized) >= MIN_HOOK_DISTANCE && Vector3.Angle(thrust, v) <= MAX_HOOK_ALIGNMENT_ANGLE;
            }
        };

        private static bool IsStab(PhysicsEstimator physicsEstimator)
        {
            return Vector3.Angle(physicsEstimator.GetAverageVelocityInSnapshots(), LocalWeaponWield.weaponForward) < MAX_STAB_ANGLE;
        }

        private static Vector3 GetSagittalComponentOfVector(Vector3 v)
        {
            Vector3 vInPlayerCoordinates = Player.m_localPlayer.transform.InverseTransformVector(v);
            return new Vector3(0, vInPlayerCoordinates.y, vInPlayerCoordinates.z);
        }
    }
}
