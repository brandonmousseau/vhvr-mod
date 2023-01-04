using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts {
    public class AttackTargetMeshCooldown : MeshCooldown {
        public static float damageMultiplier;
        public static bool staminaDrained;
        public static bool durabilityDrained;
        private static MeshCooldown lastAttackTargetMeshCooldown;

        public override bool tryTrigger(float cd) {
            bool isTriggered = base.tryTrigger(cd);
            if (isTriggered && lastAttackTargetMeshCooldown == null) {
                lastAttackTargetMeshCooldown = this;
                damageMultiplier = 1;
            }
            return isTriggered;
        }

        public static float calcDamageMultiplier() {
            var dmgMultiplier = damageMultiplier;
            
            if (damageMultiplier == 1) {
                damageMultiplier /= 3;
            }
            else {
                damageMultiplier /= 2;
            }

            return dmgMultiplier;
        }

        public static bool isLastTargetInCooldown()
        {
            return lastAttackTargetMeshCooldown != null && lastAttackTargetMeshCooldown.inCoolDown();
        }
        
        protected override bool keepOutlineInstance()
        {
            return false;
        }

        protected override void OnDisable() {
            lastAttackTargetMeshCooldown = null;
            base.OnDisable();
        }

        protected override void FixedUpdate() {
            base.FixedUpdate();
            if (! inCoolDown()) {
                if (lastAttackTargetMeshCooldown == this) {
                    staminaDrained = false;
                    durabilityDrained = false;
                    lastAttackTargetMeshCooldown = null;
                }
            }
        }
    }
}
