using System.Collections.Generic;
using UnityEngine;
using Valve.VR;
using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.VRCore.UI
{
    class VRControls : MonoBehaviour
    {

        private HashSet<string> ignoredZInputs = new HashSet<string>();
        private SteamVR_ActionSet actionSet = SteamVR_Actions.Valheim;
        private Dictionary<string, SteamVR_Action_Boolean> zInputToBooleanAction = new Dictionary<string, SteamVR_Action_Boolean>();

        private SteamVR_Action_Vector2 walk;
        private SteamVR_Action_Vector2 pitchAndYaw;
        public static VRControls instance { get { return _instance; } }
        private static VRControls _instance;
        public void Awake()
        {
            init();
            _instance = this;
        }

        public void Update()
        {
            if (actionSet.IsActive() && !VRPlayer.handsActive)
            {
                actionSet.Deactivate();
            }
            else if (!actionSet.IsActive() && VRPlayer.handsActive)
            {
                actionSet.Activate();
            }
        }

        public bool GetButtonDown(string zinput)
        {
            if (!actionSet.IsActive() || ignoredZInputs.Contains(zinput))
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
            return action.GetStateDown(SteamVR_Input_Sources.Any);
        }

        public bool GetButton(string zinput)
        {
            if (!actionSet.IsActive() || ignoredZInputs.Contains(zinput))
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
            if (!actionSet.IsActive() || ignoredZInputs.Contains(zinput))
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
            if (!actionSet.IsActive())
            {
                return 0.0f;
            }
            return walk.axis.x;
        }

        public float GetJoyLeftStickY()
        {
            if (!actionSet.IsActive())
            {
                return 0.0f;
            }
            return -walk.axis.y;
        }

        public float GetJoyRightStickX()
        {
            if (!actionSet.IsActive())
            {
                return 0.0f;
            }
            return pitchAndYaw.axis.x;
        }

        public float GetJoyRightStickY()
        {
            if (!actionSet.IsActive())
            {
                return 0.0f;
            }
            return pitchAndYaw.axis.y;
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
            zInputToBooleanAction.Add("JoyDPadLeft", SteamVR_Input.GetBooleanActionFromPath("/actions/valheim/in/HotbarDown"));
            zInputToBooleanAction.Add("JoyDPadRight", SteamVR_Input.GetBooleanActionFromPath("/actions/valheim/in/HotbarUp"));

            walk = SteamVR_Input.GetVector2ActionFromPath("/actions/Valheim/in/Walk");
            pitchAndYaw = SteamVR_Input.GetVector2ActionFromPath("/actions/Valheim/in/PitchAndYaw");
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
            ignoredZInputs.Add("MapZoomOut");
            ignoredZInputs.Add("MapZoomIn");
            ignoredZInputs.Add("JoyHide");
            ignoredZInputs.Add("JoyUse");
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
            ignoredZInputs.Add("JoyPlace");
            ignoredZInputs.Add("JoyRemove")
            ignoredZInputs.Add("JoySecondAttack");
            ignoredZInputs.Add("JoyCrouch");
            ignoredZInputs.Add("JoyRun");
            ignoredZInputs.Add("JoyLStickDown");
            ignoredZInputs.Add("JoyDPadDown");
            ignoredZInputs.Add("JoyDPadUp"); // maybe need
        }

    }
}
