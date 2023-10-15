using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using ValheimVRMod.Scripts.Block;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts {
    public class MainWeaponCollision : WeaponCollision {
        private const float MIN_STAB_SPEED = 1f;
        private const float MAX_STAB_ANGLE = 30f;
        private const float MAX_STAB_ANGLE_TWOHAND = 40f;

        private Attack secondaryAttack;
        private float postSecondaryAttackCountdown;
        public LocalWeaponWield weaponWield;
        public PhysicsEstimator mainHandPhysicsEstimator { get { return weaponWield.mainHand == VRPlayer.leftHand ? VRPlayer.leftHandPhysicsEstimator : VRPlayer.rightHandPhysicsEstimator; } }
        public float twoHandedMultitargetSwipeCountdown { get; private set; } = 0;

        protected override bool isAttackAvailable(GameObject target)
        {
            if (Player.m_localPlayer.m_blocking && !weaponWield.allowBlocking() && VHVRConfig.BlockingType() == "GrabButton")
            {
                return false;
            }

            if (ButtonSecondaryAttackManager.firstPos != Vector3.zero || ButtonSecondaryAttackManager.isSecondaryAttackStarted)
            {
                return false;
            }

            return base.isAttackAvailable(target);
        }

        protected override Attack tryHitTarget(AttackTargetMeshCooldown attackTargetMeshCooldown) {
            bool isSecondaryAttack = postSecondaryAttackCountdown <= 0 && RoomscaleSecondaryAttackUtils.IsSecondaryAttack(this.physicsEstimator, this.mainHandPhysicsEstimator);
            if (EquipScript.getRight() == EquipType.Polearms)
            {
                // Allow continuing an ongoing atgeir secondary attack (multitarget swipe) until cooldown finishes.
                isSecondaryAttack = postSecondaryAttackCountdown > 0 || isSecondaryAttack;
            }

            Attack currentAttack;
            if (isSecondaryAttack)
            {
                // Use the target cooldown time of the primary attack if it is shorter to allow a primary attack immediately after secondary attack;
                // The secondary attack cooldown time is managed by postSecondaryAttackCountdown in this class intead.
                float targetCooldownTime = Mathf.Min(WeaponUtils.GetAttackDuration(attack), WeaponUtils.GetAttackDuration(secondaryAttack));
                bool attackSucceeded = attackTargetMeshCooldown.tryTriggerSecondaryAttack(targetCooldownTime);
                if (!attackSucceeded)
                {
                    return null;
                }
                currentAttack = secondaryAttack;
                if (postSecondaryAttackCountdown <= 0)
                {
                    postSecondaryAttackCountdown = WeaponUtils.GetAttackDuration(secondaryAttack);
                }
            }
            else
            {
                currentAttack = base.tryHitTarget(attackTargetMeshCooldown);
            }

            if (currentAttack != null && WeaponUtils.IsTwoHandedMultitargetSwipe(currentAttack) && twoHandedMultitargetSwipeCountdown <= 0)
            {
                twoHandedMultitargetSwipeCountdown = WeaponUtils.GetAttackDuration(currentAttack);
            }

            return currentAttack;
        }

        protected override bool outlineTarget() {

            if (!outline || ButtonSecondaryAttackManager.isSecondaryAttackStarted) {
                return false;
            }

            bool outlineUpdatedForPrimaryAttack = base.outlineTarget();
            if (outlineUpdatedForPrimaryAttack)
            {
                return true;
            }

            if (postSecondaryAttackCountdown > 0.5f) {
                outline.enabled = true;
                outline.OutlineColor = Color.Lerp(new Color(1, 1, 0, 0), new Color(1, 1, 0, 0.5f), postSecondaryAttackCountdown - 0.5f);
                outline.OutlineWidth = 10;
                return true;
            }

            return false;
        }

        private void FixedUpdate()
        {
            if (postSecondaryAttackCountdown > 0)
            {
                postSecondaryAttackCountdown -= Time.fixedDeltaTime;
            }
            if (twoHandedMultitargetSwipeCountdown > 0)
            {
                postSecondaryAttackCountdown -= Time.fixedDeltaTime;
            }
        }

        private bool isStab()
        {
            Vector3 attackVelocity = mainHandPhysicsEstimator == null ? Vector3.zero : mainHandPhysicsEstimator.GetAverageVelocityInSnapshots();

            if (Vector3.Angle(LocalWeaponWield.weaponForward, attackVelocity) > (LocalWeaponWield.isCurrentlyTwoHanded() ? MAX_STAB_ANGLE_TWOHAND : MAX_STAB_ANGLE))
            {
                return false;
            }

            if (Vector3.Dot(attackVelocity, LocalWeaponWield.weaponForward) > MIN_STAB_SPEED)
            {
                LogUtils.LogDebug("VHVR: stab detected on weapon direction: " + LocalWeaponWield.weaponForward);
                return true;
            }
            return false;
        }
    }
}
