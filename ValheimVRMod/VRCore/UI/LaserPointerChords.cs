using UnityEngine;
using ValheimVRMod.Utilities;
using Valve.VR;

namespace ValheimVRMod.VRCore.UI
{
    /**
     * Applies the laser pointer chord actions (AddMapPin, SendMapPing, FavoriteBuildPiece) and hides the clicks
     * they are chorded with from the game.
     *
     * SteamVR still reports the buttons that make up a chord, e.g. the trigger (left click) of a grip + trigger
     * chord, so without this the game would receive both the chord action and the plain click. When a chord
     * action applies, the click button it is chorded with is suppressed until that button is released, and
     * everything that turns laser pointer clicks into game input reads the filtered click states from here.
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
        private static int lastAddMapPinFrame = -1;
        private static int lastSendMapPingFrame = -1;
        private static int lastFavoriteBuildPieceFrame = -1;

        public static bool isLeftClickSuppressed { get; private set; }
        public static bool isRightClickSuppressed { get; private set; }

        // Laser pointer click states (from any hand) with the suppressed clicks hidden.
        public static bool leftClick { get; private set; }
        public static bool leftClickDown { get; private set; }
        public static bool leftClickUp { get; private set; }
        public static bool rightClick { get; private set; }
        public static bool rightClickDown { get; private set; }
        public static bool rightClickUp { get; private set; }

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

        private static void OnActionsUpdated()
        {
            SteamVR_Action_Boolean leftClickAction = SteamVR_Actions.laserPointers_LeftClick;
            SteamVR_Action_Boolean rightClickAction = SteamVR_Actions.laserPointers_RightClick;
            SteamVR_Action_Boolean clickModifierAction = SteamVR_Actions.laserPointers_ClickModifier;

            if (VRControls.laserControlsActive)
            {
                if (isChordDown(SteamVR_Actions.laserPointers_AddMapPin, ref lastAddMapPinFrame) && isPointerOverLargeMap())
                {
                    Minimap.instance.OnMapDblClick();
                    resetMapPointerState();
                    isLeftClickSuppressed = true;
                }
                if (isChordDown(SteamVR_Actions.laserPointers_SendMapPing, ref lastSendMapPingFrame) && isPointerOverLargeMap())
                {
                    Minimap.instance.OnMapMiddleClick(null);
                    resetMapPointerState();
                    isRightClickSuppressed = true;
                }
                if (isChordDown(SteamVR_Actions.laserPointers_FavoriteBuildPiece, ref lastFavoriteBuildPieceFrame) && tryFavoriteHoveredBuildPiece())
                {
                    isRightClickSuppressed = true;
                }
            }

            // Keep a suppression through the frame its button is released, so that release is hidden too. The left
            // pointer clicks with ClickModifier, so both have to be up before the left click can be used again.
            if (!isHeldOrJustReleased(leftClickAction) && !isHeldOrJustReleased(clickModifierAction))
            {
                isLeftClickSuppressed = false;
            }
            if (!isHeldOrJustReleased(rightClickAction))
            {
                isRightClickSuppressed = false;
            }

            leftClick = FilterLeftClick(leftClickAction.GetState(SteamVR_Input_Sources.Any));
            leftClickDown = FilterLeftClick(leftClickAction.GetStateDown(SteamVR_Input_Sources.Any));
            leftClickUp = FilterLeftClick(leftClickAction.GetStateUp(SteamVR_Input_Sources.Any));

            rightClick = FilterRightClick(rightClickAction.GetState(SteamVR_Input_Sources.Any));
            rightClickDown = FilterRightClick(rightClickAction.GetStateDown(SteamVR_Input_Sources.Any));
            rightClickUp = FilterRightClick(rightClickAction.GetStateUp(SteamVR_Input_Sources.Any));

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

        // Favorites (or opens the favorite category dropdown for) the build piece under the laser pointer, which
        // vanilla does with a middle click that laser pointers do not have.
        private static bool tryFavoriteHoveredBuildPiece()
        {
            if (!Hud.IsPieceSelectionVisible())
            {
                return false;
            }
            BuildUi buildUi = Hud.instance.m_buildUi;
            if (buildUi.m_currentHoveredPieceButton == null || buildUi.m_favoritesDropdown.IsOpen())
            {
                return false;
            }
            buildUi.PressedFavoriteButton(buildUi.m_currentHoveredPieceButton);
            return true;
        }
    }
}
