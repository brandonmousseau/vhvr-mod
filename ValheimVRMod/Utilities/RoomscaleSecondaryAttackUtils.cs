using System;
using UnityEngine;
using ValheimVRMod.Scripts;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Utilities
{
    public static class RoomscaleSecondaryAttackUtils
    {
        public static bool IsSecondaryAttack(WeaponCollision weaponCollision)
        {
            foreach (SecondaryAttackCheck check in secondaryAttackChecks)
            {
                if (check(weaponCollision))
                {
                    return true;
                }
            }
            return false;
        }

        private delegate bool SecondaryAttackCheck(WeaponCollision weaponCollision);

        private const float MAX_STAB_ANGLE = 30;

        private static SecondaryAttackCheck[] secondaryAttackChecks = new SecondaryAttackCheck[] {

            delegate(WeaponCollision weaponCollision) // Sword check
            {
                if (EquipScript.getRight() != EquipType.Sword)
                {
                    return false;
                }

                const float MIN_STAB_SPEED = 3.5f;
                const float MIN_THRUST_DISTANCE = 1f;

                if (!IsStab(weaponCollision.mainHandPhysicsEstimator))
                {
                    return false;
                }

                float stabSpeed = Vector3.Dot(weaponCollision.mainHandPhysicsEstimator.GetAverageVelocityInSnapshots(), WeaponWield.weaponForward);
                if (stabSpeed < MIN_STAB_SPEED)
                {
                    return false;
                }

                Vector3 thrust = weaponCollision.mainHandPhysicsEstimator == null ? Vector3.zero : weaponCollision.mainHandPhysicsEstimator.GetLongestLocomotion(1f);
                if (Vector3.Dot(thrust, WeaponWield.weaponForward) < MIN_THRUST_DISTANCE)
                {
                    return false;
                }

                return Vector3.Angle(thrust, WeaponWield.weaponForward) <= MAX_STAB_ANGLE;
            },

            delegate (WeaponCollision weaponCollision) // Single-handed axe check
            {
                if (EquipScript.getRight() != EquipType.Axe || EquipScript.isTwoHandedAxeEquiped())
                {
                    return false;
                }

                // TODO: consider adjusting these constants to account for weapon length.
                const float MIN_SPEED = 10f;
                const float MIN_THRUST_DISTANCE = 1.8f;
                const float MAX_ANGLE = 60;

                if (IsStab(weaponCollision.mainHandPhysicsEstimator))
                {
                    return false;
                }

                Vector3 v = weaponCollision.physicsEstimator.GetAverageVelocityInSnapshots();
                Vector3 sagittalSpeed = GetSagittalComponentOfVector(v);
                float speedDownAndForward = new Vector2(Mathf.Min(sagittalSpeed.y, 0), Mathf.Max(sagittalSpeed.z, 0)).magnitude;
                if (speedDownAndForward < MIN_SPEED)
                {
                    return false;
                }

                Vector3 thrust = weaponCollision.physicsEstimator.GetLongestLocomotion(0.5f);
                Vector3 sagittalThrust = GetSagittalComponentOfVector(thrust);
                float thrustDownAndFoward = new Vector2(Mathf.Min(sagittalThrust.y, 0), Mathf.Max(sagittalThrust.z, 0)).magnitude;
                if (thrustDownAndFoward < MIN_THRUST_DISTANCE)
                {
                    return false;
                }

                return Vector3.Angle(thrust, v) <= MAX_ANGLE;
            },

            delegate (WeaponCollision weaponCollision) // Two-handed axe check
            {
                if (!EquipScript.isTwoHandedAxeEquiped())
                {
                    return false;
                }
                return !WeaponWield.isCurrentlyTwoHanded() || IsStab(weaponCollision.mainHandPhysicsEstimator);
            },

            delegate (WeaponCollision weaponCollision) // Single-handed club check
            {
                if (EquipScript.getRight() != EquipType.Club || EquipScript.isTwoHandedClubEquiped() || IsStab(weaponCollision.mainHandPhysicsEstimator))
                {
                    return false;
                }

                // TODO: consider adjusting these constants to account for weapon length variation.
                const float MIN_SPEED = 10f;
                const float MIN_THRUST_DISTANCE = 1.5f;
                const float MAX_ANGLE = 60;

                Vector3 v = weaponCollision.physicsEstimator.GetAverageVelocityInSnapshots();
                Vector3 sagittalVelocity = GetSagittalComponentOfVector(v);
                float sagittalSpeed = new Vector2(sagittalVelocity.y, Mathf.Max(sagittalVelocity.z, 0)).magnitude;
                if (sagittalSpeed < MIN_SPEED)
                {
                    return false;
                }

                Vector3 thrust = weaponCollision.physicsEstimator.GetLongestLocomotion(0.5f);
                Vector3 sagittalThrust = GetSagittalComponentOfVector(thrust);
                float sagittalSpeedThrust = new Vector2(sagittalThrust.y, Mathf.Max(sagittalThrust.z, 0)).magnitude;
                if (sagittalSpeedThrust < MIN_THRUST_DISTANCE)
                {
                    return false;
                }

                return Vector3.Angle(thrust, v) <= MAX_ANGLE;
            },

            delegate (WeaponCollision weaponCollision) // Polearms check
            {
                if (EquipScript.getRight() != EquipType.Polearms || !WeaponWield.isCurrentlyTwoHanded()) {
                    return false;
                }
                    
                const float MIN_SWIPING_SPEED = 2.5f;
                float swipingSpeed = Vector3.ProjectOnPlane(weaponCollision.physicsEstimator.GetAverageVelocityInSnapshots(), WeaponWield.weaponForward).magnitude;
                return !IsStab(weaponCollision.mainHandPhysicsEstimator) && swipingSpeed >= MIN_SWIPING_SPEED;
            },

            delegate (WeaponCollision weaponCollision) // Knife check
            {
                if (EquipScript.getRight() != EquipType.Knife || !SteamVR_Actions.valheim_Grab.GetState(VRPlayer.dominantHandInputSource)) {
                    return false;
                }

                const float MIN_SPEED = 4f;
                const float MIN_THRUST_DISTANCE = 1f;

                Vector3 velocity = weaponCollision.mainHandPhysicsEstimator.GetAverageVelocityInSnapshots();
                if (velocity.magnitude < MIN_SPEED)
                {
                    return false;
                }

                Vector3 thrust = weaponCollision.mainHandPhysicsEstimator.GetLongestLocomotion(0.5f);

                return thrust.magnitude >= MIN_THRUST_DISTANCE && IsStab(weaponCollision.mainHandPhysicsEstimator);
            }
        };

        private static bool IsStab(PhysicsEstimator physicsEstimator)
        {
            return Vector3.Angle(physicsEstimator.GetAverageVelocityInSnapshots(), WeaponWield.weaponForward) < MAX_STAB_ANGLE;
        }

        private static Vector3 GetSagittalComponentOfVector(Vector3 v)
        {
            Vector3 vInPlayerCoordinates = Player.m_localPlayer.transform.InverseTransformVector(v);
            return new Vector3(0, vInPlayerCoordinates.y, vInPlayerCoordinates.z);
        }
    }
}
