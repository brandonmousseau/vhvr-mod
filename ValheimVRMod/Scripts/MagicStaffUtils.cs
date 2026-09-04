using UnityEngine;
using ValheimVRMod.VRCore;
using Valve.VR;
using Valve.VR.Extras;

namespace ValheimVRMod.Scripts
{
    // Shared helpers for the magic staff managers (SwingableStaffManager, DeadRaiserManager, ShootingStaffManager).
    public static class MagicStaffUtils
    {
        public static bool IsInRightHand(bool isDominantHandWeapon)
        {
            return isDominantHandWeapon ^ !VRPlayer.isRightHandMainWeaponHand;
        }

        public static SteamVR_LaserPointer WeaponHandPointer(bool isDominantHandWeapon)
        {
            return IsInRightHand(isDominantHandWeapon) ? VRPlayer.rightPointer : VRPlayer.leftPointer;
        }

        public static SteamVR_Action_Boolean AttackTriggerAction(bool isDominantHandWeapon)
        {
            return IsInRightHand(isDominantHandWeapon) ? SteamVR_Actions.valheim_Use : SteamVR_Actions.valheim_UseLeft;
        }

        public static SteamVR_Action_Boolean SecondaryTriggerAction(bool isDominantHandWeapon)
        {
            return IsInRightHand(isDominantHandWeapon) ? SteamVR_Actions.valheim_UseLeft : SteamVR_Actions.valheim_Use;
        }

        public static bool IsSecondaryAttack(bool isDominantHandWeapon)
        {
            var secondaryAttack = Player.m_localPlayer?.GetRightItem()?.m_shared.m_secondaryAttack;
            return SecondaryTriggerAction(isDominantHandWeapon).state && secondaryAttack.m_attackAnimation != "";
        }

        public static bool TrySecondaryAttack(bool isDominantHandWeapon)
        {
            var secondaryAttack = Player.m_localPlayer?.GetRightItem()?.m_shared.m_secondaryAttack;
            var isEitrEnough = false;
            if (SecondaryTriggerAction(isDominantHandWeapon).state && secondaryAttack.m_attackAnimation != "")
            {
                isEitrEnough = Player.m_localPlayer.TryUseEitr(secondaryAttack.m_attackEitr);
            }
            return isEitrEnough;
        }

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
        public static IMagicStaffManager MainHand
        {
            get { return (IMagicStaffManager) SwingableStaffManager.instance ?? ShootingStaffManager.instance; }
        }

        public static IMagicStaffManager OffHand
        {
            get { return DeadRaiserManager.instance; }
        }
    }
}
