using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;
using Valve.VR.Extras;

namespace ValheimVRMod.Scripts {
    
    // Manages the manual operation and the shape change of the cross bow during pulling.
    // TODO: implement this class as a component added to the weapon object and reduce static instance and singletion usage.
    class MagicWeaponManager {

        private static readonly HashSet<string> SWING_LAUNCH_MAGIC_STAFF_NAMES =
            new HashSet<string>(new string[] {
                "$item_stafffireball", "$item_staffgreenroots", "$item_staffclusterbomb", "$item_staffredtroll" });

        private static bool IsMagicWeaponEquipped { get { return EquipScript.getLeft() == EquipType.Magic || EquipScript.getRight() == EquipType.Magic;  } }
        // Right-handed weapons in vanilla game is treated as domininant hand weapon in VHVR.
        private static bool IsDominantHandWeapon { get { return EquipScript.getRight() == EquipType.Magic; } }       

        private static bool IsRightHandWeapon { get { return IsDominantHandWeapon ^ VHVRConfig.LeftHanded(); } }
        private static SteamVR_LaserPointer WeaponHandPointer { get { return IsRightHandWeapon ? VRPlayer.rightPointer : VRPlayer.leftPointer; } }
        protected static SteamVR_Action_Boolean AttackTriggerAction { get { return IsRightHandWeapon ? SteamVR_Actions.valheim_Use : SteamVR_Actions.valheim_UseLeft; } }

        public static Vector3 AimDir {
            get
            {
                return UseSwingForCurrentAttack() ? SwingLaunchManager.aimDir : LocalWeaponWield.isCurrentlyTwoHanded() ? LocalWeaponWield.weaponForward : WeaponHandPointer.rayDirection * Vector3.forward;
            }
        }

        public static bool IsSwingLaunchEnabled() {
            return SWING_LAUNCH_MAGIC_STAFF_NAMES.Contains(Player.m_localPlayer?.GetRightItem()?.m_shared?.m_name); 
        }

        public static bool AttemptingAttack {
            get
            {
                if (!IsMagicWeaponEquipped)
                {
                    return false;
                }
                return UseSwingForCurrentAttack() ? SwingLaunchManager.isThrowing : AttackTriggerAction.state;
            }
        }

        public static bool ShouldSkipAttackAnimation()
        {
            return UseSwingForCurrentAttack();
        }

        public static Vector3 GetProjectileSpawnPoint(Attack attack) 
        {
            var offsetDirection = LocalWeaponWield.weaponForward;
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
            return WeaponHandPointer.rayStartingPosition + offsetDirection * offsetAmount;
        }

        public static bool UseSwingForCurrentAttack()
        {
            // Disable swing launch if the staff is held with two hands like a rifle
            // (dominant hand behind the other hand).
            return IsSwingLaunchEnabled() && !LocalWeaponWield.IsDominantHandBehind;
        }
    }
 }
