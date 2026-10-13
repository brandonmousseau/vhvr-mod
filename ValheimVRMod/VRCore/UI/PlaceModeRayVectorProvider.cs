using ValheimVRMod.VRCore.Backends;
using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.Scripts;
using ValheimVRMod.Utilities;

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
                if (LocalWeaponWield.isCurrentlyTwoHanded())
                {
                    return LocalWeaponWield.localWeaponTip + Vector3.up * 0.25f;
                }
                return _groundHandRayActive ? _groundHandRayStartingPosition : _startingPosition;
            }
        }

        public static Vector3 rayDirection {
            get
            {
                ensureInstance();
                return LocalWeaponWield.isCurrentlyTwoHanded() || _groundHandRayActive ? -Vector3.up : _rayDirection * Vector3.forward;
            }
        }

        // Whether the placement ray is currently cast straight down from the hand that is not holding the
        // building tool, because that hand is close to the ground, instead of from the dominant hand's pointer.
        public static bool isGroundHandRayActive
        {
            get
            {
                return _groundHandRayActive && !LocalWeaponWield.isCurrentlyTwoHanded();
            }
        }

        // The hand that the placement ray comes from, which is therefore the hand whose trigger places the piece.
        public static VRInputSource rayHandInputSource
        {
            get
            {
                return isGroundHandRayActive ? VRPlayer.secondaryWeaponHandInputSource : VRPlayer.dominantHandInputSource;
            }
        }

        public static Vector3 startingPositionNonDominant
        {
            get
            {
                ensureInstance();
                return _startingPositionNonDominant;
            }
        }

        public static Vector3 rayDirectionNonDominant
        {
            get
            {
                ensureInstance();
                return _rayDirectionNonDominant * Vector3.forward;
            }
        }

        private static GameObject _instance;

        private static float _pitch = 0f;
        private static Quaternion _yaw = Quaternion.identity;
        private static Quaternion _rayDirection = Quaternion.identity;
        private static Vector3 _startingPosition = Vector3.zero;
        private static Quaternion _rayDirectionNonDominant = Quaternion.identity;
        private static Vector3 _startingPositionNonDominant = Vector3.zero;
        private static GameObject _vrCamObj;

        // Building tools that let the free hand place pieces on the ground right beneath it.
        private static readonly HashSet<EquipType> GROUND_HAND_RAY_TOOLS = new HashSet<EquipType>
        {
            EquipType.Cultivator
        };
        private const float GROUND_HAND_RAY_MAX_HAND_HEIGHT = 0.125f;
        // The ray starts above the hand so that it still finds the ground when the controller dips below it.
        private const float GROUND_HAND_RAY_START_HEIGHT = 0.25f;
        private static bool _groundHandRayActive = false;
        private static Vector3 _groundHandRayStartingPosition = Vector3.zero;

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
                updateGroundHandRay();
            }
            else
            {
                _groundHandRayActive = false;
            }
        }

        private void updateGroundHandRay()
        {
            _groundHandRayActive = false;
            if (!canUseGroundHandRay())
            {
                return;
            }
            // The controller position, as opposed to the IK hand that may be kept from reaching it.
            Vector3 start = VRPlayer.mainWeaponHand.otherHand.transform.position + Vector3.up * GROUND_HAND_RAY_START_HEIGHT;
            if (Physics.Raycast(start, -Vector3.up, GROUND_HAND_RAY_START_HEIGHT + GROUND_HAND_RAY_MAX_HAND_HEIGHT, Player.m_localPlayer.m_placeRayMask))
            {
                _groundHandRayStartingPosition = start;
                _groundHandRayActive = true;
            }
        }

        private bool canUseGroundHandRay()
        {
            if (!VHVRConfig.UseVrControls() || VRPlayer.mainWeaponHand == null || VRPlayer.mainWeaponHand.otherHand == null)
            {
                return false;
            }
            if (LocalWeaponWield.isCurrentlyTwoHanded() || !GROUND_HAND_RAY_TOOLS.Contains(EquipScript.CurrentMainHandEquipType()))
            {
                return false;
            }
            Player player = Player.m_localPlayer;
            Piece piece = player.GetSelectedPiece();
            return piece != null && player.HaveRequirements(piece, Player.RequirementMode.CanBuild);
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
            if (VHVRConfig.UseVrControls() && VRPlayer.dominantPointer != null)
            {
                _rayDirection = VRPlayer.dominantPointer.rayDirection;
            }
            else
            {
                _rayDirection = Quaternion.Euler(_pitch, _yaw.eulerAngles.y, 0f);
            }
            if (VHVRConfig.UseVrControls() && VRPlayer.nonDominantPointer != null)
            {
                _rayDirectionNonDominant = VRPlayer.nonDominantPointer.rayDirection;
            }
            else
            {
                _rayDirectionNonDominant = Quaternion.identity;
            }
        }

        private void setRayStartingPosition()
        {
            if (VHVRConfig.UseVrControls() && VRPlayer.dominantPointer != null)
            {
                _startingPosition = VRPlayer.dominantPointer.rayStartingPosition;
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
            if (VHVRConfig.UseVrControls() && VRPlayer.nonDominantPointer != null)
            {
                _startingPositionNonDominant = VRPlayer.nonDominantPointer.rayStartingPosition;
            } else
            {
                _startingPositionNonDominant = Vector3.zero;
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
            if (FejdStartup.instance != null && FejdStartup.instance.isActiveAndEnabled)
            {
                return false;
            }
            if (Chat.instance != null && Chat.instance.HasFocus())
            {
                return false;
            }
            if (Console.IsVisible() || TextInput.IsVisible())
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
