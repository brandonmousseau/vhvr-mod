// Paths and capitalization match the tracked upstream SteamVR action catalog.
namespace ValheimVRMod.VRCore.Backends
{
    public static class VRInputActions
    {
        public static VRActionSet _default => VRBackend.Active.Input.ActionSet("/actions/default");
        public static VRActionSet Valheim => VRBackend.Active.Input.ActionSet("/actions/Valheim");
        public static VRBooleanAction default_InteractUI => VRBackend.Active.Input.Boolean("/actions/default/in/InteractUI");
        public static VRBooleanAction default_Teleport => VRBackend.Active.Input.Boolean("/actions/default/in/Teleport");
        public static VRBooleanAction default_GrabPinch => VRBackend.Active.Input.Boolean("/actions/default/in/GrabPinch");
        public static VRBooleanAction default_GrabGrip => VRBackend.Active.Input.Boolean("/actions/default/in/GrabGrip");
        public static VRPoseAction default_Pose => VRBackend.Active.Input.Pose("/actions/default/in/Pose");
        public static VRBooleanAction default_HeadsetOnHead => VRBackend.Active.Input.Boolean("/actions/default/in/HeadsetOnHead");
        public static VRHapticAction default_Haptic => VRBackend.Active.Input.Haptic("/actions/default/out/Haptic");
        public static VRBooleanAction valheim_ToggleInventory => VRBackend.Active.Input.Boolean("/actions/Valheim/in/ToggleInventory");
        public static VRBooleanAction valheim_ToggleMenu => VRBackend.Active.Input.Boolean("/actions/Valheim/in/ToggleMenu");
        public static VRBooleanAction valheim_Jump => VRBackend.Active.Input.Boolean("/actions/Valheim/in/Jump");
        public static VRBooleanAction valheim_Use => VRBackend.Active.Input.Boolean("/actions/Valheim/in/Use");
        public static VRBooleanAction valheim_Sit => VRBackend.Active.Input.Boolean("/actions/Valheim/in/Sit");
        public static VRVector2Action valheim_PitchAndYaw => VRBackend.Active.Input.Axis("/actions/Valheim/in/PitchAndYaw");
        public static VRPoseAction valheim_PoseL => VRBackend.Active.Input.Pose("/actions/Valheim/in/PoseL");
        public static VRPoseAction valheim_PoseR => VRBackend.Active.Input.Pose("/actions/Valheim/in/PoseR");
        public static VRPoseAction valheim_BodyPose => VRBackend.Active.Input.Pose("/actions/Valheim/in/BodyPose");
        public static VRBooleanAction valheim_HotbarUp => VRBackend.Active.Input.Boolean("/actions/Valheim/in/HotbarUp");
        public static VRBooleanAction valheim_HotbarDown => VRBackend.Active.Input.Boolean("/actions/Valheim/in/HotbarDown");
        public static VRVector2Action valheim_HotbarScroll => VRBackend.Active.Input.Axis("/actions/Valheim/in/HotbarScroll");
        public static VRBooleanAction valheim_ToggleMap => VRBackend.Active.Input.Boolean("/actions/Valheim/in/ToggleMap");
        public static VRBooleanAction valheim_HotbarUse => VRBackend.Active.Input.Boolean("/actions/Valheim/in/HotbarUse");
        public static VRVector2Action valheim_ContextScroll => VRBackend.Active.Input.Axis("/actions/Valheim/in/ContextScroll");
        public static VRBooleanAction valheim_Grab => VRBackend.Active.Input.Boolean("/actions/Valheim/in/Grab");
        public static VRBooleanAction valheim_QuickSwitch => VRBackend.Active.Input.Boolean("/actions/Valheim/in/QuickSwitch");
        public static VRVector2Action valheim_Walk => VRBackend.Active.Input.Axis("/actions/Valheim/in/Walk");
        public static VRBooleanAction valheim_QuickActions => VRBackend.Active.Input.Boolean("/actions/Valheim/in/QuickActions");
        public static VRBooleanAction valheim_StopGesturedLocomotion => VRBackend.Active.Input.Boolean("/actions/Valheim/in/StopGesturedLocomotion");
        public static VRBooleanAction valheim_ToggleRun => VRBackend.Active.Input.Boolean("/actions/Valheim/in/ToggleRun");
        public static VRBooleanAction valheim_HoldRun => VRBackend.Active.Input.Boolean("/actions/Valheim/in/HoldRun");
        public static VRBooleanAction valheim_ToggleCrouch => VRBackend.Active.Input.Boolean("/actions/Valheim/in/ToggleCrouch");
        public static VRBooleanAction valheim_Dodge => VRBackend.Active.Input.Boolean("/actions/Valheim/in/Dodge");
        public static VRBooleanAction valheim_ToggleAutoPickup => VRBackend.Active.Input.Boolean("/actions/Valheim/in/ToggleAutoPickup");
        public static VRBooleanAction valheim_RightClick => VRBackend.Active.Input.Boolean("/actions/Valheim/in/RightClick");
        public static VRBooleanAction valheim_LeftClick => VRBackend.Active.Input.Boolean("/actions/Valheim/in/LeftClick");
        public static VRBooleanAction valheim_AddMapPin => VRBackend.Active.Input.Boolean("/actions/Valheim/in/AddMapPin");
        public static VRBooleanAction valheim_MiddleClick => VRBackend.Active.Input.Boolean("/actions/Valheim/in/MiddleClick");
        public static VRBooleanAction valheim_DiscardItem => VRBackend.Active.Input.Boolean("/actions/Valheim/in/DiscardItem");
        public static VRBooleanAction valheim_SplitStack => VRBackend.Active.Input.Boolean("/actions/Valheim/in/SplitStack");
        public static VRBooleanAction valheim_ScrollUp => VRBackend.Active.Input.Boolean("/actions/Valheim/in/ScrollUp");
        public static VRBooleanAction valheim_ScrollDown => VRBackend.Active.Input.Boolean("/actions/Valheim/in/ScrollDown");
        public static VRBooleanAction valheim_RemovePiece => VRBackend.Active.Input.Boolean("/actions/Valheim/in/RemovePiece");
        public static VRHapticAction valheim_Haptic => VRBackend.Active.Input.Haptic("/actions/Valheim/out/Haptic");
    }
}
