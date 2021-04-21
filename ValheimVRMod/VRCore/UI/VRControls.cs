using System.Collections.Generic;
using UnityEngine;
using Valve.VR;
using HarmonyLib;
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
            if (mainActionSet.IsActive() && !VRPlayer.handsActive)
            {
                mainActionSet.Deactivate();
            }
            else if (!mainActionSet.IsActive() && VRPlayer.handsActive)
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
            zInputToBooleanAction.Add("JoyMenu", SteamVR_Input.GetBooleanActionFromPath("/actions/valheim/in/ToggleMenu"));
            zInputToBooleanAction.Add("Inventory", SteamVR_Input.GetBooleanActionFromPath("/actions/valheim/in/ToggleInventory"));
            zInputToBooleanAction.Add("Attack", SteamVR_Input.GetBooleanActionFromPath("/actions/valheim/in/Attack"));
            zInputToBooleanAction.Add("SecondAttack", SteamVR_Input.GetBooleanActionFromPath("/actions/valheim/in/SecondaryAttack"));
            zInputToBooleanAction.Add("Jump", SteamVR_Input.GetBooleanActionFromPath("/actions/valheim/in/Jump"));
            zInputToBooleanAction.Add("Block", SteamVR_Input.GetBooleanActionFromPath("/actions/valheim/in/Block"));
            zInputToBooleanAction.Add("Crouch", SteamVR_Input.GetBooleanActionFromPath("/actions/valheim/in/Crouch"));
            zInputToBooleanAction.Add("Run", SteamVR_Input.GetBooleanActionFromPath("/actions/valheim/in/Run"));
            zInputToBooleanAction.Add("Use", SteamVR_Input.GetBooleanActionFromPath("/actions/valheim/in/Use"));
            zInputToBooleanAction.Add("Hide", SteamVR_Input.GetBooleanActionFromPath("/actions/valheim/in/Hide"));
            zInputToBooleanAction.Add("Sit", SteamVR_Input.GetBooleanActionFromPath("/actions/valheim/in/Sit"));
            zInputToBooleanAction.Add("GPower", SteamVR_Input.GetBooleanActionFromPath("/actions/valheim/in/GPower"));
            zInputToBooleanAction.Add("Map", SteamVR_Input.GetBooleanActionFromPath("/actions/valheim/in/ToggleMap"));

            // These placement commands re-use some of the normal game inputs
            zInputToBooleanAction.Add("BuildMenu", SteamVR_Input.GetBooleanActionFromPath("/actions/laserpointers/in/rightclick"));
            zInputToBooleanAction.Add("Remove", SteamVR_Input.GetBooleanActionFromPath("/actions/valheim/in/SecondaryAttack"));
            zInputToBooleanAction.Add("AltPlace", SteamVR_Input.GetBooleanActionFromPath("/actions/valheim/in/GPower"));

            hotbarDown = SteamVR_Input.GetBooleanActionFromPath("/actions/valheim/in/HotbarDown");
            hotbarUp = SteamVR_Input.GetBooleanActionFromPath("/actions/valheim/in/HotbarUp");
            hotbarScroll = SteamVR_Input.GetVector2ActionFromPath("/actions/valheim/in/HotbarScroll");
            hotbarUse = SteamVR_Input.GetBooleanActionFromPath("/actions/valheim/in/HotbarUse");

            contextScroll = SteamVR_Input.GetVector2ActionFromPath("/actions/Valheim/in/ContextScroll");

            walk = SteamVR_Input.GetVector2ActionFromPath("/actions/Valheim/in/Walk");
            pitchAndYaw = SteamVR_Input.GetVector2ActionFromPath("/actions/Valheim/in/PitchAndYaw");
            poseL = SteamVR_Input.GetPoseActionFromPath("/actions/Valheim/in/PoseL");
            poseR = SteamVR_Input.GetPoseActionFromPath("/actions/Valheim/in/PoseR");
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
            //ignoredZInputs.Add("MapZoomOut");
            //ignoredZInputs.Add("MapZoomIn");
            ignoredZInputs.Add("JoyHide");
            ignoredZInputs.Add("JoyUse");
            ignoredZInputs.Add("JoyRemove");
            ignoredZInputs.Add("ToggleWalk");
            ignoredZInputs.Add("JoySit");
            ignoredZInputs.Add("JoyGPower");
            ignoredZInputs.Add("JoyJump");
            ignoredZInputs.Add("AutoRun");
            ignoredZInputs.Add("Forward");
            ignoredZInputs.Add("Backward");
            ignoredZInputs.Add("Left");
            ignoredZInputs.Add("Right");
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
