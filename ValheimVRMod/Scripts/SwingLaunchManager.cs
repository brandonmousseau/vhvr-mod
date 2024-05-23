using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts
{

    // Manager of poles that can be swung to launch a projectile from its tip.
    public class SwingLaunchManager : MonoBehaviour
    {
        private const float MIN_THROW_SPEED = 3.5f;
        private const float FULL_THROW_SPEED = 5f;

        public static float attackDrawPercentage { get; private set; }
        public static Vector3 spawnPoint { get; private set; }
        public static Vector3 aimDir { get; private set; }
        public static bool isThrowing;
        private static bool preparingThrow;

        protected SteamVR_Action_Boolean dominantHandInputAction { get { return VHVRConfig.LeftHanded() ? SteamVR_Actions.valheim_UseLeft : SteamVR_Actions.valheim_Use; } }
        private LocalWeaponWield weaponWield { get { return gameObject.GetComponentInParent<LocalWeaponWield>(); } }
        private PhysicsEstimator handPhysicsEstimator { get { return VHVRConfig.LeftHanded() ? VRPlayer.leftHandPhysicsEstimator : VRPlayer.rightHandPhysicsEstimator; } }
        private float peakSpeed = 0;

        protected virtual void OnRenderObject()
        {
            if (dominantHandInputAction.GetStateDown(VRPlayer.dominantHandInputSource))
            {
                preparingThrow = true;
                peakSpeed = 0;
            }

            spawnPoint = GetProjectileSpawnPoint();

            if (dominantHandInputAction.state)
            {
                UpdateThrowDirAndSpeed();
            }
            
            MaybeReleaseProjectile();

            if (!dominantHandInputAction.state)
            {
                preparingThrow = false;
            }
        }

        private void UpdateThrowDirAndSpeed()
        {
            Vector3 v;
            if (LocalWeaponWield.isCurrentlyTwoHanded() && weaponWield != null)
            {
                v = weaponWield.physicsEstimator.GetVelocityOfPoint(spawnPoint);
                aimDir = v.normalized;
            }
            else
            {
                v = handPhysicsEstimator.GetVelocityOfPoint(spawnPoint);
                // When wielding single-handed, using hand movement direction may be more intuitive than using spawn point movement direction.
                aimDir = handPhysicsEstimator.GetAverageVelocityInSnapshots().normalized;
            }

            float currentSpeed = v.magnitude;
            if (currentSpeed > peakSpeed)
            {
                peakSpeed = currentSpeed;
            }

            attackDrawPercentage = currentSpeed / FULL_THROW_SPEED;
        }

        private void MaybeReleaseProjectile() {
            if (!preparingThrow || isThrowing || peakSpeed < MIN_THROW_SPEED)
            {
                return;
            }

            if (ReleaseTriggerToAttack() && !dominantHandInputAction.GetStateUp(VRPlayer.dominantHandInputSource))
            {
                return;
            }

            if (!MountedAttackUtils.StartAttackIfRiding())
            {
                // Let control patches and vanilla game handle attack if the player is not riding.
                isThrowing = true;
            }
            preparingThrow = false;
            
        }

        protected virtual Vector3 GetProjectileSpawnPoint()
        {
            // TODO: Consider moving MagicWeaponManager.GetProjectileSpawnPoint() to WeaponUtils since its logic is not specific to magic weapons.
            return MagicWeaponManager.GetProjectileSpawnPoint(Player.m_localPlayer.GetRightItem().m_shared.m_attack);
        }

        protected virtual bool ReleaseTriggerToAttack()
        {
            return true;
        }
    }
}
