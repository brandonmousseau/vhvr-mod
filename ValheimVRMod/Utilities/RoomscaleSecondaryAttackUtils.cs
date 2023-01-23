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

        private static SecondaryAttackCheck[] secondaryAttackChecks = new SecondaryAttackCheck[] {

            delegate(WeaponCollision weaponCollision) // Sword check
            {
                if (EquipScript.getRight() != EquipType.Sword)
                {
                    return false;
                }

                const float MIN_STAB_SPEED = 3.5f;
                const float MIN_THRUST_DISTANCE = 1f;
                const float MAX_STAB_ANGLE = 30;

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
                if (EquipScript.getRight() != EquipType.Axe || EquipScript.isTwoHandedAxeEquiped() || weaponCollision.lastAttackWasStab)
                {
                    return false;
                }

                // TODO: consider adjusting these constants to account for weapon length.
                const float MIN_SPEED = 10f;
                const float MIN_THRUST_DISTANCE = 1.8f;
                const float MAX_ANGLE = 60;

                Vector3 swingVelocity = weaponCollision.physicsEstimator.GetAverageVelocityInSnapshots();
                Vector3 swingVelocityInPlayerCoordinates = Player.m_localPlayer.transform.InverseTransformVector(swingVelocity);
                float speedDownAndForward = new Vector2(Mathf.Min(swingVelocityInPlayerCoordinates.y, 0), Mathf.Max(swingVelocityInPlayerCoordinates.z, 0)).magnitude;
                if (speedDownAndForward < MIN_SPEED)
                {
                    return false;
                }

                Vector3 thrust = weaponCollision.physicsEstimator.GetLongestLocomotion(0.5f);
                Vector3 thrustInPlayerCoordinates = Player.m_localPlayer.transform.InverseTransformVector(thrust);
                float thrustDownAndFoward = new Vector2(Mathf.Min(thrustInPlayerCoordinates.y, 0), Mathf.Max(thrustInPlayerCoordinates.z, 0)).magnitude;
                if (thrustDownAndFoward < MIN_THRUST_DISTANCE)
                {
                    return false;
                }

                return Vector3.Angle(thrust, swingVelocity) <= MAX_ANGLE;
            },

            delegate (WeaponCollision weaponCollision) // Two-handed axe check
            {
                if (!EquipScript.isTwoHandedAxeEquiped())
                {
                    return false;
                }
                return !WeaponWield.isCurrentlyTwoHanded() || weaponCollision.lastAttackWasStab;
            },

            delegate (WeaponCollision weaponCollision) // Single-handed club check
            {
                if (EquipScript.getRight() != EquipType.Club || EquipScript.isTwoHandedClubEquiped() || weaponCollision.lastAttackWasStab)
                {
                    return false;
                }

                // TODO: consider adjusting these constants to account for weapon length variation.
                const float MIN_SPEED = 10f;
                const float MIN_THRUST_DISTANCE = 1.5f;
                const float MAX_ANGLE = 60;

                Vector3 swingVelocity = weaponCollision.physicsEstimator.GetAverageVelocityInSnapshots();
                Vector3 swingVelocityInPlayerCoordinates = Player.m_localPlayer.transform.InverseTransformVector(swingVelocity);
                float sagittalSpeed = new Vector2(swingVelocityInPlayerCoordinates.y, Mathf.Max(swingVelocityInPlayerCoordinates.z, 0)).magnitude;
                if (sagittalSpeed < MIN_SPEED)
                {
                    return false;
                }

                Vector3 thrust = weaponCollision.physicsEstimator.GetLongestLocomotion(0.5f);
                Vector3 thrustInPlayerCoordinates = Player.m_localPlayer.transform.InverseTransformVector(thrust);
                float sagittalSpeedThrust = new Vector2(thrustInPlayerCoordinates.y, Mathf.Max(thrustInPlayerCoordinates.z, 0)).magnitude;
                if (sagittalSpeedThrust < MIN_THRUST_DISTANCE)
                {
                    return false;
                }

                return Vector3.Angle(thrust, swingVelocity) <= MAX_ANGLE;
            },

            delegate (WeaponCollision weaponCollision) // Polearms check
            {
                if (EquipScript.getRight() != EquipType.Polearms || !WeaponWield.isCurrentlyTwoHanded()) {
                    return false;
                }
                    
                const float MIN_SWIPING_SPEED = 2.5f;
                float swipingSpeed = Vector3.ProjectOnPlane(weaponCollision.physicsEstimator.GetAverageVelocityInSnapshots(), WeaponWield.weaponForward).magnitude;
                LogUtils.LogWarning("Atgeir: " + swipingSpeed);
                return !weaponCollision.lastAttackWasStab && swipingSpeed >= MIN_SWIPING_SPEED;
            },

            delegate (WeaponCollision weaponCollision) // Knife check
            {
                if (EquipScript.getRight() != EquipType.Knife || !SteamVR_Actions.valheim_Grab.GetState(VRPlayer.dominantHandInputSource)) {
                    return false;
                }

                const float MIN_SPEED = 4f;
                const float MIN_THRUST_DISTANCE = 1f;

                Vector3 velocity = weaponCollision.mainHandPhysicsEstimator.GetAverageVelocityInSnapshots();
                Vector3 velocityInPlayerCoordinates = Player.m_localPlayer.transform.InverseTransformVector(velocity);
                float sagittalSpeed = new Vector2(velocityInPlayerCoordinates.y, Mathf.Max(velocityInPlayerCoordinates.z, 0)).magnitude;
                if (sagittalSpeed < MIN_SPEED)
                {
                    return false;
                }

                Vector3 thrust = weaponCollision.mainHandPhysicsEstimator.GetLongestLocomotion(0.5f);
                Vector3 thrustInPlayerCoordinates = Player.m_localPlayer.transform.InverseTransformVector(thrust);
                float sagittalSpeedThrust = new Vector2(thrustInPlayerCoordinates.y, Mathf.Max(thrustInPlayerCoordinates.z, 0)).magnitude;

                return sagittalSpeedThrust >= MIN_THRUST_DISTANCE;
            }
        };
    }
}
