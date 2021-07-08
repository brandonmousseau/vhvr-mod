using static ValheimVRMod.Utilities.LogUtils;

using AmplifyOcclusion;
using System.Reflection;
using RootMotion.FinalIK;
using UnityEngine;
using UnityEngine.PostProcessing;
using UnityEngine.SceneManagement;
using UnityStandardAssets.ImageEffects;
using ValheimVRMod.Scripts;
using ValheimVRMod.Patches;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore.UI;
using Valve.VR;
using Valve.VR.Extras;
using Valve.VR.InteractionSystem;

/**
 * VRPlayer manages instantiating the SteamVR Player
 * prefab as well as controlling it's world position and rotation.
 * 
 * Also manages enabling and disabling the hand laser pointer
 * based on what hands are active and what the user sets as
 * their preferred hand.
 */
namespace ValheimVRMod.VRCore
{
    class VRPlayer : MonoBehaviour
    {
        private enum HeadZoomLevel
        {
            FirstPerson,
            ThirdPerson0,
            ThirdPerson1,
            ThirdPerson2,
            ThirdPerson3
        }

        private static readonly string PLAYER_PREFAB_NAME = "ValheimVRPlayer";
        private static readonly string START_SCENE = "start";
        public static readonly string RIGHT_HAND = "RightHand";
        public static readonly string LEFT_HAND = "LeftHand";
        // This layer must be set in the hand model prefab in the
        // Unity AssetBundle project too. If they don't match,
        // the hands won't be rendered by the handsCam.
        private static Vector3 FIRST_PERSON_OFFSET = Vector3.zero;
        private static float SIT_HEIGHT_ADJUST = -0.7f;
        private static float CROUCH_HEIGHT_ADJUST = -0.4f;
        private static Vector3 THIRD_PERSON_0_OFFSET = new Vector3(0f, 1.0f, -0.6f);
        private static Vector3 THIRD_PERSON_1_OFFSET = new Vector3(0f, 1.4f, -1.5f);
        private static Vector3 THIRD_PERSON_2_OFFSET = new Vector3(0f, 1.9f, -2.6f);
        private static Vector3 THIRD_PERSON_3_OFFSET = new Vector3(0f, 3.2f, -4.4f);
        private static Vector3 THIRD_PERSON_CONFIG_OFFSET = Vector3.zero;
        private static float NECK_OFFSET = 0.2f;
        public const float ROOMSCALE_STEP_ANIMATION_SMOOTHING = 0.3f;
        public const float ROOMSCALE_ANIMATION_WEIGHT = 2f;
        public static bool justUnsheathed = false;

        private static float referencePlayerHeight;
        public static bool isRoomscaleSneaking {  get { return _isRoomscaleSneaking; } }
        private static bool _isRoomscaleSneaking = false;

        private static GameObject _prefab;
        private static GameObject _instance;
        private static VRPlayer _vrPlayerInstance;
        private static HeadZoomLevel _headZoomLevel = HeadZoomLevel.FirstPerson;

        private Camera _vrCam;
        private Camera _handsCam;
        private Camera _skyboxCam;

        //Roomscale movement variables
        private Transform _vrCameraRig;
        private Vector3 _lastCamPosition = Vector3.zero;
        private Vector3 _lastPlayerPosition = Vector3.zero;
        private FadeToBlackManager _fadeManager;
        private float _forwardSmoothVel = 0.0f, _sideSmoothVel = 0.0f;
        private static float _roomscaleAnimationForwardSpeed = 0.0f;
        private static float _roomscaleAnimationSideSpeed = 0.0f;
        public static float roomscaleAnimationForwardSpeed { get { return _roomscaleAnimationForwardSpeed;} }
        public static float roomscaleAnimationSideSpeed { get { return _roomscaleAnimationSideSpeed;} }
        public static Vector3 roomscaleMovement { get; private set; }

        private static Hand _leftHand;
        private static SteamVR_LaserPointer _leftPointer;
        private static Hand _rightHand;
        private static SteamVR_LaserPointer _rightPointer;
        private string _preferredHand;

        public static Hand leftHand { get { return _leftHand;} }
        public static Hand rightHand { get { return _rightHand;} }

        public static bool toggleShowLeftHand = true;
        public static bool toggleShowRightHand = true;

        public static bool handsActive
        {
            get
            {
                return handIsActive(_leftHand, _leftPointer) && handIsActive(_rightHand, _rightPointer);
            }
        }

        public static SteamVR_LaserPointer leftPointer { get { return _leftPointer;} }
        public static SteamVR_LaserPointer rightPointer { get { return _rightPointer; } }
        public static SteamVR_LaserPointer activePointer
        {
            get
            {
                if (leftPointer != null && leftPointer.pointerIsActive())
                {
                    return leftPointer;
                }
                else if (rightPointer != null && rightPointer.pointerIsActive())
                {
                    return rightPointer;
                }
                else
                {
                    return null;
                }
            }
        }

        public static bool inFirstPerson { get
            {
                return (_headZoomLevel == HeadZoomLevel.FirstPerson) && attachedToPlayer;
            }
        }

        public static bool isMoving
        {
            get
            {
                if (Player.m_localPlayer != null && attachedToPlayer)
                {
                    Vector3 relativeVelocity = Player.m_localPlayer.GetVelocity();
                    if(Player.m_localPlayer.m_lastGroundBody)
                        relativeVelocity -= Player.m_localPlayer.m_lastGroundBody.velocity;
                    return relativeVelocity.magnitude > 0.5f;
                }
                return false;
            }
        }

        public static GameObject instance { get { return _instance; } }
        public static VRPlayer vrPlayerInstance => _vrPlayerInstance;
        public static bool attachedToPlayer = false;

        private static float FIRST_PERSON_HEIGHT_OFFSET = 0.0f;
        private static bool _headPositionInitialized = false;
        public static bool headPositionInitialized
        {
            get
            {
                return _headPositionInitialized;
            }
            set
            {
                _headPositionInitialized = value;
                if (!_headPositionInitialized)
                {
                    FIRST_PERSON_HEIGHT_OFFSET = 0.0f;
                    FIRST_PERSON_OFFSET = Vector3.zero;
                }
            }
        }

        void Awake()
        {
            _vrPlayerInstance = this;
            _prefab = VRAssetManager.GetAsset<GameObject>(PLAYER_PREFAB_NAME);
            _preferredHand = VHVRConfig.GetPreferredHand();
            headPositionInitialized = false;
            FIRST_PERSON_OFFSET = Vector3.zero;
            THIRD_PERSON_CONFIG_OFFSET = VHVRConfig.GetThirdPersonHeadOffset();
            ensurePlayerInstance();
            gameObject.AddComponent<VRControls>();
        }

        void Update()
        {
            if (!ensurePlayerInstance())
            {
                return;
            }
            maybeUpdateHeadPosition();
            attachVrPlayerToWorldObject();
            enableCameras();
            checkAndSetHandsAndPointers();
            updateVrik();
            UpdateAmplifyOcclusionStatus();
            checkInteractions();
            CheckSneakRoomscale();
            
        }

        private void FixedUpdate() 
        {
            if(inFirstPerson)
            {
                DoRoomScaleMovement();
            } else roomscaleMovement = Vector3.zero;
        }

        void maybeUpdateHeadPosition()
        {
            if (VHVRConfig.AllowHeadRepositioning())
            {
                if (Input.GetKeyDown(VHVRConfig.GetHeadForwardKey()))
                {
                    updateHeadOffset(new Vector3(0f, 0f, 0.1f));
                }
                if (Input.GetKeyDown(VHVRConfig.GetHeadBackwardKey()))
                {
                    updateHeadOffset(new Vector3(0f, 0f, -0.1f));
                }
                if (Input.GetKeyDown(VHVRConfig.GetHeadLeftKey()))
                {
                    updateHeadOffset(new Vector3(-0.1f, 0f, 0f));
                }
                if (Input.GetKeyDown(VHVRConfig.GetHeadRightKey()))
                {
                    updateHeadOffset(new Vector3(0.1f, 0f, 0f));
                }
                if (Input.GetKeyDown(VHVRConfig.GetHeadUpKey()))
                {
                    updateHeadOffset(new Vector3(0f, 0.1f, 0f));
                }
                if (Input.GetKeyDown(VHVRConfig.GetHeadDownKey()))
                {
                    updateHeadOffset(new Vector3(0f, -0.1f, 0f));
                }
            }
        }

        private void updateHeadOffset(Vector3 offset)
        {
            if (!attachedToPlayer)
            {
                return;
            }
            if (inFirstPerson)
            {
                FIRST_PERSON_OFFSET += offset;
            } else
            {
                THIRD_PERSON_CONFIG_OFFSET += offset;
                VHVRConfig.UpdateThirdPersonHeadOffset(THIRD_PERSON_CONFIG_OFFSET);
            }
        }

        void UpdateAmplifyOcclusionStatus()
        {
            if (_vrCam == null || _vrCam.gameObject.GetComponent<AmplifyOcclusionEffect>() == null)
            {
                return;
            }
            var effect = _vrCam.gameObject.GetComponent<AmplifyOcclusionEffect>();
            effect.SampleCount = SampleCountLevel.Medium;
            effect.enabled = VHVRConfig.UseAmplifyOcclusion();
        }

        private void checkAndSetHandsAndPointers()
        {
            tryInitializeHands();
            if (_leftHand != null)
            {
                _leftHand.enabled = VHVRConfig.UseVrControls();
                _leftHand.SetVisibility(_leftHand.enabled && !vrikEnabled());
            }
            if (_rightHand != null)
            {
                _rightHand.enabled = VHVRConfig.UseVrControls();
                _rightHand.SetVisibility(_rightHand.enabled && !vrikEnabled());
            }
            // Next check whether the hands are active, and enable the appropriate pointer based
            // on what is available and what the options set as preferred. Disable the inactive pointer(s).
            if (handIsActive(_leftHand, _leftPointer) && handIsActive(_rightHand, _rightPointer))
            {
                // Both hands active, so choose preferred hand
                if (_preferredHand == LEFT_HAND)
                {
                    setPointerActive(_leftPointer, true);
                    setPointerActive(_rightPointer, false);
                } else
                {
                    setPointerActive(_rightPointer, true);
                    setPointerActive(_leftPointer, false);
                }
            } else if (handIsActive(_rightHand, _rightPointer))
            {
                setPointerActive(_rightPointer, true);
                setPointerActive(_leftPointer, false);
            } else if (handIsActive(_leftHand, _leftPointer))
            {
                setPointerActive(_leftPointer, true);
                setPointerActive(_rightPointer, false);
            } else
            {
                setPointerActive(_leftPointer, false);
                setPointerActive(_rightPointer, false);
            }
        }

        private void tryInitializeHands()
        {
            // First try and initialize both hands and pointer scripts
            if (_leftHand == null || _leftPointer == null)
            {
                _leftHand = getHand(LEFT_HAND, _instance);
                if (_leftHand != null)
                {
                    _leftPointer = _leftHand.GetComponent<SteamVR_LaserPointer>();
                    if (_leftPointer != null)
                    {
                        _leftPointer.raycastLayerMask = LayerUtils.UI_PANEL_LAYER_MASK;
                    }
                }
            }
            if (_rightHand == null || _rightPointer == null)
            {
                _rightHand = getHand(RIGHT_HAND, _instance);
                if (_rightHand != null)
                {
                    _rightPointer = _rightHand.GetComponent<SteamVR_LaserPointer>();
                    if (_rightPointer != null)
                    {
                        _rightPointer.raycastLayerMask = LayerUtils.UI_PANEL_LAYER_MASK;
                    }
                }
            }
        }

        // Sets the given pointer active if "active" parameter is true
        // and laser pointers should currently be active.
        private void setPointerActive(SteamVR_LaserPointer p, bool active)
        {
            if (p == null)
            {
                return;
            }
            p.setUsePointer(active && shouldLaserPointersBeActive());
            p.setVisible(p.pointerIsActive() && Cursor.visible);
        }

        private bool shouldLaserPointersBeActive()
        {
            bool isInPlaceMode = (getPlayerCharacter() != null) && getPlayerCharacter().InPlaceMode();
            return VHVRConfig.UseVrControls() && (Cursor.visible || isInPlaceMode);
        }

        // Returns true if both the hand and pointer are not null
        // and the hand is active
        private static bool handIsActive(Hand h, SteamVR_LaserPointer p)
        {
            if (h == null || p == null)
            {
                return false;
            }
            return h.enabled && h.isActive && h.isPoseValid;
        }

        private Hand getHand(string hand, GameObject playerInstance)
        {
            foreach (Hand h in playerInstance.GetComponentsInChildren<Hand>())
            {
                if (h.gameObject.name == hand)
                {
                    return h;
                }
            }
            return null;
        }

        private bool ensurePlayerInstance()
        {
            if (_instance == null)
            {
                // Need to create an instance of the Player prefab
                if (_prefab == null)
                {
                    LogError("SteamVR Player Prefab is not loaded!");
                    return false;
                }
                _instance = Instantiate(_prefab);
                // Rigid bodies built into the SteamVR Player prefab will
                // cause problems and we don't actually need them for anything,
                // so disable all of them.
                if (_instance != null)
                {
                    DisableRigidBodies(_instance);
                }
            }
            return _instance != null;
        }

        private void enableCameras()
        {
            if (_vrCam == null || !_vrCam.enabled)
            {
                enableVrCamera();
            }
            if (_handsCam == null || !_handsCam.enabled)
            {
                enableHandsCamera();
            }
            if (_skyboxCam == null || !_skyboxCam.enabled)
            {
                enableSkyboxCamera();
            }
        }

        private void enableVrCamera()
        {
            if (_instance == null)
            {
                LogError("Cannot enable VR Camera with null SteamVR Player instance.");
                return;
            }
            Camera mainCamera = CameraUtils.getCamera(CameraUtils.MAIN_CAMERA);
            if (mainCamera == null)
            {
                LogError("Main Camera is null.");
                return;
            }
            Camera vrCam = CameraUtils.getCamera(CameraUtils.VR_CAMERA);
            CameraUtils.copyCamera(mainCamera, vrCam);
            maybeCopyPostProcessingEffects(vrCam, mainCamera);
            maybeAddAmplifyOcclusion(vrCam);
            // Prevent visibility of the head
            vrCam.nearClipPlane = VHVRConfig.GetNearClipPlane();
            // Turn off rendering the UI panel layer. We need to capture
            // it in a camera of higher depth so that it
            // is rendered on top of everything else. (except hands)
            vrCam.cullingMask &= ~(1 << LayerUtils.getUiPanelLayer());
            vrCam.cullingMask &= ~(1 << LayerMask.NameToLayer("UI"));
            vrCam.cullingMask &= ~(1 << LayerUtils.getHandsLayer());
            vrCam.cullingMask &= ~(1 << LayerUtils.getWorldspaceUiLayer());
            mainCamera.enabled = false;
            AudioListener mainCamListener = mainCamera.GetComponent<AudioListener>();
            if (mainCamListener != null)
            {
                LogDebug("Destroying MainCamera AudioListener");
                DestroyImmediate(mainCamListener);
            }
            //Add fade component to camera for transition handling
            _fadeManager = vrCam.gameObject.AddComponent<FadeToBlackManager>();
            _instance.SetActive(true);
            vrCam.enabled = true;
            _vrCam = vrCam;
            _vrCameraRig = vrCam.transform.parent;

            _fadeManager.OnFadeToWorld += () => {
                //Recenter
                VRPlayer.headPositionInitialized = false;
                VRPlayer.vrPlayerInstance?.ResetRoomscaleCamera();
            };
        }

        private void enableHandsCamera()
        {
            // Create a camera and assign its culling mask
            // to the unused layer. Assign depth to be higher
            // than the UI panel depth to ensure they are drawn
            // on top of the GUI.
            LogDebug("Enabling Hands Camera");
            GameObject handsCameraObject = new GameObject(CameraUtils.HANDS_CAMERA);
            Camera handsCamera = handsCameraObject.AddComponent<Camera>();
            handsCamera.CopyFrom(CameraUtils.getCamera(CameraUtils.VR_CAMERA));
            handsCamera.depth = 4;
            handsCamera.clearFlags = CameraClearFlags.Depth;
            handsCamera.cullingMask = LayerUtils.HANDS_LAYER_MASK;
            handsCamera.transform.parent =
                CameraUtils.getCamera(CameraUtils.VR_CAMERA).gameObject.transform;
            handsCamera.enabled = true;
            _handsCam = handsCamera;
        }

        // Search for the original skybox cam, if found, copy it, disable it,
        // and make new camera child of VR camera
        private void enableSkyboxCamera()
        {
            Camera originalSkyboxCamera = CameraUtils.getCamera(CameraUtils.SKYBOX_CAMERA);
            if (originalSkyboxCamera == null)
            {
                return;
            }
            GameObject vrSkyboxCamObj = new GameObject(CameraUtils.VRSKYBOX_CAMERA);
            Camera vrSkyboxCam = vrSkyboxCamObj.AddComponent<Camera>();
            vrSkyboxCam.CopyFrom(originalSkyboxCamera);
            vrSkyboxCam.depth = -2;
            vrSkyboxCam.transform.parent =
                 CameraUtils.getCamera(CameraUtils.VR_CAMERA).gameObject.transform;
            originalSkyboxCamera.enabled = false;
            vrSkyboxCam.enabled = true;
            _skyboxCam = vrSkyboxCam;
        }

        private void attachVrPlayerToWorldObject()
        {
            if (shouldAttachToPlayerCharacter())
            {
                updateZoomLevel();
                attachVrPlayerToPlayerCharacter();
            }
            else
            {
                attachVrPlayerToMainCamera();
            }
        }

        private void updateZoomLevel()
        {
            if (!canAdjustCameraDistance())
            {
                return;
            }
            float mouseScroll = Input.GetAxis("Mouse ScrollWheel");
            if (mouseScroll > 0f)
            {
                zoomCamera(true);
            } else if (mouseScroll < 0f)
            {
                zoomCamera(false);
            }
        }

        private void zoomCamera(bool zoomIn)
        {
            switch(_headZoomLevel)
            {
                case HeadZoomLevel.FirstPerson:
                    _headZoomLevel = zoomIn ? HeadZoomLevel.FirstPerson : HeadZoomLevel.ThirdPerson0;
                    break;
                case HeadZoomLevel.ThirdPerson0:
                    _headZoomLevel = zoomIn ? HeadZoomLevel.FirstPerson : HeadZoomLevel.ThirdPerson1;
                    break;
                case HeadZoomLevel.ThirdPerson1:
                    _headZoomLevel = zoomIn ? HeadZoomLevel.ThirdPerson0 : HeadZoomLevel.ThirdPerson2;
                    break;
                case HeadZoomLevel.ThirdPerson2:
                    _headZoomLevel = zoomIn ? HeadZoomLevel.ThirdPerson1 : HeadZoomLevel.ThirdPerson3;
                    break;
                case HeadZoomLevel.ThirdPerson3:
                    _headZoomLevel = zoomIn ? HeadZoomLevel.ThirdPerson2 : HeadZoomLevel.ThirdPerson3;
                    break;
            }
        }

        private static Vector3 getHeadOffset(HeadZoomLevel headZoomLevel)
        {
            switch (headZoomLevel)
            {
                case HeadZoomLevel.FirstPerson:
                    return FIRST_PERSON_OFFSET;
                case HeadZoomLevel.ThirdPerson0:
                    return THIRD_PERSON_0_OFFSET + THIRD_PERSON_CONFIG_OFFSET;
                case HeadZoomLevel.ThirdPerson1:
                    return THIRD_PERSON_1_OFFSET + THIRD_PERSON_CONFIG_OFFSET;
                case HeadZoomLevel.ThirdPerson2:
                    return THIRD_PERSON_2_OFFSET + THIRD_PERSON_CONFIG_OFFSET;
                case HeadZoomLevel.ThirdPerson3:
                    return THIRD_PERSON_3_OFFSET + THIRD_PERSON_CONFIG_OFFSET;
                default:
                    return FIRST_PERSON_OFFSET;
            }
        }

        // Some logic from GameCamera class
        private bool canAdjustCameraDistance()
        {
            return (!Chat.instance || !Chat.instance.HasFocus()) &&
                    !Console.IsVisible() &&
                    !InventoryGui.IsVisible() &&
                    !StoreGui.IsVisible() &&
                    !Menu.IsVisible() &&
                    !Minimap.IsOpen() &&
                    attachedToPlayer &&
                    !getPlayerCharacter().InCutscene() ? !getPlayerCharacter().InPlaceMode() : false;
        }

        private bool shouldAttachToPlayerCharacter()
        {
            return getPlayerCharacter() != null &&
                SceneManager.GetActiveScene().name != START_SCENE &&
                ensurePlayerInstance() &&
                !getPlayerCharacter().InCutscene() &&
                !getPlayerCharacter().IsDead() &&
                !getPlayerCharacter().InBed();
        }

        private void attachVrPlayerToPlayerCharacter()
        {
            Player playerCharacter = getPlayerCharacter();
            if (playerCharacter == null)
            {
                LogError("Player character is null. Cannot attach!");
                return;
            }
            if (!ensurePlayerInstance())
            {
                LogError("SteamVR Player instance is null. Cannot attach!");
                return;
            }
            _instance.transform.SetParent(playerCharacter.transform, false);
            attachedToPlayer = true;
            maybeInitHeadPosition(playerCharacter);
            float firstPersonAdjust = inFirstPerson ? FIRST_PERSON_HEIGHT_OFFSET : 0.0f;
            setHeadVisibility(!inFirstPerson);
            // Update the position with the first person adjustment calculated in init phase
            Vector3 desiredPosition = getDesiredPosition(playerCharacter);
            
            _instance.transform.localPosition = desiredPosition - playerCharacter.transform.position  // Base Positioning
                                               + Vector3.up * getHeadHeightAdjust(playerCharacter)
                                               + Vector3.up * firstPersonAdjust; // Offset from calibration on tracking recenter
                                               
            if(_headZoomLevel != HeadZoomLevel.FirstPerson)
            {
                _instance.transform.localPosition += getHeadOffset(_headZoomLevel) // Player controlled offset (zeroed on tracking reset)
                            + Vector3.forward * NECK_OFFSET; // Move slightly forward to position on neck
                setPlayerVisualsOffset(playerCharacter.transform, Vector3.zero);
            }
            else
                setPlayerVisualsOffset(playerCharacter.transform,
                                -getHeadOffset(_headZoomLevel) // Player controlled offset (zeroed on tracking reset)
                                -Vector3.forward * NECK_OFFSET // Move slightly forward to position on neck
                                );
        }

        //Moves all the effects and the meshes that compose the player, doesn't move the Rigidbody
        private void setPlayerVisualsOffset(Transform playerTransform, Vector3 offset)
        {
            for(int i = 0; i < playerTransform.childCount; i++)
            {
                Transform child = playerTransform.GetChild(i);
                if(child == _instance.transform || child.name == "EyePos") continue;
                playerTransform.GetChild(i).localPosition = offset;                          
            }
        }

        private float getHeadHeightAdjust(Player player)
        {
            if (player.IsSitting())
            {
                return SIT_HEIGHT_ADJUST;
            }
            if (player.IsCrouching() && Player_SetControls_SneakPatch.isJoystickSneaking)
            {
                return CROUCH_HEIGHT_ADJUST;
            }
            return 0f;
        }

        private void updateVrik()
        {
            var player = getPlayerCharacter();
            if (player == null)
            {
                return;
            }
            maybeAddVrik(player);
            var vrik = player.gameObject.GetComponent<VRIK>();
            if (vrik != null) {
                vrik.enabled = VHVRConfig.UseVrControls() &&
                    inFirstPerson &&
                    validVrikAnimatorState(player.GetComponentInChildren<Animator>());
            }
        }

        private bool validVrikAnimatorState(Animator animator)
        {
            if (animator == null)
            {
                return false;
            }
            return !animator.GetBool("wakeup");
        }

        private void maybeAddVrik(Player player)
        {
            if (!VHVRConfig.UseVrControls() || player.gameObject.GetComponent<VRIK>() != null)
            {
                return;
            }
            var cam = CameraUtils.getCamera(CameraUtils.VR_CAMERA);
            var vrik = VrikCreator.initialize(player.gameObject, 
                leftHand.transform, rightHand.transform, cam.transform);
            VrikCreator.resetVrikHandTransform(player);
            vrik.references.leftHand.gameObject.AddComponent<HandGesture>().sourceHand = leftHand;
            vrik.references.rightHand.gameObject.AddComponent<HandGesture>().sourceHand = rightHand;
            StaticObjects.leftFist().setColliderParent(vrik.references.leftHand, false);
            StaticObjects.rightFist().setColliderParent(vrik.references.rightHand, true);
            var vrPlayerSync = player.gameObject.AddComponent<VRPlayerSync>();
            vrPlayerSync.camera = cam.gameObject;
            vrPlayerSync.leftHand = leftHand.gameObject;
            vrPlayerSync.rightHand = rightHand.gameObject;
            StaticObjects.addQuickActions(leftHand.transform);
            StaticObjects.addQuickSwitch(rightHand.transform);

        }

        private bool vrikEnabled()
        {
            var player = getPlayerCharacter();
            if (player == null)
            {
                return false;
            }
            var vrik = player.gameObject.GetComponent<VRIK>();
            if (vrik != null && vrik != null)
            {
                return vrik.enabled;
            }
            return false;
        }

        private void maybeInitHeadPosition(Player playerCharacter)
        {
            if (!headPositionInitialized && inFirstPerson)
            {
                // First set the position without any adjustment
                Vector3 desiredPosition = getDesiredPosition(playerCharacter);
                _instance.transform.localPosition = desiredPosition - playerCharacter.transform.position;

                if(_headZoomLevel != HeadZoomLevel.FirstPerson)
                    _instance.transform.localPosition += getHeadOffset(_headZoomLevel);
                else
                    setPlayerVisualsOffset(playerCharacter.transform, -getHeadOffset(_headZoomLevel));

                var hmd = Valve.VR.InteractionSystem.Player.instance.hmdTransform;
                // Measure the distance between HMD and desires location, and save it.
                FIRST_PERSON_HEIGHT_OFFSET = desiredPosition.y - hmd.position.y;
                if (VHVRConfig.UseLookLocomotion())
                {
                    _instance.transform.localRotation = Quaternion.Euler(0f, -hmd.localRotation.eulerAngles.y, 0f);
                }
                headPositionInitialized = true;

                referencePlayerHeight = Valve.VR.InteractionSystem.Player.instance.eyeHeight;
                playerCharacter.GetComponent<CapsuleCollider>().radius = 0.2f;
            }
        }

        private static Vector3 getDesiredPosition(Player playerCharacter)
        {
            if (playerCharacter == null)
            {
                return Vector3.zero;
            }
            return new Vector3(playerCharacter.transform.position.x,
                    playerCharacter.GetEyePoint().y, playerCharacter.transform.position.z);
        }

        private void attachVrPlayerToMainCamera()
        {
            if (_instance == null)
            {
                LogError("SteamVR Player instance is null while attaching to main camera!");
                return;
            }
            Camera mainCamera = CameraUtils.getCamera(CameraUtils.MAIN_CAMERA);
            if (mainCamera == null)
            {
                LogError("Main camera not found.");
                return;
            }
            setHeadVisibility(true);
            // Orient the player with the main camera
            _instance.transform.parent = mainCamera.gameObject.transform;
            _instance.transform.position = mainCamera.gameObject.transform.position;
            _instance.transform.rotation = mainCamera.gameObject.transform.rotation;
            attachedToPlayer = false;
            headPositionInitialized = false;
        }
        
        // Used to turn off the head model when player is currently occupying it.
        private void setHeadVisibility(bool isVisible)
        {
            if (VHVRConfig.UseVrControls()) {
                return;
            }
            
            var headBone = getHeadBone();
            if (headBone != null)
            {
                headBone.localScale = isVisible ? new Vector3(1f, 1f, 1f) : new Vector3(0.001f, 0.001f, 0.001f);
            }
        }

        private Transform getHeadBone()
        {
            var playerCharacter = getPlayerCharacter();
            if (playerCharacter == null)
            {
                return null;
            }
            var animator = playerCharacter.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                return null;
            }
            return animator.GetBoneTransform(HumanBodyBones.Head);
        }

        private void maybeAddAmplifyOcclusion(Camera vrCamera)
        {
            if (vrCamera == null)
            {
                return;
            }
            AmplifyOcclusionEffect effect = vrCamera.gameObject.GetComponent<AmplifyOcclusionEffect>();
            if (effect != null)
            {
                return;
            }
            vrCamera.gameObject.AddComponent<AmplifyOcclusionEffect>();
        }

        // This will, if it hasn't already done so, try and find the PostProcessingBehaviour
        // script attached to the main camera and copy it over to the VR camera and then
        // add the DepthOfField & CameraEffects components. This enables all the post
        // processing effects to work. Bloom and anti-aliasing have strange artifacts
        // and don't work well. Depth of Field doesn't seem to actually generate depth - others
        // work okay and the world looks much nicer. SSAO is a significant boost in image
        // quality, but it comes at a heavy performance cost.
        private void maybeCopyPostProcessingEffects(Camera vrCamera, Camera mainCamera)
        {
            if (vrCamera == null || mainCamera == null)
            {
                return;
            }
            if (vrCamera.gameObject.GetComponent<PostProcessingBehaviour>() != null)
            {
                return;
            }
            PostProcessingBehaviour postProcessingBehavior = null;
            bool foundMainCameraPostProcesor = false;
            foreach (var ppb in GameObject.FindObjectsOfType<PostProcessingBehaviour>())
            {
                if (ppb.name == CameraUtils.MAIN_CAMERA)
                {
                    foundMainCameraPostProcesor = true;
                    postProcessingBehavior = vrCamera.gameObject.AddComponent<PostProcessingBehaviour>();
                    LogDebug("Copying Main Camera PostProcessingBehaviour");
                    var profileClone = Instantiate(ppb.profile);
                    //Need to copy only the profile and jitterFuncMatrix, everything else will be instanciated when enabled
                    postProcessingBehavior.profile = profileClone;
                    postProcessingBehavior.jitteredMatrixFunc = ppb.jitteredMatrixFunc;
                    if(ppb.enabled) ppb.enabled = false;
                }
            }
            if (!foundMainCameraPostProcesor)
            {
                return;
            }
            var mainCamDepthOfField = mainCamera.gameObject.GetComponent<DepthOfField>();
            var vrCamDepthOfField =  vrCamera.gameObject.AddComponent<DepthOfField>();
            if (mainCamDepthOfField != null)
            {
                CopyClassFields(mainCamDepthOfField, ref vrCamDepthOfField);
            }
            var vrCamSunshaft = vrCamera.gameObject.AddComponent<SunShafts>();
            var mainCamSunshaft = mainCamera.gameObject.GetComponent<SunShafts>();
            if (mainCamSunshaft != null)
            {
                CopyClassFields(mainCamSunshaft, ref vrCamSunshaft);
            }
            var vrCamEffects = vrCamera.gameObject.AddComponent<CameraEffects>();
            var mainCamEffects = mainCamera.gameObject.GetComponent<CameraEffects>();
            if (mainCamEffects != null)
            {
                // Need to copy over only the DOF fields
                vrCamEffects.m_forceDof = mainCamEffects.m_forceDof;
                vrCamEffects.m_dofRayMask = mainCamEffects.m_dofRayMask;
                vrCamEffects.m_dofAutoFocus = mainCamEffects.m_dofAutoFocus;
                vrCamEffects.m_dofMinDistance = mainCamEffects.m_dofMinDistance;
                vrCamEffects.m_dofMaxDistance = mainCamEffects.m_dofMaxDistance;
            }
        }

        private void CopyClassFields<T>(T source, ref T dest)
        {
            FieldInfo[] fieldsToCopy = source.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fieldsToCopy)
            {
                var value = field.GetValue(source);
                field.SetValue(dest, value);
            }
        }

        private Player getPlayerCharacter()
        {
            return Player.m_localPlayer;
        }

        private static void DisableRigidBodies(GameObject root)
        {
            foreach (var rb in root.GetComponentsInChildren<Rigidbody>())
            {
                rb.gameObject.SetActive(false);
            }
            foreach (var sc in root.GetComponentsInChildren<SphereCollider>())
            {
                sc.gameObject.SetActive(false);
            }
        }

        private void checkInteractions()
        {
            
            if (getPlayerCharacter() == null || !VRControls.mainControlsActive)
            {
                return;
            }

            checkHandOverShoulder(true, rightHand, SteamVR_Input_Sources.RightHand, ref toggleShowLeftHand);
            checkHandOverShoulder(false, leftHand, SteamVR_Input_Sources.LeftHand, ref toggleShowRightHand);
        }

        private void checkHandOverShoulder(bool isRightHand, Hand hand, SteamVR_Input_Sources inputSource, ref bool toggleShowHand)
        {
            var camera = CameraUtils.getCamera(CameraUtils.VR_CAMERA).transform;
            var action = SteamVR_Actions.valheim_Grab;
            if (camera.InverseTransformPoint(hand.transform.position).y > -0.4f &&
                camera.InverseTransformPoint(hand.transform.position).z < 0)
            {

                if (action.GetStateDown(inputSource))
                {
                    toggleShowHand = false;
                    hand.hapticAction.Execute(0, 0.2f, 100, 0.3f, inputSource);
                    if(isRightHand && isHoldingItem(isRightHand) && isUnpressSheath()) {

                    } else if (isRightHand && EquipScript.getLeft() == EquipType.Bow) {
                        BowLocalManager.instance.toggleArrow();
                    } else if (isHoldingItem(isRightHand)) {
                        getPlayerCharacter().HideHandItems();
                    } else {
                        getPlayerCharacter().ShowHandItems();
                        justUnsheathed = true;
                    }
                }
                else if (!justUnsheathed && isRightHand && action.GetStateUp(inputSource)) {
                    if (isHoldingItem(isRightHand) && isUnpressSheath()) {
                        getPlayerCharacter().HideHandItems();
                    }
                }
            }
            if (justUnsheathed && isRightHand && action.GetStateUp(inputSource)&& isUnpressSheath()) {
                justUnsheathed = false;
            }
        }
        
        private void CheckSneakRoomscale() {
            if (VHVRConfig.RoomScaleSneakEnabled()) {
                float height = Valve.VR.InteractionSystem.Player.instance.eyeHeight;
                float heightThreshold = referencePlayerHeight * VHVRConfig.RoomScaleSneakHeight();
                if (height < heightThreshold) {
                    _isRoomscaleSneaking = true;
                }
                else if (height > heightThreshold + heightThreshold * 0.05f)
                {
                    _isRoomscaleSneaking = false;
                }
            }
            else {
                _isRoomscaleSneaking = false;
            }
        }

        /// <summary>
        /// Moves the physics player to the head position and cancels the movement of the VRCamera by moving the VRRig
        /// </summary>
        void DoRoomScaleMovement()
        {
            var player = getPlayerCharacter();
            Vector3 deltaPosition = _vrCam.transform.localPosition - _lastCamPosition;
            deltaPosition.y = 0;
            bool shouldMove = deltaPosition.magnitude > 0.005f;
            if(shouldMove)
            {
                //Check for motion discrepancies
                if(VHVRConfig.RoomscaleFadeToBlack() && !_fadeManager.IsFadingToBlack)
                {
                    var lastDeltaMovement = player.m_body.position - _lastPlayerPosition;
                    lastDeltaMovement.y = 0;
                    if(roomscaleMovement.sqrMagnitude * 0.5 > lastDeltaMovement.sqrMagnitude)
                    {
                        SteamVR_Fade.Start(Color.black, 0);
                        SteamVR_Fade.Start(Color.clear, 1.5f); 
                    }
                    _lastPlayerPosition = player.m_body.position;
                }

                //Calculate new postion
                _lastCamPosition = _vrCam.transform.localPosition;
                var globalDeltaPosition = _instance.transform.TransformVector(deltaPosition);
                globalDeltaPosition.y = 0;
                roomscaleMovement = globalDeltaPosition;
                _vrCameraRig.localPosition -= deltaPosition;
            } else roomscaleMovement = Vector3.zero;

            //Set animation parameters
            _roomscaleAnimationForwardSpeed =  Mathf.SmoothDamp(_roomscaleAnimationForwardSpeed, shouldMove ? deltaPosition.z / Time.fixedDeltaTime : 0, ref _forwardSmoothVel, ROOMSCALE_STEP_ANIMATION_SMOOTHING, 99f);
            _roomscaleAnimationSideSpeed =  Mathf.SmoothDamp(_roomscaleAnimationSideSpeed, shouldMove ? deltaPosition.x / Time.fixedDeltaTime : 0, ref _sideSmoothVel, ROOMSCALE_STEP_ANIMATION_SMOOTHING, 99f);
        }

        public void ResetRoomscaleCamera()
        {
            if(_vrCameraRig != null)
            {
                Vector3 vrCamPosition = _vrCam.transform.localPosition;
                vrCamPosition.y = 0;
                _vrCameraRig.localPosition = -vrCamPosition;
            }
        }

        private bool isUnpressSheath() {
            return isBuildingTool() 
                || isHoldingThrowable();
        }
        private bool isHoldingThrowable() {
            return EquipScript.getRight() == EquipType.Spear 
                || EquipScript.getRight() == EquipType.SpearChitin 
                || EquipScript.getRight() == EquipType.Fishing;
        }
        private bool isBuildingTool() {
            return EquipScript.getRight() == EquipType.Hammer 
                || EquipScript.getRight() == EquipType.Hoe 
                || EquipScript.getRight() == EquipType.Cultivator;
        }
        private bool isHoldingItem(bool isRightHand) {
            return isRightHand && getPlayerCharacter().GetRightItem() != null
                   || !isRightHand && getPlayerCharacter().GetLeftItem() != null;
        }
    }
}
