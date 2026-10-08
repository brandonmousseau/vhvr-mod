using UnityEngine;
using UnityEngine.UI;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.VRCore.UI
{
    // The GUI is drawn into a texture that is then blended over the world by the alpha left in it, see VRGUI. Some
    // of the game's shaders, e. g. those of the item and piece icons and of the map, leave a lower alpha than what
    // they are drawn over had, even over an opaque panel, so the world shows through them: through the shadows of
    // the icons, or through the Mistlands on the map. Drawing to the screen never reads that alpha, which is why it
    // only shows on the VR GUI panel.
    //
    // Whatever is drawn over an opaque graphic, the alpha there should end up no lower than the graphic's own. So
    // such a graphic gets a copy of itself that is drawn after everything else and writes nothing but its alpha.
    static class GuiAlphaRestorer
    {
        private const string RESTORER_NAME = "VHVRAlphaRestorer";
        private const float ALPHA_ONLY_COLOR_MASK = (float)UnityEngine.Rendering.ColorWriteMask.Alpha;

        private static Material alphaOnlyMaterial;

        // Restores the alpha of an opaque panel, in the shape of its sprite.
        public static void RestorePanel(Transform parent, string panelPath)
        {
            if (VHVRConfig.NonVrPlayer() || parent == null)
            {
                return;
            }
            Transform panel = parent.Find(panelPath);
            Image image = panel == null ? null : panel.GetComponent<Image>();
            if (image == null)
            {
                LogUtils.LogWarning("No GUI panel '" + panelPath + "' in " + parent.name + ", the world may show through what is drawn on it");
                return;
            }
            if (panel.Find(RESTORER_NAME) != null)
            {
                return;
            }
            Image restorer = createRestorer(image.rectTransform);
            restorer.sprite = image.sprite;
            restorer.type = image.type;
            restorer.fillCenter = image.fillCenter;
            restorer.preserveAspect = image.preserveAspect;
            restorer.pixelsPerUnitMultiplier = image.pixelsPerUnitMultiplier;
        }

        // Restores the alpha of the whole rect of a graphic that is opaque without being a panel, e. g. the map.
        public static void RestoreRect(RectTransform rect)
        {
            if (VHVRConfig.NonVrPlayer() || rect == null || rect.Find(RESTORER_NAME) != null)
            {
                return;
            }
            createRestorer(rect);
        }

        // A child of the graphic, so that it follows its rect, activation and fading.
        private static Image createRestorer(RectTransform parent)
        {
            if (alphaOnlyMaterial == null)
            {
                alphaOnlyMaterial = new Material(Graphic.defaultGraphicMaterial);
                alphaOnlyMaterial.SetFloat("_ColorMask", ALPHA_ONLY_COLOR_MASK);
            }

            GameObject restorerObject = new GameObject(RESTORER_NAME, typeof(RectTransform));
            restorerObject.layer = parent.gameObject.layer;
            RectTransform rectTransform = (RectTransform)restorerObject.transform;
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            // In case the parent lays out its children.
            restorerObject.AddComponent<LayoutElement>().ignoreLayout = true;

            Image restorer = restorerObject.AddComponent<Image>();
            restorer.material = alphaOnlyMaterial;
            restorer.color = Color.white;
            restorer.raycastTarget = false;
            // A mask would replace the material with one that writes the colors too.
            restorer.maskable = false;
            restorerObject.AddComponent<DrawnLast>();
            return restorer;
        }

        // Has the restorer drawn after everything else on its canvas, including what comes after it in the
        // hierarchy, which is where the icons are.
        private class DrawnLast : MonoBehaviour
        {
            private const int SORTING_ORDER = 30000;

            private Canvas canvas;

            void OnEnable()
            {
                if (canvas == null)
                {
                    canvas = gameObject.AddComponent<Canvas>();
                }
                // Not kept if set while inactive.
                canvas.overrideSorting = true;
                canvas.sortingOrder = SORTING_ORDER;
            }
        }
    }
}
