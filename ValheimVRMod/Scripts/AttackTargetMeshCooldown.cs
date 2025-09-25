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

        private bool isSecondaryAttackCooldown;

        public bool tryTriggerPrimaryAttack(float cd, float speed)
        {
            float? overideMinAttackInterval;
            if (VHVRConfig.MomentumScalesAttackDamage() && EquipScript.getRight() != EquipType.Sledge)
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
            primaryTargetMeshCooldown = null;
            base.OnDisable();
        }

        protected override void FixedUpdate() {
            base.FixedUpdate();
            if (! inCoolDown()) {
                if (primaryTargetMeshCooldown == this) {
                    staminaDrained = false;
                    durabilityDrained = false;
                    primaryTargetMeshCooldown = null;
                }
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
