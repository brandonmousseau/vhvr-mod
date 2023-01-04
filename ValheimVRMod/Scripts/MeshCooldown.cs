using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts {
    public class MeshCooldown : MonoBehaviour {

        private static readonly Color FullOutlineColor = Color.red;
        private static readonly Color HiddenOutlineColor = new Color(1, 0, 0, 0);

        private float cooldownStart;
        private float cooldown;
        private Outline outline;

        public virtual bool tryTrigger(float cd) {
            if (inCoolDown()) {
                return false;
            }

            cooldown = cooldownStart = cd;
            resetOutline();
            return true;
        }

        private void ensureOutline() {
            if (outline == null) {
                outline = gameObject.AddComponent<Outline>();
            }
        }

        // Whether the outline should be kept (as opposed to destroyed) after cooldown finishes.
        protected virtual bool keepOutlineInstance()
        {
            return true;
        }

        protected virtual void OnDisable() {
            if (outline != null) {
                outline.OutlineMode = Outline.Mode.OutlineHidden;
            }
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

        private static Color GetOutlineColor(Color fullColor, Color hiddenColor, float percentage)
        {
            return Color.Lerp(hiddenColor, fullColor, percentage > 0.5f ? 1 : percentage * 2);
        }
        
        protected virtual void FixedUpdate() {
            if (outline == null)
            {
                return;
            }
            outline.OutlineColor = GetOutlineColor(FullOutlineColor, HiddenOutlineColor, Mathf.Max(cooldown, 0) / cooldownStart);
            if (! inCoolDown()) {
                return;
            }

            cooldown -= Time.fixedDeltaTime;
           
            if (! inCoolDown()) {
                outline.OutlineMode = Outline.Mode.OutlineHidden;
                if (!keepOutlineInstance()) {
                    Destroy(outline);
                    outline = null;
                }
            }
        }
    }
}
