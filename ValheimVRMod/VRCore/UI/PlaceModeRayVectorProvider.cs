using UnityEngine;
using ValheimVRMod.Utilities;
using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.VRCore.UI
{
    // This class is used to provide custom vectors for the placement
    // mode raycasts. This is necessary for mouse and keyboard controls
    // so that the ray isn't set by the direction of the HMD, which is
    // awkward and uncomfortable to play with, and insteaded uses the
    // mouse as the way to affect the ray direction.
    class PlaceModeRayVectorProvider : MonoBehaviour
    {

        public static PlaceModeRayVectorProvider instance {
            get
            {
                ensureInstance();
                return _instance.GetComponent<PlaceModeRayVectorProvider>();
            }
        }

        public static Vector3 startingPosition {
            get
            {
                ensureInstance();
                return _startingPosition;
            } 
        }

        public static Vector3 rayDirection {
            get
            {
                ensureInstance();
                return _rayDirection * Vector3.forward;
            }
        }

        public static Vector3 startingPositionLeft
        {
            get
            {
                ensureInstance();
                return _startingPositionLeft;
            }
        }

        public static Vector3 rayDirectionLeft
        {
            get
            {
                ensureInstance();
                return _rayDirectionLeft * Vector3.forward;
            }
        }

        private static GameObject _instance;

        private static float _pitch = 0f;
        private static Quaternion _yaw = Quaternion.identity;
        private static Quaternion _rayDirection = Quaternion.identity;
        private static Vector3 _startingPosition = Vector3.zero;
        private static Quaternion _rayDirectionLeft = Quaternion.identity;
        private static Vector3 _startingPositionLeft = Vector3.zero;
        private static GameObject _vrCamObj;

        private bool inPlaceMode = false;

        private static void ensureInstance()
        {
            if (_instance == null)
            {
                _instance = new GameObject("PlaceModeRayVectorProvider");
                DontDestroyOnLoad(_instance);
                _instance.AddComponent<PlaceModeRayVectorProvider>();
            }
        }

        void Update()
        {
            UpdatePlaceModeState();
        }

        void LateUpdate()
        {
            if (shouldUpdateRayVectors())
            {
                if (VRPlayer.activePointer == null) {
                    setPitch();
                    setYaw();
                }
                setRayDirection();
                setRayStartingPosition();
            }
        }

        private void UpdatePlaceModeState()
        {
            if (Player.m_localPlayer != null)
            {
                bool playerInPlaceMode = Player.m_localPlayer.InPlaceMode();
                if (!inPlaceMode && playerInPlaceMode)
                {
                    onPlaceModeEntered();
                }
                inPlaceMode = playerInPlaceMode;
            }
        }

        private void setRayDirection()
        {
            if (VHVRConfig.UseVrControls() && VRPlayer.rightPointer != null)
            {
                _rayDirection = VRPlayer.rightPointer.rayDirection;
            }
            else
            {
                _rayDirection = Quaternion.Euler(_pitch, _yaw.eulerAngles.y, 0f);
            }
            if (VHVRConfig.UseVrControls() && VRPlayer.leftPointer != null)
            {
                _rayDirectionLeft = VRPlayer.leftPointer.rayDirection;
            }
            else
            {
                _rayDirectionLeft = Quaternion.identity;
            }
        }

        private void setRayStartingPosition()
        {
            if (VHVRConfig.UseVrControls() && VRPlayer.rightPointer != null)
            {
                _startingPosition = VRPlayer.rightPointer.rayStartingPosition;
            }
            else if (_vrCamObj != null)
            {
                _startingPosition = _vrCamObj.transform.position;
            }
            else
            {
                _vrCamObj = GameObject.Find(CameraUtils.VR_CAMERA);
                if (_vrCamObj != null)
                {
                    _startingPosition = _vrCamObj.transform.position;
                } else
                {
                    _startingPosition = Vector3.zero;
                }
            }
            if (VHVRConfig.UseVrControls() && VRPlayer.leftPointer != null)
            {
                _startingPositionLeft = VRPlayer.leftPointer.rayStartingPosition;
            } else
            {
                _startingPositionLeft = Vector3.zero;
            }
        }

        private void setPitch()
        {
            float yAxis = Input.GetAxis("Mouse Y") * PlayerController.m_mouseSens;
            yAxis += -ZInput.GetJoyRightStickY() * 110f * Time.unscaledDeltaTime;
            _pitch = Mathf.Clamp(_pitch - yAxis, -89f, 89f);
        }

        private void setYaw()
        {
            if (VRPlayer.instance != null)
            {
                _yaw = Quaternion.LookRotation(VRPlayer.instance.transform.forward);
            }
        }

        private void onPlaceModeEntered()
        {
            // Reset pitch
            _pitch = 0f;
        }

        private bool shouldUpdateRayVectors()
        {
            if ((Chat.instance != null && Chat.instance.HasFocus()) || Console.IsVisible() || TextInput.IsVisible())
            {
                return false;
            }
            return inPlaceMode &&
                !StoreGui.IsVisible() &&
                !InventoryGui.IsVisible() &&
                !Menu.IsVisible() &&
                !(TextViewer.instance && TextViewer.instance.IsVisible()) &&
                !Minimap.IsOpen() &&
                !Hud.IsPieceSelectionVisible();
        }
    }
}
