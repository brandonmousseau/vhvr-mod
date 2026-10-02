using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts
{
    /**
     * Shows the status effect icons (vanilla's list in the top right corner of the HUD) above the buttons on the wrist
     * quick bar that holds the Forsaken Power, sit, map, recenter and chat buttons.
     *
     * Like GuardianPowerCountdown, this reroutes the vanilla list rather than reimplementing it: Hud.m_statusEffectListRoot
     * is pointed at a small canvas that follows the wrist bar, so Hud.UpdateStatusEffects() keeps creating, updating and
     * removing the icons there. Only their layout is redone here, centering them in a single row above the buttons.
     *
     * The canvas is not a child of the wrist bar, since that is deactivated whenever it is out of view, and vanilla looks
     * up the icons' text and animator with GetComponentInChildren(), which skips inactive objects. It is only hidden by
     * disabling the canvas instead, which leaves the icons active.
     */
    public class WristStatusEffects : MonoBehaviour
    {
        // Distance between two icons. A bit less than the 5 cm between two wrist buttons, so that five icons are about as
        // wide as a row of buttons.
        private const float SPACING_METERS = 0.04f;

        private static WristStatusEffects instance;

        private Hud hud;
        private RectTransform originalRoot;
        private RectTransform root;
        private Canvas canvas;
        private GameObject wrist;
        // Where the center of the row of icons goes, in the wrist bar's space.
        private float rowY;
        // From an icon's pivot to the center of its image, which is what gets laid out.
        private Vector2? iconCenterOffset;

        // Puts the status effect icons on the given wrist bar, centered in a row at the given height, taking them
        // over from the HUD if not done yet.
        public static void AttachTo(GameObject wrist, float rowY)
        {
            if (instance != null && instance.hud != Hud.instance)
            {
                // The HUD was recreated, e. g. after logging out and back in.
                Destroy(instance.gameObject);
                instance = null;
            }
            if (instance == null)
            {
                instance = create();
                if (instance == null)
                {
                    return;
                }
            }
            instance.wrist = wrist;
            instance.rowY = rowY;
        }

        // Hands the icons back to the HUD.
        public static void Detach()
        {
            if (instance != null)
            {
                // Right away rather than once the canvas is actually destroyed, in case a new one takes over in between.
                instance.release();
                Destroy(instance.gameObject);
                instance = null;
            }
        }

        // Hands the icons back to the HUD if they are on the given wrist bar.
        public static void DetachFrom(GameObject wrist)
        {
            if (instance != null && instance.wrist == wrist)
            {
                Detach();
            }
        }

        private static WristStatusEffects create()
        {
            Hud hud = Hud.instance;
            if (hud == null || hud.m_statusEffectListRoot == null || hud.m_statusEffectTemplate == null || hud.m_statusEffectSpacing <= 0)
            {
                return null;
            }

            GameObject canvasObject = new GameObject("VHVRWristStatusEffects", typeof(RectTransform));
            canvasObject.layer = LayerUtils.getWorldspaceUiLayer();
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            // In front of the wrist buttons' sprites, which use sorting orders 0 to 3.
            canvas.sortingOrder = 4;
            canvas.enabled = false;
            canvasObject.GetComponent<RectTransform>().sizeDelta = Vector2.zero;

            // A point, so that every icon's anchoredPosition is where its pivot is, whatever anchors the template has.
            RectTransform root = new GameObject("StatusEffects", typeof(RectTransform)).GetComponent<RectTransform>();
            root.gameObject.layer = LayerUtils.getWorldspaceUiLayer();
            root.SetParent(canvasObject.transform, false);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = Vector2.zero;
            root.anchoredPosition = Vector2.zero;

            WristStatusEffects statusEffects = canvasObject.AddComponent<WristStatusEffects>();
            statusEffects.hud = hud;
            statusEffects.originalRoot = hud.m_statusEffectListRoot;
            statusEffects.root = root;
            statusEffects.canvas = canvas;

            // Vanilla only recreates the icons when their number changes, so have it start over under the new root.
            clearIcons(hud);
            hud.m_statusEffectListRoot = root;
            return statusEffects;
        }

        private static void clearIcons(Hud hud)
        {
            foreach (RectTransform icon in hud.m_statusEffects)
            {
                if (icon != null)
                {
                    Destroy(icon.gameObject);
                }
            }
            hud.m_statusEffects.Clear();
        }

        // After Hud.Update(), which may have just created the icons.
        private void LateUpdate()
        {
            if (hud != Hud.instance || wrist == null)
            {
                Destroy(gameObject);
                return;
            }

            // Parented to what the wrist bar is parented to (the hand), so that it moves along with the bar whenever the
            // hand's pose is updated.
            Transform wristTransform = wrist.transform;
            if (transform.parent != wristTransform.parent)
            {
                transform.SetParent(wristTransform.parent, false);
            }
            transform.SetPositionAndRotation(wristTransform.TransformPoint(0, rowY, 0), wristTransform.rotation);
            transform.localScale = wristTransform.localScale * (SPACING_METERS / hud.m_statusEffectSpacing);
            canvas.enabled = wrist.activeInHierarchy && hud.IsVisible();

            layOut(hud.m_statusEffects);
        }

        private void layOut(List<RectTransform> icons)
        {
            float spacing = hud.m_statusEffectSpacing;
            int layer = LayerUtils.getWorldspaceUiLayer();
            for (int i = 0; i < icons.Count; i++)
            {
                RectTransform icon = icons[i];
                if (icon == null)
                {
                    continue;
                }
                if (icon.gameObject.layer != layer)
                {
                    // Vanilla just created it from the template, which is on the layer of the HUD.
                    foreach (Transform child in icon.GetComponentsInChildren<Transform>(includeInactive: true))
                    {
                        child.gameObject.layer = layer;
                    }
                }

                Vector2 center = new Vector2((i - (icons.Count - 1) / 2f) * spacing, 0);
                icon.anchoredPosition = center - getIconCenterOffset(icon);
            }
        }

        private Vector2 getIconCenterOffset(RectTransform icon)
        {
            if (!iconCenterOffset.HasValue)
            {
                // The template may have its pivot anywhere, and holds the name under the image too.
                RectTransform image = icon.Find("Icon") as RectTransform;
                iconCenterOffset = image == null ?
                    Vector2.zero :
                    (Vector2)root.InverseTransformPoint(image.TransformPoint(image.rect.center)) - icon.anchoredPosition;
            }
            return iconCenterOffset.Value;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
            release();
        }

        private void release()
        {
            if (hud == null || hud.m_statusEffectListRoot != root)
            {
                return;
            }
            // The icons go away with this canvas; vanilla creates them again under the original root on its next update.
            hud.m_statusEffects.Clear();
            hud.m_statusEffectListRoot = originalRoot;
        }
    }
}
