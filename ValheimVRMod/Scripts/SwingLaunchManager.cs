using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using ValheimVRMod.VRCore.UI;
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

        public static bool isRightHandRear { get { return LocalWeaponWield.LocalPlayerTwoHandedState == WeaponWield.TwoHandedState.RightHandBehind; } }
        public static SteamVR_Input_Sources frontHandInputSource { get { return isRightHandRear ? SteamVR_Input_Sources.LeftHand : SteamVR_Input_Sources.RightHand; } }
        // The hand whose trigger arms and releases the swing: the front hand when wielding two-handed, which
        // leaves the rear hand trigger free to aim and shoot, and the main weapon hand when wielding single-handed.
        protected SteamVR_Input_Sources swingInputSource { get { return LocalWeaponWield.isCurrentlyTwoHanded() ? frontHandInputSource : VRPlayer.mainWeaponHandInputSource; } }
        private LocalWeaponWield weaponWield { get { return gameObject.GetComponentInParent<LocalWeaponWield>(); } }
        private PhysicsEstimator handPhysicsEstimator { get { return VRPlayer.isRightHandMainWeaponHand ? VRPlayer.rightHandPhysicsEstimator : VRPlayer.leftHandPhysicsEstimator; } }
        private float peakSpeed = 0;

        protected virtual void OnRenderObject()
        {
            // The item may already be unequipped while its instance awaits destruction at the end of the frame.
            if (Player.m_localPlayer == null || Player.m_localPlayer.GetRightItem() == null)
            {
                return;
            }

            // Don't arm a new swing-launch while any laser pointer is up (e.g. fishing shouldn't cast just
            // because the player waved the rod around while clicking through a GUI), but once armed, let the
            // preparation and the eventual release proceed even if a pointer comes up mid-swing.
            if (!LaserPointerChords.IsLaserActiveFor(SteamVR_Input_Sources.Any) &&
                SteamVR_Actions.valheim_Use.GetStateDown(swingInputSource))
            {
                preparingThrow = true;
                peakSpeed = 0;
            }

            spawnPoint = GetProjectileSpawnPoint();

            if (SteamVR_Actions.valheim_Use.GetState(swingInputSource))
            {
                UpdateThrowDirAndSpeed();
            }
            
            MaybeReleaseProjectile();

            if (!SteamVR_Actions.valheim_Use.GetState(swingInputSource))
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

            if (ReleaseTriggerToAttack() && !SteamVR_Actions.valheim_Use.GetStateUp(swingInputSource))
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
            // TODO: Consider moving this default to WeaponUtils since its logic is not specific to magic weapons.
            return MagicStaffUtils.GetProjectileSpawnPoint(
                Player.m_localPlayer.GetRightItem().m_shared.m_attack,
                LocalWeaponWield.weaponForward.normalized,
                MagicStaffUtils.WeaponHandPointer);
        }

        protected virtual bool ReleaseTriggerToAttack()
        {
            return true;
        }
    }
}
