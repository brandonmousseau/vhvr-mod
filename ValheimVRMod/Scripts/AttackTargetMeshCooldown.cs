using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts {
    public class AttackTargetMeshCooldown : MeshCooldown {
        public static float damageMultiplier;
        public static bool staminaDrained;
        public static bool durabilityDrained;
        private static MeshCooldown primaryTargetMeshCooldown;
        public override bool tryTrigger(float cd) {
            bool isTriggered = base.tryTrigger(cd);
            if (isTriggered && primaryTargetMeshCooldown == null) {
                primaryTargetMeshCooldown = this;
                damageMultiplier = 1;
            }
            return isTriggered;
        }

        public static float calcDamageMultiplier() {
            var oldDamageMultiplier = damageMultiplier;
            
            if (damageMultiplier == 1) {
                damageMultiplier /= 3;
            }
            else {
                damageMultiplier /= 2;
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
