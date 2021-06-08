using System.Collections.Generic;
using UnityEngine;
using Valve.VR;
using HarmonyLib;
using ValheimVRMod.Scripts;
using ValheimVRMod.Utilities;

using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.VRCore.UI
{
    class VRControls : MonoBehaviour
    {

        // Time in seconds that Recenter pose must be held to recenter
        private static readonly float RECENTER_POSE_TIME = 3f;
        // Local Position relative to HMD that will trigger the Recenter action
        private static readonly Vector3 RECENTER_POSE_POSITION_L = new Vector3(-0.1f, 0f, 0.1f);
        private static readonly Vector3 RECENTER_POSE_POSITION_R = new Vector3(0.1f, 0f, 0.1f);
        // Tolerance for above pose
        private static readonly float RECENTER_POSE_TOLERANCE = 0.2f; // Magnitude
        // number of updates to skip before allowing a "rotation" update to occur
        // when using the alt piece rotation mode (ie, context scroll is not bound).
        private static readonly int ALT_PIECE_ROTATION_RESISTOR = 5;

        private int currentRotationUpdateFrame = 0;

        private HashSet<string> ignoredZInputs = new HashSet<string>();
        private SteamVR_ActionSet mainActionSet = SteamVR_Actions.Valheim;
        private SteamVR_ActionSet laserActionSet = SteamVR_Actions.LaserPointers;
        private Dictionary<string, SteamVR_Action_Boolean> zInputToBooleanAction = new Dictionary<string, SteamVR_Action_Boolean>();

        private SteamVR_Action_Vector2 walk;
        private SteamVR_Action_Vector2 pitchAndYaw;

        private SteamVR_Action_Vector2 contextScroll;

        private SteamVR_Action_Pose poseL;
        private SteamVR_Action_Pose poseR;

        // Action for "Use" using the left hand controller
        private SteamVR_Action_Boolean _useLeftHand = SteamVR_Actions.valheim_UseLeft;

        public SteamVR_Action_Boolean useLeftHandAction { get
            {
                return _useLeftHand;
            } }

        private float recenteringPoseDuration;

        public static bool mainControlsActive
        {
            get
            {
                return _instance != null && _instance.mainActionSet.IsActive();
            }
        }

        public static bool laserControlsActive
        {
            get
            {
                return _instance != null && _instance.laserActionSet.IsActive();
            }
        }

        public static VRControls instance { get { return _instance; } }
        private static VRControls _instance;
        public void Awake()
        {
            init();
            recenteringPoseDuration = 0f;
            _instance = this;
        }

        public void Update()
        {
            updateMainActionSetState();
            updateLasersActionSetState();
            if (mainActionSet.IsActive())
            {
                checkRecenterPose(Time.deltaTime);
            }

            checkQuickItems();
        }
        
        private void checkQuickItems() {
            
            var quickActions = StaticObjects.quickActions;
            var quickSwitch = StaticObjects.quickSwitch;
            if (!quickActions || !quickSwitch) {
                return;
            }
            
            if (SteamVR_Actions.valheim_QuickActions.GetStateDown(SteamVR_Input_Sources.Any)
            || SteamVR_Actions.valheim_QuickSwitch.GetStateDown(SteamVR_Input_Sources.Any)) {
                quickActions.SetActive(true);
                quickSwitch.SetActive(true);
            }

            if (SteamVR_Actions.valheim_QuickActions.GetStateUp(SteamVR_Input_Sources.Any)
            || SteamVR_Actions.valheim_QuickSwitch.GetStateUp(SteamVR_Input_Sources.Any)) {
                quickActions.GetComponent<QuickActions>().selectHoveredItem();
                quickActions.SetActive(false);
                quickSwitch.GetComponent<QuickSwitch>().selectHoveredItem();
                quickSwitch.SetActive(false);
            }
        }
        
        private void checkRecenterPose(float dt)
        {
            if (isInRecenterPose())
            {
                recenteringPoseDuration += dt;
                if (recenteringPoseDuration >= RECENTER_POSE_TIME)
                {
                    LogDebug("Triggered Recenter pose action.");
                    VRManager.tryRecenter();
                    recenteringPoseDuration = 0f;
                }
            } else
            {
                recenteringPoseDuration = 0f;
            }
        }

        private bool isInRecenterPose()
        {
            var hmd = VRPlayer.instance.GetComponent<Valve.VR.InteractionSystem.Player>().hmdTransform;
            var targetLocationLeft = hmd.localPosition + hmd.localRotation * RECENTER_POSE_POSITION_L;
            var targetLocationRight = hmd.localPosition + hmd.localRotation * RECENTER_POSE_POSITION_R;
            var leftHand = poseL.localPosition;
            var rightHand = poseR.localPosition;
            var leftHandDiff = leftHand - targetLocationLeft;
            var rightHandDiff = rightHand - targetLocationRight;
            return leftHandDiff.magnitude <= RECENTER_POSE_TOLERANCE && rightHandDiff.magnitude <= RECENTER_POSE_TOLERANCE;
        }

        private void updateMainActionSetState()
        {
            bool useVrControls = VHVRConfig.UseVrControls();
            if (!useVrControls || mainActionSet.IsActive() && !VRPlayer.handsActive)
            {
                mainActionSet.Deactivate();
            }
            else if (useVrControls && !mainActionSet.IsActive() && VRPlayer.handsActive)
            {
                mainActionSet.Activate();
            }
        }

        private void updateLasersActionSetState()
        {
            if (!mainActionSet.IsActive())
            {
                laserActionSet.Deactivate();
                return;
            }
            if (laserActionSet.IsActive() && VRPlayer.activePointer == null)
            {
                laserActionSet.Deactivate();
            }
            else if (!laserActionSet.IsActive() && VRPlayer.activePointer != null)
            {
                laserActionSet.Activate(SteamVR_Input_Sources.Any, 1 /* Higher priority than main action set */);
            }
        }

        public bool GetButtonDown(string zinput)
        {
            if (!mainActionSet.IsActive() || ignoredZInputs.Contains(zinput))
            {
                return false;
            }
            if (zinput == "Jump" && shouldDisableJump())
            {
                return false;
            }

            // Handle Map zoom specially using context scroll input
            if (zinput == "MapZoomOut")
            {
                if (contextScroll.axis.y < 0)
                {
                    return true;
                } else
                {
                    return false;
                }
            } else if (zinput == "MapZoomIn")
            {
                if (contextScroll.axis.y > 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            if (zinput == "Map" && altPieceRotationControlsActive())
            {
                // Disable the regular map input if alternative piece
                // rotation controls are active.
                return false;
            }
            SteamVR_Action_Boolean action;
            zInputToBooleanAction.TryGetValue(zinput, out action);
            if (action == null)
            {
                LogWarning("Unmapped ZInput Key:" + zinput);
                return false;
            }
            return action.GetStateDown(SteamVR_Input_Sources.Any);
        }

        public bool GetButton(string zinput)
        {
            if (!mainActionSet.IsActive() || ignoredZInputs.Contains(zinput))
            {
                return false;
            }
            if (zinput == "Jump" && shouldDisableJump())
            {
                return false;
            }
            if (zinput == "Map" && altPieceRotationControlsActive())
            {
                // Disable the regular map input if alternative piece
                // rotation controls are active.
                return false;
            }
            SteamVR_Action_Boolean action;
            zInputToBooleanAction.TryGetValue(zinput, out action);
            if (action == null)
            {
                LogWarning("Unmapped ZInput Key:" + zinput);
                return false;
            }
            return action.GetState(SteamVR_Input_Sources.Any);
        }

        public bool GetButtonUp(string zinput)
        {
            if (!mainActionSet.IsActive() || ignoredZInputs.Contains(zinput))
            {
                return false;
            }
            if (zinput == "Jump" && shouldDisableJump())
            {
                return false;
            }
            if (zinput == "Map" && altPieceRotationControlsActive())
            {
                // Disable the regular map input if alternative piece
                // rotation controls are active.
                return false;
            }
            SteamVR_Action_Boolean action;
            zInputToBooleanAction.TryGetValue(zinput, out action);
            if (action == null)
            {
                LogWarning("Unmapped ZInput Key:" + zinput);
                return false;
            }
            return action.GetStateUp(SteamVR_Input_Sources.Any);
        }

        public float GetJoyLeftStickX()
        {
            if (!mainActionSet.IsActive())
            {
                return 0.0f;
            }
            return walk.axis.x;
        }

        public float GetJoyLeftStickY()
        {
            if (!mainActionSet.IsActive())
            {
                return 0.0f;
            }
            return -walk.axis.y;
        }

        public float GetJoyRightStickX()
        {
            // Disable rotation if "altPieceRotationControlsActive" is true
            if (!mainActionSet.IsActive() || altPieceRotationControlsActive())
            {
                return 0.0f;
            }
            return pitchAndYaw.axis.x;
        }

        public float GetJoyRightStickY()
        {
            // Even though Y axis is not used for piece rotation with alternative
            // controls, disable it to avoid situations where the player is angling
            // the joystick up/down while trying to rotate causing unintended actions
            if (!mainActionSet.IsActive() || altPieceRotationControlsActive())
            {
                return 0.0f;
            }
            return -pitchAndYaw.axis.y;
        }

        public int getPieceRotation()
        {
            if (!contextScroll.activeBinding)
            {
                // Since we don't have a context scroll bound (becaus of limited input
                // options), we need to control rotation using the right joystick
                // when a special button is held - we are using the Map button for this purpose.
                // As a result, when in "build mode", the map button is disabled for the purpose
                // of bringing up the map and when the player is holding down the map button,
                // then they cannot rotate their character.
                if (altPieceRotationControlsActive())
                {
                    return getAltPieceRotation();
                } else
                {
                    return 0;
                }
            }
            if (contextScroll.axis.y > 0)
            {
                return 1;
            } else if (contextScroll.axis.y < 0)
            {
                return -1;
            } else
            {
                return 0;
            }
        }

        private int getAltPieceRotation()
        {
            float rightStickXAxis = pitchAndYaw.axis.x;
            currentRotationUpdateFrame += 1;
            if (currentRotationUpdateFrame == ALT_PIECE_ROTATION_RESISTOR + 1)
            {
                currentRotationUpdateFrame = 0;
            }
            // Only allow a rotation every ALT_PIECE_ROTATION_RESISTOR updates
            if (currentRotationUpdateFrame != ALT_PIECE_ROTATION_RESISTOR)
            {
                return 0;
            }
            if (rightStickXAxis > 0.1f)
            {
                return -1;
            }
            else if (rightStickXAxis < -0.1f)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }

        private bool inPlaceMode()
        {
            return Player.m_localPlayer != null && Player.m_localPlayer.InPlaceMode();
        }

        private bool hasPlacementGhost()
        {
            if (Player.m_localPlayer == null)
            {
                return false;
            }
            var fieldRef = AccessTools.FieldRefAccess<Player, GameObject>(Player.m_localPlayer, "m_placementGhost");
            return fieldRef != null && fieldRef.activeSelf;
        }

        private bool hasHoverObject()
        {
            if (Player.m_localPlayer == null)
            {
                return false;
            }
            var fieldRef = AccessTools.FieldRefAccess<Player, GameObject>(Player.m_localPlayer, "m_hovering");
            return fieldRef != null;
        }

        // Used to determine when the player is in a mode where the right joystick should
        // be used for rotation of an object while building rather than rotating the
        // player character
        private bool altPieceRotationControlsActive()
        {
            return (!contextScroll.activeBinding) &&
                        inPlaceMode() &&
                        hasPlacementGhost() &&
                        !Hud.IsPieceSelectionVisible() &&
                        SteamVR_Actions.valheim_ToggleMap.GetState(SteamVR_Input_Sources.Any);
        }

        // disable Jump input under certain conditions
        // * In placement mode
        private bool shouldDisableJump()
        {
            return inPlaceMode();
        }

        private void init()
        {
            zInputToBooleanAction.Add("JoyMenu", SteamVR_Actions.valheim_ToggleMenu);
            zInputToBooleanAction.Add("Inventory", SteamVR_Actions.valheim_ToggleInventory);
            zInputToBooleanAction.Add("Jump", SteamVR_Actions.valheim_Jump);
            zInputToBooleanAction.Add("Use", SteamVR_Actions.valheim_Use);
            zInputToBooleanAction.Add("Sit", SteamVR_Actions.valheim_Sit);
            zInputToBooleanAction.Add("Map", SteamVR_Actions.valheim_ToggleMap);

            // These placement commands re-use some of the normal game inputs
            zInputToBooleanAction.Add("BuildMenu", SteamVR_Actions.laserPointers_RightClick);
            zInputToBooleanAction.Add("JoyPlace", SteamVR_Actions.laserPointers_LeftClick);
            zInputToBooleanAction.Add("Remove", SteamVR_Actions.valheim_Jump);

            contextScroll = SteamVR_Actions.valheim_ContextScroll;

            walk = SteamVR_Actions.valheim_Walk;
            pitchAndYaw = SteamVR_Actions.valheim_PitchAndYaw;
            poseL = SteamVR_Actions.valheim_PoseL;
            poseR = SteamVR_Actions.valheim_PoseR;
            initIgnoredZInputs();
        }

        private void initIgnoredZInputs()
        {
            ignoredZInputs.Add("JoyButtonY");
            ignoredZInputs.Add("JoyButtonX");
            ignoredZInputs.Add("JoyButtonA");
            ignoredZInputs.Add("JoyButtonB");
            ignoredZInputs.Add("JoyButtonX");
            ignoredZInputs.Add("JoyLStickLeft");
            ignoredZInputs.Add("JoyHide");
            ignoredZInputs.Add("JoyUse");
            ignoredZInputs.Add("JoyRemove");
            ignoredZInputs.Add("ToggleWalk");
            ignoredZInputs.Add("JoySit");
            ignoredZInputs.Add("JoyGPower");
            ignoredZInputs.Add("JoyJump");
            ignoredZInputs.Add("Attack");
            ignoredZInputs.Add("SecondAttack");
            ignoredZInputs.Add("Crouch");
            ignoredZInputs.Add("Run");
            ignoredZInputs.Add("Crouch");
            ignoredZInputs.Add("AutoRun");
            ignoredZInputs.Add("Forward");
            ignoredZInputs.Add("Backward");
            ignoredZInputs.Add("Left");
            ignoredZInputs.Add("Right");
            ignoredZInputs.Add("Block");
            ignoredZInputs.Add("Hide");
            ignoredZInputs.Add("GPower");
            ignoredZInputs.Add("JoyAttack");
            ignoredZInputs.Add("JoyBlock");
            ignoredZInputs.Add("JoyRotate");
            ignoredZInputs.Add("JoySecondAttack");
            ignoredZInputs.Add("JoyCrouch");
            ignoredZInputs.Add("JoyRun");
            ignoredZInputs.Add("JoyLStickDown");
            ignoredZInputs.Add("JoyDPadDown");
            ignoredZInputs.Add("JoyDPadLeft");
            ignoredZInputs.Add("JoyDPadRight");
            ignoredZInputs.Add("JoyMap");
            ignoredZInputs.Add("JoyLStickUp");
            ignoredZInputs.Add("JoyTabLeft");
            ignoredZInputs.Add("JoyTabRight");
            ignoredZInputs.Add("JoyLStickRight");
            ignoredZInputs.Add("JoyRTrigger");
            ignoredZInputs.Add("JoyLTrigger");
            ignoredZInputs.Add("JoyDPadUp");
            ignoredZInputs.Add("BuildNext");
            ignoredZInputs.Add("BuildPrev");
            ignoredZInputs.Add("AltPlace");
            ignoredZInputs.Add("JoyAltPlace");
        }

    }
}
