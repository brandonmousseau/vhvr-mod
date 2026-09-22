using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.VRCore.UI
{
    /**
     * Applies the laser pointer chord actions:
     * - AddMapPin adds a pin on the large map. It is chorded with the left click.
     * - DiscardItem moves the inventory item under the pointer to the open container (or back from it), or drops it
     *   when no container is open. It is chorded with the left click.
     * - SplitStack splits the inventory item stack under the pointer. It is chorded with the left click.
     * - MiddleClick is sent to the GUI as a middle mouse button (see VRGUI), which vanilla uses to ping on the map,
     *   favorite a build piece and delete a favorite category. It is chorded with the right click.
     *
     * SteamVR still reports the buttons that make up a chord, e.g. the trigger (left click) of a grip + trigger
     * chord, so the game receives both the chord action and the plain click. Everything that turns laser pointer
     * clicks into game input reads the filtered click states from here. A click is hidden where the plain click
     * would fight the chord action: MiddleClick always hides its own, AddMapPin hides its click while the map is
     * open, where it would otherwise drag the map out from under the pin name input, and SplitStack while the
     * inventory is open, where it would otherwise land behind the split dialog. Anywhere else the click goes
     * through - in place mode especially, where the grips are the modifiers of the building controls (the
     * reference plane, snapping off, exclusive snap and the rotation gizmo) and hiding the click would swallow
     * the placement.
     *
     * The decisions are made in SteamVR_Input.onNonVisualActionsUpdated, after all actions have been updated. Action
     * state-down listeners cannot do this since they fire while actions are still being updated, before the chord
     * actions are. Action values can be updated more than once per frame, so edges come from the actions' own
     * per-frame down/up states rather than from comparing with the previous update, and each chord action is applied
     * at most once per frame.
     */
    static class LaserPointerChords
    {
        private const string BUILD_MENU_BUTTON = "BuildMenu";
        private const string PLACE_BUTTON = "JoyPlace";

        private static bool initialized = false;
        // Whether the game has been given the current left/right click press, see OnActionsUpdated().
        private static bool leftClickDelivered;
        private static bool rightClickDelivered;
        private static int lastAddMapPinFrame = -1;
        private static int lastDiscardItemFrame = -1;
        private static int lastSplitStackFrame = -1;

        public static bool isLeftClickSuppressed { get; private set; }
        public static bool isRightClickSuppressed { get; private set; }

        // Laser pointer click states (from any hand) with the suppressed clicks hidden.
        public static bool leftClick { get; private set; }
        public static bool leftClickDown { get; private set; }
        public static bool leftClickUp { get; private set; }
        public static bool rightClick { get; private set; }
        public static bool rightClickDown { get; private set; }
        public static bool rightClickUp { get; private set; }
        public static bool middleClick { get; private set; }

        public static void Initialize()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;
            SteamVR_Input.onNonVisualActionsUpdated += OnActionsUpdated;
        }

        public static bool FilterLeftClick(bool pressed)
        {
            return pressed && !isLeftClickSuppressed;
        }

        public static bool FilterRightClick(bool pressed)
        {
            return pressed && !isRightClickSuppressed;
        }

        // Whether the action is held on a hand whose own laser pointer is not up, for player controls that share
        // their buttons with laser pointer controls on the same hand (e.g. the run and crouch toggles on the stick
        // that scrolls while pointing).
        public static bool IsHeldWithoutLaser(SteamVR_Action_Boolean action)
        {
            return
                (action.GetState(SteamVR_Input_Sources.LeftHand) && !IsLaserActiveFor(SteamVR_Input_Sources.LeftHand)) ||
                (action.GetState(SteamVR_Input_Sources.RightHand) && !IsLaserActiveFor(SteamVR_Input_Sources.RightHand));
        }

        // Whether the given hand's own laser pointer is currently up. "Any" (asked by call sites that don't care
        // which hand) is true when either hand's pointer is active, matching VRControls.laserControlsActive.
        public static bool IsLaserActiveFor(SteamVR_Input_Sources hand)
        {
            switch (hand)
            {
                case SteamVR_Input_Sources.LeftHand:
                    return VRPlayer.leftPointer != null && VRPlayer.leftPointer.pointerIsActive();
                case SteamVR_Input_Sources.RightHand:
                    return VRPlayer.rightPointer != null && VRPlayer.rightPointer.pointerIsActive();
                default:
                    return VRPlayer.activePointer != null;
            }
        }

        private static void OnActionsUpdated()
        {
            SteamVR_Action_Boolean leftClickAction = SteamVR_Actions.valheim_LeftClick;
            SteamVR_Action_Boolean rightClickAction = SteamVR_Actions.valheim_RightClick;

            if (VRControls.laserControlsActive)
            {
                if (Minimap.IsOpen() && SteamVR_Actions.valheim_AddMapPin.GetState(SteamVR_Input_Sources.Any))
                {
                    isLeftClickSuppressed = true;
                }
                if (isChordDown(SteamVR_Actions.valheim_AddMapPin, ref lastAddMapPinFrame) && isPointerOverLargeMap())
                {
                    Minimap.instance.OnMapDblClick();
                    resetMapPointerState();
                }
                if (InventoryGui.IsVisible() && SteamVR_Actions.valheim_SplitStack.GetState(SteamVR_Input_Sources.Any))
                {
                    isLeftClickSuppressed = true;
                }
                if (isChordDown(SteamVR_Actions.valheim_DiscardItem, ref lastDiscardItemFrame))
                {
                    VRGUI.SelectHoveredInventoryItem(InventoryGrid.Modifier.Move);
                }
                if (isChordDown(SteamVR_Actions.valheim_SplitStack, ref lastSplitStackFrame))
                {
                    VRGUI.SelectHoveredInventoryItem(InventoryGrid.Modifier.Split);
                }
                if (SteamVR_Actions.valheim_MiddleClick.GetState(SteamVR_Input_Sources.Any))
                {
                    isRightClickSuppressed = true;
                }
            }
            middleClick = VRControls.laserControlsActive && SteamVR_Actions.valheim_MiddleClick.GetState(SteamVR_Input_Sources.Any);

            // Keep a suppression through the frame its button is released, so that release is hidden too, and so
            // that closing the GUI mid-click cannot leave the game with a button up it never saw go down.
            if (!isHeldOrJustReleased(leftClickAction))
            {
                isLeftClickSuppressed = false;
            }
            if (!isHeldOrJustReleased(rightClickAction))
            {
                isRightClickSuppressed = false;
            }

            // Both clicks live in the single Valheim action set now (there is no more separate, higher priority
            // laser pointer set to silence them while a pointer is inactive), so both are explicitly gated on
            // laserControlsActive here rather than relying on the action itself going quiet - otherwise pulling
            // the same physical trigger for Use/UseLeft while no pointer is up would also register as a click.
            leftClick = VRControls.laserControlsActive && FilterLeftClick(leftClickAction.GetState(SteamVR_Input_Sources.Any));
            leftClickDown = VRControls.laserControlsActive && FilterLeftClick(leftClickAction.GetStateDown(SteamVR_Input_Sources.Any));
            if (leftClickDown)
            {
                leftClickDelivered = true;
            }
            // The release is reported even once the laser controls are gone, so that a press the game has seen
            // cannot be left without its button up, but a press that was hidden here stays hidden on release too.
            leftClickUp = leftClickDelivered && FilterLeftClick(leftClickAction.GetStateUp(SteamVR_Input_Sources.Any));
            if (leftClickUp)
            {
                leftClickDelivered = false;
            }

            rightClick = VRControls.laserControlsActive && FilterRightClick(rightClickAction.GetState(SteamVR_Input_Sources.Any));
            rightClickDown = VRControls.laserControlsActive && FilterRightClick(rightClickAction.GetStateDown(SteamVR_Input_Sources.Any));
            if (rightClickDown)
            {
                rightClickDelivered = true;
            }
            // The release is reported even once the laser controls are gone, so that a press the game has seen
            // cannot be left without its button up, but a press that was hidden here stays hidden on release too.
            rightClickUp = rightClickDelivered && FilterRightClick(rightClickAction.GetStateUp(SteamVR_Input_Sources.Any));
            if (rightClickUp)
            {
                rightClickDelivered = false;
            }

            // Patching ZInput may not be sufficient to emulate button input since Jotunn could undo those patches,
            // so also update the actual button states in ZInput (see VRControls.registerBooleanActionListeners()).
            updateZInputButton(PLACE_BUTTON, leftClickDown, leftClickUp);
            updateZInputButton(BUILD_MENU_BUTTON, rightClickDown, rightClickUp);
        }

        private static bool isChordDown(SteamVR_Action_Boolean chordAction, ref int lastHandledFrame)
        {
            if (!chordAction.GetStateDown(SteamVR_Input_Sources.Any) || lastHandledFrame == Time.frameCount)
            {
                return false;
            }
            lastHandledFrame = Time.frameCount;
            return true;
        }

        private static bool isHeldOrJustReleased(SteamVR_Action_Boolean action)
        {
            return action.GetState(SteamVR_Input_Sources.Any) || action.GetStateUp(SteamVR_Input_Sources.Any);
        }

        private static void updateZInputButton(string buttonName, bool down, bool up)
        {
            if (!VRControls.mainControlsActive)
            {
                return;
            }
            if (down)
            {
                GetButtonPatchUtils.Press(buttonName);
            }
            else if (up)
            {
                GetButtonPatchUtils.Release(buttonName);
            }
        }

        private static bool isPointerOverLargeMap()
        {
            if (!Minimap.IsOpen() || Minimap.instance.m_mapImageLarge == null)
            {
                return false;
            }
            RectTransform map = Minimap.instance.m_mapImageLarge.rectTransform;
            // Test against the canvas camera, like InventoryGrid_GetHoveredElement_Patch, so this agrees with what
            // the EventSystem considers hovered.
            var canvas = map.GetComponentInParent<Canvas>();
            var camera = canvas == null ? null : canvas.rootCanvas.worldCamera;
            return RectTransformUtility.RectangleContainsScreenPoint(map, SoftwareCursor.simulatedMousePosition, camera);
        }

        // A click that was pressed before its chord completed has already reached the map. Without resetting this,
        // Minimap.Update() would start dragging the map, which also hides the pin name input.
        private static void resetMapPointerState()
        {
            Minimap.instance.m_leftDownTime = 0f;
            Minimap.instance.m_dragView = false;
        }
    }
}
