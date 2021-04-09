using UnityEngine;
using Valve.VR;
using ValheimVRMod.Utilities;
using UnityEngine.EventSystems;
using Valve.VR.Extras;

using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.VRCore.UI
{
    /**
     * Translates the Valheim GUI into VR
     *
     * It converts the existing GUI Canvas renderMode property to 
     * WorldSpace, assigns a camera to that GUI, assigns a RenderTexture
     * to the camera, and passes that texture as the input to either
     * an OpenVR Overlay or simple Quad with texture set to the
     * output of the GUI camera. Originally this only used an Overlay,
     * but I found that there was no way to pass depth information
     * to OpenVR Overlays. This meant that inserting the VR controls/player
     * hands into the game was not going to work very well as the Overlay
     * would always be drawn on over top of them and there was no way to
     * fix it. If only playing with mouse and keyboard, Overlay is better
     * because it produces a cleaner GUI image with less flickering as the
     * camera rotates.
     *
     * However, if you want to play with the motion controllers, as explained,
     * the Overlay isn't a good option as you can't interact with the GUI. With
     * the simple Quad mesh, we can set the layer of the Quad to its own layer and
     * have a separate camera rendering only that layer with a higher depth than
     * the world (and thus it is drawn on top of everything in the world). At the
     * same time I can put the Player's hand models on their own layer and use a
     * third camera with a depth even higher than the GUI. That way the GUI
     * won't be obstructed by things in the world as the player moves around (like trees,
     * rocks, etc...) but the hands will still be drawn over top the GUI, so we can
     * use them like a laser pointer for mouse input.
     * 
     * VRGUI attaches a SoftwareCursor to the main UI Canvas to use instead of the real one.
     * This makes use of a Harmony patch to fake Unity's "Input.mousePosition" that is
     * used to retrieve the "real" hardware mouse position with the value calcualated
     * in SoftwareCursor. That allows us to control the mouse position in whatever
     * way we want without having to do anything messy with the existing user input system
     * and all the GUI interaction stuff just works out of the box.
     */
    class VRGUI : MonoBehaviour
    {
        public static readonly string GUI_CANVAS = "GUI";
        public static readonly int UI_LAYER = LayerMask.NameToLayer("UI");
        public static readonly int UI_LAYER_MASK = (1 << UI_LAYER);
        public static readonly int UI_PANEL_LAYER = 22;
        public static readonly int UI_PANEL_LAYER_MASK = (1 << UI_PANEL_LAYER);
        private static readonly string OVERLAY_KEY = "VALHEIM_VR_MOD_OVERLAY";
        private static readonly string OVERLAY_NAME = "Valheim VR";
        private static readonly string UI_PANEL_NAME = "VRUIPanel";
        // Default UI Panel Size & Position
        private static readonly float UI_PANEL_SCALER = 3.0f;
        private static readonly float UI_PANEL_DISTANCE = 3f;
        private static readonly float UI_PANEL_HEIGHT = 1f;

        private float OVERLAY_CURVATURE = 0.25f; /* 0f - 1f */
        private bool USING_OVERLAY = true;

        private Camera _uiPanelCamera;
        private Camera _guiCamera;
        private Canvas _guiCanvas;
        private GameObject _uiPanel;
        private RenderTexture _guiTexture;
        private RenderTexture _overlayTexture;

        private SteamVR_LaserPointer _leftPointer;
        private SteamVR_LaserPointer _rightPointer;

        private VRGUI_InputModule _inputModule;

        // Native handle to OpenVR overlay
        private ulong _overlay = OpenVR.k_ulOverlayHandleInvalid;

        public void Awake()
        {
            LogDebug("VRGUI: Awake()");
            USING_OVERLAY = VVRConfig.GetUseOverlayGui();

            LogInfo("VRGUI: Using Overlay - " + USING_OVERLAY);
            OVERLAY_CURVATURE = VVRConfig.GetOverlayCurvature();

            _inputModule = EventSystem.current.gameObject.AddComponent<VRGUI_InputModule>();
        }

        public void OnEnable()
        {
            LogDebug("VRGUI: OnEnable()");
            creatGuiCamera();
            if (USING_OVERLAY)
            {
                createOverlay();
            }
        }

        public void Update()
        {
            if (ensureGuiCanvas())
            {
                CrosshairManager.instance.maybeReparentCrosshair();
                if (USING_OVERLAY)
                {
                    checkAndSetCurvatureUpdates();
                    updateOverlay();
                } else
                {
                    updateUiPanel();
                    maybeInitializePointers();
                }
            }
        }

        public void OnDisable()
        {
            LogDebug("VRGUI: OnDisable()");
            destroyOverlay();
        }

        private void checkAndSetCurvatureUpdates()
        {
            if (Input.GetKeyDown(KeyCode.Minus))
            {
                OVERLAY_CURVATURE -= 0.01f;
            }
            if (Input.GetKeyDown(KeyCode.Equals))
            {
                OVERLAY_CURVATURE += 0.01f;
            }
            OVERLAY_CURVATURE = Mathf.Clamp(OVERLAY_CURVATURE, 0f, 1f);
            VVRConfig.UpdateOverlayCurvature(OVERLAY_CURVATURE);
        }

        private void updateUiPanel()
        {
            if (!ensureUIPanel())
            {
                return;
            }
            updateUiPanelScaleAndPosition();
            if (_guiCanvas != null && _guiTexture != null) 
            {
                _uiPanel.GetComponent<Renderer>().material.mainTexture = _guiTexture;
            }
        }

        private void updateUiPanelScaleAndPosition()
        {
            _uiPanel.transform.rotation = VRPlayer.instance.transform.rotation;
            _uiPanel.transform.position = VRPlayer.instance.transform.position +
                VRPlayer.instance.transform.forward * UI_PANEL_DISTANCE + Vector3.up * UI_PANEL_HEIGHT;
            float ratio = (float)Screen.width / (float)Screen.height;
            _uiPanel.transform.localScale = new Vector3(UI_PANEL_SCALER * ratio, UI_PANEL_SCALER, 1f);
        }

        private bool ensureUIPanel()
        {
            if (_uiPanel != null)
            {
                return true;
            }
            if (VRPlayer.instance == null || _guiTexture == null)
            {   
                return false;
            }
            createUiPanelCamera();
            _uiPanel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _uiPanel.name = UI_PANEL_NAME;
            _uiPanel.layer = UI_PANEL_LAYER;
            Material mat = VRAssetManager.GetAsset<Material>("vr_panel_unlit");
            _uiPanel.GetComponent<Renderer>().material = mat;
            _uiPanel.GetComponent<Renderer>().material.mainTexture = _guiTexture;
            return (_uiPanel != null);
        }

        private void maybeInitializePointers()
        {
            if (VRPlayer.instance == null)
            {
                return;
            }
            if (_leftPointer == null)
            {
                _leftPointer = VRPlayer.leftPointer;
                if (_leftPointer != null)
                {
                    _leftPointer.PointerClick += OnPointerClick;
                    _leftPointer.PointerTracking += OnPointerTracking;
                }
            }
            if (_rightPointer == null)
            {
                _rightPointer = VRPlayer.rightPointer;
                if (_rightPointer != null)
                {
                    _rightPointer.PointerClick += OnPointerClick;
                    _rightPointer.PointerTracking += OnPointerTracking;
                }
            }
        }

        private void createUiPanelCamera()
        {
            GameObject uiPanelCameraObj = new GameObject(CameraUtils.VR_UI_CAMERA);
            _uiPanelCamera = uiPanelCameraObj.AddComponent<Camera>();
            _uiPanelCamera.CopyFrom(CameraUtils.getCamera(CameraUtils.VR_CAMERA));
            _uiPanelCamera.depth = _guiCamera.depth;
            _uiPanelCamera.clearFlags = CameraClearFlags.Depth;
            _uiPanelCamera.cullingMask = UI_PANEL_LAYER_MASK;
            _uiPanelCamera.transform.parent =
               CameraUtils.getCamera(CameraUtils.VR_CAMERA).gameObject.transform;
        }

        public void OnPointerClick(object p, PointerEventArgs e)
        {
            if (isUiPanel(e.target))
            {
                _inputModule.SimulateClick();
            }
        }

        public void OnPointerTracking(object p, PointerEventArgs e)
        {
            if (isUiPanel(e.target))
            {
                SoftwareCursor.simulatedMousePosition =
                    convertLocalUiPanelCoordinatesToCursorCoordinates(e.target.InverseTransformPoint(e.position));
            }
        }

        private Vector2 convertLocalUiPanelCoordinatesToCursorCoordinates(Vector3 localCoordinates)
        {
            float x = localCoordinates.x + 0.5f;
            float y = localCoordinates.y + 0.5f;
            Vector2 cursorSpace = new Vector2(Screen.width*x, Screen.height*y);
            return cursorSpace;
        }

        private bool isUiPanel(Transform t)
        {
            return (t != null) && (t.gameObject != null) &&
                t.gameObject.name == UI_PANEL_NAME;
        }

        private void createOverlay()
        {
            var overlay = OpenVR.Overlay;
            if (overlay != null)
            {
                _overlayTexture = new RenderTexture(new RenderTextureDescriptor(Screen.width, Screen.height));
                var error = overlay.CreateOverlay(OVERLAY_KEY, OVERLAY_NAME, ref _overlay);
                if (error != EVROverlayError.None)
                {
                    LogError("Problem creating VR GUI Overlay. GUI is disabled.");
                    enabled = false;
                    return;
                }
            }
            else
            {
                LogError("OpenVR.Overlay is null.");
            }
        }

        private void destroyOverlay()
        {
            if (_overlay != OpenVR.k_ulOverlayHandleInvalid)
            {
                var overlay = OpenVR.Overlay;
                if (overlay != null)
                {
                    overlay.DestroyOverlay(_overlay);
                }
                _overlay = OpenVR.k_ulOverlayHandleInvalid;
            }
        }

        private bool ensureGuiCanvas()
        {
            if (_guiCanvas == null)
            {
                foreach (var canvas in GameObject.FindObjectsOfType<Canvas>())
                {
                    if (canvas.name == GUI_CANVAS)
                    {
                        _guiCanvas = canvas;
                        onGuiCanvasFound();
                        return true;
                    }
                }
            } else
            {
                return true;
            }
            LogInfo("GUI Canvas not found yet.");
            return false;
        }

        private void updateOverlay()
        {
            var overlay = OpenVR.Overlay;
            if (overlay == null)
            {
                return;
            }
            var error = overlay.SetOverlayCurvature(_overlay, OVERLAY_CURVATURE);
            if (error != EVROverlayError.None)
            {
                LogError("Error setting overlay curvature.");
            }
            error = overlay.ShowOverlay(_overlay);
            if (error == EVROverlayError.InvalidHandle || error == EVROverlayError.UnknownOverlay)
            {
                LogDebug("Invalid Handle or UnknownOverlay");
                if (overlay.FindOverlay(OVERLAY_KEY, ref _overlay) != EVROverlayError.None)
                    return;
            }
            Texture_t tex = new Texture_t();
            // We need to blit the _guiTexture into a secondary texture upside down
            // and pass the second texture into the overlay instead to get the gui
            // to display right-side up. Alternative to this was using a negative scale
            // on the immediate children of the canvas, but that results in having inverted
            // mouse cursor controls.
            Graphics.Blit(_guiTexture, _overlayTexture, new Vector2(1, -1), new Vector2(0, 1));
            tex.handle = _overlayTexture.GetNativeTexturePtr();
            tex.eType = SteamVR.instance.textureType;
            tex.eColorSpace = EColorSpace.Auto;
            overlay.SetOverlayTexture(_overlay, ref tex);
            overlay.SetOverlayAlpha(_overlay, 1.0f);
            if (VRPlayer.instance != null)
            {
                // Passing the same transform to this RigidTransform constructor returns a RigidTransform
                // with the same position but inverse rotation. We then offset it in the Z direction, which creates
                // the result of having a screen "z" units offset in front of the player. The screen will be at a fixed
                // distance in front of the character and drawn over top of everything else regardless of distance,
                // hence "overlay". This is great for the GUI because things like minimap and health won't ever be
                // obstructed by world objects, regardless of whether or not the GUI element is behind the object with
                // regard to distance from the camera.
                var offset = new SteamVR_Utils.RigidTransform(VRPlayer.instance.transform, VRPlayer.instance.transform);
                float width = 4;
                overlay.SetOverlayWidthInMeters(_overlay, width);
                offset.pos.z += 2f;
                offset.pos.y += 1f;
                offset.pos.x += 0.5f;
                var t = offset.ToHmdMatrix34();
                overlay.SetOverlayTransformAbsolute(_overlay, SteamVR.settings.trackingSpace, ref t);
            }
        }

        private void onGuiCanvasFound()
        {
            LogDebug("Found GUI Canvas");
            SoftwareCursor.instance.GetComponent<RectTransform>().SetParent(_guiCanvas.transform, false);
            SoftwareCursor.instance.GetComponent<SoftwareCursor>().parent = _guiCanvas.GetComponent<RectTransform>();
            CrosshairManager.instance.guiCanvas = _guiCanvas;
            // Need to assign the camera to enable UI interactions
            _guiCanvas.worldCamera = _guiCamera;
            // Originally this was using ScreenSpaceCamera, which was handy to auto-size the canvas/camera
            // so I didn't need to worry about orthographic size or camera position. The problem
            // is that there are certain UI elements, particularly in the minimap, that are added
            // to the canvas using absolute pixel sizes - which when using ScreenSpaceCamera didn't translate
            // and ended up with map icons extremely large and obscuring the entire map. By using WorldSpace
            // for the render mode, we can keep the world coordinates equal to the screen space coordinates,
            // i.e. 1 pixel on screen = 1 unit of world space. That way when any elements are added to the GUI
            // at a specific pixel size, they are scaled properly.
            _guiCanvas.renderMode = RenderMode.WorldSpace;
            _guiCamera.gameObject.transform.position = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, -1);
            _guiCamera.orthographicSize = Screen.height * 0.5f;
        }

        private void creatGuiCamera()
        {
            LogDebug("Creating GUI Camera");
            _guiTexture = new RenderTexture(new RenderTextureDescriptor(Screen.width, Screen.height));
            GameObject guiCamObj = new GameObject(CameraUtils.VRGUI_SCREENSPACE_CAM);
            DontDestroyOnLoad(guiCamObj);
            _guiCamera = guiCamObj.AddComponent<Camera>();
            _guiCamera.orthographic = true;
            // Assign the RenderTexture to the camera
            _guiCamera.targetTexture = _guiTexture;
            _guiCamera.depth = 1;
            _guiCamera.useOcclusionCulling = false;
            // This enables transparency on the GUI
            // I tried "Depth" for the clear flags, but
            // it had weird/unexpected results and Color
            // just works...
            _guiCamera.clearFlags = CameraClearFlags.Color;
            // Required to actually capture only the GUI layer
            _guiCamera.cullingMask = UI_LAYER_MASK;
            _guiCamera.farClipPlane = 1.1f;
            _guiCamera.nearClipPlane = 0.9f;
            _guiCamera.enabled = true;
        }

        class VRGUI_InputModule : StandaloneInputModule
        {
            public void SimulateClick()
            {
                // Use the existing EventSystems input module input as the
                // input for our custom input module.
                m_InputOverride = EventSystem.current.currentInputModule.input;
                MouseState mousePointerEventData = GetMousePointerEventData();
                MouseButtonEventData buttonState = mousePointerEventData.GetButtonState(PointerEventData.InputButton.Left).eventData;
                // Set the mouse button state to "PressedAndReleased" to indicate a mouse click occurred
                buttonState.buttonState = PointerEventData.FramePressState.PressedAndReleased;
                ProcessMousePress(buttonState);
            }
        }

    }
}
