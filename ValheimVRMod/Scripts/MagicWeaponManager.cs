using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts {
    
    // Manages the manual operation and the shape change of the cross bow during pulling.
    class MagicWeaponManager {
        // Right-handed weapons in vanilla game is treated as domininant hand weapon in VHVR.
        private static bool IsDominantHandWeapon { get { return EquipScript.getRight() == EquipType.Magic; } }       

        private static bool IsRigthHandWeapon { get { return IsDominantHandWeapon() ^ VHVRConfig.LeftHanded(); } }
        private static SteamVR_LaserPointer WeaponHandPointer { get { return IsRigthHandWeapon() ? VRPlayer.rightPointer : VRPlayer.leftPointer; } }
      
        public static Vector3 AimDir { get { return WeaponWield.isCurrentlyTwoHanded() ? WeaponWield.weaponForward : WeaponHandPointer.rayDirection * Vector3.forward; } }
        
        public static Vector3 GetProjectileSpawnPoint(Attak attack) 
        {
            return WeaponHandPointer.rayStartingPosition + WeaponWield.weaponForward * (new Vector3(attack.m_attackOffset, attack.m_attackRange, attack.m_attackHeight)).magnitude * 0.6f;
        }
    }
 }
    
