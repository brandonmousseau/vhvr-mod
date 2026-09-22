using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using ValheimVRMod.VRCore.UI;
using Valve.VR;

namespace ValheimVRMod.Scripts
{
    // Manages staves that can either swing-launch or aim-and-shoot their projectile
    // (fireball, greenroots, clusterbomb, redtroll, echo spike, lightning strike).
    public class SwingableStaffManager : SwingLaunchManager, IMagicStaffManager
    {
        public static readonly HashSet<string> STAFF_NAMES =
            new HashSet<string>(new string[] {
                "$item_stafffireball", "$item_staffgreenroots", "$item_staffclusterbomb", "$item_staffredtroll",
                "$item_staff_orbofahri", "$item_staff_thunderblood" });

        public static SwingableStaffManager instance;

        private enum SwingAttackMode { None, AimAndShoot, SwingLaunch }

        private SwingAttackMode currentSwingAttackMode = SwingAttackMode.None;

        // The rear hand trigger shoots at aiming direction whereas the front hand trigger swing-launches.
        private SteamVR_Action_Boolean RearHandTriggerAction { get { return MagicStaffUtils.RearHandTriggerAction; } }
        private SteamVR_Input_Sources RearHandInputSource { get { return MagicStaffUtils.RearHandInputSource; } }

        private void Awake()
        {
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public Vector3 AimDir
        {
            get
            {
                if (UseSwingForCurrentAttack())
                {
                    return aimDir;
                }
                return LocalWeaponWield.isCurrentlyTwoHanded() || LocalWeaponWield.isAiming ?
                    LocalWeaponWield.weaponForward :
                    MagicStaffUtils.WeaponHandPointer.rayDirection * Vector3.forward;
            }
        }

        public bool AttemptingAttack
        {
            get
            {
                if (UseSwingForCurrentAttack())
                {
                    return isThrowing;
                }
                // AimAndShoot: two-handed reads the rear hand's own trigger, disabled while any laser pointer is
                // up like any other weapon trigger. Single-handed, AimAndShoot is the trigger pressed without grab
                // (grab + trigger swing-launches instead), so it is only available with AllowSimpleMagicAttack.
                if (LocalWeaponWield.isCurrentlyTwoHanded())
                {
                    return !LaserPointerChords.IsLaserActiveFor(SteamVR_Input_Sources.Any) && RearHandTriggerAction.GetState(RearHandInputSource);
                }
                var mainHand = VRPlayer.mainWeaponHandInputSource;
                return VHVRConfig.AllowSimpleMagicAttack() &&
                    !LaserPointerChords.IsLaserActiveFor(mainHand) &&
                    MagicStaffUtils.AttackTriggerAction.GetState(mainHand);
            }
        }

        public bool IsSecondaryAttack
        {
            get { return MagicStaffUtils.IsSecondaryAttack(); }
        }

        public bool TrySecondaryAttack
        {
            get { return MagicStaffUtils.TrySecondaryAttack(); }
        }

        public bool ShouldSkipAttackAnimation()
        {
            return UseSwingForCurrentAttack();
        }

        public Vector3 GetProjectileSpawnPoint(Attack attack)
        {
            return MagicStaffUtils.GetProjectileSpawnPoint(attack, LocalWeaponWield.weaponForward.normalized, MagicStaffUtils.WeaponHandPointer);
        }

        protected override Vector3 GetProjectileSpawnPoint()
        {
            return GetProjectileSpawnPoint(Player.m_localPlayer.GetRightItem().m_shared.m_attack);
        }

        private void UpdateSwingAttackMode()
        {
            if (LocalWeaponWield.isCurrentlyTwoHanded())
            {
                if (RearHandTriggerAction.GetStateDown(RearHandInputSource) && !frontHandTriggerAction.state)
                {
                    currentSwingAttackMode = SwingAttackMode.AimAndShoot;
                }
                else if (frontHandTriggerAction.GetStateDown(frontHandInputSource) && !RearHandTriggerAction.state)
                {
                    currentSwingAttackMode = SwingAttackMode.SwingLaunch;
                }
            }
            else
            {
                // Single-handed: swing-launch only if grip is held down the moment the trigger is pressed.
                SteamVR_Input_Sources mainHandInputSource = VRPlayer.mainWeaponHandInputSource;
                if (MagicStaffUtils.AttackTriggerAction.GetStateDown(mainHandInputSource))
                {
                    currentSwingAttackMode =
                        SteamVR_Actions.valheim_Grab.GetState(mainHandInputSource) ?
                        SwingAttackMode.SwingLaunch :
                        SwingAttackMode.AimAndShoot;
                }
            }
        }

        public bool UseSwingForCurrentAttack()
        {
            UpdateSwingAttackMode();
            return currentSwingAttackMode == SwingAttackMode.SwingLaunch;
        }
    }
}
