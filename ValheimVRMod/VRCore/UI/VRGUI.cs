using UnityEngine;
using UnityEngine.EventSystems;
using ValheimVRMod.Utilities;
using Valve.VR;
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
        private static readonly string OVERLAY_KEY = "VALHEIM_VR_MOD_OVERLAY";
        private static readonly string OVERLAY_NAME = "Valheim VR";
        private static readonly string UI_PANEL_NAME = "VRUIPanel";
        // The angle difference that is acceptable for the GUI being
        // considered to be "centered"
        private static readonly float RECENTERED_TOLERANCE = 1f;

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

        private static bool isRecentering = false;
        private bool movingLastFrame = false;
        private Quaternion lastVrPlayerRotation = Quaternion.identity;

        // Native handle to OpenVR overlay
        private ulong _overlay = OpenVR.k_ulOverlayHandleInvalid;

        public void Awake()
        {
            USING_OVERLAY = VHVRConfig.GetUseOverlayGui();
            OVERLAY_CURVATURE = VHVRConfig.GetOverlayCurvature();
            _inputModule = EventSystem.current.gameObject.AddComponent<VRGUI_InputModule>();
        }

        public void OnEnable()
        {
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
                if (VHVRConfig.ShowRepairHammer() && RepairModePositionIndicator.instance != null)
                {
                    RepairModePositionIndicator.instance.Update();
                }
                maybeTriggerGuiRecenter();
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
            VHVRConfig.UpdateOverlayCurvature(OVERLAY_CURVATURE);
        }

        private void updateUiPanel()
        {
            if (!ensureUIPanel())
            {
                return;
            }
            if (_uiPanelCamera == null)
            {
                createUiPanelCamera();
            }
            updateUiPanelScaleAndPosition();
            if (_guiCanvas != null && _guiTexture != null) 
            {
                _uiPanel.GetComponent<Renderer>().material.mainTexture = _guiTexture;
            }
        }

        private void updateUiPanelScaleAndPosition()
        {
            var offsetPosition = new Vector3(0f, VHVRConfig.GetUiPanelVerticalOffset(), VHVRConfig.GetUiPanelDistance());
            if (useDynamicallyPositionedGui())
            {
                var playerInstance = Player.m_localPlayer;
                var currentRotation = getCurrentGuiRotation();
                if (isRecentering)
                {
                    // We are currently recentering, so calculate a new rotation a step towards the targe rotation
                    // and set the GUI position using that rotation. If the new rotation is close enough to
                    // the target rotation, then stop recentering for the next frame.
                    var targetRotation = getTargetGuiRotation();
                    var stepRotation = Quaternion.RotateTowards(currentRotation, targetRotation, getRecenterStepSize(targetRotation, currentRotation));
                    _uiPanel.transform.rotation = stepRotation;
                    _uiPanel.transform.position = playerInstance.transform.position + stepRotation * offsetPosition;
                    maybeResetIsRecentering(stepRotation, targetRotation);
                } else
                {
                    // We are not recentering, so keep the GUI in front of the player. Need to account for
                    // any rotation of the VRPlayer instance caused by mouse or joystick input since the last frame.
                    Quaternion rotationDelta = VRPlayer.instance.transform.rotation * Quaternion.Inverse(lastVrPlayerRotation);
                    lastVrPlayerRotation = VRPlayer.instance.transform.rotation;
                    var newRotation = currentRotation * rotationDelta;
                    _uiPanel.transform.rotation = newRotation;
                    _uiPanel.transform.position = playerInstance.transform.position +  newRotation * offsetPosition;
                }
            }
            else
            {
                _uiPanel.transform.rotation = VRPlayer.instance.transform.rotation;
                _uiPanel.transform.position = VRPlayer.instance.transform.position + VRPlayer.instance.transform.rotation * offsetPosition;
            }
            float ratio = (float)Screen.width / (float)Screen.height;
            _uiPanel.transform.localScale = new Vector3(VHVRConfig.GetUiPanelSize() * ratio,
                                                        VHVRConfig.GetUiPanelSize(), 1f);
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
            _uiPanel.layer = LayerUtils.getUiPanelLayer();
            Material mat = VRAssetManager.GetAsset<Material>("vr_panel_unlit");
            _uiPanel.GetComponent<Renderer>().material = mat;
            _uiPanel.GetComponent<Renderer>().material.mainTexture = _guiTexture;
            _uiPanel.GetComponent<MeshCollider>().convex = true;
            _uiPanel.GetComponent<MeshCollider>().isTrigger = true;
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
                    _leftPointer.PointerTracking += OnPointerTracking;
                }
            }
            if (_rightPointer == null)
            {
                _rightPointer = VRPlayer.rightPointer;
                if (_rightPointer != null)
                {
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
            _uiPanelCamera.cullingMask = LayerUtils.UI_PANEL_LAYER_MASK;
            _uiPanelCamera.transform.parent =
               CameraUtils.getCamera(CameraUtils.VR_CAMERA).gameObject.transform;
        }

        public void OnPointerClick(object p, PointerEventArgs e)
        {
            if (isUiPanel(e.target))
                _inputModule.SimulateClick();
        }

        public void OnPointerRightClick(object p, PointerEventArgs e)
        {
            if (isUiPanel(e.target))
                _inputModule.SimulateRightClick();
        }

        public void OnPointerTracking(object p, PointerEventArgs e)
        {
            if (isUiPanel(e.target))
            {
                SoftwareCursor.simulatedMousePosition =
                    convertLocalUiPanelCoordinatesToCursorCoordinates(e.target.InverseTransformPoint(e.position));
                _inputModule.UpdateLeftButtonState(e.buttonStateLeft);
                _inputModule.UpdateRightButtonState(e.buttonStateRight);
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
            // to display right-side up.
            Graphics.Blit(_guiTexture, _overlayTexture, new Vector2(1, -1), new Vector2(0, 1));
            tex.handle = _overlayTexture.GetNativeTexturePtr();
            tex.eType = SteamVR.instance.textureType;
            tex.eColorSpace = EColorSpace.Auto;
            overlay.SetOverlayTexture(_overlay, ref tex);
            overlay.SetOverlayAlpha(_overlay, 1.0f);
            updateOverlayGuiSizeAndPosition();
        }

        private void updateOverlayGuiSizeAndPosition()
        {
            var overlay = OpenVR.Overlay;
            if (overlay == null)
            {
                return;
            }
            var offsetPosition = new Vector3(0f, VHVRConfig.GetOverlayVerticalOffset(), VHVRConfig.GetOverlayDistance());
            var offsetRotation = Quaternion.identity;
            if (useDynamicallyPositionedGui())
            {
                var currentRotation = getCurrentGuiRotation();
                if (isRecentering)
                {
                    // We are currently recentering, so calculate a new rotation a step towards the target
                    // rotation and reposition the GUI to that rotation. If the new rotation is close
                    // enough to the target, then end recentering for next frame.
                    var targetRotation = getTargetGuiRotation();
                    var stepRotation = Quaternion.RotateTowards(currentRotation, targetRotation, getRecenterStepSize(targetRotation, currentRotation));
                    offsetPosition = stepRotation * offsetPosition;
                    offsetRotation = stepRotation;
                    maybeResetIsRecentering(stepRotation, targetRotation);
                }
                else
                {
                    // Not recentering, so leave the GUI position where it is at
                    offsetPosition = currentRotation * offsetPosition;
                    offsetRotation = currentRotation;
                }
            }
            var offset = new SteamVR_Utils.RigidTransform(offsetPosition, offsetRotation);
            overlay.SetOverlayWidthInMeters(_overlay, VHVRConfig.GetOverlayWidth());
            var t = offset.ToHmdMatrix34();
            overlay.SetOverlayTransformAbsolute(_overlay, SteamVR.settings.trackingSpace, ref t);
        }

        private bool useDynamicallyPositionedGui()
        {
            return VRPlayer.attachedToPlayer && VRPlayer.inFirstPerson && VHVRConfig.UseLookLocomotion();
        }

        private void maybeTriggerGuiRecenter()
        {
            if (isRecentering)
            {
                return;
            }
            bool movingThisFrame = VRPlayer.isMoving;
            bool startedMovingThisFrame = movingThisFrame && !movingLastFrame;
            movingLastFrame = movingThisFrame;
            if ((startedMovingThisFrame && VHVRConfig.RecenterGuiOnMove()) || headGuiAngleExceeded(movingThisFrame))
            {
                triggerGuiRecenter();
            }
        }

        private void maybeResetIsRecentering(Quaternion updatedAngle, Quaternion target)
        {
            if (Mathf.Abs(Quaternion.Angle(updatedAngle, target)) <= RECENTERED_TOLERANCE)
            {
                isRecentering = false;
            }
        }

        private float getRecenterStepSize(Quaternion target, Quaternion current)
        {
            // Scale the step size by the angle difference between the
            // target rotation and current rotation
            return Mathf.Abs(Quaternion.Angle(target, current)) * .1f;
        }

        private Quaternion getTargetGuiRotation()
        {
            if (Player.m_localPlayer != null)
            {
                if (USING_OVERLAY)
                {
                    return Player.m_localPlayer.transform.rotation * Quaternion.Inverse(VRPlayer.instance.transform.rotation);
                } else
                {
                    float hmdAngle = VRPlayer.instance.GetComponent<Valve.VR.InteractionSystem.Player>().hmdTransform.rotation.eulerAngles.y;
                    return Quaternion.Euler(0f, hmdAngle, 0f);
                }
            } else
            {
                return Quaternion.identity;
            }
        }

        private Quaternion getCurrentGuiRotation()
        {
            if (USING_OVERLAY)
            {
                var overlay = OpenVR.Overlay;
                if (overlay == null)
                {
                    return Quaternion.identity;
                }
                var currentTransform = new HmdMatrix34_t();
                var trackingOrigin = SteamVR.settings.trackingSpace;
                var error = overlay.GetOverlayTransformAbsolute(_overlay, ref trackingOrigin, ref currentTransform);
                if (error != EVROverlayError.None)
                {
                    return Quaternion.identity;
                }
                return new SteamVR_Utils.RigidTransform(currentTransform).rot;
            } else
            {
                if (!ensureUIPanel())
                {
                    return Quaternion.identity;
                }
                return _uiPanel.transform.rotation;
            }
        }

        private bool headGuiAngleExceeded(bool moving)
        {
            var maxAngle = moving ? VHVRConfig.MobileGuiRecenterAngle() : VHVRConfig.StationaryGuiRecenterAngle();
            return Mathf.Abs(Quaternion.Angle(getCurrentGuiRotation(), getTargetGuiRotation())) > maxAngle;
        }

        public static void triggerGuiRecenter()
        {
            isRecentering = true;
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
            _guiCamera.cullingMask = (1 << LayerMask.NameToLayer("UI"));
            _guiCamera.farClipPlane = 1.1f;
            _guiCamera.nearClipPlane = 0.9f;
            _guiCamera.enabled = true;
        }

        class VRGUI_InputModule : StandaloneInputModule
        {
            bool lastLeftState = false;
            bool lastRightState = false;

            private void SimulateButtonState(PointerEventData.FramePressState state, PointerEventData.InputButton button)
            {
                // Use the existing EventSystems input module input as the
                // input for our custom input module.
                m_InputOverride = EventSystem.current.currentInputModule.input;
                MouseState mousePointerEventData = GetMousePointerEventData();
                // Retrieve button state data for intended button
                MouseButtonEventData buttonState = mousePointerEventData.GetButtonState(button).eventData;
                // Set the mouse button state to required state
                buttonState.buttonState = state;
                ProcessMousePress(buttonState);
            }

            public void SimulateClick()
            {
                SimulateButtonState(PointerEventData.FramePressState.PressedAndReleased, PointerEventData.InputButton.Left);
            }

            public void SimulateRightClick()
            {
                SimulateButtonState(PointerEventData.FramePressState.PressedAndReleased, PointerEventData.InputButton.Right);
            }

            public void SimulateLeftMouseHold()
            {
                SimulateButtonState(PointerEventData.FramePressState.Pressed, PointerEventData.InputButton.Left);
            }

            public void SimulateRightMouseHold()
            {
                SimulateButtonState(PointerEventData.FramePressState.Pressed, PointerEventData.InputButton.Right);
            }

            public void UpdateLeftButtonState(bool state)
            {
                var buttonState = PointerEventData.FramePressState.NotChanged;
                if (lastLeftState != state)
                {
                    if (state)
                    {
                        buttonState = PointerEventData.FramePressState.Pressed;
                    } else
                    {
                        buttonState = PointerEventData.FramePressState.Released;
                    }
                }
                lastLeftState = state;
                SimulateButtonState(buttonState, PointerEventData.InputButton.Left);
            }

            public void UpdateRightButtonState(bool state)
            {
                var buttonState = PointerEventData.FramePressState.NotChanged;
                if (lastRightState != state)
                {
                    if (state)
                    {
                        buttonState = PointerEventData.FramePressState.Pressed;
                    }
                    else
                    {
                        buttonState = PointerEventData.FramePressState.Released;
                    }
                }
                lastRightState = state;
                SimulateButtonState(buttonState, PointerEventData.InputButton.Right);
            }
        }

    }
}
