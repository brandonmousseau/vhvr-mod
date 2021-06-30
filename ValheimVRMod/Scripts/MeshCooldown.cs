using UnityEngine;

namespace ValheimVRMod.Scripts {
    public class MeshCooldown : MonoBehaviour {
        private float cooldown;
        private Outline outline;

        public static MeshCooldown sharedInstance;
        public static bool staminaDrained;
        
        public bool tryTrigger(float cd) {
            if (inCoolDown()) {
                return false;
            }

            getOutline();
            cooldown = cd;
            if (sharedInstance == null) {
                sharedInstance = this;
            }
            return true;
        }

        private void OnDisable() {
            Destroy(this);
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
            
            if (! inCoolDown()) {
                Destroy(getOutline());
                
                if (sharedInstance == this) {
                    staminaDrained = false;
                    sharedInstance = null;
                }
            }
        }
    }
}