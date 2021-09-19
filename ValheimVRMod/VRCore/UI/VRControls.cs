﻿using System.Collections.Generic;
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
        private static readonly float ALT_PIECE_ROTATION_TIME_DELAY = 0.250f;

        private float altPieceRotationElapsedTime = 0f;
        private bool altPieceTriggered = false;

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

        // An input where the user holds down the button when clicking for an alternate behavior (ie, stack split)
        private SteamVR_Action_Boolean _clickModifier = SteamVR_Actions.laserPointers_ClickModifier;

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

            checkQuickItems<QuickSwitch>(StaticObjects.quickSwitch, SteamVR_Actions.valheim_QuickSwitch, true);
            checkQuickItems<QuickActions>(StaticObjects.quickActions, SteamVR_Actions.valheim_QuickActions, false);
        }

        public void FixedUpdate()
        {
            updateAltPieceRotataionTimer();
        }

        private void updateAltPieceRotataionTimer()
        {
            altPieceRotationElapsedTime += Time.deltaTime;
            if (altPieceRotationElapsedTime >= ALT_PIECE_ROTATION_TIME_DELAY * VHVRConfig.AltPieceRotationDelay())
            {
                altPieceTriggered = true;
                altPieceRotationElapsedTime = 0f;
            }
        }
        
        private void checkQuickItems<T>(GameObject obj, SteamVR_Action_Boolean action, bool useRightClick) where T : QuickAbstract {
            
            if (!obj) {
                return;
            }

            // Due to complicated bindings/limited inputs, the QuickSwitch and Right click are sharing a button
            // and when the hammer is equipped, the bindings conflict... so we'll share the right click button
            // here to activate quick switch. This is hacky because rebinding things can break the controls, but
            // it works and allows users to use the quick select while the hammer is equipped.
            bool rightClickDown = false;
            bool rightClickUp = false;
            if (useRightClick && laserControlsActive && inPlaceMode())
            {
                rightClickDown = SteamVR_Actions.laserPointers_RightClick.GetStateDown(SteamVR_Input_Sources.Any);
                rightClickUp = SteamVR_Actions.laserPointers_RightClick.GetStateUp(SteamVR_Input_Sources.Any);
            }
            
            if (action.GetStateDown(SteamVR_Input_Sources.Any) || rightClickDown) {
                obj.SetActive(true);
            }

            if (action.GetStateUp(SteamVR_Input_Sources.Any) || rightClickUp) {
                obj.GetComponent<T>().selectHoveredItem();
                obj.SetActive(false);
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
            if (!useVrControls && mainActionSet.IsActive())
            {
                mainActionSet.Deactivate();
            }
            else if (useVrControls && !mainActionSet.IsActive())
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
            if (zinput == "Map" && QuickActions.toggleMap) {
                QuickActions.toggleMap = false;
                return true;
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
            if(zinput == "JoyAltPlace")
            {
                return CheckAltButton();
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

        private bool CheckAltButton()
        {
            //If both triggers are pressed during this check, the alternate action is enabled
            return (SteamVR_Actions.valheim_Use.GetState(SteamVR_Input_Sources.Any) && SteamVR_Actions.valheim_UseLeft.GetState(SteamVR_Input_Sources.Any))
                || (SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand));
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

        public bool getClickModifier()
        {
            return _clickModifier.GetState(SteamVR_Input_Sources.Any);
        }

        private int getAltPieceRotation()
        {
            if (!altPieceTriggered)
            {
                return 0;
            }
            altPieceTriggered = false;
            float rightStickXAxis = pitchAndYaw.axis.x;
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
            var ghost = Player.m_localPlayer.m_placementGhost;
            return ghost != null && ghost.activeSelf;
        }

        private bool hasHoverObject()
        {
            if (Player.m_localPlayer == null)
            {
                return false;
            }
            return Player.m_localPlayer.m_hovering != null;
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
                        SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand);
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
            ignoredZInputs.Add("AutoPickup");
            ignoredZInputs.Add("ChatUp");
            ignoredZInputs.Add("ChatDown");
            ignoredZInputs.Add("ScrollChatUp");
            ignoredZInputs.Add("ScrollChatDown");
        }

    }
}
