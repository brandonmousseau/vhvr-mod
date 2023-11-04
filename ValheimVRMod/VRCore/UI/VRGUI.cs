using UnityEngine;
using UnityEngine.EventSystems;
using ValheimVRMod.Utilities;
using Valve.VR;
using Valve.VR.Extras;
using System.Collections.Generic;

using static ValheimVRMod.Utilities.LogUtils;
using UnityEngine.InputSystem.UI;

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
    [DefaultExecutionOrder(int.MaxValue)]
    class VRGUI : MonoBehaviour
    {
        public static Vector2 GUI_DIMENSIONS = VHVRConfig.GetUiPanelResolution();
        public static Vector2 originalResolution;
        public static bool originalFullScreen;
        public static bool isResized;
        public static readonly string MENU_GUI_CANVAS = "GUI";
        public static readonly string GAME_GUI_CANVAS = "LoadingGUI";
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
        private GameObject _uiPanelTransformLocker;
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
            //Automatically Change resolution aspect ratio on UI Start
            //GUI_DIMENSIONS = new Vector2(GUI_DIMENSIONS.x, GUI_DIMENSIONS.x / Screen.width * Screen.height);
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

        public void OnRenderObject()
        {
            if (ensureGuiCanvas())
            {
                if (!USING_OVERLAY)
                {
                    updateUiPanel();
                    maybeInitializePointers();
                }
            }
        }

        public void FixedUpdate()
        {
            if (ensureGuiCanvas())
            {
                GUI_DIMENSIONS = VHVRConfig.GetUiPanelResolution();
                _guiCanvas.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, GUI_DIMENSIONS.x);
                _guiCanvas.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, GUI_DIMENSIONS.y);
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

        public void Update()
        {
            disableVanillaInputSystemUiInputModule();
            if (VHVRConfig.UseVrControls())
            {
                return;
            }
            bool leftButtonPressed = Input.GetMouseButton(0);
            bool rightButtonPressed = Input.GetMouseButton(1);
            bool middleButtonPressed = Input.GetMouseButton(2);
            _inputModule.UpdateButtonStates(leftButtonPressed, rightButtonPressed, middleButtonPressed);
        }

        // The Input system was replaced and it is incompabible. We now rely on inserting mouse controls
        // via the StandaloneInputModule. So we'll disable this one to avoid conflicts (e.g. double
        // mouse cursors interacting with UI).
        private void disableVanillaInputSystemUiInputModule()
        {
            if (EventSystem.current.gameObject)
            {
                var inputUiModule = EventSystem.current.GetComponent<InputSystemUIInputModule>();
                inputUiModule.enabled = false;
            }
        }

        public void LateUpdate()
        {
            // Needs to go into LateUpdate to ensure it runs after VRIK calculations
            // since the model HumanBodyBones are being referenced
            VRHud.instance.Update();
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
                if (shouldLockDynamicGuiPosition())
                {
                    // Restore the locked position and rotation of GUI's relative to the VR camera rig.
                    _uiPanel.transform.SetPositionAndRotation(_uiPanelTransformLocker.transform.position, _uiPanelTransformLocker.transform.rotation);
                    isRecentering = false;
                    return;
                }
                // Record the GUI's transform in case it will be locked in that position and rotation.
                _uiPanelTransformLocker.transform.SetPositionAndRotation(_uiPanel.transform.position, _uiPanel.transform.rotation);

                var playerInstance = Player.m_localPlayer;

                if (playerInstance.IsAttachedToShip())
                {
                    // Always lock the UI to the forward direction of ship when sailing.
                    var upTarget = Vector3.up;
                    if (VHVRConfig.IsShipImmersiveCamera())
                    {
                        upTarget = VRPlayer.instance.transform.up;
                    }
                    Vector3 forwardDirection = Vector3.ProjectOnPlane(Player.m_localPlayer.m_attachPoint.forward, upTarget).normalized;
                    _uiPanel.transform.rotation = Quaternion.LookRotation(forwardDirection, VRPlayer.instance.transform.up);
                    _uiPanel.transform.position = playerInstance.transform.position + _uiPanel.transform.rotation * offsetPosition;
                    return;
                }
                _uiPanel.transform.localScale = new Vector3(VHVRConfig.GetUiPanelSize() * GUI_DIMENSIONS.x / GUI_DIMENSIONS.y,
                                                        VHVRConfig.GetUiPanelSize(), 0.00001f);
                var currentDirection = getCurrentGuiDirection();
                if (isRecentering)
                {
                    // We are currently recentering, so calculate a new rotation a step towards the targe rotation
                    // and set the GUI position using that rotation. If the new rotation is close enough to
                    // the target rotation, then stop recentering for the next frame.
                    var targetDirection = getTargetGuiDirection();
                    var stepDirection = Vector3.Slerp(currentDirection, targetDirection, VHVRConfig.GuiRecenterSpeed() * Mathf.Deg2Rad * Time.unscaledDeltaTime);
                    var stepRotation = Quaternion.LookRotation(stepDirection, VRPlayer.instance.transform.up);
                    _uiPanel.transform.rotation = stepRotation;
                    _uiPanel.transform.position = playerInstance.transform.position + stepRotation * offsetPosition;
                    lastVrPlayerRotation = VRPlayer.instance.transform.rotation;
                    maybeResetIsRecentering(stepDirection, targetDirection);
                } else
                {
                    // We are not recentering, so keep the GUI in front of the player. Need to account for
                    // any rotation of the VRPlayer instance caused by mouse or joystick input since the last frame.
                    float rotationDelta = VRPlayer.instance.transform.rotation.eulerAngles.y - lastVrPlayerRotation.eulerAngles.y;
                    lastVrPlayerRotation = VRPlayer.instance.transform.rotation;
                    var newRotation = Quaternion.LookRotation(currentDirection, VRPlayer.instance.transform.up);
                    newRotation *= Quaternion.AngleAxis(rotationDelta, Vector3.up);
                    _uiPanel.transform.rotation = newRotation;
                    _uiPanel.transform.position = playerInstance.transform.position + newRotation * offsetPosition;
                }
            }
            else
            {
                _uiPanel.transform.rotation = VRPlayer.instance.transform.rotation;
                _uiPanel.transform.position = VRPlayer.instance.transform.position + VRPlayer.instance.transform.rotation * offsetPosition;
            }
            float ratio = (float)GUI_DIMENSIONS.x / (float)GUI_DIMENSIONS.y;
            _uiPanel.transform.localScale = new Vector3(VHVRConfig.GetUiPanelSize() * ratio,
                                                        VHVRConfig.GetUiPanelSize(), 0.00001f);
        }

        private bool shouldLockDynamicGuiPosition()
        {
            return VHVRConfig.LockGuiWhileMenuOpen() && menuIsOpen() && !Player.m_localPlayer.IsAttachedToShip();
        }

        private bool menuIsOpen()
        {
            return StoreGui.IsVisible() || InventoryGui.IsVisible() || Menu.IsVisible() || (TextViewer.instance && TextViewer.instance.IsVisible()) || Minimap.IsOpen();
        }

        private bool ensureUIPanel()
        {
            if (_uiPanel != null && _uiPanelTransformLocker != null)
            {
                return true;
            }
            var vrCam = CameraUtils.getCamera(CameraUtils.VR_CAMERA);
            if (VRPlayer.instance == null || _guiTexture == null 
                || vrCam == null || vrCam.gameObject == null || vrCam.transform.parent == null)
            {
                return false;
            }

            createUiPanelCamera();

            if (_uiPanel == null)
            {
                _uiPanel = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _uiPanel.name = UI_PANEL_NAME;
                _uiPanel.layer = LayerUtils.getUiPanelLayer();
                Material mat = VRAssetManager.GetAsset<Material>("vr_panel_unlit");
                _uiPanel.GetComponent<Renderer>().material = mat;
                _uiPanel.GetComponent<Renderer>().material.mainTexture = _guiTexture;
                _uiPanel.GetComponent<MeshCollider>().convex = true;
                _uiPanel.GetComponent<MeshCollider>().isTrigger = true;
            }

            if (_uiPanelTransformLocker == null)
            {
                _uiPanelTransformLocker = new GameObject();
                // The locker should move with the vr camera rig in case we need to use it to lock the UI panel in place.
                _uiPanelTransformLocker.transform.SetParent(vrCam.transform.parent, false);
            }

            return _uiPanel != null && _uiPanelTransformLocker != null;
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
                    _leftPointer.PointerTracking += OnPointerTrackingLeftHand;
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
            var vrCam = CameraUtils.getCamera(CameraUtils.VR_CAMERA);
            if(vrCam == null || vrCam.gameObject == null)
            {
                return;
            }
            GameObject uiPanelCameraObj = new GameObject(CameraUtils.VR_UI_CAMERA);
            _uiPanelCamera = uiPanelCameraObj.AddComponent<Camera>();
            _uiPanelCamera.CopyFrom(CameraUtils.getCamera(CameraUtils.VR_CAMERA));
            _uiPanelCamera.depth = _guiCamera.depth;
            _uiPanelCamera.clearFlags = CameraClearFlags.Depth;
            _uiPanelCamera.renderingPath = RenderingPath.Forward;
            _uiPanelCamera.cullingMask = LayerUtils.UI_PANEL_LAYER_MASK;
            _uiPanelCamera.transform.SetParent(vrCam.transform);
        }


        public void OnPointerTrackingLeftHand(object p, PointerEventArgs e)
        {
            if (isUiPanel(e.target))
            {
                SoftwareCursor.simulatedMousePosition =
                    convertLocalUiPanelCoordinatesToCursorCoordinates(e.target.InverseTransformPoint(e.position));
                // PointerEventArgs#buttonStateLeft does not give valid state of the trigger, so we need check the action states explicitly.
                // Note: when the laser pointer action set is active, it takes priority over the Valheim action set so SteamVR_Actions.valheim_Use are SteamVR_Actions.valheim_UseLeft are unused.
                // TODO: update click modifier to use grab buttons and left click to use both controller's triggers in laser action set and update this method accordingly.
                LogUtils.LogWarning("Left hand: " + SteamVR_Actions.valheim_UseLeft.state + " " + SteamVR_Actions.LaserPointers.ClickModifier.GetState(SteamVR_Input_Sources.LeftHand));
                _inputModule.UpdateButtonStates(
                    SteamVR_Actions.LaserPointers.ClickModifier.GetState(SteamVR_Input_Sources.LeftHand) || SteamVR_Actions.LaserPointers.LeftClick.GetState(SteamVR_Input_Sources.LeftHand),
                    SteamVR_Actions.valheim_QuickActions.GetState(SteamVR_Input_Sources.LeftHand),
                    false);
            }
        }

        public void OnPointerTracking(object p, PointerEventArgs e)
        {
            if (isUiPanel(e.target))
            {
                SoftwareCursor.simulatedMousePosition =
                    convertLocalUiPanelCoordinatesToCursorCoordinates(e.target.InverseTransformPoint(e.position));
                _inputModule.UpdateButtonStates(e.buttonStateLeft, e.buttonStateRight, false);
            }
        }

        private Vector2 convertLocalUiPanelCoordinatesToCursorCoordinates(Vector3 localCoordinates)
        {
            float x = localCoordinates.x + 0.5f;
            float y = localCoordinates.y + 0.5f;
            Vector2 cursorSpace = new Vector2(GUI_DIMENSIONS.x * x, GUI_DIMENSIONS.y * y);
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
                _overlayTexture = new RenderTexture(new RenderTextureDescriptor((int)GUI_DIMENSIONS.x, (int)GUI_DIMENSIONS.y));
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
                    if (canvas.name == GAME_GUI_CANVAS 
                        || canvas.name == MENU_GUI_CANVAS)
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
                var currentDirection = getCurrentGuiDirection();
                if (isRecentering)
                {
                    // We are currently recentering, so calculate a new rotation a step towards the target
                    // rotation and reposition the GUI to that rotation. If the new rotation is close
                    // enough to the target, then end recentering for next frame.
                    var targetDirection = getTargetGuiDirection();
                    var stepDirection = Vector3.Slerp(currentDirection, targetDirection, VHVRConfig.GuiRecenterSpeed() * Mathf.Deg2Rad * Time.unscaledDeltaTime);
                    var stepRotation = Quaternion.LookRotation(stepDirection, VRPlayer.instance.transform.up);
                    offsetPosition = stepRotation * offsetPosition;
                    offsetRotation = stepRotation;
                    maybeResetIsRecentering(stepDirection, targetDirection);
                }
                else
                {
                    // Not recentering, so leave the GUI position where it is at
                    var currentRotation = Quaternion.LookRotation(currentDirection, VRPlayer.instance.transform.up);
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

        private void maybeResetIsRecentering(Vector3 newDirection, Vector3 target)
        {
            float recenterAngle = Mathf.Abs(Vector3.Angle(newDirection, target)) % 180f;
            if (recenterAngle <= RECENTERED_TOLERANCE || recenterAngle >= 180f - RECENTERED_TOLERANCE)
            {
                isRecentering = false;
            }
        }

        private Vector3 getTargetGuiDirection()
        {
            Vector3 forwardDirection = Vector3.forward;
            if (Player.m_localPlayer != null)
            {
                if (!USING_OVERLAY)
                {
                    forwardDirection = Valve.VR.InteractionSystem.Player.instance.hmdTransform.forward;
                    forwardDirection.y = 0;
                    forwardDirection.Normalize();
                }
            }
            
            return forwardDirection;
        }

        private Vector3 getCurrentGuiDirection()
        {
            if (USING_OVERLAY)
            {
                var overlay = OpenVR.Overlay;
                if (overlay == null)
                {
                    return Vector3.forward;
                }
                var currentTransform = new HmdMatrix34_t();
                var trackingOrigin = SteamVR.settings.trackingSpace;
                var error = overlay.GetOverlayTransformAbsolute(_overlay, ref trackingOrigin, ref currentTransform);
                if (error != EVROverlayError.None)
                {
                    return Vector3.forward;
                }
                return new SteamVR_Utils.RigidTransform(currentTransform).rot * Vector3.forward;
            } else
            {
                if (!ensureUIPanel())
                {
                    return Vector3.forward;
                }
                return _uiPanel.transform.forward;
            }
        }

        private bool headGuiAngleExceeded(bool moving)
        {
            var maxAngle = moving ? VHVRConfig.MobileGuiRecenterAngle() : VHVRConfig.StationaryGuiRecenterAngle();
            return Mathf.Abs(Vector3.Angle(getCurrentGuiDirection(), getTargetGuiDirection())) % 360f > maxAngle;
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
            _guiCanvas.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, GUI_DIMENSIONS.x);
            _guiCanvas.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, GUI_DIMENSIONS.y);
            _guiCamera.gameObject.transform.position = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, -1);
            _guiCamera.orthographicSize = GUI_DIMENSIONS.y * 0.5f;
        }

        private void creatGuiCamera()
        {
            LogDebug("Creating GUI Camera");
            _guiTexture = new RenderTexture(new RenderTextureDescriptor((int)GUI_DIMENSIONS.x, (int)GUI_DIMENSIONS.y));
            GameObject guiCamObj = new GameObject(CameraUtils.VRGUI_SCREENSPACE_CAM);
            DontDestroyOnLoad(guiCamObj);
            _guiCamera = guiCamObj.AddComponent<Camera>();
            _guiCamera.orthographic = true;
            // Assign the RenderTexture to the camera
            _guiCamera.targetTexture = _guiTexture;
            _guiCamera.depth = 1;
            _guiCamera.useOcclusionCulling = false;
            _guiCamera.renderingPath = RenderingPath.Forward;
            // This enables transparency on the GUI
            // I tried "Depth" for the clear flags, but
            // it had weird/unexpected results and Color
            // just works...
            _guiCamera.clearFlags = CameraClearFlags.Color;
            // Required to actually capture only the GUI layer
            _guiCamera.cullingMask = (1 << LayerMask.NameToLayer("UI"));
            _guiCamera.farClipPlane = 5f;
            _guiCamera.nearClipPlane = 0.1f;
            _guiCamera.enabled = true;
        }

        class VRGUI_InputModule : StandaloneInputModule
        {

            Dictionary<PointerEventData.InputButton, bool> lastButtonStateMap = new Dictionary<PointerEventData.InputButton, bool>();
            private bool inDragDeadZone;

            public VRGUI_InputModule() {
                lastButtonStateMap[PointerEventData.InputButton.Left] = false;
                lastButtonStateMap[PointerEventData.InputButton.Right] = false;
                lastButtonStateMap[PointerEventData.InputButton.Middle] = false;
            }

            public void UpdateButtonStates(bool leftButtonPressed, bool rightButtonPressed, bool middleButtonPressed) {
                // Use the existing EventSystems input module input as the
                // input for our custom input module.
                m_InputOverride = EventSystem.current.currentInputModule.input;
                UpdateButtonState(leftButtonPressed, PointerEventData.InputButton.Left);
                UpdateButtonState(rightButtonPressed, PointerEventData.InputButton.Right);
                UpdateButtonState(middleButtonPressed, PointerEventData.InputButton.Middle);
            }

            private void UpdateButtonState(bool state, PointerEventData.InputButton button)
            {
                var buttonState = PointerEventData.FramePressState.NotChanged;
                bool lastButtonState = lastButtonStateMap[button];
                if (lastButtonState != state)
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
                lastButtonStateMap[button] = state;
                SimulateButtonState(buttonState, button);
            }

            private void SimulateButtonState(PointerEventData.FramePressState state, PointerEventData.InputButton button) {
                MouseState mousePointerEventData = GetMousePointerEventData();
                // Retrieve button state data for intended button
                MouseButtonEventData buttonState = mousePointerEventData.GetButtonState(button).eventData;
                // Set the mouse button state to required state
                buttonState.buttonState = state;
                ProcessMousePress(buttonState);
                if (button != PointerEventData.InputButton.Left)
                {
                    return;
                }
                ProcessMove(buttonState.buttonData);

                if (state == PointerEventData.FramePressState.Pressed) {
                    inDragDeadZone = true;
                }

                if (inDragDeadZone && Vector2.Distance(buttonState.buttonData.pressPosition, buttonState.buttonData.position) > 15) {
                    inDragDeadZone = false;
                }

                if (!inDragDeadZone) {
                    ProcessDrag(buttonState.buttonData);
                }
            }
        }
    }
}
