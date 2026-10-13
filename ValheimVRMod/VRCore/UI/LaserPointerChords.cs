using ValheimVRMod.VRCore.Backends;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

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
     * The decisions are made in VRInput.onNonVisualActionsUpdated, after all actions have been updated. Action
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
        // The frame a click release was last reported in. OnActionsUpdated() runs more than once per frame, and the
        // later runs see the release edge again but with the press no longer marked as delivered, so without this
        // they would take back the release before the game has read it.
        private static int leftClickUpFrame = -1;
        private static int rightClickUpFrame = -1;
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

        // The action to read the left click from. A binding that leaves LeftClick unbound, usually a custom one saved
        // for an older version of the mod, falls back to Use, which the default bindings put on the same triggers.
        // This keeps the laser pointer usable, and with it the popup of MissingBindingsPrompt that tells the player
        // what is wrong. Only a LeftClick that is unbound on both hands falls back: it is read per hand in places
        // (e.g. only the dominant hand places a build piece), and Use is bound on both.
        public static VRBooleanAction leftClickAction
        {
            get
            {
                return VRInputActions.valheim_LeftClick.activeBinding ?
                    VRInputActions.valheim_LeftClick : VRInputActions.valheim_Use;
            }
        }

        public static void Initialize()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;
            VRInput.onNonVisualActionsUpdated += OnActionsUpdated;
        }

        public static bool FilterLeftClick(bool pressed)
        {
            return pressed && !isLeftClickSuppressed;
        }

        public static bool FilterRightClick(bool pressed)
        {
            return pressed && !isRightClickSuppressed;
        }

        // Whether the action is held on a hand that without scrolling with its laser pointer, for player controls that
        // share their buttons with the ScrollUp/ScrollDown chords (e.g. the run and crouch toggles on the Touch right
        // stick, whose grip + stick chords scroll). SteamVR still reports the stick direction of a chord, so without
        // this, scrolling would also toggle run or crouch. Pointing alone doesn't block them.
        public static bool IsHeldWithoutLaserScroll(VRBooleanAction action)
        {
            return
                (action.GetState(VRInputSource.LeftHand) && !IsScrollingWithLaser(VRInputSource.LeftHand)) ||
                (action.GetState(VRInputSource.RightHand) && !IsScrollingWithLaser(VRInputSource.RightHand));
        }

        // Whether the given hand's laser pointer is up while a scroll chord is held. The chords are read on Any,
        // since a chord's source hand isn't necessarily the hand of its stick.
        public static bool IsScrollingWithLaser(VRInputSource hand)
        {
            return
                IsLaserActiveFor(hand) &&
                (VRInputActions.valheim_ScrollUp.GetState(VRInputSource.Any) ||
                    VRInputActions.valheim_ScrollDown.GetState(VRInputSource.Any));
        }

        // Whether the given hand's own laser pointer is currently up. "Any" (asked by call sites that don't care
        // which hand) is true when either hand's pointer is active, matching VRControls.laserControlsActive.
        public static bool IsLaserActiveFor(VRInputSource hand)
        {
            switch (hand)
            {
                case VRInputSource.LeftHand:
                    return VRPlayer.leftPointer != null && VRPlayer.leftPointer.pointerIsActive();
                case VRInputSource.RightHand:
                    return VRPlayer.rightPointer != null && VRPlayer.rightPointer.pointerIsActive();
                default:
                    return VRPlayer.activePointer != null;
            }
        }

        private static void OnActionsUpdated()
        {
            VRBooleanAction leftClickAction = LaserPointerChords.leftClickAction;
            VRBooleanAction rightClickAction = VRInputActions.valheim_RightClick;

            if (VRControls.laserControlsActive)
            {
                if (Minimap.IsOpen() && VRInputActions.valheim_AddMapPin.GetState(VRInputSource.Any))
                {
                    isLeftClickSuppressed = true;
                }
                if (isChordDown(VRInputActions.valheim_AddMapPin, ref lastAddMapPinFrame) && isPointerOverLargeMap())
                {
                    Minimap.instance.OnMapDblClick();
                    resetMapPointerState();
                }
                if (InventoryGui.IsVisible() && VRInputActions.valheim_SplitStack.GetState(VRInputSource.Any))
                {
                    isLeftClickSuppressed = true;
                }
                if (isChordDown(VRInputActions.valheim_DiscardItem, ref lastDiscardItemFrame))
                {
                    VRGUI.SelectHoveredInventoryItem(InventoryGrid.Modifier.Move);
                }
                if (isChordDown(VRInputActions.valheim_SplitStack, ref lastSplitStackFrame))
                {
                    VRGUI.SelectHoveredInventoryItem(InventoryGrid.Modifier.Split);
                }
                if (VRInputActions.valheim_MiddleClick.GetState(VRInputSource.Any))
                {
                    isRightClickSuppressed = true;
                }
            }
            middleClick = VRControls.laserControlsActive && VRInputActions.valheim_MiddleClick.GetState(VRInputSource.Any);

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
            // the same physical trigger for Use while no pointer is up would also register as a click.
            leftClick = VRControls.laserControlsActive && FilterLeftClick(leftClickAction.GetState(VRInputSource.Any));
            leftClickDown = VRControls.laserControlsActive && FilterLeftClick(leftClickAction.GetStateDown(VRInputSource.Any));
            if (leftClickDown)
            {
                leftClickDelivered = true;
            }
            // The release is reported even once the laser controls are gone, so that a press the game has seen
            // cannot be left without its button up, but a press that was hidden here stays hidden on release too.
            if (leftClickUpFrame != Time.frameCount)
            {
                leftClickUp = leftClickDelivered && FilterLeftClick(leftClickAction.GetStateUp(VRInputSource.Any));
                if (leftClickUp)
                {
                    leftClickDelivered = false;
                    leftClickUpFrame = Time.frameCount;
                }
            }

            rightClick = VRControls.laserControlsActive && FilterRightClick(rightClickAction.GetState(VRInputSource.Any));
            rightClickDown = VRControls.laserControlsActive && FilterRightClick(rightClickAction.GetStateDown(VRInputSource.Any));
            if (rightClickDown)
            {
                rightClickDelivered = true;
            }
            // The release is reported even once the laser controls are gone, so that a press the game has seen
            // cannot be left without its button up, but a press that was hidden here stays hidden on release too.
            if (rightClickUpFrame != Time.frameCount)
            {
                rightClickUp = rightClickDelivered && FilterRightClick(rightClickAction.GetStateUp(VRInputSource.Any));
                if (rightClickUp)
                {
                    rightClickDelivered = false;
                    rightClickUpFrame = Time.frameCount;
                }
            }

            // Patching ZInput may not be sufficient to emulate button input since Jotunn could undo those patches,
            // so also update the actual button states in ZInput (see VRControls.registerBooleanActionListeners()).
            updateZInputButton(PLACE_BUTTON, leftClickDown, leftClickUp);
            updateZInputButton(BUILD_MENU_BUTTON, rightClickDown, rightClickUp);
        }

        private static bool isChordDown(VRBooleanAction chordAction, ref int lastHandledFrame)
        {
            if (!chordAction.GetStateDown(VRInputSource.Any) || lastHandledFrame == Time.frameCount)
            {
                return false;
            }
            lastHandledFrame = Time.frameCount;
            return true;
        }

        private static bool isHeldOrJustReleased(VRBooleanAction action)
        {
            return action.GetState(VRInputSource.Any) || action.GetStateUp(VRInputSource.Any);
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
