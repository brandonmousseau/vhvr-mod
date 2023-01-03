using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts {
    public class AttackTargetMeshCooldown : MeshCooldown {
        public static MeshCooldown lastAttackTargetMeshCooldown;
        public static float damageMultiplier;

        public override bool tryTrigger(float cd) {
            bool isTriggered = base.tryTrigger(cd);
            if (isTriggered) {
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

        protected override void OnDisable() {
            lastAttackTargetMeshCooldown = null;
            base.OnDisable();
        }

        protected override void FixedUpdate() {
            base.FixedUpdate();
            if (! inCoolDown()) {
                if (lastAttackTargetMeshCooldown == this) {
                    lastAttackTargetMeshCooldown = null;
                }
            }
        }
    }
}
