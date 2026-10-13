// Paths and capitalization match the tracked upstream SteamVR action catalog.
namespace ValheimVRMod.VRCore.Backends
{
    public static class VRInputActions
    {
        // Null until Initialize() has run, so that a read before the input system is up fails right away
        // instead of holding on to an action that is not backed by the runtime.
        public static VRActionSet _default { get; private set; }
        public static VRActionSet Valheim { get; private set; }
        public static VRBooleanAction default_InteractUI { get; private set; }
        public static VRBooleanAction default_Teleport { get; private set; }
        public static VRBooleanAction default_GrabPinch { get; private set; }
        public static VRBooleanAction default_GrabGrip { get; private set; }
        public static VRPoseAction default_Pose { get; private set; }
        public static VRBooleanAction default_HeadsetOnHead { get; private set; }
        public static VRHapticAction default_Haptic { get; private set; }
        public static VRBooleanAction valheim_ToggleInventory { get; private set; }
        public static VRBooleanAction valheim_ToggleMenu { get; private set; }
        public static VRBooleanAction valheim_Jump { get; private set; }
        public static VRBooleanAction valheim_Use { get; private set; }
        public static VRBooleanAction valheim_Sit { get; private set; }
        public static VRVector2Action valheim_PitchAndYaw { get; private set; }
        public static VRPoseAction valheim_PoseL { get; private set; }
        public static VRPoseAction valheim_PoseR { get; private set; }
        public static VRPoseAction valheim_BodyPose { get; private set; }
        public static VRBooleanAction valheim_HotbarUp { get; private set; }
        public static VRBooleanAction valheim_HotbarDown { get; private set; }
        public static VRVector2Action valheim_HotbarScroll { get; private set; }
        public static VRBooleanAction valheim_ToggleMap { get; private set; }
        public static VRBooleanAction valheim_HotbarUse { get; private set; }
        public static VRVector2Action valheim_ContextScroll { get; private set; }
        public static VRBooleanAction valheim_Grab { get; private set; }
        public static VRBooleanAction valheim_QuickSwitch { get; private set; }
        public static VRVector2Action valheim_Walk { get; private set; }
        public static VRBooleanAction valheim_QuickActions { get; private set; }
        public static VRBooleanAction valheim_StopGesturedLocomotion { get; private set; }
        public static VRBooleanAction valheim_ToggleRun { get; private set; }
        public static VRBooleanAction valheim_HoldRun { get; private set; }
        public static VRBooleanAction valheim_ToggleCrouch { get; private set; }
        public static VRBooleanAction valheim_Dodge { get; private set; }
        public static VRBooleanAction valheim_ToggleAutoPickup { get; private set; }
        public static VRBooleanAction valheim_RightClick { get; private set; }
        public static VRBooleanAction valheim_LeftClick { get; private set; }
        public static VRBooleanAction valheim_AddMapPin { get; private set; }
        public static VRBooleanAction valheim_MiddleClick { get; private set; }
        public static VRBooleanAction valheim_DiscardItem { get; private set; }
        public static VRBooleanAction valheim_SplitStack { get; private set; }
        public static VRBooleanAction valheim_ScrollUp { get; private set; }
        public static VRBooleanAction valheim_ScrollDown { get; private set; }
        public static VRBooleanAction valheim_RemovePiece { get; private set; }
        public static VRBooleanAction valheim_FacePieceToward { get; private set; }
        public static VRBooleanAction valheim_FacePieceAway { get; private set; }
        public static VRBooleanAction valheim_RotatePieceClockwise { get; private set; }
        public static VRBooleanAction valheim_RotatePieceCounterclockwise { get; private set; }
        public static VRHapticAction valheim_Haptic { get; private set; }

        // Every action is resolved here, once, rather than where it is read: reads stay a plain static load,
        // and a backend that has to declare its actions up front (as OpenXR does) is given the whole catalog.
        public static void Initialize(IVRInputBackend input)
        {
            _default = input.ActionSet("/actions/default");
            Valheim = input.ActionSet("/actions/Valheim");
            default_InteractUI = input.Boolean("/actions/default/in/InteractUI");
            default_Teleport = input.Boolean("/actions/default/in/Teleport");
            default_GrabPinch = input.Boolean("/actions/default/in/GrabPinch");
            default_GrabGrip = input.Boolean("/actions/default/in/GrabGrip");
            default_Pose = input.Pose("/actions/default/in/Pose");
            default_HeadsetOnHead = input.Boolean("/actions/default/in/HeadsetOnHead");
            default_Haptic = input.Haptic("/actions/default/out/Haptic");
            valheim_ToggleInventory = input.Boolean("/actions/Valheim/in/ToggleInventory");
            valheim_ToggleMenu = input.Boolean("/actions/Valheim/in/ToggleMenu");
            valheim_Jump = input.Boolean("/actions/Valheim/in/Jump");
            valheim_Use = input.Boolean("/actions/Valheim/in/Use");
            valheim_Sit = input.Boolean("/actions/Valheim/in/Sit");
            valheim_PitchAndYaw = input.Axis("/actions/Valheim/in/PitchAndYaw");
            valheim_PoseL = input.Pose("/actions/Valheim/in/PoseL");
            valheim_PoseR = input.Pose("/actions/Valheim/in/PoseR");
            valheim_BodyPose = input.Pose("/actions/Valheim/in/BodyPose");
            valheim_HotbarUp = input.Boolean("/actions/Valheim/in/HotbarUp");
            valheim_HotbarDown = input.Boolean("/actions/Valheim/in/HotbarDown");
            valheim_HotbarScroll = input.Axis("/actions/Valheim/in/HotbarScroll");
            valheim_ToggleMap = input.Boolean("/actions/Valheim/in/ToggleMap");
            valheim_HotbarUse = input.Boolean("/actions/Valheim/in/HotbarUse");
            valheim_ContextScroll = input.Axis("/actions/Valheim/in/ContextScroll");
            valheim_Grab = input.Boolean("/actions/Valheim/in/Grab");
            valheim_QuickSwitch = input.Boolean("/actions/Valheim/in/QuickSwitch");
            valheim_Walk = input.Axis("/actions/Valheim/in/Walk");
            valheim_QuickActions = input.Boolean("/actions/Valheim/in/QuickActions");
            valheim_StopGesturedLocomotion = input.Boolean("/actions/Valheim/in/StopGesturedLocomotion");
            valheim_ToggleRun = input.Boolean("/actions/Valheim/in/ToggleRun");
            valheim_HoldRun = input.Boolean("/actions/Valheim/in/HoldRun");
            valheim_ToggleCrouch = input.Boolean("/actions/Valheim/in/ToggleCrouch");
            valheim_Dodge = input.Boolean("/actions/Valheim/in/Dodge");
            valheim_ToggleAutoPickup = input.Boolean("/actions/Valheim/in/ToggleAutoPickup");
            valheim_RightClick = input.Boolean("/actions/Valheim/in/RightClick");
            valheim_LeftClick = input.Boolean("/actions/Valheim/in/LeftClick");
            valheim_AddMapPin = input.Boolean("/actions/Valheim/in/AddMapPin");
            valheim_MiddleClick = input.Boolean("/actions/Valheim/in/MiddleClick");
            valheim_DiscardItem = input.Boolean("/actions/Valheim/in/DiscardItem");
            valheim_SplitStack = input.Boolean("/actions/Valheim/in/SplitStack");
            valheim_ScrollUp = input.Boolean("/actions/Valheim/in/ScrollUp");
            valheim_ScrollDown = input.Boolean("/actions/Valheim/in/ScrollDown");
            valheim_RemovePiece = input.Boolean("/actions/Valheim/in/RemovePiece");
            valheim_FacePieceToward = input.Boolean("/actions/Valheim/in/FacePieceToward");
            valheim_FacePieceAway = input.Boolean("/actions/Valheim/in/FacePieceAway");
            valheim_RotatePieceClockwise = input.Boolean("/actions/Valheim/in/RotatePieceClockwise");
            valheim_RotatePieceCounterclockwise = input.Boolean("/actions/Valheim/in/RotatePieceCounterclockwise");
            valheim_Haptic = input.Haptic("/actions/Valheim/out/Haptic");
        }
    }
}
