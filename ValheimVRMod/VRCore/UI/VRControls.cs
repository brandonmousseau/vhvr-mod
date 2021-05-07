using System.Collections.Generic;
using UnityEngine;
using Valve.VR;
using HarmonyLib;
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

        private HashSet<string> ignoredZInputs = new HashSet<string>();
        private SteamVR_ActionSet mainActionSet = SteamVR_Actions.Valheim;
        private SteamVR_ActionSet laserActionSet = SteamVR_Actions.LaserPointers;
        private Dictionary<string, SteamVR_Action_Boolean> zInputToBooleanAction = new Dictionary<string, SteamVR_Action_Boolean>();

        private SteamVR_Action_Vector2 walk;
        private SteamVR_Action_Vector2 pitchAndYaw;

        private SteamVR_Action_Boolean hotbarDown;
        private SteamVR_Action_Boolean hotbarUp;
        private SteamVR_Action_Vector2 hotbarScroll;
        private SteamVR_Action_Boolean hotbarUse;

        private SteamVR_Action_Vector2 contextScroll;

        private SteamVR_Action_Pose poseL;
        private SteamVR_Action_Pose poseR;

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
            if (!mainActionSet.IsActive())
            {
                return 0.0f;
            }
            return pitchAndYaw.axis.x;
        }

        public float GetJoyRightStickY()
        {
            if (!mainActionSet.IsActive())
            {
                return 0.0f;
            }
            return -pitchAndYaw.axis.y;
        }

        public int getHotbarScrollUpdate()
        {
            if (hotbarUp.GetStateDown(SteamVR_Input_Sources.Any))
            {
                return 1;
            } else if (hotbarDown.GetStateDown(SteamVR_Input_Sources.Any))
            {
                return -1;
            } else if (hotbarScroll.axis.y > 0)
            {
                return 1;
            } else if (hotbarScroll.axis.y < 0) {
                return -1;
            } else
            {
                return 0;
            }
        }

        public bool getHotbarUseInput()
        {
            if (hotbarUse.activeBinding)
            {
                return hotbarUse.GetStateDown(SteamVR_Input_Sources.Any);
            } else
            {
                return !hasHoverObject() && GetButtonDown("Use");
            }
        }

        public int getPieceRotation()
        {
            if (!contextScroll.activeBinding)
            {
                return 0;
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

        private bool hasHoverObject()
        {
            if (Player.m_localPlayer == null)
            {
                return false;
            }
            var fieldRef = AccessTools.FieldRefAccess<Player, GameObject>(Player.m_localPlayer, "m_hovering");
            return fieldRef != null;
        }

        private void init()
        {
            zInputToBooleanAction.Add("JoyMenu", SteamVR_Actions.valheim_ToggleMenu);
            zInputToBooleanAction.Add("Inventory", SteamVR_Actions.valheim_ToggleInventory);
            zInputToBooleanAction.Add("Jump", SteamVR_Actions.valheim_Jump);
            zInputToBooleanAction.Add("Use", SteamVR_Actions.valheim_Use);
            zInputToBooleanAction.Add("Sit", SteamVR_Actions.valheim_Sit);
            zInputToBooleanAction.Add("GPower", SteamVR_Actions.valheim_GPower);
            zInputToBooleanAction.Add("Map", SteamVR_Actions.valheim_ToggleMap);

            // These placement commands re-use some of the normal game inputs
            zInputToBooleanAction.Add("BuildMenu", SteamVR_Actions.laserPointers_RightClick);
            zInputToBooleanAction.Add("Remove", SteamVR_Actions.valheim_Jump);
            zInputToBooleanAction.Add("AltPlace", SteamVR_Actions.valheim_GPower);

            hotbarDown = SteamVR_Actions.valheim_HotbarDown;
            hotbarUp = SteamVR_Actions.valheim_HotbarUp;
            hotbarScroll = SteamVR_Actions.valheim_HotbarScroll;
            hotbarUse = SteamVR_Actions.valheim_HotbarUse;

            contextScroll = SteamVR_Actions.valheim_ContextScroll;

            walk = SteamVR_Actions.valheim_Walk;
            pitchAndYaw = SteamVR_Actions.valheim_PitchAndYaw;
            poseL = SteamVR_Actions.valheim_PoseL;
            poseR = SteamVR_Actions.valheim_PoseR;
            //crouch = SteamVR_Actions.valheim_Crouch;
            //run = SteamVR_Actions.valheim_Run;
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
            ignoredZInputs.Add("JoyPlace");
        }

    }
}
