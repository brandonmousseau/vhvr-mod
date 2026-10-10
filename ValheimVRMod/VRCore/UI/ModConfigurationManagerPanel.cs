using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.VRCore.UI
{
    /**
     * Shows the ConfigurationManager window on the VR GUI.
     *
     * The plugin draws its window with IMGUI, which is rendered straight to the desktop window and never reaches
     * the headset. So while this panel is shown, the plugin's own OnGUI() is turned off, and this panel runs the
     * plugin's window function in an ImguiContainer instead, rendering it into a texture shown by a RawImage on the
     * menu canvas. That canvas is already on the VR GUI and gets the laser pointer or the simulated mouse through the
     * EventSystem, so the pointer events arriving at the RawImage are translated back into IMGUI events at the
     * matching point of the window.
     */
    class ModConfigurationManagerPanel : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IScrollHandler
    {
        // The size the window is laid out for, in IMGUI points. Kept close to the plugin's own window, which is at
        // most 650 points wide, and small enough that its text is readable once stretched over the panel.
        private static readonly Vector2 WINDOW_SIZE = new Vector2(820, 700);
        // How much of the height of the canvas the window takes up.
        private const float CANVAS_HEIGHT_FRACTION = 0.85f;
        private const string WINDOW_TITLE = "Plugin / mod settings";
        // IMGUI scroll views move 20 points per unit of scroll delta, see GUI.EndScrollView(). A mouse wheel notch
        // gives 3 on Windows.
        private const float SCROLL_DELTA_PER_WHEEL_NOTCH = 3f;
        // Matches the distance VRGUI_InputModule.ScrollBySteps() scrolls a uGUI scroll view per step.
        private const float SCROLL_DELTA_PER_VR_STEP = 2.5f;
        // Used as the mouse position while the pointer is not over the window, so nothing is highlighted.
        private static readonly Vector2 OUTSIDE_WINDOW = new Vector2(-10000, -10000);

        private static bool triedPatching;

        private struct QueuedInput
        {
            public EventType type;
            public int button;
            public Vector2 position;
            public Vector2 delta;
        }

        private readonly Queue<QueuedInput> queuedInputs = new Queue<QueuedInput>();
        private ImguiContainer container;
        private RenderTexture texture;
        private RawImage image;
        private Canvas canvas;
        private Event passEvent;
        private Texture2D windowBackground;
        private int pressedButtons;
        private Vector2 lastDragPosition;
        private bool isShown;
        private bool hasFailed;

        public static ModConfigurationManagerPanel Create(Transform anyChildOfCanvas)
        {
            if (!ModConfigurationManagerBridge.IsAvailableInVr)
            {
                return null;
            }
            Canvas canvas = anyChildOfCanvas.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                LogWarning("No canvas found for the mod configuration manager panel.");
                return null;
            }
            patchDragWindow();

            GameObject panelObject = new GameObject("VHVRModConfigurationManagerPanel", typeof(RectTransform));
            panelObject.layer = canvas.gameObject.layer;
            panelObject.SetActive(false);
            RectTransform rectTransform = panelObject.GetComponent<RectTransform>();
            rectTransform.SetParent(canvas.transform, false);
            rectTransform.anchorMin = rectTransform.anchorMax = rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;

            ModConfigurationManagerPanel panel = panelObject.AddComponent<ModConfigurationManagerPanel>();
            // Its OnGUI() only drives the container, which does its own layout. Unity would otherwise lay out
            // whatever GUILayout state is selected at the end of it on every Layout event.
            panel.useGUILayout = false;
            panel.canvas = canvas;
            panel.image = panelObject.AddComponent<RawImage>();
            panel.image.raycastTarget = true;
            try
            {
                panel.container = new ImguiContainer(panel.GetInstanceID());
            }
            catch (Exception e)
            {
                ModConfigurationManagerBridge.OnVrDrawingFailed(e);
                Destroy(panelObject);
                return null;
            }
            return panel;
        }

        // Whether the window is open and drawn by this panel, which it stays while the panel is merely suspended.
        public bool ownsWindow { get { return isShown && !hasFailed; } }

        public void Show()
        {
            if (hasFailed)
            {
                return;
            }
            if (isShown)
            {
                if (ModConfigurationManagerBridge.isOpen)
                {
                    // Resuming after Suspend().
                    gameObject.SetActive(true);
                    transform.SetAsLastSibling();
                    return;
                }
                // Closed while suspended.
                stopShowing(closeWindow: false);
            }
            ModConfigurationManagerBridge.SetOpen(true);
            if (!ModConfigurationManagerBridge.isOpen)
            {
                return;
            }
            // After opening, since the plugin sizes its window by the desktop screen every time it is opened.
            ModConfigurationManagerBridge.SetWindowSize(WINDOW_SIZE);
            ModConfigurationManagerBridge.SetPluginEnabled(false);
            isShown = true;
            queuedInputs.Clear();
            pressedButtons = 0;
            updateSize();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            LogDebug(
                "ModConfigurationManagerPanel shown under canvas " + canvas.name + " (root " + canvas.rootCanvas.name + ", " +
                canvas.rootCanvas.renderMode + ", camera " + (canvas.rootCanvas.worldCamera ? canvas.rootCanvas.worldCamera.name : "none") +
                "), rect " + ((RectTransform)transform).rect + ", sibling " + transform.GetSiblingIndex() + "/" + transform.parent.childCount +
                ", raycaster " + (canvas.GetComponent<GraphicRaycaster>() != null));
        }

        public void Hide()
        {
            if (!isShown)
            {
                return;
            }
            stopShowing(closeWindow: true);
            gameObject.SetActive(false);
        }

        // Stops drawing the window but keeps it open for Show() to bring back, e. g. while another tab is selected.
        // The ToggleMenu action still closes it, see ModConfigurationManagerBridge.CloseWindow().
        public void Suspend()
        {
            gameObject.SetActive(false);
        }

        // Hands the window back to the plugin, closing it unless it has already been closed from within.
        private void stopShowing(bool closeWindow)
        {
            if (!isShown)
            {
                return;
            }
            isShown = false;
            queuedInputs.Clear();
            pressedButtons = 0;
            if (closeWindow)
            {
                ModConfigurationManagerBridge.SetOpen(false);
            }
            ModConfigurationManagerBridge.SetPluginEnabled(true);
        }

        public void ScrollBySteps(float steps)
        {
            Vector2 position;
            bool inside = tryGetCursorPosition(out position);
            LogDebug("ModConfigurationManagerPanel.ScrollBySteps " + steps + " at " + position + ", inside " + inside);
            if (inside)
            {
                queuedInputs.Enqueue(
                    new QueuedInput { type = EventType.ScrollWheel, position = position, delta = new Vector2(0, -steps * SCROLL_DELTA_PER_VR_STEP) });
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Vector2 position;
            int button = (int)eventData.button;
            bool inside = tryGetWindowPosition(eventData.position, out position);
            LogDebug("ModConfigurationManagerPanel.OnPointerDown " + eventData.button + " at " + eventData.position + " -> " + position + ", inside " + inside);
            if (!inside)
            {
                return;
            }
            pressedButtons |= 1 << button;
            lastDragPosition = position;
            queuedInputs.Enqueue(new QueuedInput { type = EventType.MouseDown, button = button, position = position });
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            int button = (int)eventData.button;
            if ((pressedButtons & (1 << button)) == 0)
            {
                return;
            }
            pressedButtons &= ~(1 << button);
            Vector2 position;
            // A release outside the window still has to reach IMGUI, which may have captured the mouse.
            tryGetWindowPosition(eventData.position, out position);
            LogDebug("ModConfigurationManagerPanel.OnPointerUp " + eventData.button + " at " + eventData.position + " -> " + position);
            queuedInputs.Enqueue(new QueuedInput { type = EventType.MouseUp, button = button, position = position });
        }

        public void OnScroll(PointerEventData eventData)
        {
            LogDebug("ModConfigurationManagerPanel.OnScroll " + eventData.scrollDelta + " at " + eventData.position);
            Vector2 position;
            if (Mathf.Approximately(eventData.scrollDelta.y, 0) || !tryGetWindowPosition(eventData.position, out position))
            {
                return;
            }
            queuedInputs.Enqueue(
                new QueuedInput {
                    type = EventType.ScrollWheel,
                    position = position,
                    delta = new Vector2(0, -Mathf.Sign(eventData.scrollDelta.y) * SCROLL_DELTA_PER_WHEEL_NOTCH)
                });
        }

        private void Update()
        {
            if (!isShown)
            {
                return;
            }
            if (hasFailed)
            {
                Hide();
                return;
            }
            if (!ModConfigurationManagerBridge.isOpen)
            {
                // Closed from within the window, e. g. with its Close button.
                stopShowing(closeWindow: false);
                gameObject.SetActive(false);
                return;
            }

            updateSize();
            if (pressedButtons != 0)
            {
                Vector2 position;
                tryGetCursorPosition(out position);
                if (position != lastDragPosition)
                {
                    queuedInputs.Enqueue(
                        new QueuedInput {
                            type = EventType.MouseDrag,
                            button = lowestPressedButton(),
                            position = position,
                            delta = position - lastDragPosition
                        });
                    lastDragPosition = position;
                }
            }
        }

        private void OnGUI()
        {
            if (!isShown || hasFailed)
            {
                return;
            }
            Event current = Event.current;
            try
            {
                if (current.type == EventType.Repaint)
                {
                    processQueuedInputs();
                    render();
                }
                else if (current.isKey)
                {
                    forwardKey(current);
                }
            }
            catch (Exception e)
            {
                hasFailed = true;
                ModConfigurationManagerBridge.OnVrDrawingFailed(e);
            }
        }

        private void OnDisable()
        {
            // The menu was hidden or the tab switched while the window was up. It stays open, but input in progress
            // is dropped.
            queuedInputs.Clear();
            pressedButtons = 0;
        }

        private void OnDestroy()
        {
            // The VHVR dialog is gone, so the window goes back to the plugin, which keeps it open on the desktop.
            stopShowing(closeWindow: false);
            container?.Dispose();
            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
            }
            if (windowBackground != null)
            {
                Destroy(windowBackground);
            }
        }

        private void processQueuedInputs()
        {
            while (queuedInputs.Count > 0)
            {
                QueuedInput input = queuedInputs.Dequeue();
                // Like IMGUIContainer, lay the window out again before every event, so the event is matched against
                // controls at their current place.
                runPass(EventType.Layout, input.position);
                prepareEvent(input.type, input.position);
                passEvent.button = input.button;
                passEvent.delta = input.delta;
                passEvent.clickCount = 1;
                int hotControlBefore = GUIUtility.hotControl;
                container.Run(passEvent, WINDOW_SIZE, drawWindow);
            }
        }

        private void forwardKey(Event keyEvent)
        {
            Vector2 position;
            tryGetCursorPosition(out position);
            runPass(EventType.Layout, position);
            Event forwarded = new Event(keyEvent);
            forwarded.mousePosition = position;
            container.Run(forwarded, WINDOW_SIZE, drawWindow);
            if (forwarded.type == EventType.Used)
            {
                keyEvent.Use();
            }
        }

        private void render()
        {
            ensureTexture();
            Vector2 position;
            tryGetCursorPosition(out position);
            runPass(EventType.Layout, position);

            RenderTexture previousTarget = RenderTexture.active;
            RenderTexture.active = texture;
            try
            {
                GL.Clear(clearDepth: false, clearColor: true, Color.clear);
                // The container sets up the projection itself, see ImguiContainer.Run().
                runPass(EventType.Repaint, position);
            }
            finally
            {
                RenderTexture.active = previousTarget;
            }
        }

        private void runPass(EventType type, Vector2 position)
        {
            prepareEvent(type, position);
            container.Run(passEvent, WINDOW_SIZE, drawWindow);
        }

        private void prepareEvent(EventType type, Vector2 position)
        {
            if (passEvent == null)
            {
                passEvent = new Event();
            }
            passEvent.type = type;
            passEvent.mousePosition = position;
            passEvent.delta = Vector2.zero;
            passEvent.button = 0;
            passEvent.clickCount = 0;
            passEvent.keyCode = KeyCode.None;
            passEvent.character = '\0';
            // Keep shift etc. from the real keyboard, e. g. for selecting text in the search box.
            passEvent.modifiers = Event.current.modifiers;
        }

        // Stands in for the GUILayout.Window() the plugin would put its window function in. A real window would only
        // be drawn once the whole OnGUI() is done, outside the container.
        private void drawWindow()
        {
            Rect windowRect = new Rect(Vector2.zero, WINDOW_SIZE);
            if (Event.current.type == EventType.Repaint)
            {
                // The default window style is translucent, the plugin puts an opaque background behind it as well.
                if (windowBackground == null)
                {
                    windowBackground = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    windowBackground.SetPixel(0, 0, new Color(0.5f, 0.5f, 0.5f, 1f));
                    windowBackground.Apply();
                }
                GUI.DrawTexture(windowRect, windowBackground);
            }
            GUIStyle windowStyle = GUI.skin.window;
            GUI.Box(windowRect, WINDOW_TITLE, windowStyle);
            RectOffset padding = windowStyle.padding;
            GUILayout.BeginArea(
                new Rect(padding.left, padding.top, WINDOW_SIZE.x - padding.horizontal, WINDOW_SIZE.y - padding.vertical));
            try
            {
                ModConfigurationManagerBridge.DrawWindowContent();
            }
            finally
            {
                GUILayout.EndArea();
            }
        }

        private void ensureTexture()
        {
            if (texture != null && texture.IsCreated())
            {
                return;
            }
            if (texture == null)
            {
                texture = new RenderTexture((int)WINDOW_SIZE.x, (int)WINDOW_SIZE.y, 0, RenderTextureFormat.ARGB32);
                texture.name = "VHVRModConfigurationManager";
            }
            texture.Create();
            image.texture = texture;
        }

        private void updateSize()
        {
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            float height = canvasRect.rect.height * CANVAS_HEIGHT_FRACTION;
            float width = Mathf.Min(height * WINDOW_SIZE.x / WINDOW_SIZE.y, canvasRect.rect.width * 0.95f);
            RectTransform rectTransform = (RectTransform)transform;
            Vector2 size = new Vector2(width, width * WINDOW_SIZE.y / WINDOW_SIZE.x);
            if (rectTransform.sizeDelta != size)
            {
                rectTransform.sizeDelta = size;
            }
        }

        private bool tryGetCursorPosition(out Vector2 position)
        {
            return tryGetWindowPosition(SoftwareCursor.simulatedMousePosition, out position);
        }

        // Converts a point in the simulated screen space the EventSystem works in into IMGUI points on the window,
        // with the origin at its top left.
        private bool tryGetWindowPosition(Vector2 screenPoint, out Vector2 position)
        {
            position = OUTSIDE_WINDOW;
            RectTransform rectTransform = (RectTransform)transform;
            Camera camera = canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, camera, out localPoint))
            {
                return false;
            }
            Rect rect = rectTransform.rect;
            float x = (localPoint.x - rect.xMin) / rect.width;
            float y = (localPoint.y - rect.yMin) / rect.height;
            position = new Vector2(x * WINDOW_SIZE.x, (1 - y) * WINDOW_SIZE.y);
            return x >= 0 && x <= 1 && y >= 0 && y <= 1;
        }

        private int lowestPressedButton()
        {
            for (int button = 0; button < 3; button++)
            {
                if ((pressedButtons & (1 << button)) != 0)
                {
                    return button;
                }
            }
            return 0;
        }

        // The plugin's window function ends with GUI.DragWindow(), which only makes sense inside a real window.
        private static void patchDragWindow()
        {
            if (triedPatching)
            {
                return;
            }
            triedPatching = true;
            try
            {
                var harmony = new Harmony("com.valheimvrmod.patches.modconfigurationmanager");
                var prefix = new HarmonyMethod(typeof(ModConfigurationManagerPanel), nameof(skipWhileDrawingInContainer));
                harmony.Patch(AccessTools.Method(typeof(GUI), nameof(GUI.DragWindow), Type.EmptyTypes), prefix: prefix);
                harmony.Patch(AccessTools.Method(typeof(GUI), nameof(GUI.DragWindow), new Type[] { typeof(Rect) }), prefix: prefix);
            }
            catch (Exception e)
            {
                LogWarning("Failed to patch GUI.DragWindow for the mod configuration manager panel: " + e);
            }
        }

        private static bool skipWhileDrawingInContainer()
        {
            return !ImguiContainer.isDrawing;
        }
    }
}
