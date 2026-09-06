using UnityEngine;
using ValheimVRMod.VRCore;
using Valve.VR;
using Valve.VR.Extras;

namespace ValheimVRMod.Scripts
{
    // Shared helpers for the magic staff managers (SwingableStaffManager, ShootingStaffManager).
    // All staves are main hand weapons, so a staff is always held in the main weapon hand.
    public static class MagicStaffUtils
    {
        public static SteamVR_LaserPointer WeaponHandPointer
        {
            get { return VRPlayer.isRightHandMainWeaponHand ? VRPlayer.rightPointer : VRPlayer.leftPointer; }
        }

        public static SteamVR_Action_Boolean AttackTriggerAction
        {
            get { return VRPlayer.isRightHandMainWeaponHand ? SteamVR_Actions.valheim_Use : SteamVR_Actions.valheim_UseLeft; }
        }

        // The hand that is behind the other when wielding two-handed, or the main weapon hand when wielding
        // single-handed. Aim-and-shoot staves fire from this hand's trigger, which leaves the front hand
        // trigger free for the swing-launch gesture of the swingable staves.
        public static SteamVR_Input_Sources RearHandInputSource
        {
            get
            {
                switch (LocalWeaponWield.LocalPlayerTwoHandedState)
                {
                    case WeaponWield.TwoHandedState.LeftHandBehind:
                        return SteamVR_Input_Sources.LeftHand;
                    case WeaponWield.TwoHandedState.RightHandBehind:
                        return SteamVR_Input_Sources.RightHand;
                    default:
                        return VRPlayer.mainWeaponHandInputSource;
                }
            }
        }

        public static SteamVR_Action_Boolean RearHandTriggerAction
        {
            get
            {
                return RearHandInputSource == SteamVR_Input_Sources.RightHand ?
                    SteamVR_Actions.valheim_Use : SteamVR_Actions.valheim_UseLeft;
            }
        }

        // Whether the rear hand trigger is currently held, which is what makes an aim-and-shoot staff attack.
        // This is deliberately a level read rather than an edge read: a looping staff attack (the staff of
        // frost) is aborted by Player#PlayerAttackInput as soon as the attack hold drops, so the trigger has
        // to keep reporting the attack for the whole wind-up rather than only on the frame it was pressed.
        public static bool IsShootingTriggerHeld()
        {
            return RearHandTriggerAction.GetState(RearHandInputSource);
        }

        public static SteamVR_Action_Boolean SecondaryTriggerAction
        {
            get { return VRPlayer.isRightHandMainWeaponHand ? SteamVR_Actions.valheim_UseLeft : SteamVR_Actions.valheim_Use; }
        }

        // Only some (mostly modded) staves have a secondary attack, so it must be null-checked before use.
        private static Attack GetSecondaryAttack()
        {
            var secondaryAttack = Player.m_localPlayer?.GetRightItem()?.m_shared?.m_secondaryAttack;
            return string.IsNullOrEmpty(secondaryAttack?.m_attackAnimation) ? null : secondaryAttack;
        }

        public static bool IsSecondaryAttack()
        {
            return SecondaryTriggerAction.state && GetSecondaryAttack() != null;
        }

        public static bool TrySecondaryAttack()
        {
            if (!SecondaryTriggerAction.state)
            {
                return false;
            }
            var secondaryAttack = GetSecondaryAttack();
            return secondaryAttack != null && Player.m_localPlayer.TryUseEitr(secondaryAttack.m_attackEitr);
        }

        // TODO: Consider moving this to WeaponUtils since its logic is not specific to magic weapons.
        public static Vector3 GetProjectileSpawnPoint(Attack attack, Vector3 offsetDirection, SteamVR_LaserPointer weaponHandPointer)
        {
            var offsetAmount =
                (new Vector3(attack.m_attackOffset, attack.m_attackRange, attack.m_attackHeight)).magnitude;
            if (attack.m_attackAnimation.Contains("summon"))
            {
                // Summon distance should not depend on the tilt of pointing direction.
                offsetDirection.y = 0;
                offsetDirection = offsetDirection.normalized;
            }
            else
            {
                offsetAmount *= 0.6f;
            }
            return weaponHandPointer.rayStartingPosition + offsetDirection * offsetAmount;
        }
    }

    // Common interface implemented by the per-category magic staff managers so call sites that don't
    // know which staff subtype is equipped can dispatch without repeated type checks.
    public interface IMagicStaffManager
    {
        bool AttemptingAttack { get; }
        bool IsSecondaryAttack { get; }
        bool TrySecondaryAttack { get; }
        Vector3 AimDir { get; }
        Vector3 GetProjectileSpawnPoint(Attack attack);
    }

    public static class MagicStaffManagers
    {
        // Staves are always main hand weapons, so at most one staff manager can be active at a time.
        public static IMagicStaffManager Current
        {
            get { return (IMagicStaffManager) SwingableStaffManager.instance ?? ShootingStaffManager.instance; }
        }
    }
}
