using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts {
    public class MeshCooldown : MonoBehaviour {
        public bool keepOutlineInstance = false;

        private static readonly Color FullOutlineColor = Color.red;
        private static readonly Color HiddenOutlineColor = new Color(1, 0, 0, 0);

        private float cooldownStart;
        private float cooldown;
        private Outline outline;

        public static MeshCooldown sharedInstance;
        public static bool staminaDrained;
        public static float damageMultiplier;

        public bool tryTrigger(float cd) {
            if (inCoolDown()) {
                return false;
            }

            cooldown = cooldownStart = cd;
            resetOutline();
            if (sharedInstance == null) {
                sharedInstance = this;
                damageMultiplier = 1;
            }
            return true;
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

        private void ensureOutline() {
            if (outline == null) {
                outline = gameObject.AddComponent<Outline>();
            }
        }

        void OnDisable() {
            outline.OutlineMode = Outline.Mode.OutlineHidden;
        }

        void OnDestory() {
            Destroy(outline);
        }

        public bool inCoolDown() {
            return cooldown > 0;
        }

        private void resetOutline() {
            ensureOutline();
            outline.OutlineWidth = 10;
            outline.OutlineColor = FullOutlineColor;
            outline.OutlineMode = Outline.Mode.OutlineVisible;
        }
        
        private void FixedUpdate() {
            outline.OutlineColor = Color.Lerp(HiddenOutlineColor, FullOutlineColor, Mathf.Max(cooldown, 0) / cooldownStart);
            if (! inCoolDown()) {
                return;
            }

            cooldown -= Time.fixedDeltaTime;
           
            if (! inCoolDown()) {
                outline.OutlineMode = Outline.Mode.OutlineHidden;
                if (!keepOutlineInstance) {
                    Destroy(outline);
                    outline = null;                    
                }
                
                if (sharedInstance == this) {
                    staminaDrained = false;
                    sharedInstance = null;
                }
            }
        }
    }
}
