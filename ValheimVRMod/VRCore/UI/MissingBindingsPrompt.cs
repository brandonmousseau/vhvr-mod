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
        // The poses are not in here: missing hand tracking is self evident, and they serve as the sign that the binding
        // is in a state fit for checking instead (see IsReadyToCheck()).
        private static readonly SteamVR_Action[] ESSENTIAL_ACTIONS = {
            SteamVR_Actions.valheim_Walk,
            SteamVR_Actions.valheim_PitchAndYaw,
            SteamVR_Actions.valheim_Grab,
            // TODO: find out why chords have false activeBinding
            // SteamVR_Actions.valheim_ToggleMenu,
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

        // Actions bound on both hands, where each hand is separately essential. These cannot go in the list above:
        // activeBinding is a shortcut to the Any source, which cannot tell an action bound on one hand only from one
        // bound on both, so a binding that leaves a single hand out would go unreported.
        private static readonly SteamVR_Action_Boolean[] TWO_HANDED_ACTIONS = {
            SteamVR_Actions.valheim_Use,
        };

        private static readonly SteamVR_Input_Sources[] HANDS = {
            SteamVR_Input_Sources.LeftHand,
            SteamVR_Input_Sources.RightHand,
        };

        // SteamVR loads the bindings asynchronously, and an action only reports its binding once a device it is bound
        // to is connected, so the check waits until both controllers and both poses have been ready for a while.
        private const float SETTLE_TIME = 5f;

        private static float readyTime;
        private static bool hasChecked;
        // The result of the check, latched until the popup can be shown: null when nothing essential is missing.
        private static string missingActionList;
        private static bool canClickPopup;

        // Called every frame while the Valheim action set is active.
        public static void Update(float deltaTime)
        {
            if (!hasChecked)
            {
                if (!IsReadyToCheck())
                {
                    readyTime = 0;
                    return;
                }
                readyTime += deltaTime;
                if (readyTime < SETTLE_TIME)
                {
                    return;
                }
                hasChecked = true;
                CheckBindings();
            }

            if (missingActionList == null || !UnifiedPopup.IsAvailable() || UnifiedPopup.IsVisible())
            {
                return;
            }
            string actionList = missingActionList;
            missingActionList = null;

            if (!canClickPopup)
            {
                SteamVR_Input.OpenBindingUI(SteamVR_Actions.Valheim);
                return;
            }

            UnifiedPopup.Push(new YesNoPopup(
                "Missing controller bindings",
                "Your SteamVR controller binding has nothing bound to: " + actionList + ". " +
                "This usually happens with a custom binding saved for an older version of the mod.\n\n" +
                "Open the SteamVR binding settings to bind them, or to switch back to the default binding?",
                () => {
                    UnifiedPopup.Pop();
                    SteamVR_Input.OpenBindingUI(SteamVR_Actions.Valheim);
                },
                () => UnifiedPopup.Pop(),
                localizeText: false));
        }

        // Unbound poses mean either a binding that is plainly broken, which the player notices without being told, or
        // one that is not fully loaded yet, in which case the other actions cannot be judged either.
        private static bool IsReadyToCheck()
        {
            return IsControllerConnected(ETrackedControllerRole.LeftHand) &&
                IsControllerConnected(ETrackedControllerRole.RightHand) &&
                SteamVR_Actions.valheim_PoseL.activeBinding &&
                SteamVR_Actions.valheim_PoseR.activeBinding;
        }

        private static void CheckBindings()
        {
            IEnumerable<SteamVR_Action> unboundActions = ESSENTIAL_ACTIONS.Where(action => !action.activeBinding);
            if (!SteamVR_Actions.valheim_ContextScroll.activeBinding)
            {
                // TODO: find out why chords have false activeBinding
                // unboundActions = unboundActions.Concat(SCROLL_BUTTON_ACTIONS.Where(action => !action.activeBinding));
            }
            string[] missingActions = unboundActions.Select(action => action.GetShortName())
                .Concat(TWO_HANDED_ACTIONS.SelectMany(
                    action => HANDS
                        .Where(hand => !action[hand].activeBinding)
                        .Select(hand => action.GetShortName() + GetHandSuffix(hand))))
                .ToArray();
            if (missingActions.Length == 0)
            {
                return;
            }
            missingActionList = string.Join(", ", missingActions);
            LogWarning("The current SteamVR binding leaves essential actions unbound: " + missingActionList);
            // Without the laser click the popup could not be clicked, so the binding UI is opened in its place.
            canClickPopup = SteamVR_Actions.valheim_LeftClick.activeBinding;
        }

        private static string GetHandSuffix(SteamVR_Input_Sources hand)
        {
            return hand == SteamVR_Input_Sources.LeftHand ? " (left hand)" : " (right hand)";
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
