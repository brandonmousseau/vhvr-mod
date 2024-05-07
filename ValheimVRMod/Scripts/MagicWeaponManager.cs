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
        private static readonly HashSet<string> OPPOSITE_HAND_SUMMONER_NAMES =
            new HashSet<string>(new string[] {"$item_staffskeleton" });

        private static bool IsMagicWeaponEquipped { get { return EquipScript.getLeft() == EquipType.Magic || EquipScript.getRight() == EquipType.Magic;  } }
        // Right-handed weapons in vanilla game is treated as domininant hand weapon in VHVR.
        private static bool IsDominantHandWeapon { get { return EquipScript.getRight() == EquipType.Magic; } }       

        private static bool IsRightHandWeapon { get { return IsDominantHandWeapon ^ VHVRConfig.LeftHanded(); } }
        private static SteamVR_LaserPointer WeaponHandPointer { get { return IsRightHandWeapon ? VRPlayer.rightPointer : VRPlayer.leftPointer; } }
        protected static SteamVR_Action_Boolean AttackTriggerAction { get { return IsRightHandWeapon ? SteamVR_Actions.valheim_Use : SteamVR_Actions.valheim_UseLeft; } }

        public static Vector3 AimDir {
            get
            {
                if (CanSummonWithOppositeHand())
                {
                    return VHVRConfig.LeftHanded() ? VRPlayer.leftHandBone.up : VRPlayer.rightHandBone.up;
                }
                return UseSwingForCurrentAttack() ? SwingLaunchManager.aimDir : LocalWeaponWield.isCurrentlyTwoHanded() ? LocalWeaponWield.weaponForward : WeaponHandPointer.rayDirection * Vector3.forward;
            }
        }

        public static bool IsSwingLaunchEnabled() {
            return SWING_LAUNCH_MAGIC_STAFF_NAMES.Contains(Player.m_localPlayer?.GetRightItem()?.m_shared?.m_name); 
        }

        public static bool CanSummonWithOppositeHand()
        {
            return IsMagicWeaponEquipped && OPPOSITE_HAND_SUMMONER_NAMES.Contains(Player.m_localPlayer?.GetLeftItem()?.m_shared?.m_name);

        }

        public static bool AttemptingAttack {
            get
            {
                if (!IsMagicWeaponEquipped)
                {
                    return false;
                }

                if (CanSummonWithOppositeHand() && UpwardHandSummonManager.pendingSummon)
                {
                    UpwardHandSummonManager.pendingSummon = false;
                    return true;
                }

                return UseSwingForCurrentAttack() ? SwingLaunchManager.isThrowing : AttackTriggerAction.state;
            }
        }

        public static bool ShouldUseVanillaMagicAnimation(Player player)
        {
            var attack = player.m_currentAttack;
            return player.InAttack() && attack != null && attack.m_attackAnimation.Contains("staff_summon");
        }

        public static bool ShouldSkipAttackAnimation()
        {
            return UseSwingForCurrentAttack();
        }

        public static Vector3 GetProjectileSpawnPoint(Attack attack) 
        {
            var offsetDirection =
                CanSummonWithOppositeHand() ?
                (VHVRConfig.LeftHanded() ? VRPlayer.leftHandBone.up : VRPlayer.rightHandBone.up) :
                LocalWeaponWield.weaponForward;
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

        private static bool UseSwingForCurrentAttack()
        {
            // Disable swing launch if the staff is held with two hands like a rifle
            // (dominant hand behind the other hand).
            return IsSwingLaunchEnabled() && !LocalWeaponWield.IsDominantHandBehind;
        }

        public class UpwardHandSummonManager : MonoBehaviour
        {
            private const float MIN_SUMMONING_HAND_SPEED = 0.25f;
            private const float SUMMON_TIME = 1.5f;
            private float currentMaxHandHight = Mathf.NegativeInfinity;
            private float summonTimer = 0;
            private bool hasSummonedInCurrentMotion = false;

            public static bool pendingSummon = false;

            void FixedUpdate()
            {
                if (SteamVR_Actions.valheim_Use.GetState(VRPlayer.dominantHandInputSource))
                {
                    if (hasSummonedInCurrentMotion)
                    {
                        return;
                    }

                    float handHeight = VRPlayer.dominantHand.transform.position.y;
                    if (handHeight < currentMaxHandHight)
                    {
                        // Pause summoning unless the hand is moving upward.
                        return;
                    }
                    currentMaxHandHight = handHeight;

                    var velocity =
                        (VHVRConfig.LeftHanded() ? VRPlayer.leftHandPhysicsEstimator : VRPlayer.rightHandPhysicsEstimator).GetVelocity();
                    if (velocity.y > MIN_SUMMONING_HAND_SPEED)
                    {
                        summonTimer += Time.fixedDeltaTime;
                        VRPlayer.dominantHand.hapticAction.Execute(
                            0, 0.1f, 50, 0.3f, VRPlayer.dominantHandInputSource);
                    }

                    if (summonTimer > SUMMON_TIME)
                    {
                        hasSummonedInCurrentMotion = true;
                        pendingSummon = true;
                    }
                }
                else
                {
                    pendingSummon = hasSummonedInCurrentMotion = false;
                    summonTimer = 0;
                    currentMaxHandHight = float.NegativeInfinity;
                }
            }
        }
    }
 }
    
