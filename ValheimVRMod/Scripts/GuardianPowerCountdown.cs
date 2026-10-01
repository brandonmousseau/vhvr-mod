using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ValheimVRMod.Utilities;

using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.Scripts
{
    /**
     * Shows the Forsaken Power countdown right below its button on the wrist quick bar, in place of the vanilla one
     * on the HUD.
     *
     * Rather than reimplementing the countdown, this reroutes the vanilla one, the same way the VRHud elements do:
     * Hud.m_gpRoot and the widgets under it are pointed at a small canvas on the button, so Hud.UpdateGuardianPower()
     * keeps writing the cooldown ("Ready" once it is over) and shows or hides the canvas along with the power. The time
     * left on the power's effect is not shown here, since vanilla already shows it on the effect's status effect icon.
     */
    public class GuardianPowerCountdown : MonoBehaviour
    {
        // In canvas units. The canvas is as wide as the spacing between two wrist buttons.
        private const float CANVAS_WIDTH = 240f;
        private const float CANVAS_HEIGHT = 40f;
        private const float CANVAS_WIDTH_METERS = 0.05f;
        private const float FONT_SIZE = 28f;
        // Below the button's icon, whose background sprite is 4 cm across. Above it is where WristStatusEffects puts the
        // status effect icons. The Forsaken Power button is the leftmost of the first row, clear of the chat button that
        // the second row holds in the middle.
        private static readonly Vector3 OFFSET_FROM_BUTTON = new Vector3(0, -0.03f, 0);

        private static GuardianPowerCountdown instance;

        private Hud hud;
        private RectTransform originalRoot;
        private Image originalIcon;
        private TMP_Text originalName;
        private TMP_Text originalCooldown;
        private RectTransform canvasRect;

        // Puts the countdown below the given Forsaken Power button, taking it over from the HUD if not done yet.
        public static void AttachTo(Transform powerButton)
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
            if (instance.transform.parent != powerButton)
            {
                instance.transform.SetParent(powerButton, false);
                instance.transform.localPosition = OFFSET_FROM_BUTTON;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one * CANVAS_WIDTH_METERS / CANVAS_WIDTH;
            }
        }

        // Hands the countdown back to the HUD.
        public static void Detach()
        {
            if (instance != null)
            {
                Destroy(instance.gameObject);
                instance = null;
            }
        }

        // Hands the countdown back to the HUD if it is on the given wrist bar.
        public static void DetachFrom(Transform wrist)
        {
            if (instance != null && instance.transform.IsChildOf(wrist))
            {
                Detach();
            }
        }

        private static GuardianPowerCountdown create()
        {
            Hud hud = Hud.instance;
            if (hud == null || hud.m_gpRoot == null || hud.m_gpCooldown == null || hud.m_gpIcon == null || hud.m_gpName == null)
            {
                return null;
            }

            GameObject canvasObject = new GameObject("VHVRGuardianPowerCountdown", typeof(RectTransform));
            canvasObject.layer = LayerUtils.getWorldspaceUiLayer();
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            // In front of the button's sprites, which use sorting orders 0 to 3.
            canvas.sortingOrder = 4;
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(CANVAS_WIDTH, CANVAS_HEIGHT);

            // Vanilla still writes the icon and the name every frame, so they need somewhere to go. They stay hidden
            // since the button already shows the icon.
            GameObject clone = Instantiate(hud.m_gpRoot.gameObject, canvasRect, false);
            Image icon = findCounterpart(hud.m_gpRoot, clone.transform, hud.m_gpIcon);
            TMP_Text name = findCounterpart(hud.m_gpRoot, clone.transform, hud.m_gpName);
            TMP_Text cooldown = findCounterpart(hud.m_gpRoot, clone.transform, hud.m_gpCooldown);
            if (icon == null || name == null || cooldown == null)
            {
                LogWarning("Failed to clone the Forsaken Power HUD, keeping its countdown on the HUD.");
                Destroy(canvasObject);
                return null;
            }
            cooldown.transform.SetParent(canvasRect, false);
            clone.SetActive(false);

            placeText(cooldown);
            foreach (Transform child in canvasObject.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                child.gameObject.layer = LayerUtils.getWorldspaceUiLayer();
            }

            GuardianPowerCountdown countdown = canvasObject.AddComponent<GuardianPowerCountdown>();
            countdown.hud = hud;
            countdown.originalRoot = hud.m_gpRoot;
            countdown.originalIcon = hud.m_gpIcon;
            countdown.originalName = hud.m_gpName;
            countdown.originalCooldown = hud.m_gpCooldown;
            countdown.canvasRect = canvasRect;

            // Vanilla shows or hides m_gpRoot with the power, which now does the same with this canvas.
            hud.m_gpRoot.gameObject.SetActive(false);
            hud.m_gpRoot = canvasRect;
            hud.m_gpIcon = icon;
            hud.m_gpName = name;
            hud.m_gpCooldown = cooldown;
            return countdown;
        }

        // The widget in the clone at the same place as the given one in the original.
        private static T findCounterpart<T>(Transform originalRoot, Transform cloneRoot, T original) where T : Component
        {
            if (original.transform == originalRoot)
            {
                return cloneRoot.GetComponent<T>();
            }
            string path = original.name;
            for (Transform parent = original.transform.parent; parent != originalRoot; parent = parent.parent)
            {
                if (parent == null)
                {
                    return null;
                }
                path = parent.name + "/" + path;
            }
            Transform counterpart = cloneRoot.Find(path);
            return counterpart == null ? null : counterpart.GetComponent<T>();
        }

        private static void placeText(TMP_Text text)
        {
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(CANVAS_WIDTH, CANVAS_HEIGHT);
            rect.anchoredPosition = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            text.enableAutoSizing = false;
            text.fontSize = FONT_SIZE;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
        }

        private void Update()
        {
            if (hud != Hud.instance)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
            if (hud == null || hud.m_gpRoot != canvasRect)
            {
                return;
            }
            // Vanilla shows the original again on its next update if there still is a power.
            hud.m_gpRoot = originalRoot;
            hud.m_gpIcon = originalIcon;
            hud.m_gpName = originalName;
            hud.m_gpCooldown = originalCooldown;
        }
    }
}
