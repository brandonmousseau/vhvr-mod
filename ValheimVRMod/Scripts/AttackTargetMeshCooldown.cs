using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts {
    public class AttackTargetMeshCooldown : MeshCooldown {
        protected override Color FullOutlineColor { get { return isSecondaryAttackCooldown? Color.yellow : base.FullOutlineColor; } }

        public static float damageMultiplier;
        public static bool staminaDrained;
        public static bool durabilityDrained;
        private static AttackTargetMeshCooldown primaryTargetMeshCooldown;

        private bool isSecondaryAttackCooldown;

        public bool tryTriggerPrimaryAttack(float cd)
        {
            if (tryTrigger(cd))
            {
                isSecondaryAttackCooldown = false;
                if (primaryTargetMeshCooldown == null)
                {
                    primaryTargetMeshCooldown = this;
                    damageMultiplier = 1;
                }
                return true;
            }
            return false;
        }

        public bool tryTriggerSecondaryAttack(float cd, bool ignorePrimaryAttackCooldown = true)
        {
            if (tryTrigger(cd))
            {
                isSecondaryAttackCooldown = true;
                
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

            MainWeaponCollision weaponCollision = Player.m_localPlayer.gameObject.GetComponentInChildren<MainWeaponCollision>();
            if (weaponCollision && weaponCollision.twoHandedMultitargetSwipeCountdown > 0)
            {
                return 1;
            }
            
            return oldDamageMultiplier;
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
    }
}
