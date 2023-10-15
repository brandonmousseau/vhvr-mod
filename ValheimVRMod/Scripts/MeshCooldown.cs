using UnityEngine;

namespace ValheimVRMod.Scripts {
    public class MeshCooldown : MonoBehaviour {
        protected virtual Color FullOutlineColor { get; } = Color.red;
        private Color HiddenOutlineColor { get { return new Color(FullOutlineColor.r, FullOutlineColor.g, FullOutlineColor.b, 0); } }

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

        private bool ensureOutline() {
            if (outline == null) {
                outline = gameObject.GetComponent<Outline>();
                if (outline)
                    Destroy(outline);
                outline = gameObject.AddComponent<Outline>();
            }
            return outline != null;
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

        private void OnDestroy() {
            Destroy(outline);
        }

        public bool inCoolDown() {
            return cooldown > 0;
        }

        private void resetOutline() {
            if (ensureOutline())
            {
                outline.OutlineWidth = 10;
                outline.OutlineColor = FullOutlineColor;
                outline.OutlineMode = Outline.Mode.OutlineVisible;
            }
        }

        private static Color GetOutlineColor(Color fullColor, Color hiddenColor, float percentage)
        {
            return Color.Lerp(hiddenColor, fullColor, percentage > 0.5f ? 1 : percentage * 2);
        }
        
        protected virtual void FixedUpdate() {
            if (! inCoolDown()) {
                return;
            }

            cooldown -= Time.fixedDeltaTime;

            // TODO: find out why outline failed to be added.
            if (outline == null)
            {
                return;
            }

            outline.OutlineColor = GetOutlineColor(FullOutlineColor, HiddenOutlineColor, Mathf.Max(cooldown, 0) / cooldownStart);
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
