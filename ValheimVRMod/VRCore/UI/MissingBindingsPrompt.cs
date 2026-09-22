using System.Collections.Generic;
using System.Linq;
using Valve.VR;
using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.VRCore.UI
{
    /**
     * Checks once per session whether the SteamVR binding in use leaves any essential action unbound, which usually
     * means a custom binding saved for an older version of the mod whose actions have since changed. If so, it offers
     * to open the SteamVR binding UI, where the missing actions can be bound or the default binding selected again.
     *
     * The popup itself is clicked with the laser pointer, so when the laser click is among the missing actions the
     * binding UI is opened right away instead: the binding UI is operated with SteamVR's own pointer and does not
     * depend on the game's bindings.
     */
    static class MissingBindingsPrompt
    {
        // Actions the game cannot be played without, all bound by every default binding shipped with the mod. This is
        // deliberately not the "mandatory" flag of actions.json, which also covers actions with a fallback, e.g.
        // ToggleRun falls back to the stick and is left unbound by the default holographic binding.
        private static readonly SteamVR_Action[] ESSENTIAL_ACTIONS = {
            SteamVR_Actions.valheim_PoseL,
            SteamVR_Actions.valheim_PoseR,
            SteamVR_Actions.valheim_Walk,
            SteamVR_Actions.valheim_PitchAndYaw,
            SteamVR_Actions.valheim_Use,
            SteamVR_Actions.valheim_UseLeft,
            SteamVR_Actions.valheim_Grab,
            SteamVR_Actions.valheim_ToggleMenu,
            SteamVR_Actions.valheim_ToggleInventory,
            SteamVR_Actions.valheim_Jump,
            SteamVR_Actions.valheim_LeftClick,
            SteamVR_Actions.valheim_RightClick,
        };

        // Scrolling the UI and zooming the map is done either with the ContextScroll trackpad or, on controllers
        // without one, with the ScrollUp/ScrollDown buttons standing in for it, so neither is essential on its own:
        // these are only reported as missing when ContextScroll is unbound as well.
        private static readonly SteamVR_Action[] SCROLL_BUTTON_ACTIONS = {
            SteamVR_Actions.valheim_ScrollUp,
            SteamVR_Actions.valheim_ScrollDown,
        };

        // SteamVR loads the bindings asynchronously, and an action only reports its binding once a device it is bound
        // to is connected, so the check waits until both controllers have been connected for a while.
        private const float SETTLE_TIME = 5f;

        private static float controllersConnectedTime;
        private static bool hasChecked;

        // Called every frame while the Valheim action set is active.
        public static void Update(float deltaTime)
        {
            if (hasChecked)
            {
                return;
            }
            if (!IsControllerConnected(ETrackedControllerRole.LeftHand) || !IsControllerConnected(ETrackedControllerRole.RightHand))
            {
                controllersConnectedTime = 0;
                return;
            }
            controllersConnectedTime += deltaTime;
            if (controllersConnectedTime < SETTLE_TIME || !UnifiedPopup.IsAvailable() || UnifiedPopup.IsVisible())
            {
                return;
            }
            hasChecked = true;

            IEnumerable<SteamVR_Action> unboundActions = ESSENTIAL_ACTIONS.Where(action => !action.activeBinding);
            if (!SteamVR_Actions.valheim_ContextScroll.activeBinding)
            {
                unboundActions = unboundActions.Concat(SCROLL_BUTTON_ACTIONS.Where(action => !action.activeBinding));
            }
            string[] missingActions = unboundActions.Select(action => action.GetShortName()).ToArray();
            if (missingActions.Length == 0)
            {
                return;
            }
            string missingActionList = string.Join(", ", missingActions);
            LogWarning("The current SteamVR binding leaves essential actions unbound: " + missingActionList);

            if (!SteamVR_Actions.valheim_LeftClick.activeBinding)
            {
                // The popup could not be clicked.
                SteamVR_Input.OpenBindingUI(SteamVR_Actions.Valheim);
                return;
            }

            UnifiedPopup.Push(new YesNoPopup(
                "Missing controller bindings",
                "Your SteamVR controller binding has nothing bound to: " + missingActionList + ". " +
                "This usually happens with a custom binding saved for an older version of the mod.\n\n" +
                "Open the SteamVR binding settings to bind them, or to switch back to the default binding?",
                () => {
                    UnifiedPopup.Pop();
                    SteamVR_Input.OpenBindingUI(SteamVR_Actions.Valheim);
                },
                () => UnifiedPopup.Pop(),
                localizeText: false));
        }

        private static bool IsControllerConnected(ETrackedControllerRole role)
        {
            if (OpenVR.System == null)
            {
                return false;
            }
            uint deviceIndex = OpenVR.System.GetTrackedDeviceIndexForControllerRole(role);
            return deviceIndex != OpenVR.k_unTrackedDeviceIndexInvalid && OpenVR.System.IsTrackedDeviceConnected(deviceIndex);
        }
    }
}
