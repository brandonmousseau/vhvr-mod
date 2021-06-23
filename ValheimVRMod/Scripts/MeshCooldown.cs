using UnityEngine;

namespace ValheimVRMod.Scripts {
    public class TriggerCooldown : MonoBehaviour {
        private float cooldown;
        private Outline outline;
        private bool sharingCooldown;

        public static float sharedCooldown;
        public static bool staminaDrained;
        
        public bool tryTrigger(float cd) {
            if (inCoolDown()) {
                return false;
            }

            getOutline().enabled = true;
            cooldown = cd;
            if (sharedCooldown <= 0) {
                sharingCooldown = true;
                sharedCooldown = cd;
            }
            return true;
        }

        public bool inCoolDown() {
            return cooldown > 0;
        }

        private Outline getOutline() {
            if (outline != null) {
                return outline;
            }

            outline = gameObject.AddComponent<Outline>();
            outline.OutlineColor = Color.red;
            outline.OutlineWidth = 10;
            outline.OutlineMode = Outline.Mode.OutlineVisible;
            
            return outline;
        }
        
        private void FixedUpdate() {
            if (! inCoolDown()) {
                return;
            }

            cooldown -= Time.fixedDeltaTime;

            if (sharingCooldown) {
                sharedCooldown = cooldown;
            }
            
            if (! inCoolDown()) {
                getOutline().enabled = false;
                sharingCooldown = false;
                staminaDrained = false;
            }
        }
    }
}