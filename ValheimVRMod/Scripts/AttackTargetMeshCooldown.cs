using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts {
    public class AttackTargetMeshCooldown : MeshCooldown {
        private float colorModifier = 1;
        protected override Color FullOutlineColor { get { return isSecondaryAttackCooldown? Color.yellow : Color.Lerp(Color.black, base.FullOutlineColor, colorModifier); } }

        public static float speedScaledDamageFactor;
        public static float damageMultiplier;
        public static bool staminaDrained;
        public static bool durabilityDrained;
        private static AttackTargetMeshCooldown primaryTargetMeshCooldown;

        // Vanilla charges stamina (in Attack.Update) and weapon durability (in Attack.DoMeleeAttack) once
        // per attack, no matter how many targets that attack sweeps through. Roomscale attacks instead
        // call Attack.Start once per target hit, so the charge is made on the first hit of an attack and
        // then suppressed until this deadline, which marks the end of that attack.
        private static float attackResourceDrainDeadline = float.NegativeInfinity;

        private bool isSecondaryAttackCooldown;

        public bool tryTriggerPrimaryAttack(float cd, float speed)
        {
            float? overideMinAttackInterval;
            if (VHVRConfig.MomentumScalesAttackDamage() && EquipScript.CurrentMainHandEquipType() != EquipType.Sledge)
            {
                speedScaledDamageFactor = Mathf.Min(GetSpeedScaledDamageFactor(cd, speed), 1 - getRemaningCooldownPercentage());
                overideMinAttackInterval = 0.25f;
            }
            else
            {
                speedScaledDamageFactor = 1;
                overideMinAttackInterval = null;
            }

            if (tryTrigger(cd, overideMinAttackInterval))
            {
                maybeStartAttackResourceDrainWindow(cd);
                isSecondaryAttackCooldown = false;
                if (primaryTargetMeshCooldown == null)
                {
                    primaryTargetMeshCooldown = this;
                }
                if (primaryTargetMeshCooldown == this)
                {
                    damageMultiplier = 1;
                }

                colorModifier = Mathf.Min(speedScaledDamageFactor, damageMultiplier);

                return true;
            }
            return false;
        }

        public bool tryTriggerSecondaryAttack(float cd, bool ignorePrimaryAttackCooldown = true)
        {
            if (tryTrigger(cd))
            {
                maybeStartAttackResourceDrainWindow(cd);
                isSecondaryAttackCooldown = true;
                speedScaledDamageFactor = 1;
                colorModifier = 1;

                if (ignorePrimaryAttackCooldown)
                {
                    damageMultiplier = 1;
                }
                else if (primaryTargetMeshCooldown == null)
                {
                    primaryTargetMeshCooldown = this;
                    damageMultiplier = 1;
                }

                return true;
            }
            return false;
        }

        // Starts a new window during which stamina and durability are charged at most once, unless the hit
        // being triggered still belongs to an attack that has already been charged for. Unlike
        // primaryTargetMeshCooldown, this is not tied to any particular target: an attack that sweeps
        // through several targets must only be charged for once, even when the target it was charged for
        // dies or is left behind mid-swing.
        private static void maybeStartAttackResourceDrainWindow(float attackDuration)
        {
            if (Time.time < attackResourceDrainDeadline)
            {
                return;
            }
            attackResourceDrainDeadline = Time.time + attackDuration;
            staminaDrained = false;
            durabilityDrained = false;
        }

        public static float calcDamageMultiplier() {
            var oldDamageMultiplier = damageMultiplier;
            
            if (damageMultiplier == 1) {
                damageMultiplier /= 3;
            }
            else {
                damageMultiplier /= 2;
            }

            WeaponCollision weaponCollision = Player.m_localPlayer.gameObject.GetComponentInChildren<WeaponCollision>();
            if (weaponCollision && weaponCollision.isTwoHandedMultitargetSwipeActive)
            {
                return 1;
            }
            
            return Mathf.Min(oldDamageMultiplier, speedScaledDamageFactor);
        }

        public static bool isPrimaryTargetInCooldown()
        {
            return primaryTargetMeshCooldown != null && primaryTargetMeshCooldown.inCoolDown();
        }
        
        protected override bool keepOutlineInstance()
        {
            return false;
        }

        protected override void OnDisable() {
            if (primaryTargetMeshCooldown == this)
            {
                primaryTargetMeshCooldown = null;
            }
            base.OnDisable();
        }

        protected override void FixedUpdate() {
            base.FixedUpdate();
            if (!inCoolDown() && primaryTargetMeshCooldown == this) {
                primaryTargetMeshCooldown = null;
            }
        }

        private static float GetSpeedScaledDamageFactor(float cd, float speed)
        {
            if (FistCollision.hasDualWieldingWeaponEquipped())
            {
                cd *= 2;
            }
            var fullDamageSpeed = cd + 5;
            return speed >= fullDamageSpeed ? 1 : speed / fullDamageSpeed;
        }
    }
}
