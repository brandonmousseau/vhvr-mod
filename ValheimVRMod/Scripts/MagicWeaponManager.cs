using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;
using Valve.VR.Extras;

namespace ValheimVRMod.Scripts {
    
    // Manages the manual operation and the shape change of the cross bow during pulling.
    class MagicWeaponManager {

        private static readonly HashSet<string> SWING_LAUNCH_MAGIC_STAFF_NAMES = new HashSet<string>(new string[] { "$item_stafffireball" });
        private static bool IsMagicWeaponEquipped { get { return EquipScript.getLeft() == EquipType.Magic || EquipScript.getRight() == EquipType.Magic;  } }
        // Right-handed weapons in vanilla game is treated as domininant hand weapon in VHVR.
        private static bool IsDominantHandWeapon { get { return EquipScript.getRight() == EquipType.Magic; } }       

        private static bool IsRightHandWeapon { get { return IsDominantHandWeapon ^ VHVRConfig.LeftHanded(); } }
        private static SteamVR_LaserPointer WeaponHandPointer { get { return IsRightHandWeapon ? VRPlayer.rightPointer : VRPlayer.leftPointer; } }
        public static Vector3 AimDir {
            get
            {
                return IsSwingLaunchEnabled() ? SwingLaunchManager.aimDir : WeaponWield.isCurrentlyTwoHanded() ? WeaponWield.weaponForward : WeaponHandPointer.rayDirection * Vector3.forward;
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
                return IsSwingLaunchEnabled() ? SwingLaunchManager.isThrowing : IsRightHandWeapon ? SteamVR_Actions.valheim_Use.state : SteamVR_Actions.valheim_UseLeft.state;
            }
        }

        public static bool ShouldSkipAttackAnimation()
        {
            return IsSwingLaunchEnabled();
        }

        public static Vector3 GetProjectileSpawnPoint(Attack attack) 
        {
            return WeaponHandPointer.rayStartingPosition + WeaponWield.weaponForward * (new Vector3(attack.m_attackOffset, attack.m_attackRange, attack.m_attackHeight)).magnitude * 0.6f;
        }
    }
 }
    
