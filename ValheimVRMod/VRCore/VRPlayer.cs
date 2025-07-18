using AmplifyOcclusion;
using RootMotion.FinalIK;
using System.Reflection;
using UnityEngine;
using UnityEngine.PostProcessing;
using UnityEngine.SceneManagement;
using UnityEngine.SpatialTracking;
using UnityStandardAssets.ImageEffects;
using ValheimVRMod.Patches;
using ValheimVRMod.Scripts;
using ValheimVRMod.Scripts.Block;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore.UI;
using Valve.VR;
using Valve.VR.Extras;
using Valve.VR.InteractionSystem;
using static ValheimVRMod.Utilities.LogUtils;
using Pose = ValheimVRMod.Utilities.Pose;

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
        private const float MAX_ROOMSCALE_MOVEMENT_SPEED = 4f; // Slightly faster than vanilla slow-run speed
        // This layer must be set in the hand model prefab in the
        // Unity AssetBundle project too. If they don't match,
        // the hands won't be rendered by the handsCam.
        private static Vector3 firstPersonOffset = Vector3.zero;
        private static float CROUCH_HEIGHT_ADJUST = -0.4f;
        private static float RIDE_HEIGHT_ADJUST = -0.85f;
        private static float SIT_ATTACH_HEIGHT_ADJUST = -0.6f;
        private static float SIT_HEIGHT_ADJUST = -0.7f;
        private static Vector3 THIRD_PERSON_0_OFFSET = new Vector3(0f, 1.0f, -0.6f);
        private static Vector3 THIRD_PERSON_1_OFFSET = new Vector3(0f, 1.4f, -1.5f);
        private static Vector3 THIRD_PERSON_2_OFFSET = new Vector3(0f, 1.9f, -2.6f);
        private static Vector3 THIRD_PERSON_3_OFFSET = new Vector3(0f, 3.2f, -4.4f);
        private static Vector3 THIRD_PERSON_CONFIG_OFFSET = Vector3.zero;
        private const float NECK_OFFSET = 0.25f;
        public const float ROOMSCALE_STEP_ANIMATION_SMOOTHING = 0.3f;
        public const float ROOMSCALE_ANIMATION_WEIGHT = 2f;

        public static VRIK vrikRef { get; private set; }
        private static SteamVR_TrackedObject hipTracker { get { return trackedObjects[hipTrackerIndex]; } }
        private static MeshRenderer hipTrackerRenderer;
        public static Transform pelvis { get; private set; }
        private Vector3 roomscaleLocomotive {
            get {
                return VHVRConfig.IsHipTrackingEnabled() && hipTracker != null && hipTracker.isValid ?
                    hipTracker.transform.localPosition : _vrCam.transform.localPosition;
            }
        }
        private Vector3 initialRoomscaleLocomotiveOffsetFromHead;

        public static Camera vrCam { get { return vrPlayerInstance != null ? vrPlayerInstance._vrCam : null; } }
        public static float referencePlayerHeight { get; private set; }
        public static bool startingSit { get; private set; }
        public static bool isRoomscaleSneaking { get { return _isRoomscaleSneaking; } }
        private static bool _isRoomscaleSneaking = false;

        private static GameObject _prefab;
        private static GameObject _instance;
        private static VRPlayer _vrPlayerInstance;
        private static HeadZoomLevel _headZoomLevel = HeadZoomLevel.FirstPerson;

        private Camera _vrCam;
        private Camera _handsCam;
        private Camera _skyboxCam;
        private Camera _thirdPersonCamera;

        //Roomscale movement variables
        private Transform _vrCameraRig;
        private Vector3 lastRoomscaleLocomotivePosition = Vector3.zero;
        private Vector3 _lastPlayerPosition = Vector3.zero;
        private Vector3 _lastPlayerAttachmentPosition = Vector3.zero;
        private FadeToBlackManager _fadeManager;
        private float _forwardSmoothVel = 0.0f, _sideSmoothVel = 0.0f;
        private static float _roomscaleAnimationForwardSpeed = 0.0f;
        private static float _roomscaleAnimationSideSpeed = 0.0f;
        public static float roomscaleAnimationForwardSpeed { get { return _roomscaleAnimationForwardSpeed; } }
        public static float roomscaleAnimationSideSpeed { get { return _roomscaleAnimationSideSpeed; } }
        public static Vector3 roomscaleMovement { get; private set; }
        public bool wasDodging { get; private set; } = false;
        public static GesturedLocomotionManager gesturedLocomotionManager { get; private set; } = null;

        private static SteamVR_LaserPointer _leftPointer;
        private static SteamVR_LaserPointer _rightPointer;
        private string _preferredHand;

        private Vector3 roomLocalPositionBeforeDodge;
        private Transform _dodgingRoom;
        private Transform dodgingRoom { get { return _dodgingRoom == null ? (_dodgingRoom = new GameObject().transform) : _dodgingRoom; } }
        private bool pausedMovement = false;

        private float timerLeft;
        private float timerRight;
        public static Hand leftHand { get; private set; }
        public static Hand rightHand { get; private set; }

        // Objects that are parented to the VR controllers but rotated and positioned like the character's hand bones.
        // They have the advantage of being in sync with the VR controller transform all the time without being
        // affect by vanilla game character animation while having a more intuitive orientation like the that of the
        // character's hand bones as opposed to that of the VR controllers.
        public static Transform leftHandBone { get; private set; }
        public static Transform rightHandBone { get; private set; }
        public static Hand dominantHand { get { return VHVRConfig.LeftHanded() ? leftHand : rightHand; } }
        public static bool ShouldPauseMovement { get { return PlayerCustomizaton.IsBarberGuiVisible() || (Menu.IsVisible() && !VHVRConfig.AllowMovementWhenInMenu()); } }
        public static bool IsClickableGuiOpen
        {
            get
            {
                return
                    Hud.IsPieceSelectionVisible() ||
                    StoreGui.IsVisible() ||
                    InventoryGui.IsVisible() ||
                    Menu.IsVisible() ||
                    (TextViewer.instance && TextViewer.instance.IsVisible()) ||
                    Minimap.IsOpen();
            }
        }

        public static PhysicsEstimator leftHandPhysicsEstimator
        {
            get
            {
                PhysicsEstimator value = leftHand.gameObject.GetComponent<PhysicsEstimator>();
                if (value == null && attachedToPlayer)
                {
                    value = leftHand.gameObject.AddComponent<PhysicsEstimator>();
                    value.UseVrHandControllerPhysics(leftHand);
                    value.refTransform = CameraUtils.getCamera(CameraUtils.VR_CAMERA)?.transform.parent;
                }
                return value;
            }
        }

        public static PhysicsEstimator rightHandPhysicsEstimator
        {
            get
            {
                PhysicsEstimator value = rightHand.gameObject.GetComponent<PhysicsEstimator>();
                if (value == null && attachedToPlayer)
                {
                    value = rightHand.gameObject.AddComponent<PhysicsEstimator>();
                    value.UseVrHandControllerPhysics(rightHand);
                    value.refTransform = CameraUtils.getCamera(CameraUtils.VR_CAMERA)?.transform.parent;
                }
                return value;
            }
        }

        public static PhysicsEstimator headPhysicsEstimator
        {
            get
            {
                var camera = CameraUtils.getCamera(CameraUtils.VR_CAMERA);
                if (camera == null) return null;
                PhysicsEstimator value = camera.GetComponent<PhysicsEstimator>();
                if (value == null && attachedToPlayer)
                {
                    value = camera.gameObject.AddComponent<PhysicsEstimator>();
                    value.refTransform = camera.transform.parent;
                }
                return value;
            }
        }

        public static PhysicsEstimator leftFootPhysicsEstimator { get; private set; }
        public static PhysicsEstimator rightFootPhysicsEstimator { get; private set; }

        public static float leftFootElevation
        {
            get { return leftFoot.parent == null || vrPlayerInstance == null ? 0 : vrPlayerInstance._vrCameraRig.InverseTransformPoint(leftFoot.position).y - baseFootHeight; }
        }
        public static float rightFootElevation
        {
            get { return rightFoot.parent == null || vrPlayerInstance == null ? 0 : vrPlayerInstance._vrCameraRig.InverseTransformPoint(rightFoot.position).y - baseFootHeight; }
        }

        private static float baseFootHeight;
        

        public static SteamVR_Input_Sources dominantHandInputSource { get { return VHVRConfig.LeftHanded() ? SteamVR_Input_Sources.LeftHand : SteamVR_Input_Sources.RightHand; } }
        public static SteamVR_Input_Sources nonDominantHandInputSource { get { return VHVRConfig.LeftHanded() ? SteamVR_Input_Sources.RightHand : SteamVR_Input_Sources.LeftHand; } }

        public static bool handsActive
        {
            get
            {
                return handIsActive(leftHand, _leftPointer) && handIsActive(rightHand, _rightPointer);
            }
        }

        public static SteamVR_LaserPointer leftPointer { get { return _leftPointer; } }
        public static SteamVR_LaserPointer rightPointer { get { return _rightPointer; } }

        public static Vector3 dominantHandRayDirection { get
            {
                var pointer =
                 VHVRConfig.LeftHanded() ? VRPlayer.leftPointer : VRPlayer.rightPointer;
                return (pointer.rayDirection * Vector3.forward).normalized;
            }
        }

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

        public static bool inFirstPerson
        {
            get
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
                    if (Player.m_localPlayer.m_lastGroundBody)
                        relativeVelocity -= Player.m_localPlayer.m_lastGroundBody.velocity;
                    return relativeVelocity.magnitude > 0.5f;
                }
                return false;
            }
        }

        public static bool inImmersiveDodge
        {
            get
            {
                if (!VHVRConfig.ImmersiveDodgeRoll() || Player.m_localPlayer == null || VRPlayer.vrPlayerInstance == null)
                {
                    return false;
                }
                return Player.m_localPlayer.InDodge() || VRPlayer.vrPlayerInstance.wasDodging;
            }
        }

        public static GameObject instance { get { return _instance; } }
        public static VRPlayer vrPlayerInstance => _vrPlayerInstance;
        public static bool attachedToPlayer = false;

        private static float? firstPersonHeightOffset = null;
        public static bool headPositionInitialized { get; private set; }
        private static bool bodyTrackingCaliberationPending;
        private static Vector3 caliberatedPelvisLocalPosition = Vector3.zero;
        private static Quaternion caliberatedPelvisLocalRotation = Quaternion.identity;
        private static SteamVR_TrackedObject[] trackedObjects = new SteamVR_TrackedObject[32];
        private static int hipTrackerIndex = 0;

        public static Transform leftFoot { get; private set; }
        public static Transform rightFoot { get; private set; }

        public static void RequestRecentering()
        {
            headPositionInitialized = false;
            firstPersonOffset = Vector3.zero;
            firstPersonHeightOffset = null;
        }

        public static void RequestPelvisCaliberation()
        {
            bodyTrackingCaliberationPending = true;
        }

        public static void DestroyVrik()
        {
            if (vrikRef == null)
            {
                return;
            }

            Destroy(vrikRef);
            vrikRef = null;

            LogUtils.LogDebug("Destroyed local player stale VRIK");
        }

        void Awake()
        {
            _vrPlayerInstance = this;
            _prefab = VRAssetManager.GetAsset<GameObject>(PLAYER_PREFAB_NAME);
            _preferredHand = VHVRConfig.GetPreferredHand();
            headPositionInitialized = false;
            firstPersonOffset = Vector3.zero;
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
            if (ensureBodyTrackers())
            {
                hipTrackerRenderer.enabled =
                    VHVRConfig.IsHipTrackingEnabled() &&
                    (Menu.IsVisible() || (FejdStartup.m_instance != null && FejdStartup.m_instance.isActiveAndEnabled));
                if (hipTrackerRenderer.transform.parent = hipTracker.transform)
                {
                    hipTrackerRenderer.transform.SetParent(hipTracker.transform, worldPositionStays: false);
                    hipTrackerRenderer.transform.localPosition = Vector3.zero;
                    hipTrackerRenderer.transform.localRotation = Quaternion.identity;
                    hipTrackerRenderer.transform.localScale = Vector3.one * 0.125f;
                }
            }
            maybeUpdateHeadPosition();
            attachVrPlayerToWorldObject();
            enableCameras();
            checkAndSetHandsAndPointers();
            updateBodyTracking();
            updateVrik();
            // When dodge starts, we need to make sure that updateVrik() has been called first so that the head is no longer controlled by Vrik before doing any dodge-related camera rotation.
            maybeMoveVRPlayerDuringDodge();
            UpdateAmplifyOcclusionStatus();
            Pose.checkInteractions();
            CheckSitRoomscale();
            CheckSneakRoomscale();

            // Vanilla game does not support attack when riding, so force initiate ranged attack here.
            MountedAttackUtils.CheckMountedMagicAndCrossbowAttack();

            if (timerLeft > 0)
            {
                timerLeft -= Time.deltaTime;
                leftHand.hapticAction.Execute(0f, 0.1f, 20f, 0.1f, SteamVR_Input_Sources.LeftHand);
            }
            if (timerRight > 0)
            {
                timerRight -= Time.deltaTime;
                rightHand.hapticAction.Execute(0f, 0.1f, 20f, 0.1f, SteamVR_Input_Sources.RightHand);
            }
        }

        void OnDestroy()
        {
            if (_dodgingRoom != null)
            {
                Destroy(_dodgingRoom.gameObject);
            }
        }

        private void FixedUpdate()
        {
            if (ShouldPauseMovement)
            {
                if (vrikEnabled() && !pausedMovement)
                {
                    VrikCreator.PauseLocalPlayerVrik();
                    pausedMovement = true;
                }
            }
            else
            {
                if (vrikEnabled() && pausedMovement)
                {
                    // Before unpausing, move the camera back to the position before the pause to prevent teleporting the player to the cuurent camera position.
                    _vrCameraRig.localPosition -= Vector3.ProjectOnPlane(roomscaleLocomotive - lastRoomscaleLocomotivePosition, Vector3.up);
                    lastRoomscaleLocomotivePosition = roomscaleLocomotive;
                    VrikCreator.UnpauseLocalPlayerVrik();
                    pausedMovement = false;
                }
                if (inFirstPerson)
                {
                    if (VHVRConfig.CharaterMovesWithHeadset())
                    {
                        DoRoomScaleMovement(Time.fixedDeltaTime);
                    }
                    gesturedLocomotionManager?.UpdateMovementFromGestures(Time.fixedDeltaTime);
                }
                else
                {
                    roomscaleMovement = Vector3.zero;
                }
            }

            UpdateThirdPersonCamera();
        }

        public static void StartSit()
        {
            startingSit = true;
            _isRoomscaleSneaking = false;
        }

        private void UpdateThirdPersonCamera()
        {
            if (_thirdPersonCamera == null)
            {
                if (!VHVRConfig.UseThirdPersonCameraOnFlatscreen())
                {
                    return;
                }
                enableThirdPersonCamera();

                if (_thirdPersonCamera == null)
                {
                    return;
                }
            }

            _thirdPersonCamera.gameObject.SetActive(VHVRConfig.UseThirdPersonCameraOnFlatscreen());
        }

        // Fixes an issue on Pimax HMDs that causes rotation to be incorrect:
        // See: https://www.reddit.com/r/Pimax/comments/qhkrfp/pimax_unity_xr_plugin_issue/
        private static void UpdateTrackedPoseDriverPoseSource()
        {
            var hmd = Valve.VR.InteractionSystem.Player.instance.hmdTransform;
            var trackedPoseDriver = hmd.gameObject.GetComponent<TrackedPoseDriver>();
            if (trackedPoseDriver == null)
            {
                LogWarning("Null TrackedPoseDriver on HMD transform.");
            }
            else
            {
                LogInfo("Setting TrackedPoseDriver.poseSource to Head.");
                trackedPoseDriver.SetPoseSource(trackedPoseDriver.deviceType, TrackedPoseDriver.TrackedPose.Head);
            }
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
                firstPersonOffset += offset;
            }
            else
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
            if (leftHand != null)
            {
                leftHand.enabled = VHVRConfig.UseVrControls();
                leftHand.SetVisibility(leftHand.enabled && !vrikEnabled());
            }
            if (rightHand != null)
            {
                rightHand.enabled = VHVRConfig.UseVrControls();
                rightHand.SetVisibility(rightHand.enabled && !vrikEnabled());
            }
            // Next check whether the hands are active, and enable the appropriate pointer based
            // on what is available and what the options set as preferred. Disable the inactive pointer(s).
            if (handIsActive(leftHand, _leftPointer) && handIsActive(rightHand, _rightPointer))
            {
                // Both hands active, so choose preferred hand
                if (_preferredHand == LEFT_HAND)
                {
                    setPointerActive(_leftPointer, true);
                    setPointerActive(_rightPointer, false);
                }
                else
                {
                    setPointerActive(_rightPointer, true);
                    setPointerActive(_leftPointer, false);
                }
            }
            else if (handIsActive(rightHand, _rightPointer))
            {
                setPointerActive(_rightPointer, true);
                setPointerActive(_leftPointer, false);
            }
            else if (handIsActive(leftHand, _leftPointer))
            {
                setPointerActive(_leftPointer, true);
                setPointerActive(_rightPointer, false);
            }
            else
            {
                setPointerActive(_leftPointer, false);
                setPointerActive(_rightPointer, false);
            }
        }

        private void tryInitializeHands()
        {
            // First try and initialize both hands and pointer scripts
            if (leftHand == null || _leftPointer == null)
            {
                leftHand = getHand(LEFT_HAND, _instance);
                if (leftHand != null)
                {
                    (leftHandBone = new GameObject().transform).parent = leftHand.transform;
                    leftHandBone.localPosition = VrikCreator.leftUnequippedPosition;
                    leftHandBone.localRotation = VrikCreator.leftUnequippedRotation;
                    _leftPointer = leftHand.GetComponent<SteamVR_LaserPointer>();
                    if (_leftPointer != null)
                    {
                        _leftPointer.raycastLayerMask = LayerUtils.UI_PANEL_LAYER_MASK;
                    }
                }
            }
            if (rightHand == null || _rightPointer == null)
            {
                rightHand = getHand(RIGHT_HAND, _instance);
                if (rightHand != null)
                {
                    (rightHandBone = new GameObject().transform).parent = rightHand.transform;
                    rightHandBone.localPosition = VrikCreator.rightUnequippedPosition;
                    rightHandBone.localRotation = VrikCreator.rightUnequippedRotation;
                    _rightPointer = rightHand.GetComponent<SteamVR_LaserPointer>();
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
                    UpdateTrackedPoseDriverPoseSource();
                }
            }
            return _instance != null;
        }

        private bool ensureBodyTrackers()
        {
            if (trackedObjects[0] != null)
            {
                return true;
            }

            if (_vrCam == null || _vrCam.transform.parent == null)
            {
                return false;
            }

            for (int i = 0; i < trackedObjects.Length; i++)
            {
                if (trackedObjects[i] == null)
                {
                    trackedObjects[i] = new GameObject().AddComponent<Valve.VR.SteamVR_TrackedObject>();
                }
                trackedObjects[i].SetDeviceIndex(i);
                trackedObjects[i].transform.parent = _vrCameraRig;
            }

            pelvis = new GameObject().transform;
            pelvis.parent = hipTracker.transform;

            hipTrackerRenderer = GameObject.CreatePrimitive(PrimitiveType.Cube).GetComponent<MeshRenderer>();
            hipTrackerRenderer.transform.SetParent(hipTracker.transform, false);
            hipTrackerRenderer.gameObject.layer = LayerUtils.getWorldspaceUiLayer();
            hipTrackerRenderer.transform.localScale = new Vector3(0.125f, 0.125f, 0.125f);
            hipTrackerRenderer.material = Instantiate(VRAssetManager.GetAsset<Material>("Unlit"));
            hipTrackerRenderer.material.color = new Vector4(0.5f, 0.5f, 0, 0.5f);
            hipTrackerRenderer.receiveShadows = false;

            RequestPelvisCaliberation();

            return true;
        }

        private void enableCameras()
        {
            if (_vrCam == null || !_vrCam.enabled)
            {
                if (_thirdPersonCamera != null)
                {
                    Destroy(_thirdPersonCamera);
                }
                enableVrCamera();
            }
            else
            {
                _vrCam.nearClipPlane = VHVRConfig.GetNearClipPlane();
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
            vrCam.cullingMask &= ~(1 << LayerUtils.CHARARCTER_TRIGGER);
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
            gesturedLocomotionManager = new GesturedLocomotionManager();

            _fadeManager.OnFadeToWorld += () => {
                //Recenter
                VRPlayer.headPositionInitialized = false;
                firstPersonOffset = Vector3.zero;
                VRPlayer.vrPlayerInstance?.ResetRoomscaleCamera();
            };
        }

        // Create a camera and assign its culling mask
        // to the unused layer. Assign depth to be higher
        // than the UI panel depth to ensure they are drawn
        // on top of the GUI.
        private void enableHandsCamera()
        {
            Camera vrCam = CameraUtils.getCamera(CameraUtils.VR_CAMERA);
            if (vrCam == null || vrCam.gameObject == null)
            {
                return;
            }
            LogDebug("Enabling Hands Camera");
            GameObject handsCameraObject = new GameObject(CameraUtils.HANDS_CAMERA);
            Camera handsCamera = handsCameraObject.AddComponent<Camera>();
            handsCamera.CopyFrom(CameraUtils.getCamera(CameraUtils.VR_CAMERA));
            handsCamera.depth = 4;
            handsCamera.clearFlags = CameraClearFlags.Depth;
            handsCamera.cullingMask = LayerUtils.HANDS_LAYER_MASK;
            handsCamera.transform.SetParent(vrCam.transform);
            handsCamera.enabled = true;
            _handsCam = handsCamera;
        }

        private void enableThirdPersonCamera()
        {
            Camera vrCam = CameraUtils.getCamera(CameraUtils.VR_CAMERA);
            if (vrCam == null || vrCam.gameObject == null)
            {
                return;
            }

            if (PlayerOnDeathPatch.hasCharacterDied && !attachedToPlayer)
            {
                // Do not enable follow camera until the VRCamera is attached to the player after character death,
                // otherwise the projection matrix of the VRCamera might become wrong.
                return;
            }

            LogDebug("Enabling third person camera");
            _thirdPersonCamera = new GameObject(CameraUtils.FOLLOW_CAMERA).AddComponent<Camera>();
            _thirdPersonCamera.CopyFrom(vrCam);
            _thirdPersonCamera.depth = 4;
            // Borrow the character trigger layer to render headgears which should be hidden for the VR camera.
            _thirdPersonCamera.cullingMask |= (1 << LayerUtils.CHARARCTER_TRIGGER);
            _thirdPersonCamera.cullingMask |= (1 << LayerUtils.getUiPanelLayer());
            _thirdPersonCamera.cullingMask |= (1 << LayerUtils.getWorldspaceUiLayer());
            _thirdPersonCamera.transform.position = vrCam.transform.position;
            _thirdPersonCamera.stereoTargetEye = StereoTargetEyeMask.None;
            _thirdPersonCamera.gameObject.AddComponent<ThirdPersonCameraUpdater>();
            _thirdPersonCamera.enabled = true;
            _thirdPersonCamera.ResetAspect();
            _thirdPersonCamera.fieldOfView = 75;
        }

        // Search for the original skybox cam, if found, copy it, disable it,
        // and make new camera child of VR camera
        private void enableSkyboxCamera()
        {
            Camera originalSkyboxCamera = CameraUtils.getCamera(CameraUtils.SKYBOX_CAMERA);
            if (originalSkyboxCamera == null || originalSkyboxCamera.gameObject == null)
            {
                return;
            }
            Camera vrCam = CameraUtils.getCamera(CameraUtils.VR_CAMERA);
            if (vrCam == null || vrCam.gameObject == null)
            {
                return;
            }
            GameObject vrSkyboxCamObj = new GameObject(CameraUtils.VRSKYBOX_CAMERA);
            Camera vrSkyboxCam = vrSkyboxCamObj.AddComponent<Camera>();
            vrSkyboxCam.CopyFrom(originalSkyboxCamera);
            vrSkyboxCam.depth = -2;
            vrSkyboxCam.transform.SetParent(vrCam.transform);
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
            }
            else if (mouseScroll < 0f)
            {
                zoomCamera(false);
            }
        }

        private void zoomCamera(bool zoomIn)
        {
            switch (_headZoomLevel)
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
                    return firstPersonOffset;
                case HeadZoomLevel.ThirdPerson0:
                    return THIRD_PERSON_0_OFFSET + THIRD_PERSON_CONFIG_OFFSET;
                case HeadZoomLevel.ThirdPerson1:
                    return THIRD_PERSON_1_OFFSET + THIRD_PERSON_CONFIG_OFFSET;
                case HeadZoomLevel.ThirdPerson2:
                    return THIRD_PERSON_2_OFFSET + THIRD_PERSON_CONFIG_OFFSET;
                case HeadZoomLevel.ThirdPerson3:
                    return THIRD_PERSON_3_OFFSET + THIRD_PERSON_CONFIG_OFFSET;
                default:
                    return firstPersonOffset;
            }
        }

        // Some logic from GameCamera class
        private bool canAdjustCameraDistance()
        {
            return !IsClickableGuiOpen &&
                (!Chat.instance || !Chat.instance.HasFocus()) &&
                !Console.IsVisible() &&
                attachedToPlayer &&
                !getPlayerCharacter().InCutscene() &&
                !getPlayerCharacter().InPlaceMode();
        }

        private bool shouldAttachToPlayerCharacter()
        {
            return getPlayerCharacter() != null &&
                SceneManager.GetActiveScene().name != START_SCENE &&
                ensurePlayerInstance() &&
                !getPlayerCharacter().InCutscene() &&
                !getPlayerCharacter().IsDead() &&
                !getPlayerCharacter().InBed() &&
                !PlayerCustomizaton.IsBarberGuiVisible();
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
            _instance.transform.localScale = Vector3.one / VrikCreator.ROOT_SCALE;
            attachedToPlayer = true;

            maybeExitDodge();

            maybeInitHeadPosition(playerCharacter);
            if (bodyTrackingCaliberationPending)
            {
                caliberateBodyTracking();
                if (!bodyTrackingCaliberationPending)
                {
                    initialRoomscaleLocomotiveOffsetFromHead = roomscaleLocomotive - vrCam.transform.localPosition;
                }
            }
            else
            {
                initialRoomscaleLocomotiveOffsetFromHead = Vector3.zero;
            }
            float firstPersonAdjust = inFirstPerson ? (float) firstPersonHeightOffset : 0.0f;
            setHeadVisibility(!inFirstPerson);
            // Update the position with the first person adjustment calculated in init phase
            _instance.transform.localPosition = getDesiredLocalPosition(playerCharacter) // Base Positioning
                + (firstPersonAdjust // Offset from calibration on tracking recenter
                + getHeadHeightAdjust(playerCharacter)) * Vector3.up;

            if (_headZoomLevel != HeadZoomLevel.FirstPerson)
            {
                _instance.transform.localPosition += getHeadOffset(_headZoomLevel) // Player controlled offset (zeroed on tracking reset)
                            + Vector3.forward * NECK_OFFSET; // Move slightly forward to position on neck
                setPlayerVisualsOffset(playerCharacter.transform, Vector3.zero);
            }
            else
            {
                var offset = -getHeadOffset(_headZoomLevel); // Player controlled offset (zeroed on tracking reset)
                if (playerCharacter.IsSitting())
                {
                    if (playerCharacter.IsAttached())
                    {
                        _instance.transform.localPosition += Vector3.forward * 0.33f;
                    }
                    offset += Vector3.forward * 0.0625f; // Move slightly backward to position on neck;
                }
                else if (!VHVRConfig.TrackFeet())
                {
                    offset -= Vector3.forward * NECK_OFFSET; // Move slightly forward to position on neck
                }
                setPlayerVisualsOffset(playerCharacter.transform, offset);
            }
        }

        //Moves all the effects and the meshes that compose the player, doesn't move the Rigidbody
        private void setPlayerVisualsOffset(Transform playerTransform, Vector3 offset)
        {
            for (int i = 0; i < playerTransform.childCount; i++)
            {
                Transform child = playerTransform.GetChild(i);
                if (child == _instance.transform || child.name == "EyePos") continue;
                playerTransform.GetChild(i).localPosition = offset;
            }
        }

        private float getHeadHeightAdjust(Player player)
        {
            if (player.IsRiding() || MountedAttackUtils.IsRidingMount())
            {
                // Attack animation may cause vanilla game to think that the player is standing
                // briefly while riding but we should consider the player as sitting so that the
                // view point does not shift suddenly when attacking.
                return RIDE_HEIGHT_ADJUST;
            }

            if (MountedAttackUtils.IsSteering())
            {
                // Attack animation may cause vanilla game to think that the player is standing
                // briefly while riding but we should consider the player as sitting so that the
                // view point does not shift suddenly when attacking.
                return SIT_ATTACH_HEIGHT_ADJUST;
            }

            if (player.IsSitting())
            {
                if (player.IsAttached())
                {
                    var chair = player.m_attachPoint?.GetComponentInParent<Chair>();
                    var isRidingAsksvinSkeleton = chair != null && chair.m_name == "$piece_asksvinskeleton";
                    return isRidingAsksvinSkeleton ? -1.1f : SIT_ATTACH_HEIGHT_ADJUST;
                }
                if (!VHVRConfig.CharaterMovesWithHeadset())
                {
                    return SIT_HEIGHT_ADJUST;
                }
            }

            if (player.IsCrouching() && Player_SetControls_SneakPatch.isJoystickSneaking)
            {
                return CROUCH_HEIGHT_ADJUST;
            }

            return VHVRConfig.PlayerHeightAdjust();
        }

        private void updateBodyTracking()
        {
            var player = Player.m_localPlayer;
            if (!ensureBodyTrackers())
            {
                return;
            }

            var pelvisTarget = vrikRef?.solver?.spine?.pelvisTarget;
            if (player == null || vrikRef == null || pelvisTarget == null || !headPositionInitialized || _vrCam == null || !VHVRConfig.UseVrControls() || pausedMovement)
            {
                return;
            }

            vrikRef.solver.spine.maintainPelvisPosition = attachedToPlayer ? 0 : 1;
            vrikRef.solver.spine.pelvisRotationWeight = attachedToPlayer ? 1 : 0;

            if (!VHVRConfig.IsHipTrackingEnabled())
            {
                vrikRef.solver.leftLeg.positionWeight = vrikRef.solver.rightLeg.positionWeight = 0;
                vrikRef.solver.leftLeg.rotationWeight = vrikRef.solver.rightLeg.rotationWeight = 0;
                StaticObjects.leftFootCollision().gameObject.SetActive(false);
                StaticObjects.rightFootCollision().gameObject.SetActive(false);

                if (player.IsAttached() || player.IsSitting() || Valve.VR.InteractionSystem.Player.instance.eyeHeight > referencePlayerHeight * 0.5f)
                {
                    vrikRef.solver.spine.pelvisPositionWeight = 0;
                    vrikRef.solver.spine.pelvisRotationWeight = 1;
                    pelvis.position = vrikRef.references.pelvis.position;
                    Vector3 pelvisFacing = inferPelvisFacingFromPlayerHeadingAndHands(player.transform, player.IsAttached());
                    pelvis.rotation = Quaternion.LookRotation(pelvisFacing, player.transform.up);
                    vrikRef.solver.spine.rootHeadingOffset = Vector3.SignedAngle(player.transform.forward, pelvisFacing, player.transform.up);
                }
                else
                {
                    vrikRef.solver.spine.pelvisPositionWeight = 1;
                    vrikRef.solver.spine.pelvisRotationWeight = 0;
                    pelvis.position = inferPelvisPositionFromHead(_vrCam.transform.up);
                    pelvis.rotation = vrikRef.references.pelvis.rotation;
                    vrikRef.solver.spine.rootHeadingOffset = 0;
                }

                return;
            }

            bool isFreeStanding =
                !player.IsAttached() &&
                !player.IsSitting() &&
                Vector3.Angle(pelvis.parent.rotation * caliberatedPelvisLocalRotation * Vector3.up, player.transform.up) < 30 &&
                Valve.VR.InteractionSystem.Player.instance.eyeHeight > referencePlayerHeight * 0.75f;

            if (player.IsAttached() ||
                (!VHVRConfig.TrackFeet() && (player.IsSneaking() || player.IsSitting() || isFreeStanding)))
            {
                vrikRef.solver.spine.pelvisPositionWeight = 0;
                pelvis.position = vrikRef.references.pelvis.position;
            }
            else
            {
                vrikRef.solver.spine.pelvisPositionWeight = attachedToPlayer ? 1 : 0;
                pelvis.localPosition = caliberatedPelvisLocalPosition;
            }

            if (player.IsAttached())
            {
                pelvis.rotation =
                    Quaternion.Lerp(player.transform.rotation, pelvis.parent.rotation * caliberatedPelvisLocalRotation, 0.25f);
                vrikRef.solver.spine.rootHeadingOffset = 0;
            }
            else
            {
                Vector3 caliberatedPelvisForward = pelvis.parent.rotation * caliberatedPelvisLocalRotation * Vector3.forward;
                Vector3 pelvisFacing = Vector3.ProjectOnPlane(caliberatedPelvisForward, player.transform.up);
                if (isFreeStanding && !VHVRConfig.TrackFeet())
                {
                    pelvis.rotation = Quaternion.LookRotation(pelvisFacing, player.transform.up);
                }
                else
                {
                    pelvis.localRotation = caliberatedPelvisLocalRotation;
                }
                //TODO: find out why this is not working
                vrikRef.solver.spine.rootHeadingOffset = Vector3.SignedAngle(Vector3.ProjectOnPlane(_vrCam.transform.forward, player.transform.up), pelvisFacing, player.transform.up);
            }

            if (shouldTrackFeet())
            {
                vrikRef.solver.leftLeg.rotationWeight = vrikRef.solver.rightLeg.rotationWeight = 1;
                vrikRef.solver.leftLeg.positionWeight = vrikRef.solver.rightLeg.positionWeight = 1;
                StaticObjects.leftFootCollision().gameObject.SetActive(true);
                StaticObjects.rightFootCollision().gameObject.SetActive(true);
            }
            else
            {
                vrikRef.solver.leftLeg.rotationWeight = vrikRef.solver.rightLeg.rotationWeight = 0;
                vrikRef.solver.leftLeg.positionWeight = vrikRef.solver.rightLeg.positionWeight = 0;
                StaticObjects.leftFootCollision().gameObject.SetActive(false);
                StaticObjects.rightFootCollision().gameObject.SetActive(false);
            }
        }

        private bool shouldTrackFeet()
        {
            if (!VHVRConfig.TrackFeet() || !attachedToPlayer || Player.m_localPlayer == null || Player.m_localPlayer.IsAttached())
            {
                return false;
            }

            if (GesturedLocomotionManager.isUsingFootTracking)
            {
                return true;
            }

            float standingHeadHeight = _vrCam.transform.localPosition.y - Valve.VR.InteractionSystem.Player.instance.eyeHeight + referencePlayerHeight;
            if (_vrCameraRig.InverseTransformPoint(leftFoot.position).y > standingHeadHeight - 1.5f - VHVRConfig.PlayerHeightAdjust() ||
                _vrCameraRig.InverseTransformPoint(rightFoot.position).y > standingHeadHeight - 1.5f - VHVRConfig.PlayerHeightAdjust() ||
                Vector3.ProjectOnPlane(leftFoot.position - rightFoot.position, _vrCameraRig.up).magnitude > 0.5f)
            {
                return true;
            }

            if (VRControls.smoothWalkX > 0.1f || VRControls.smoothWalkY > 0.1f ||
                VRControls.smoothWalkX < -0.1f || VRControls.smoothWalkY < -0.1f ||
                gesturedLocomotionManager.stickOutputX > 0.1f || gesturedLocomotionManager.stickOutputY > 0.1f ||
                gesturedLocomotionManager.stickOutputX < -0.1f || gesturedLocomotionManager.stickOutputY < -0.1f)
            {
                return false;
            }

            return Player.m_localPlayer.IsOnGround();
        }

        private Vector3 inferPelvisPositionFromHead(Vector3? upDirection = null)
        {
            if (upDirection == null)
            {
                upDirection = _vrCameraRig.up;
            }
            return _vrCam.transform.position - _vrCam.transform.forward * 0.1f - upDirection.Value * 0.89f * VrikCreator.ROOT_SCALE;
        }

        private Vector3 inferPelvisFacingFromPlayerHeadingAndHands(Transform playerTransform, bool isPlayerAttached)
        {
            Vector3 forward = isPlayerAttached ? Player.m_localPlayer.transform.forward : Vector3.ProjectOnPlane(_vrCam.transform.forward, _vrCameraRig.up);
            if (GesturedLocomotionManager.isInUse && Mathf.Abs(gesturedLocomotionManager.stickOutputY) > 0.25f)
            {
                return forward;
            }
            Vector3 up = isPlayerAttached ? playerTransform.up : _vrCameraRig.up;
           
            Vector3 elbowSpan = rightHandBone.TransformPoint(-Vector3.up * 0.25f) - leftHandBone.TransformPoint(-Vector3.up * 0.25f);
            Vector3 adjustment = Vector3.Cross(Vector3.ProjectOnPlane(elbowSpan, up), up);

            // Rotate pelvis slightly according to forearm positions
            return isPlayerAttached ? forward + adjustment * 0.5f : forward + adjustment;
        }


        private void updateVrik()
        {
            var player = getPlayerCharacter();
            if (player == null)
            {
                return;
            }
            maybeAddVrik(player);
            if (vrikRef == null)
            {
                return;
            }

            vrikRef.enabled =
                VHVRConfig.UseVrControls() &&
                inFirstPerson &&
                !player.InDodge() &&
                !player.IsStaggering() &&
                !player.IsSleeping() &&
                validVrikAnimatorState(player.GetComponentInChildren<Animator>());

            LeftHandQuickMenu.instance.UpdateWristBar();
            RightHandQuickMenu.instance.UpdateWristBar();
        }

        private void maybeExitDodge()
        {
            var player = getPlayerCharacter();
            if (player == null || player.InDodge() || !wasDodging)
            {
                return;
            }

            if (attachedToPlayer && VHVRConfig.ImmersiveDodgeRoll())
            {
                var cameraLocalHeading = _vrCam.transform.localRotation.eulerAngles.y;
                var desiredFacing =
                    _instance.transform.rotation * Quaternion.Euler(0, cameraLocalHeading, 0) * Vector3.forward;
                _vrCameraRig.localPosition = roomLocalPositionBeforeDodge;
                _vrCameraRig.localRotation = Quaternion.identity;
                player.m_lookDir = desiredFacing;
                player.FaceLookDirection();
                _instance.transform.localRotation = Quaternion.Euler(0, -cameraLocalHeading, 0);
                _vrCam.nearClipPlane = VHVRConfig.GetNearClipPlane();
                _vrCameraRig.position += 
                    Vector3.ProjectOnPlane(
                        player.transform.position - _vrCameraRig.TransformPoint(roomscaleLocomotive - initialRoomscaleLocomotiveOffsetFromHead),
                        _vrCameraRig.up);
                lastRoomscaleLocomotivePosition = roomscaleLocomotive;
                roomscaleMovement = Vector3.zero;
            }
            wasDodging = false;
        }

        private void maybeMoveVRPlayerDuringDodge()
        {
            if (getPlayerCharacter() == null || !getPlayerCharacter().InDodge())
            {
                return;
            }

            if (!wasDodging)
            {
                roomLocalPositionBeforeDodge = _vrCameraRig.localPosition;
                dodgingRoom.parent = getHeadBone();
                dodgingRoom.SetPositionAndRotation(_vrCameraRig.position, _vrCameraRig.rotation);
                _vrCam.nearClipPlane = 0.3f;
                wasDodging = true;
            }
            else if (attachedToPlayer && VHVRConfig.ImmersiveDodgeRoll())
            {
                float smoothener = GetDodgeExitSmoothener();

                // Head bone and Player#m_head has different scales than the player, therefore directly parenting the camera to them should be avoided lest it changes the appeared scale of the world.
                _vrCameraRig.transform.position =
                    Vector3.Lerp(dodgingRoom.position, _instance.transform.TransformPoint(roomLocalPositionBeforeDodge), smoothener);
                _vrCameraRig.transform.rotation =
                    Quaternion.Slerp(dodgingRoom.rotation, _instance.transform.rotation, smoothener);
            }
        }

        // The camera transform at the end of a dodge roll animation may not be the same as its non-dodging equivalent so we need to use a lerp to ensure an smooth exit.
        private float GetDodgeExitSmoothener()
        {
            float threshold = 0.3f;
            return UpdateDodgeVr.currdodgetimer > threshold ? 0 : (threshold - UpdateDodgeVr.currdodgetimer) / threshold;
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
            if (!VHVRConfig.UseVrControls() || vrikRef != null || !attachedToPlayer)
            {
                return;
            }
            var cam = CameraUtils.getCamera(CameraUtils.VR_CAMERA);
            leftFootPhysicsEstimator =
                (leftFoot != null ? leftFoot : (leftFoot = new GameObject().transform)).gameObject.GetOrAddComponent<PhysicsEstimator>();
            rightFootPhysicsEstimator =
                (rightFoot != null ? rightFoot : (rightFoot = new GameObject().transform)).gameObject.GetOrAddComponent<PhysicsEstimator>();
            leftFootPhysicsEstimator.refTransform = rightFootPhysicsEstimator.refTransform = _vrCameraRig;
            vrikRef = VrikCreator.initialize(player.gameObject, leftHand.transform, rightHand.transform, cam.transform, pelvis);
            if (vrikRef == null)
            {
                return;
            }
            var vrPlayerSync = player.gameObject.GetComponent<VRPlayerSync>();
            vrPlayerSync.camera = cam.gameObject;
            vrPlayerSync.leftHand = vrikRef.solver.leftArm.target.parent.gameObject;
            vrPlayerSync.rightHand = vrikRef.solver.rightArm.target.parent.gameObject;
            vrPlayerSync.pelvis = pelvis.gameObject;
            VrikCreator.resetVrikHandTransform(player);
            var leftHandGesture = vrikRef.references.leftHand.gameObject.GetOrAddComponent<HandGesture>();
            var rightHandGesture = vrikRef.references.rightHand.gameObject.GetOrAddComponent<HandGesture>();
            leftHandGesture.sourceHand = leftHand;
            rightHandGesture.sourceHand = rightHand;
            StaticObjects.leftFist().setColliderParent(leftHandBone, leftHandGesture, false);
            StaticObjects.rightFist().setColliderParent(rightHandBone, rightHandGesture, true);
            StaticObjects.leftFootCollision().setColliderParent(leftFoot);
            StaticObjects.rightFootCollision().setColliderParent(rightFoot);
            player.gameObject.GetOrAddComponent<FistBlock>();
            player.gameObject.GetOrAddComponent<ShipSteering>().Initialize(leftHandGesture, rightHandGesture);
            var reining = player.gameObject.GetOrAddComponent<Reining>();
            reining.leftHandGesture = leftHandGesture;
            reining.rightHandGesture = rightHandGesture;
            StaticObjects.mouthCollider(cam.transform);
            StaticObjects.addQuickMenus();
            LeftHandQuickMenu.instance.refreshItems();
            RightHandQuickMenu.instance.refreshItems();
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
                return vrik.enabled && !Game.IsPaused();
            }
            return false;
        }

        private void maybeInitHeadPosition(Player playerCharacter)
        {
            if (!headPositionInitialized && !playerCharacter.InDodge() && _vrCam != null)
            {
                foreach (var c in GameObject.FindObjectsOfType<Camera>())
                {
                    if (c.name == CameraUtils.VR_CAMERA && c != _vrCam)
                    {
                        LogWarning("VR Camera is stale!");
                        _vrCam = null;
                        return;
                    }
                }
            }

            if (headPositionInitialized || !inFirstPerson || playerCharacter.InDodge())
            {
                return;
            }

            // First set the position without any adjustment
            _instance.transform.localPosition = getDesiredLocalPosition(playerCharacter);
            var hmd = Valve.VR.InteractionSystem.Player.instance.hmdTransform;
            if (firstPersonHeightOffset == null)
            {
                // Measure the distance between HMD and desires location, and save it.
                firstPersonHeightOffset = Vector3.Dot(_instance.transform.position - hmd.position, playerCharacter.transform.up);
            }

            if (_headZoomLevel != HeadZoomLevel.FirstPerson)
            {
                _instance.transform.localPosition += getHeadOffset(_headZoomLevel);
            }
            else
            {
                setPlayerVisualsOffset(playerCharacter.transform, -getHeadOffset(_headZoomLevel));
            }

            if (VHVRConfig.UseLookLocomotion())
            {
                _instance.transform.localRotation = Quaternion.Euler(0f, -hmd.localRotation.eulerAngles.y, 0f);
            }

            if (PlayerCustomizaton.IsBarberGuiVisible())
            {
                _instance.transform.localRotation *= Quaternion.Euler(0, 180, 0);
            }

            headPositionInitialized = true;

            referencePlayerHeight = Valve.VR.InteractionSystem.Player.instance.eyeHeight;
            LogUtils.LogDebug("Reference player height: " + referencePlayerHeight);
        }

        private static Vector3 getDesiredLocalPosition(Player playerCharacter)
        {
            if (playerCharacter == null)
            {
                return Vector3.zero;
            }
            var desiredLocalPosition = playerCharacter.transform.InverseTransformPoint(playerCharacter.GetEyePoint());
            desiredLocalPosition.x = desiredLocalPosition.z = 0;
            return desiredLocalPosition;
        }

        private void caliberateBodyTracking()
        {
            if (vrCam == null || vrCam.transform.parent == null || !headPositionInitialized || vrikRef?.solver?.spine?.pelvisTarget == null)
            {
                return;
            }
 
            if (!VHVRConfig.IsHipTrackingEnabled())
            {
                return;
            }

            hipTrackerIndex = VHVRConfig.HipTrackerIndex() < 0 ? detectHipDeviceIndex(): VHVRConfig.HipTrackerIndex();

            Vector3 roomUpDirection = vrCam.transform.parent.up;
            pelvis.parent = hipTracker.transform;
            pelvis.position = inferPelvisPositionFromHead();
            pelvis.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(vrCam.transform.forward, roomUpDirection), roomUpDirection);
            caliberatedPelvisLocalPosition = pelvis.localPosition;
            caliberatedPelvisLocalRotation = pelvis.localRotation;
            caliberateFeet();
            if (vrikRef != null)
            {
                VrikCreator.ResetPelvisAndFootTransform(vrikRef);
            }
            bodyTrackingCaliberationPending = false;
        }

        private void caliberateFeet()
        {
            int firstFootDeviceIndex = VHVRConfig.LeftFootTrackerIndex();
            int secondFootDeviceindex = VHVRConfig.RightFootTrackerIndex();

            if (firstFootDeviceIndex <= 0)
            {
                firstFootDeviceIndex = detectFootDeviceIndex(-1);
            }
            if (secondFootDeviceindex <= 0)
            {
                secondFootDeviceindex = detectFootDeviceIndex(firstFootDeviceIndex);
            }

            if (firstFootDeviceIndex < 0 || secondFootDeviceindex < 0)
            {
                leftFoot.parent = null;
                rightFoot.parent = null;
                return;
            }

            if (vrCam.transform.InverseTransformPoint(trackedObjects[firstFootDeviceIndex].transform.position).x <
                vrCam.transform.InverseTransformPoint(trackedObjects[secondFootDeviceindex].transform.position).x)
            {
                leftFoot.parent = trackedObjects[firstFootDeviceIndex].transform;
                rightFoot.parent = trackedObjects[secondFootDeviceindex].transform;
            }
            else
            {
                leftFoot.parent = trackedObjects[secondFootDeviceindex].transform;
                rightFoot.parent = trackedObjects[firstFootDeviceIndex].transform;
            }

            leftFoot.rotation = pelvis.rotation;
            rightFoot.rotation = pelvis.rotation;

            Vector3 roomUpDirection = vrCam.transform.parent.up;
            Vector3 footHeight = _vrCam.transform.position - _vrCam.transform.forward * 0.1f - (1.7f + VHVRConfig.PlayerHeightAdjust()) * roomUpDirection;
            leftFoot.position = footHeight + Vector3.ProjectOnPlane(leftFoot.parent.position - footHeight, roomUpDirection);
            rightFoot.position = footHeight + Vector3.ProjectOnPlane(rightFoot.parent.position - footHeight, roomUpDirection);
            baseFootHeight = _vrCameraRig.InverseTransformPoint(footHeight).y;

            if (vrikRef != null)
            {
                vrikRef.solver.leftLeg.target.SetParent(leftFoot, worldPositionStays: false);
                vrikRef.solver.rightLeg.target.SetParent(rightFoot, worldPositionStays: false);
            }
        }

        private int detectHipDeviceIndex()
        {
            int deviceIndex = 0;
            for (int i = 1; i < trackedObjects.Length; i++)
            {
                if (!trackedObjects[i].isValid ||
                    trackedObjects[i].transform.localPosition.y - _vrCam.transform.localPosition.y < -0.8f ||
                    Vector3.Distance(trackedObjects[i].transform.position, _vrCam.transform.position) > 1.5f)
                {
                    continue;
                }
                if (trackedObjects[i].transform.localPosition.y < trackedObjects[deviceIndex].transform.localPosition.y)
                {
                    deviceIndex = i;
                }
            }
            LogUtils.LogDebug("Detected hip tracker index: " + deviceIndex);
            return deviceIndex;
        }

        private int detectFootDeviceIndex(int otherFootDeviceIndex)
        {
            int deviceIndex = -1;
            for (int i = 0; i < trackedObjects.Length; i++)
            {
                if (!trackedObjects[i].isValid ||
                    i == otherFootDeviceIndex ||
                    i == VHVRConfig.HipTrackerIndex() ||
                    trackedObjects[i].transform.localPosition.y > hipTracker.transform.localPosition.y)
                {
                    continue;
                }
                if (deviceIndex < 0)
                {
                    deviceIndex = i;
                }
                else if (trackedObjects[i].transform.localPosition.y < trackedObjects[deviceIndex].transform.localPosition.y)
                {
                    deviceIndex = i;
                }
            }
            return deviceIndex;
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
            var desirePosition = mainCamera.gameObject.transform.position;
            if (PlayerCustomizaton.IsBarberGuiVisible() && getPlayerCharacter())
            {
                desirePosition.y = getPlayerCharacter().transform.position.y;
            }
            _instance.transform.position = desirePosition;
            _instance.transform.rotation = mainCamera.gameObject.transform.rotation;
            attachedToPlayer = false;
            headPositionInitialized = false;
            firstPersonOffset = Vector3.zero;
        }

        // Used to turn off the head model when player is currently occupying it.
        private void setHeadVisibility(bool isVisible)
        {
            if (VHVRConfig.UseVrControls())
            {
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
                    if (ppb.enabled) ppb.enabled = false;
                }
            }
            if (!foundMainCameraPostProcesor)
            {
                return;
            }
            var mainCamDepthOfField = mainCamera.gameObject.GetComponent<DepthOfField>();
            var vrCamDepthOfField = vrCamera.gameObject.AddComponent<DepthOfField>();
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
            vrCamera.gameObject.AddComponent<UnderwaterEffectsUpdater>().Init(
                vrCamera, postProcessingBehavior, postProcessingBehavior.profile);
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

        private void CheckSitRoomscale()
        {
            var player = getPlayerCharacter();
            if (!VHVRConfig.UseVrControls() || _vrCam == null || player == null || player.IsAttached() || !player.IsOnGround())
            {
                startingSit = false;
                return;
            }

            if (player.IsSitting())
            {
                startingSit = false;
                return;
            }

            if (startingSit)
            {
                if (!player.InEmote())
                {
                    player.StartEmote("sit", false);
                }
                return;
            }

            if (VRControls.smoothWalkX > 0.1f || VRControls.smoothWalkY > 0.1f ||
                VRControls.smoothWalkX < -0.1f || VRControls.smoothWalkY < -0.1f ||
                gesturedLocomotionManager.stickOutputX > 0.1f || gesturedLocomotionManager.stickOutputY > 0.1f ||
                gesturedLocomotionManager.stickOutputX < -0.1f || gesturedLocomotionManager.stickOutputY < -0.1f)
            {
                return;
            }

            float height = Valve.VR.InteractionSystem.Player.instance.eyeHeight;
            float heightThreshold = referencePlayerHeight * VHVRConfig.RoomScaleSneakHeight();
            if (height > heightThreshold - 0.1f)
            {
                // Head too high for sitting
                return;
            }

            if (SteamVR_Actions.valheim_StopGesturedLocomotion.GetState(SteamVR_Input_Sources.Any) || EquipScript.getRight() == EquipType.Fishing)
            {
                return;
            }

            if (VHVRConfig.IsHipTrackingEnabled())
            {
                if (VHVRConfig.TrackFeet())
                {
                    Vector3 pelvisFacing = Vector3.ProjectOnPlane(pelvis.forward, _vrCameraRig.up);
                    if (Vector3.Dot(leftFoot.position - pelvis.position, pelvisFacing) > 0.3f &&
                        Vector3.Dot(rightFoot.position - pelvis.position, pelvisFacing) > 0.3f &&
                        Vector3.Dot(_vrCam.transform.position - pelvis.position, pelvisFacing) < 0.1f) // Sitting with feet in front
                    {
                        startingSit = true;
                        return;
                    }
                }
                else
                {
                    Vector3 facing = Vector3.ProjectOnPlane(vrCam.transform.forward, _vrCameraRig.up);
                    if (Vector3.Dot(pelvis.position - vrCam.transform.position, facing) > 0.15f &&
                        Vector3.Dot(pelvis.forward, _vrCameraRig.up) > 0.8f) // Laying back
                    {
                        startingSit = true;
                        return;
                    }
                }
            }

            Vector3 leftHandRelativeToHead = leftHandBone.position - _vrCam.transform.position;
            Vector3 rightHandRelativeToHead = rightHandBone.position - _vrCam.transform.position;

            if (Vector3.Dot(leftHandRelativeToHead, _vrCameraRig.up) > -0.25f || Vector3.Dot(rightHandRelativeToHead, _vrCameraRig.up) > -0.25f)
            {
                // Hands too high for sitting.
                return;
            }

            var heading = Vector3.ProjectOnPlane(_vrCam.transform.forward, _vrCameraRig.up).normalized;
            if (Vector3.Dot(leftHandRelativeToHead, heading) < -0.33f && Vector3.Dot(rightHandRelativeToHead, heading) < -0.33f)
            {
                // Hands are behind back, sit.
                startingSit = true;
                return;
            }

            Vector3 playerRight = Vector3.Cross(_vrCameraRig.up, heading);
            if (Vector3.Dot(leftHandRelativeToHead, playerRight) > 0.0625f && Vector3.Dot(rightHandRelativeToHead, playerRight) < -0.0625f)
            {
                // Hands are crosssing, sit.
                startingSit = true;
            }
    }

        private void CheckSneakRoomscale()
        {
            if (!VHVRConfig.RoomScaleSneakEnabled() || startingSit)
            {
                _isRoomscaleSneaking = false;
                return;
            }

            var player = getPlayerCharacter();
            if (player == null)
            {
                _isRoomscaleSneaking = false;
                return;
            }

            float height = Valve.VR.InteractionSystem.Player.instance.eyeHeight;
            float heightThreshold = referencePlayerHeight * VHVRConfig.RoomScaleSneakHeight();
            if (height < heightThreshold && !player.IsSitting())
            {
                _isRoomscaleSneaking = true;
            }
            else if (height > heightThreshold + heightThreshold * 0.05f)
            {
                _isRoomscaleSneaking = false;
            }
        }

        /// <summary>
        /// Moves the physics player to the head position and cancels the movement of the VRCamera by moving the VRRig
        /// </summary>
        void DoRoomScaleMovement(float deltaTime)
        {
            var player = getPlayerCharacter();
            if (_vrCam == null || player == null || player.gameObject == null || player.IsAttached())
            {
                return;
            }

            Vector3 deltaPosition = roomscaleLocomotive - lastRoomscaleLocomotivePosition;
            deltaPosition.y = 0;

            float moveThreshold = player.IsSitting() ? 0.5f : 0.005f;
            if (!VHVRConfig.IsHipTrackingEnabled())
            {
                switch (EquipScript.getRight())
                {
                    case EquipType.Claws:
                    case EquipType.DualKnives:
                    case EquipType.Hammer:
                    case EquipType.Knife:
                    case EquipType.None:
                    case EquipType.Tankard:
                        if (GesturedLocomotionManager.isInUse || SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.Any))
                        {
                            // Allow leaning when holding small weapons or bare-handed.
                            moveThreshold = 1f;
                        }
                        break;
                }
            }

            bool shouldMove = deltaPosition.magnitude > moveThreshold;
            if (shouldMove)
            {
                float maxMovement = deltaTime * MAX_ROOMSCALE_MOVEMENT_SPEED;
                if (deltaPosition.magnitude > maxMovement)
                {
                    // Clamp fast movement to prevent it from allowing going through walls
                    deltaPosition = deltaPosition.normalized * maxMovement;
                }

                // Check for motion discrepancies
                if (VHVRConfig.RoomscaleFadeToBlack() && !_fadeManager.IsFadingToBlack)
                {
                    var lastDeltaMovement = player.m_body.position - _lastPlayerPosition;
                    if (player.m_lastAttachBody && _lastPlayerAttachmentPosition != Vector3.zero)
                    {
                        // Account for ships, and moving attachments
                        lastDeltaMovement -= (player.m_lastAttachBody.position - _lastPlayerAttachmentPosition);
                    }
                    lastDeltaMovement.y = 0;

                    if (roomscaleMovement.magnitude * 0.6f > lastDeltaMovement.magnitude)
                    {
                        SteamVR_Fade.Start(Color.black, 0);
                        SteamVR_Fade.Start(Color.clear, 1.5f);
                    }

                    _lastPlayerPosition = player.m_body.position;
                    _lastPlayerAttachmentPosition = player.m_lastAttachBody ? player.m_lastAttachBody.position : Vector3.zero;

                }

                // Calculate new postion
                lastRoomscaleLocomotivePosition += deltaPosition;
                var globalDeltaPosition = _instance.transform.TransformVector(deltaPosition);
                globalDeltaPosition.y = 0;

                roomscaleMovement = globalDeltaPosition;
                _vrCameraRig.localPosition -= deltaPosition; // Since we move the VR camera rig with the player character elsewhere, we counteract that here to prevent it from moving.
            }
            else roomscaleMovement = Vector3.zero;

            //Set animation parameters
            _roomscaleAnimationForwardSpeed = Mathf.SmoothDamp(_roomscaleAnimationForwardSpeed, shouldMove ? deltaPosition.z / Time.fixedDeltaTime : 0, ref _forwardSmoothVel, ROOMSCALE_STEP_ANIMATION_SMOOTHING, 99f);
            _roomscaleAnimationSideSpeed = Mathf.SmoothDamp(_roomscaleAnimationSideSpeed, shouldMove ? deltaPosition.x / Time.fixedDeltaTime : 0, ref _sideSmoothVel, ROOMSCALE_STEP_ANIMATION_SMOOTHING, 99f);
        }

        public void ResetRoomscaleCamera()
        {
            if (_vrCameraRig == null)
            {
                return;
            }

            Vector3 vrCamPosition = _vrCam.transform.localPosition;
            vrCamPosition.y = 0;
            _vrCameraRig.localPosition = -vrCamPosition;
            lastRoomscaleLocomotivePosition = roomscaleLocomotive;
        }

        public void TriggerHandVibration(float time)
        {
            timerLeft = time;
            timerRight = time;
        }
    }
}
