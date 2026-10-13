using ValheimVRMod.VRCore.Backends;
using static ValheimVRMod.Utilities.LogUtils;

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ValheimVRMod.Patches;
using ValheimVRMod.Scripts;
using ValheimVRMod.Utilities;

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
        private static readonly float ALT_MAP_ZOOM_TIME_DELAY = 0.125f;
        private const float AUTORUN_ACTIVATION_DELAY =  0.0625f;
        private const float AUTORUN_DEACTIVATION_DELAY = 0.5f;

        private float altPieceRotationElapsedTime = 0f;
        private bool altPieceTriggered = false;
        private bool wasAltPieceTriggered = false;
        private float leftQuickMenuHoldTime;
        private float rightQuickMenuHoldTime;
        float? altMapZoomInHoldCountdown = null;
        float? altMapZoomOutHoldCountdown = null;

        private HashSet<string> ignoredZInputs = new HashSet<string>();
        private HashSet<string> quickActionEnabled = new HashSet<string>(); // never ignore these
        private VRActionSet mainActionSet = VRInputActions.Valheim;

        // Every zinput used to need an array here: with the laser pointer actions in their own, higher priority
        // action set, SteamVR would disable a lower priority set's action on a physical control that a higher
        // priority action also used, even when the two actions were of completely different types, so some
        // zinputs needed a same-shaped duplicate action bound in the laser pointer set to stay readable while it
        // was active. Now that both live in the single Valheim set, every array here has exactly one element,
        // but the type is kept since a future need for the same duplication elsewhere is not implausible.
        private Dictionary<string, VRBooleanAction[]> zInputToBooleanAction = new Dictionary<string, VRBooleanAction[]>();

        private VRVector2Action walk;
        private VRVector2Action pitchAndYaw;
        private float combinedPitchAndYawX => pitchAndYaw.axis.x * VHVRConfig.TurnAxisModifier();

        private VRVector2Action contextScroll;

        private VRPoseAction poseL;
        private VRPoseAction poseR;

        private float recenteringPoseDuration;

        public static bool mainControlsActive
        {
            get
            {
                return _instance != null && _instance.mainActionSet.IsActive();
            }
        }

        // Whether either hand's laser pointer is currently up. LaserPointers used to be its own, higher priority
        // action set that got activated/deactivated alongside this, which needed compensating for the phantom
        // edges that an action set change causes (see LaserPointerChords.IsLaserActiveFor()). Now that a laser
        // pointer being active no longer changes which action set is live, this is just VRPlayer's own notion of
        // a pointer being active, kept here since call sites already depend on the name.
        public static bool laserControlsActive
        {
            get { return VRPlayer.activePointer != null; }
        }

        // How long the quick menu button has to be held to open the menu instead of counting as a right click.
        private const float QUICK_MENU_HOLD_TIME = 0.3f;
        public static float smoothWalkX { get { return smoothWalkVelocity.x; } }
        public static float smoothWalkY { get { return smoothWalkVelocity.y; } }
        public static bool isAutoRunActive;
        public static bool isExhaustedFromRunning;
        private static Vector2 smoothWalkVelocity;

        public static string ToggleMiniMap { get { return "ToggleMiniMap"; } }

        public static VRControls instance { get { return _instance; } }
        private static VRControls _instance;

        private int quickMenuRefreshTicker = 0;
        private float autorunActivationCountdown = 1;
        private float autorunDeactivationCountdown = 1;

        void Awake()
        {
            init();
            recenteringPoseDuration = 0f;
            _instance = this;
            gameObject.GetOrAddComponent<VoiceChat>();
        }

        void Update()
        {
            updateMainActionSetState();
            if (mainActionSet.IsActive())
            {
                checkRecenterPose(Time.unscaledDeltaTime);
                MissingBindingsPrompt.Update(Time.unscaledDeltaTime);
            }
            if ((mainControlsActive && VRInputActions.valheim_ToggleInventory.GetStateDown(VRInputSource.Any)) ||
                ZInput.GetButtonDown("Inventory") ||
                GetButtonDown("JoyMenu"))
            {
                if (Minimap.IsOpen())
                {
                    Minimap.instance.SetMapMode(Minimap.MapMode.Small);
                }
            }

            if (StaticObjects.rightHandQuickMenu != null && (InventoryGui.IsVisible() || ++quickMenuRefreshTicker > 16))
            {
                quickMenuRefreshTicker = 0;
                StaticObjects.rightHandQuickMenu.GetComponent<RightHandQuickMenu>().refreshItems();
                StaticObjects.leftHandQuickMenu.GetComponent<LeftHandQuickMenu>().refreshItems();
            }

            checkQuickItems<RightHandQuickMenu>(
                StaticObjects.rightHandQuickMenu, VRInputActions.valheim_QuickSwitch, ref rightQuickMenuHoldTime);
            checkQuickItems<LeftHandQuickMenu>(
                StaticObjects.leftHandQuickMenu, VRInputActions.valheim_QuickActions, ref leftQuickMenuHoldTime);

            // Skip while the SteamVR virtual keyboard is driving chat input: that flow submits/
            // cancels via its own keyboard-closed event (see InputManager.OnKeyboardClosed), and
            // this grip-based confirm/cancel gesture handling is for the physical-keyboard flow
            // only - both would otherwise race once the chat window gains focus.
            if (QuickAbstract.shouldStartChat && Chat.instance.HasFocus() && !InputManager.chatKeyboardActive)
            {
                if (VRInputActions.default_GrabGrip.GetState(VRInputSource.Any) ||
                    VRInputActions.valheim_Grab.GetState(VRInputSource.Any)) {
                    if (VRInputActions.default_InteractUI.GetStateUp(VRInputSource.Any) ||
                        VRInputActions.valheim_LeftClick.GetStateUp(VRInputSource.Any) ||
                        VRInputActions.valheim_Use.GetStateUp(VRInputSource.Any))
                    {
                        QuickAbstract.enterChatText();
                    }
                }
                else if (VRInputActions.default_GrabGrip.GetStateUp(VRInputSource.Any) ||
                    VRInputActions.valheim_Grab.GetStateUp(VRInputSource.Any))
                {
                    QuickAbstract.unfocusChatWindow();
                }
            }

        }

        void FixedUpdate()
        {
            updateSmoothWalk(Time.fixedDeltaTime);
            updateAltPieceRotationTimer();
            updateAltMapZoomTimer(Time.fixedDeltaTime);
        }

        private void updateSmoothWalk(float deltaTime)
        {
            Vector2 input = GetJoyLeftStickInput();
            var inputSpeed = input.magnitude;

            if (Player.m_localPlayer != null)
            {
                if (Player.m_localPlayer.HaveStamina())
                {
                    if (isExhaustedFromRunning && inputSpeed < GetAutoRunActiavtionThreshold())
                    {
                        isExhaustedFromRunning = false;
                    }
                }
                else if (isAutoRunActive)
                {
                    isExhaustedFromRunning = true;
                }
            }

            if (autorunActivationCountdown >= 0)
            {
                autorunActivationCountdown -= deltaTime;
            }
            if (autorunDeactivationCountdown >= 0)
            {
                autorunDeactivationCountdown -= deltaTime;
            }

            if (inputSpeed < GetAutoRunActiavtionThreshold())
            {
                autorunActivationCountdown = AUTORUN_ACTIVATION_DELAY;
            }

            if (inputSpeed > GetAutoRunDeactiavtionThreshold())
            {
                autorunDeactivationCountdown = AUTORUN_DEACTIVATION_DELAY;
            }

            if (deltaTime == 0 ||
                smoothWalkVelocity.x == float.NaN ||
                smoothWalkVelocity.y == float.NaN ||
                VHVRConfig.WalkSpeedSmoothener() == 0 ||
                VRInputActions.valheim_Grab.GetState(VRInputSource.Any))
            {
                smoothWalkVelocity = input;
            }
            else
            {
                float relative = Vector2.Dot(input, smoothWalkVelocity) / smoothWalkVelocity.sqrMagnitude;
                if (relative < -0.5 || relative > 1)
                {
                    // The input is either opposite to or larger than the current velocity, update instantly instead of smoothening the velocity.
                    smoothWalkVelocity = input;
                }
                else
                {
                    smoothWalkVelocity =
                        Vector2.MoveTowards(smoothWalkVelocity, input, Time.deltaTime / VHVRConfig.WalkSpeedSmoothener());
                }
            }

            updateAutoRun();
        }

        private void updateAutoRun()
        {
            if (isExhaustedFromRunning)
            {
                isAutoRunActive = false;
                return;
            }

            if (VRPlayer.gesturedLocomotionManager != null && VRPlayer.gesturedLocomotionManager.isRunning)
            {
                isAutoRunActive = true;
                return;
            }

            if (autorunDeactivationCountdown <= 0)
            {
                isAutoRunActive = false;
                return;
            }

            if (VRInputActions.valheim_Grab.GetState(VRInputSource.LeftHand) && VRInputActions.valheim_Grab.GetState(VRInputSource.RightHand))
            {
                isAutoRunActive = false;
                return;
            }

            if (Player.m_localPlayer != null && !Player.m_localPlayer.IsRunning() && autorunActivationCountdown <= 0)
            {
                isAutoRunActive = true;
            }
        }

        private float GetAutoRunActiavtionThreshold()
        {
            float threshold = VHVRConfig.AutoRunThreshold();

            if (threshold > 0.99f)
            {
                return Mathf.Infinity;
            }

            if (GesturedLocomotionManager.isInUse)
            {
                return threshold;
            }

            if (threshold >= 0.75f &
                VRInputActions.valheim_StopGesturedLocomotion.GetState(VRInputSource.LeftHand) &&
                VRInputActions.valheim_StopGesturedLocomotion.GetState(VRInputSource.RightHand))
            {
                return Mathf.Infinity;
            }

            return Mathf.Lerp(threshold, 1, 0.5f);
        }

        private float GetAutoRunDeactiavtionThreshold()
        {
            float threshold = VHVRConfig.AutoRunThreshold();
            if (Player.m_localPlayer != null && !Player.m_localPlayer.IsOnGround())
            {
                return threshold * 0.25f;
            }
            if (GesturedLocomotionManager.isInUse)
            {
                return threshold * 0.5f;
            }
            return threshold;
        }

        private void updateAltPieceRotationTimer()
        {
            altPieceRotationElapsedTime += Time.unscaledDeltaTime;
            if (altPieceRotationElapsedTime >= ALT_PIECE_ROTATION_TIME_DELAY * VHVRConfig.AltPieceRotationDelay() || (combinedPitchAndYawX != 0 && !wasAltPieceTriggered))
            {
                altPieceTriggered = true;
                altPieceRotationElapsedTime = 0f;
            }
            if (combinedPitchAndYawX != 0)
            {
                wasAltPieceTriggered = true;
            }
            else
            {
                altPieceRotationElapsedTime = 0f;
                wasAltPieceTriggered = false;
            }
        }

        private void updateAltMapZoomTimer(float deltaTime)
        {
            if (altMapZoomInHoldCountdown.HasValue)
            {
                bool wasHolding = (altMapZoomInHoldCountdown.Value >= 0);
                altMapZoomInHoldCountdown -= deltaTime;
                if (wasHolding && altMapZoomInHoldCountdown.Value < 0)
                {
                    GetButtonPatchUtils.Release("MapZoomIn");
                }
            }

            if (altMapZoomOutHoldCountdown.HasValue)
            {
                bool wasHolding = (altMapZoomOutHoldCountdown.Value >= 0);
                altMapZoomOutHoldCountdown -= deltaTime;
                if (wasHolding && altMapZoomOutHoldCountdown.Value < 0)
                {
                    GetButtonPatchUtils.Release("MapZoomOut");
                }
            }

            if (altMapZoomInHoldCountdown == null || altMapZoomInHoldCountdown <= -ALT_MAP_ZOOM_TIME_DELAY)
            {
                if (getScrollButtonDirection() > 0)
                {
                    GetButtonPatchUtils.Press("MapZoomIn");
                    altMapZoomInHoldCountdown = ALT_MAP_ZOOM_TIME_DELAY;
                }
                else
                {
                    altMapZoomInHoldCountdown = null;
                }
            }

            if (altMapZoomOutHoldCountdown == null || altMapZoomOutHoldCountdown <= -ALT_MAP_ZOOM_TIME_DELAY)
            {
                if (getScrollButtonDirection() < 0)
                {
                    GetButtonPatchUtils.Press("MapZoomOut");
                    altMapZoomOutHoldCountdown = ALT_MAP_ZOOM_TIME_DELAY;
                }
                else
                {
                    altMapZoomOutHoldCountdown = null;
                }
            }
        }
        
        // Opens the quick menu while its button is held and selects the hovered item when it is released.
        // Limited inputs make the quick menu buttons double as the laser pointers' right click, which the bindings
        // put on the same buttons (see bindings_*.json), so while a pointer is active the menu waits for the button
        // to be held to tell it apart from a click.
        private void checkQuickItems<T>(GameObject obj, VRBooleanAction action, ref float holdTime) where T : QuickAbstract {
            if (!obj) {
                return;
            }

            if (!action.GetState(VRInputSource.Any))
            {
                holdTime = 0;
                if (obj.activeSelf)
                {
                    obj.GetComponent<T>().selectHoveredItem();
                    obj.SetActive(false);
                }
                return;
            }

            if (laserControlsActive)
            {
                // A click that is part of a chord (e.g. the middle click that favorites a build piece, which uses
                // this same button) is not a menu request either, so it must not count towards the hold.
                if (LaserPointerChords.isRightClickSuppressed)
                {
                    obj.SetActive(false);
                    return;
                }
                holdTime += Time.unscaledDeltaTime;
                if (holdTime < QUICK_MENU_HOLD_TIME)
                {
                    return;
                }
            }
            obj.SetActive(true);
        }

        private void checkRecenterPose(float dt)
        {
            if (!VHVRConfig.DisableRecenterPose() && isInRecenterPose())
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
            if (VRInputActions.valheim_Use.GetState(VRInputSource.Any) ||
                VRInputActions.valheim_Grab.GetState(VRInputSource.Any) ||
                VRInputActions.valheim_StopGesturedLocomotion.GetState(VRInputSource.Any))
            {
                return false;
            }
            var hmd = VRBackend.Active.Rig.GetPlayer(VRPlayer.instance).hmdTransform;
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

        public bool GetButtonDown(string zinput)
        {
            if (!mainActionSet.IsActive() || ignoredZInputs.Contains(zinput))
            {
                return false;
            }
            if (zinput == "Jump" && !canJump())
            {
                return false;
            }
            if (zinput == "Remove")
            {
                return InRemovePieceContext() &&
                    VRInputActions.valheim_RemovePiece.GetStateDown(VRInputSource.Any);
            }
            // Interacting with the world is what "Use" means to vanilla (attacking has its own, separate raw
            // action reads that don't go through here), and disabled the same way as the left hand's own interact
            // in HandBasedInteractionPatches: not usable while the right hand's laser pointer is up, even when it
            // and LeftClick happen to be bound to different physical buttons.
            // Also not usable while aiming a weapon, whose triggers then shoot or swing-launch: otherwise shooting
            // at a summon with grip held would also rename it, grip being the alt modifier (see CheckAltButton).
            if (zinput == "Use")
            {
                return !LaserPointerChords.IsLaserActiveFor(VRInputSource.RightHand) &&
                    !LocalWeaponWield.isAiming &&
                    VRInputActions.valheim_Use.GetStateDown(VRInputSource.RightHand);
            }
            if (zinput == "Map") {
                if (VHVRConfig.MinimapPanelPlacement().Equals("Legacy"))
                {
                    // Revert back to using the regular map toggle if the minimap is in legacy mode
                    return GetButtonDown(ToggleMiniMap);
                }
                return false;
            }

            // Handle Map zoom specially using context scroll input
            if (contextScroll.activeBinding)
            {
                if (zinput == "MapZoomOut")
                {
                    return contextScroll.axis.y < 0;
                }
                else if (zinput == "MapZoomIn")
                {
                    return contextScroll.axis.y > 0;
                }
            }
            if (zinput == "JoyPlace")
            {
                return LaserPointerChords.leftClickDown;
            }
            if (zinput == "BuildMenu")
            {
                return LaserPointerChords.rightClickDown;
            }
            VRBooleanAction[] action;
            zInputToBooleanAction.TryGetValue(zinput, out action);
            if (action == null)
            {
                if (!quickActionEnabled.Contains(zinput))
                {
                    LogWarning("Unmapped ZInput Key:" + zinput);
                    ignoredZInputs.Add(zinput); // Don't check for this input again
                }
                return false;
            }
            return action.Any(x => x.GetStateDown(VRInputSource.Any));
        }

        public bool GetButton(string zinput)
        {
            if (!mainActionSet.IsActive() || ignoredZInputs.Contains(zinput))
            {
                return false;
            }
            if (zinput == "Jump" && !canJump())
            {
                return false;
            }
            if (zinput == "Remove")
            {
                return InRemovePieceContext() &&
                    VRInputActions.valheim_RemovePiece.GetState(VRInputSource.Any);
            }
            if (zinput == "Use")
            {
                return !LaserPointerChords.IsLaserActiveFor(VRInputSource.RightHand) &&
                    !LocalWeaponWield.isAiming &&
                    VRInputActions.valheim_Use.GetState(VRInputSource.RightHand);
            }
            if (zinput == "JoyAltPlace")
            {
                return CheckAltButton();
            }
            if (zinput == "JoyPlace")
            {
                return LaserPointerChords.leftClick;
            }
            if (zinput == "BuildMenu")
            {
                return LaserPointerChords.rightClick;
            }
            VRBooleanAction[] action;
            zInputToBooleanAction.TryGetValue(zinput, out action);
            if (action == null)
            {
                if (!quickActionEnabled.Contains(zinput))
                {
                    LogWarning("Unmapped ZInput Key:" + zinput);
                    ignoredZInputs.Add(zinput); // Don't check for this input again
                }
                return false;
            }
            return action.Any(x => x.GetState(VRInputSource.Any));
        }

        private bool CheckAltButton()
        {
            //If both triggers are pressed during this check, the alternate action is enabled
            return (VRInputActions.valheim_Use.GetState(VRInputSource.LeftHand) && VRInputActions.valheim_Use.GetState(VRInputSource.RightHand))
                || (VRInputActions.valheim_Grab.GetState(VRInputSource.RightHand));
        }

        public bool GetButtonUp(string zinput)
        {
            if (!mainActionSet.IsActive() || ignoredZInputs.Contains(zinput))
            {
                return false;
            }
            if (zinput == "Jump" && !canJump())
            {
                return false;
            }
            if (zinput == "Remove")
            {
                return InRemovePieceContext() && VRInputActions.valheim_RemovePiece.GetStateUp(VRInputSource.Any);
            }
            // Read on the right hand like GetButtonDown and GetButton, the left hand having its own interact in
            // HandBasedInteractionPatches. Deliberately not gated on the laser pointer like they are: a pointer
            // coming up mid-hold must not swallow the release of a press the game has already seen.
            if (zinput == "Use")
            {
                return VRInputActions.valheim_Use.GetStateUp(VRInputSource.RightHand);
            }
            if (zinput == "JoyPlace")
            {
                return LaserPointerChords.leftClickUp;
            }
            if (zinput == "BuildMenu")
            {
                return LaserPointerChords.rightClickUp;
            }
            VRBooleanAction[] action;
            zInputToBooleanAction.TryGetValue(zinput, out action);
            if (action == null)
            {
                if (!quickActionEnabled.Contains(zinput))
                {
                    LogWarning("Unmapped ZInput Key:" + zinput);
                    ignoredZInputs.Add(zinput); // Don't check for this input again
                }
                return false;
            }
            return action.Any(x => x.GetStateUp(VRInputSource.Any));
        }
        
        public float GetJoyLeftStickX()
        {
            if (!mainActionSet.IsActive())
            {
                return 0.0f;
            }
            return GetJoyLeftStickInput().x;
        }

        public float GetJoyLeftStickY()
        {
            if (!mainActionSet.IsActive())
            {
                return 0.0f;
            }
            return GetJoyLeftStickInput().y;
        }

        public Vector2 GetJoyLeftStickInput()
        {
            if (!mainActionSet.IsActive())
            {
                return Vector2.zero;
            }

            if (!VHVRConfig.UseLookLocomotion() || Player.m_localPlayer == null || VRPlayer.vrCam == null)
            {
                var input = walk.axis;
                input.y = -input.y;
                return input;
            }

            Transform playerTransform = Player.m_localPlayer.transform;
            Vector3 joystickForward =
                VHVRConfig.GetJoystickForwardDirection(
                    VRPlayer.vrCam.transform,
                    VRPlayer.leftHand?.transform ?? VRPlayer.vrCam.transform,
                    VRPlayer.rightHand?.transform ?? VRPlayer.vrCam.transform,
                    body:
                        VRPlayer.isPelvisTracked && VRPlayer.trackedPelvis != null ?
                        VRPlayer.trackedPelvis : VRPlayer.vrCam.transform,
                    playerTransform);
            Vector3 heading = Vector3.ProjectOnPlane(joystickForward, playerTransform.up).normalized;
            Vector3 right = Vector3.Cross(playerTransform.up, heading);
            Vector3 velocity = right * walk.axis.x + heading * walk.axis.y;
            return new Vector2(Vector3.Dot(velocity, playerTransform.right), -Vector3.Dot(velocity, playerTransform.forward));
        }

        public float GetJoyRightStickX()
        {
            // Disable rotation if "altPieceRotationControlsActive" is true
            if (!mainActionSet.IsActive() || altPieceRotationControlsActive())
            {
                return 0.0f;
            }
            return combinedPitchAndYawX;
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
        public int getDirectPieceRotation()
        {
            if (!altPieceRotationControlsActive() || BuildingManager.instance == null)
            {
                return 999;
            }
            if (-pitchAndYaw.axis.y>0.5f)
            {
                return BuildingManager.instance.TranslateRotation() + 8;
            }
            else if (-pitchAndYaw.axis.y < -0.5f)
            {
                return BuildingManager.instance.TranslateRotation();
            }
            return 999;
        }

        public int getDirectRightYAxis()
        {
            float yAxis = -pitchAndYaw.axis.y;
            if (yAxis > 0.5f)
            {
                return -1;
            }
            else if (yAxis < -0.5f)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }
        public int getDirectRightXAxis()
        {
            float xAxis = -pitchAndYaw.axis.x;
            if (xAxis > 0.5f)
            {
                return -1;
            }
            else if (xAxis < -0.5f)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }

        public int getPieceRotation()
        {
            if (altPieceRotationControlsActive())
            {
                return getAltPieceRotation();
            }
            return 0;
            //context scrolling backup in case needed
            //if (!contextScroll.activeBinding)
            //{
            //    // Since we don't have a context scroll bound (becaus of limited input
            //    // options), we need to control rotation using the right joystick
            //    // when a special button is held - we are using the Map button for this purpose.
            //    // As a result, when in "build mode", the map button is disabled for the purpose
            //    // of bringing up the map and when the player is holding down the map button,
            //    // then they cannot rotate their character.
            //    if (altPieceRotationControlsActive())
            //    {
            //        return getAltPieceRotation();
            //    } else
            //    {
            //        return 0;
            //    }
            //}
            //if (contextScroll.axis.y > 0)
            //{
            //    return 1;
            //} else if (contextScroll.axis.y < 0)
            //{
            //    return -1;
            //} else
            //{
            //    return 0;
            //}
        }

        // The grab button of a hand whose laser pointer is active, which modifies the building controls (the
        // reference plane, snapping off, exclusive snap and the rotation gizmo) while in place mode.
        public bool getClickModifier()
        {
            if (VRPlayer.leftPointer.pointerIsActive() && VRInputActions.valheim_Grab.GetState(VRInputSource.LeftHand))
            {
                return true;
            }
            if (VRPlayer.rightPointer.pointerIsActive() && VRInputActions.valheim_Grab.GetState(VRInputSource.RightHand))
            {
                return true;
            }
            return false;
        }

        private int getAltPieceRotation()
        {
            if (!altPieceTriggered)
            {
                return 0;
            }
            altPieceTriggered = false;
            float rightStickXAxis = combinedPitchAndYawX;
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

        public int getPieceRefModifier()
        {
            float yAxis = GetJoyRightStickY();
            if(yAxis > 0.5f)
            {
                return -1;
            } else if (yAxis < -0.5f)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }

        // The direction held on the ScrollUp/ScrollDown buttons: 1 for up, -1 for down, 0 for neither. These stand
        // in for the ContextScroll trackpad on controllers without one (e.g. bound to right grip + right stick on
        // touch controllers), and zoom the map and scroll the UI under the laser pointer.
        public int getScrollButtonDirection()
        {
            int direction = 0;
            if (VRInputActions.valheim_ScrollUp.GetState(VRInputSource.Any))
            {
                direction++;
            }
            if (VRInputActions.valheim_ScrollDown.GetState(VRInputSource.Any))
            {
                direction--;
            }
            return direction;
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
        // disable context scrolling for now 
        private bool altPieceRotationControlsActive()
        {
            return inPlaceMode() && !Hud.IsPieceSelectionVisible() && VRInputActions.valheim_Grab.GetState(VRInputSource.RightHand);
        }

        private bool InRemovePieceContext()
        {
            return
                inPlaceMode() &&
                BuildingManager.instance != null &&
                !BuildingManager.instance.isCurrentlyMoving() &&
                !BuildingManager.instance.isCurrentlyPreciseMoving() &&
                !BuildingManager.instance.isHoldingPlace();
        }

        private bool canJump()
        {
            if (InRemovePieceContext() &&
                VRInputActions.valheim_RemovePiece.GetState(VRInputSource.Any)) { // Removing piece takes higher priority than jump
                return false;
            }

            if (VRInputActions.valheim_Dodge.GetState(VRInputSource.Any))
            {
                return false;
            }

            if (BuildingManager.instance == null)
            {
                return true;
            }
            
            return
                !BuildingManager.instance.isCurrentlyMoving() &&
                !BuildingManager.instance.isCurrentlyPreciseMoving() &&
                !BuildingManager.instance.isHoldingPlace();
        }

        private void init()
        {
            zInputToBooleanAction.Add("JoyMenu", new[] { VRInputActions.valheim_ToggleMenu });
            zInputToBooleanAction.Add("Inventory", new[] { VRInputActions.valheim_ToggleInventory });
            zInputToBooleanAction.Add("Jump", new [] { VRInputActions.valheim_Jump });
            zInputToBooleanAction.Add("Use", new[] { VRInputActions.valheim_Use });
            zInputToBooleanAction.Add("Sit", new[] { VRInputActions.valheim_Sit });
            zInputToBooleanAction.Add("AutoPickup", new[] { VRInputActions.valheim_ToggleAutoPickup });
            zInputToBooleanAction.Add(ToggleMiniMap, new[] { VRInputActions.valheim_ToggleMap });

            // These placement commands re-use some of the normal game inputs. They are read from the laser pointer
            // clicks as filtered by LaserPointerChords (see GetButton*() and registerBooleanActionListeners()), the
            // entries here only keep them from being treated as unmapped.
            zInputToBooleanAction.Add("BuildMenu", new[] { VRInputActions.valheim_RightClick });
            zInputToBooleanAction.Add("JoyPlace", new[] { VRInputActions.valheim_LeftClick });
            zInputToBooleanAction.Add("Remove", new[] { VRInputActions.valheim_RemovePiece });

            contextScroll = VRInputActions.valheim_ContextScroll;

            walk = VRInputActions.valheim_Walk;
            pitchAndYaw = VRInputActions.valheim_PitchAndYaw;
            poseL = VRInputActions.valheim_PoseL;
            poseR = VRInputActions.valheim_PoseR;
            initIgnoredZInputs();
            initQuickActionOnly();

            // Patching ZInput may not be sufficient to emulate button iput in some cases
            // since Jotunn could undo those patches. In those cases, we need to alter the
            // actual button states in ZInput.
            registerBooleanActionListeners();
            registerContextScrollListener();
            LaserPointerChords.Initialize();
        }

        private void registerBooleanActionListeners()
        {
            foreach (var entry in zInputToBooleanAction)
            {
                var buttonName = entry.Key;
                if (buttonName == "JoyPlace" || buttonName == "BuildMenu")
                {
                    // Pressed and released by LaserPointerChords, which hides clicks that are part of a chord.
                    continue;
                }
                // "Use" is bound on both hands, but only the right hand's press is the world interact that this
                // emulated button stands for: the left hand has its own interact in HandBasedInteractionPatches,
                // so listening on Any here would make the left hand trigger interact twice.
                var listenerSource =
                    buttonName == "Use" ? VRInputSource.RightHand : VRInputSource.Any;
                foreach (var action in entry.Value)
                {
                    // TODO: add listener of map zoom too
                    if (buttonName == "Jump")
                    {
                        action.AddOnStateDownListener(
                            (fromAction, fromSource) => {
                                if (canJump()) GetButtonPatchUtils.Press(buttonName);
                            },
                            listenerSource);
                    }
                    else if (buttonName == "Remove")
                    {
                        action.AddOnStateDownListener(
                            (fromAction, fromSource) => {
                                if (InRemovePieceContext())
                                {
                                    GetButtonPatchUtils.Press(buttonName);
                                }
                            },
                            listenerSource);
                    }
                    else if (buttonName == "Use")
                    {
                        action.AddOnStateDownListener(
                            (fromAction, fromSource) => {
                                // Gated like GetButtonDown("Use"), which this stands in for.
                                if (!LaserPointerChords.IsLaserActiveFor(VRInputSource.RightHand) &&
                                    !LocalWeaponWield.isAiming)
                                {
                                    GetButtonPatchUtils.Press(buttonName);
                                }
                            },
                            listenerSource);
                    }
                    else
                    {
                        action.AddOnStateDownListener(
                            (fromAction, fromSource) => GetButtonPatchUtils.Press(buttonName),
                            listenerSource);
                    }

                    action.AddOnStateUpListener(
                        (fromAction, fromSource) => GetButtonPatchUtils.Release(buttonName),
                        listenerSource);
                }
            }

            VRInputActions.valheim_ToggleMap.AddOnStateDownListener(
                (fromAction, fromSource) => {
                    if (VHVRConfig.MinimapPanelPlacement().Equals("Legacy"))
                        GetButtonPatchUtils.Press("Map");
                },
                VRInputSource.Any);
            VRInputActions.valheim_ToggleMap.AddOnStateUpListener(
                (fromAction, fromSource) => {
                    if (VHVRConfig.MinimapPanelPlacement().Equals("Legacy"))
                        GetButtonPatchUtils.Release("Map");
                },
                VRInputSource.Any);
        }

        private void registerContextScrollListener()
        {
            contextScroll.AddOnChangeListener(
                (fromAction, fromSource, axis, delta) => {
                    if (axis.y <= 0 && delta.y < axis.y)
                    {
                        GetButtonPatchUtils.Release("MapZoomIn");
                    }
                    if (axis.y >= 0 && delta.y > axis.y) {
                        GetButtonPatchUtils.Release("MapZoomOut");
                    }

                    if (axis.y > 0)
                    {
                        GetButtonPatchUtils.Press("MapZoomIn");
                    }
                    else if (axis.y < 0)
                    {
                        GetButtonPatchUtils.Press("MapZoomOut");
                    }
                },
                VRInputSource.Any);
        }

        private void initQuickActionOnly()
        {
            quickActionEnabled.Add("Map");
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
            ignoredZInputs.Add("ChatUp");
            ignoredZInputs.Add("ChatDown");
            ignoredZInputs.Add("ScrollChatUp");
            ignoredZInputs.Add("ScrollChatDown");
        }

    }
}
