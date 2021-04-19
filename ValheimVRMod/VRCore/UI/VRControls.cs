using UnityEngine;
using Valve.VR;
using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.VRCore.UI
{
    class VRControls : MonoBehaviour
    {

        public SteamVR_Behaviour_Pose pose;

        SteamVR_Action_Boolean toggleMenu;
        SteamVR_Action_Boolean toggleInventory;
        SteamVR_Action_Vector2 walk;

        public void Start()
        {
            if (pose == null)
                pose = this.GetComponent<SteamVR_Behaviour_Pose>();
            if (pose == null)
                LogError("No SteamVR_Behaviour_Pose component found on this object: " + this);
            LogDebug("Activating Action Set");
            SteamVR_Actions.Valheim.Activate(pose.inputSource);
            toggleMenu = SteamVR_Input.GetBooleanActionFromPath("/actions/valheim/in/togglemenu");
            toggleInventory = SteamVR_Input.GetBooleanActionFromPath("/actions/valheim/in/toggleinventory");
            walk = SteamVR_Input.GetVector2ActionFromPath("/actions/Valheim/in/Walk");
        }

        public void Update()
        {
            if (toggleMenu.GetStateUp(pose.inputSource))
            {
                LogDebug("Toggle Menu");
            }
            if (toggleInventory.GetStateUp(pose.inputSource))
            {
                LogDebug("Toggle Inventory");
            }
            if (walk.GetAxis(pose.inputSource) != Vector2.zero)
            {
                LogDebug("Walk Axis: " + walk.GetAxis(pose.inputSource));
            }
        }

    }
}
