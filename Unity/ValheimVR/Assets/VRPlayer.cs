using static ValheimVRMod.Utilities.LogUtils;

using System.Reflection;
using UnityEngine;
using ValheimVRMod.Utilities;
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
        private static Vector3 THIRD_PERSON_0_OFFSET = new Vector3(0f, 1.0f, -0.6f);
        private static Vector3 THIRD_PERSON_1_OFFSET = new Vector3(0f, 1.4f, -1.5f);
        private static Vector3 THIRD_PERSON_2_OFFSET = new Vector3(0f, 1.9f, -2.6f);
        private static Vector3 THIRD_PERSON_3_OFFSET = new Vector3(0f, 3.2f, -4.4f);
        private static Vector3 THIRD_PERSON_CONFIG_OFFSET = Vector3.zero;
        private static float NECK_OFFSET = 0.2f;

        private static GameObject _prefab;
        private static GameObject _instance;
        private static HeadZoomLevel _headZoomLevel = HeadZoomLevel.FirstPerson;

        private Camera _vrCam;
        private Camera _handsCam;
        private Camera _skyboxCam;

        private static Hand _leftHand;
        private static SteamVR_LaserPointer _leftPointer;
        private static Hand _rightHand;
        private static SteamVR_LaserPointer _rightPointer;
        private string _preferredHand;

        public static Hand leftHand { get { return _leftHand;} }
        public static Hand rightHand { get { return _rightHand;} }
        public static SteamVR_LaserPointer leftPointer { get { return _leftPointer;} }
        public static SteamVR_LaserPointer rightPointer { get { return _rightPointer; } }
        public static SteamVR_LaserPointer activePointer
        {
            get
            {
                if (leftPointer != null && leftPointer.enabled)
                {
                    return leftPointer;
                }
                else if (rightPointer != null && rightPointer.enabled)
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


        public static GameObject instance { get { return _instance; } }
        public static bool attachedToPlayer = false;

        private static Vector3 FIRST_PERSON_INIT_OFFSET = Vector3.zero;
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
                    FIRST_PERSON_INIT_OFFSET = Vector3.zero;
                    FIRST_PERSON_OFFSET = Vector3.zero;
                }
            }
        }

        void Awake()
        {
            _prefab = VRAssetManager.GetAsset<GameObject>(PLAYER_PREFAB_NAME);
            _preferredHand = "RightHand";
            headPositionInitialized = false;
            FIRST_PERSON_OFFSET = Vector3.zero;
            THIRD_PERSON_CONFIG_OFFSET = Vector3.zero;
            ensurePlayerInstance();
        }

        void Update()
        {
            if (!ensurePlayerInstance())
            {
                return;
            }
            attachVrPlayerToWorldObject();
            enableCameras();
            checkAndSetHandsAndPointers();
        }


        private void checkAndSetHandsAndPointers()
        {
            tryInitializeHands();
            if (_leftHand != null)
            {
                _leftHand.enabled = true;
            }
            if (_rightHand != null)
            {
                _rightHand.enabled = true;
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
                }
            }
            if (_rightHand == null || _rightPointer == null)
            {
                _rightHand = getHand(RIGHT_HAND, _instance);
                if (_rightHand != null)
                {
                    _rightPointer = _rightHand.GetComponent<SteamVR_LaserPointer>();
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
            p.enabled =true;
            p.setVisible(active);
        }

   
        // Returns true if both the hand and pointer are not null
        // and the hand is active
        private bool handIsActive(Hand h, SteamVR_LaserPointer p)
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
            // Turn off rendering the UI panel layer. We need to capture
            // it in a camera of higher depth so that it
            // is rendered on top of everything else. (except hands)

            mainCamera.enabled = false;
            AudioListener mainCamListener = mainCamera.GetComponent<AudioListener>();
            if (mainCamListener != null)
            {
                LogDebug("Destroying MainCamera AudioListener");
                DestroyImmediate(mainCamListener);
            }
            _instance.SetActive(true);
            vrCam.enabled = true;
            _vrCam = vrCam;
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
           attachVrPlayerToMainCamera();
        }

        private void updateZoomLevel()
        {
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

        private void maybeInitHeadPosition(Player playerCharacter)
        {
            if (!headPositionInitialized && inFirstPerson)
            {
                // First set the position without any adjustment
                Vector3 desiredPosition = Vector3.zero;
                _instance.transform.localPosition = desiredPosition -
                    playerCharacter.transform.position + getHeadOffset(_headZoomLevel);
                Vector3 hmd = Valve.VR.InteractionSystem.Player.instance.hmdTransform.position;
                // Measure the distance between HMD and desires location, and save it.
                FIRST_PERSON_INIT_OFFSET = desiredPosition - hmd;
                headPositionInitialized = true;
                //_instance.transform.localRotation = Quaternion.identity;
            }
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

            // Orient the player with the main camera
            _instance.transform.parent = mainCamera.gameObject.transform;
            _instance.transform.position = mainCamera.gameObject.transform.position;
            _instance.transform.rotation = mainCamera.gameObject.transform.rotation;
            attachedToPlayer = false;
            headPositionInitialized = false;
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
    }
}
