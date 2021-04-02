using static ValheimVRMod.Utilities.LogUtils;

using AmplifyOcclusion;
using System.Reflection;
using UnityEngine;
using UnityEngine.PostProcessing;
using UnityEngine.SceneManagement;
using UnityStandardAssets.ImageEffects;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore.UI;
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
        private static readonly int HANDS_LAYER = 23;
        private static readonly int HANDS_LAYER_MASK = (1 << HANDS_LAYER);
        private static Vector3 FIRST_PERSON_OFFSET = new Vector3(0f, 0.4f, 0f);
        private static Vector3 THIRD_PERSON_0_OFFSET = new Vector3(-0.5f, 1.0f, -0.6f);
        private static Vector3 THIRD_PERSON_1_OFFSET = new Vector3(-0.5f, 1.4f, -1.5f);
        private static Vector3 THIRD_PERSON_2_OFFSET = new Vector3(-0.5f, 1.9f, -2.6f);
        private static Vector3 THIRD_PERSON_3_OFFSET = new Vector3(-0.5f, 3.2f, -4.4f);

        private static GameObject _prefab;
        private static GameObject _instance;
        private HeadZoomLevel _headZoomLevel = HeadZoomLevel.FirstPerson;

        private static Vector3 HEAD_OFFSET = Vector3.zero;
        private static bool headPositionIsInitialized = false;

        private Camera _vrCam;
        private Camera _handsCam;
        private Camera _skyboxCam;

        private Hand _leftHand;
        private static SteamVR_LaserPointer _leftPointer;
        private Hand _rightHand;
        private static SteamVR_LaserPointer _rightPointer;
        private string _preferredHand;

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

        public static GameObject instance { get { return _instance; } }
        public static bool attachedToPlayer = false;

        void Awake()
        {
            _prefab = VRAssetManager.GetAsset<GameObject>(PLAYER_PREFAB_NAME);
            _preferredHand = VVRConfig.GetPreferredHand();
            Vector2 headOffsetConfig = VVRConfig.GetHeadOffset();
            FIRST_PERSON_OFFSET.x = headOffsetConfig.x;
            FIRST_PERSON_OFFSET.z = headOffsetConfig.y;
            ensurePlayerInstance();
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
            UpdateAmplifyOcclusionStatus();
        }

        void maybeUpdateHeadPosition()
        {
            if (VVRConfig.AllowHeadRepositioning())
            {
                if (Input.GetKeyDown(KeyCode.UpArrow))
                {
                    FIRST_PERSON_OFFSET += new Vector3(0f, 0f, 0.2f);
                }
                if (Input.GetKeyDown(KeyCode.DownArrow))
                {
                    FIRST_PERSON_OFFSET += new Vector3(0f, 0f, -0.2f);
                }
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    FIRST_PERSON_OFFSET += new Vector3(-0.2f, 0f, 0f);
                }
                if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    FIRST_PERSON_OFFSET += new Vector3(0.2f, 0f, 0f);
                }
                VVRConfig.UpdateHeadOffset(new Vector2(FIRST_PERSON_OFFSET.x, FIRST_PERSON_OFFSET.z));
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
            effect.enabled = VVRConfig.UseAmplifyOcclusion();
        }

        private void checkAndSetHandsAndPointers()
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

        // Sets the given pointer active if "active" parameter is true
        // and laser pointers should currently be active.
        private void setPointerActive(SteamVR_LaserPointer p, bool active)
        {
            if (p == null)
            {
                return;
            }
            p.enabled = active && shouldLaserPointersBeActive();
            p.setVisible(active && shouldLaserPointersBeActive());
        }

        private bool shouldLaserPointersBeActive()
        {
            return Cursor.visible || (getPlayerCharacter() != null && getPlayerCharacter().InPlaceMode());
        }

        // Returns true if both the hand and pointer are not null
        // and the hand is active
        private bool handIsActive(Hand h, SteamVR_LaserPointer p)
        {
            if (h == null || p == null)
            {
                return false;
            }
            return h.isActive && h.isPoseValid;
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
            // Turn off rendering the UI panel layer. We need to capture
            // it in a camera of higher depth so that it
            // is rendered on top of everything else. (except hands)
            vrCam.cullingMask &= ~(1 << VRGUI.UI_PANEL_LAYER);
            vrCam.cullingMask &= ~(1 << VRGUI.UI_LAYER);
            vrCam.cullingMask &= ~(1 << HANDS_LAYER);
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
            handsCamera.cullingMask = HANDS_LAYER_MASK;
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
                    return THIRD_PERSON_0_OFFSET;
                case HeadZoomLevel.ThirdPerson1:
                    return THIRD_PERSON_1_OFFSET;
                case HeadZoomLevel.ThirdPerson2:
                    return THIRD_PERSON_2_OFFSET;
                case HeadZoomLevel.ThirdPerson3:
                    return THIRD_PERSON_3_OFFSET;
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
                !getPlayerCharacter().InCutscene();
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
            if (!headPositionIsInitialized && !playerCharacter.InCutscene())
            {
                Transform head = playerCharacter.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);
                HEAD_OFFSET = new Vector3(head.position.x - playerCharacter.transform.position.x, 0f, head.position.z - playerCharacter.transform.position.z);
                headPositionIsInitialized = true;
            }
            setHeadVisibility(_headZoomLevel != HeadZoomLevel.FirstPerson);
            _instance.transform.SetParent(playerCharacter.transform, false);
            _instance.transform.localPosition = HEAD_OFFSET + getHeadOffset(_headZoomLevel);
            attachedToPlayer = true;
        }

        private void attachVrPlayerToMainCamera()
        {
            if (_instance == null)
            {
                LogError("SteamVR Player instance is null while attaching to main camera!");
                return;
            }
            headPositionIsInitialized = false;
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
        }

        // Used to turn off the head model when player is currently occupying it.
        private void setHeadVisibility(bool isVisible)
        {
            Player playerCharacter = getPlayerCharacter();
            if (playerCharacter != null)
            {
                Transform head = playerCharacter.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);
                head.localScale = isVisible ? new Vector3(1f, 1f, 1f) : new Vector3(0f, 0f, 0f);
            }
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
                    // Copy the values over from the MainCamera's PostProcessingBehaviour
                    CopyClassFields(ppb, ref postProcessingBehavior);
                    var profileClone = Instantiate(ppb.profile);
                    postProcessingBehavior.profile = profileClone;
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
    }
}
