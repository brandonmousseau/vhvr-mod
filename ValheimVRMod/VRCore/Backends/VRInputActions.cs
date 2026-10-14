using System;
using System.Collections.Generic;

// The single list of input actions for every backend. Paths and capitalization match the shipped SteamVR
// actions.json, which still carries SteamVR's default bindings and is checked against this list
// (see SteamVRActionCatalog.CheckManifest).
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

        const VRActionRequirement Mandatory = VRActionRequirement.Mandatory;
        const VRActionRequirement Suggested = VRActionRequirement.Suggested;
        const VRActionRequirement Optional = VRActionRequirement.Optional;

        // In the order SteamVR's binding UI lists them; the first set is the one SteamVR activates on start.
        public static readonly IReadOnlyList<VRActionSetDecl> Sets = new[]
        {
            new VRActionSetDecl("/actions/default", "Default", input => _default = input.ActionSet("/actions/default")),
            new VRActionSetDecl("/actions/Valheim", "Valheim", input => Valheim = input.ActionSet("/actions/Valheim")),
        };

        // Every action the backend is to create, in the order SteamVR lists them. The skeleton and squeeze actions
        // are not read by the mod, but SteamVR's own components may look them up by path.
        public static readonly IReadOnlyList<VRActionDecl> Actions = new[]
        {
            Boolean("/actions/default/in/InteractUI", "Interact With UI", Optional, a => default_InteractUI = a),
            Boolean("/actions/default/in/Teleport", "Teleport", Optional, a => default_Teleport = a),
            Boolean("/actions/default/in/GrabPinch", "Grab Pinch", Optional, a => default_GrabPinch = a),
            Boolean("/actions/default/in/GrabGrip", "Grab Grip", Optional, a => default_GrabGrip = a),
            Pose("/actions/default/in/Pose", "Pose", Suggested, a => default_Pose = a),
            Unread("/actions/default/in/SkeletonLeftHand", VRActionKind.Skeleton, "Skeleton (Left)", Suggested),
            Unread("/actions/default/in/SkeletonRightHand", VRActionKind.Skeleton, "Skeleton (Right)", Suggested),
            Unread("/actions/default/in/Squeeze", VRActionKind.Single, "Squeeze", Optional),
            Boolean("/actions/default/in/HeadsetOnHead", "Headset on head (proximity sensor)", Optional, a => default_HeadsetOnHead = a),
            Haptic("/actions/default/out/Haptic", "Haptic", Suggested, a => default_Haptic = a),
            Boolean("/actions/Valheim/in/ToggleInventory", "Toggle inventory", Mandatory, a => valheim_ToggleInventory = a),
            Boolean("/actions/Valheim/in/ToggleMenu", "Toggle menu", Mandatory, a => valheim_ToggleMenu = a),
            Boolean("/actions/Valheim/in/Jump", "Jump", Mandatory, a => valheim_Jump = a),
            Boolean("/actions/Valheim/in/Use", "Use", Mandatory, a => valheim_Use = a),
            Boolean("/actions/Valheim/in/Sit", "Sit", Optional, a => valheim_Sit = a),
            Axis("/actions/Valheim/in/PitchAndYaw", "Pitch and yaw", Mandatory, a => valheim_PitchAndYaw = a),
            Pose("/actions/Valheim/in/PoseL", "Left hand pose", Mandatory, a => valheim_PoseL = a),
            Pose("/actions/Valheim/in/PoseR", "Right hand pose", Mandatory, a => valheim_PoseR = a),
            Pose("/actions/Valheim/in/BodyPose", "Body tracker pose", Optional, a => valheim_BodyPose = a),
            Boolean("/actions/Valheim/in/HotbarUp", "Hotbar up", Optional, a => valheim_HotbarUp = a),
            Boolean("/actions/Valheim/in/HotbarDown", "Hotbar down", Optional, a => valheim_HotbarDown = a),
            Axis("/actions/Valheim/in/HotbarScroll", "Hotbar scroll", Optional, a => valheim_HotbarScroll = a),
            Boolean("/actions/Valheim/in/ToggleMap", "Toggle map", Suggested, a => valheim_ToggleMap = a),
            Boolean("/actions/Valheim/in/HotbarUse", "Use hotbar item", Optional, a => valheim_HotbarUse = a),
            Axis("/actions/Valheim/in/ContextScroll", "Context scroll", Suggested, a => valheim_ContextScroll = a),
            Boolean("/actions/Valheim/in/Grab", "Grab", Mandatory, a => valheim_Grab = a),
            Boolean("/actions/Valheim/in/QuickSwitch", "Quick switch", Mandatory, a => valheim_QuickSwitch = a),
            Axis("/actions/Valheim/in/Walk", "Walk", Mandatory, a => valheim_Walk = a),
            Boolean("/actions/Valheim/in/QuickActions", "Quick actions", Mandatory, a => valheim_QuickActions = a),
            Boolean("/actions/Valheim/in/StopGesturedLocomotion", "Stop gestured locomotion", Suggested, a => valheim_StopGesturedLocomotion = a),
            Boolean("/actions/Valheim/in/ToggleRun", "Toggle run", Mandatory, a => valheim_ToggleRun = a),
            Boolean("/actions/Valheim/in/HoldRun", "Hold to run", Suggested, a => valheim_HoldRun = a),
            Boolean("/actions/Valheim/in/ToggleCrouch", "Toggle crouch", Suggested, a => valheim_ToggleCrouch = a),
            Boolean("/actions/Valheim/in/Dodge", "Dodge", Suggested, a => valheim_Dodge = a),
            Boolean("/actions/Valheim/in/ToggleAutoPickup", "Toggle auto pickup", Optional, a => valheim_ToggleAutoPickup = a),
            Boolean("/actions/Valheim/in/RightClick", "Right click", Mandatory, a => valheim_RightClick = a),
            Boolean("/actions/Valheim/in/LeftClick", "Left click", Mandatory, a => valheim_LeftClick = a),
            Boolean("/actions/Valheim/in/AddMapPin", "Add map pin", Optional, a => valheim_AddMapPin = a),
            Boolean("/actions/Valheim/in/MiddleClick", "Middle click", Optional, a => valheim_MiddleClick = a),
            Boolean("/actions/Valheim/in/DiscardItem", "Discard or stash item", Optional, a => valheim_DiscardItem = a),
            Boolean("/actions/Valheim/in/SplitStack", "Split stack", Mandatory, a => valheim_SplitStack = a),
            Boolean("/actions/Valheim/in/ScrollUp", "Scroll up", Optional, a => valheim_ScrollUp = a),
            Boolean("/actions/Valheim/in/ScrollDown", "Scroll down", Optional, a => valheim_ScrollDown = a),
            Boolean("/actions/Valheim/in/RemovePiece", "Remove building piece", Mandatory, a => valheim_RemovePiece = a),
            Boolean("/actions/Valheim/in/FacePieceToward", "Face building piece toward you", Optional, a => valheim_FacePieceToward = a),
            Boolean("/actions/Valheim/in/FacePieceAway", "Face building piece away from you", Optional, a => valheim_FacePieceAway = a),
            Boolean("/actions/Valheim/in/RotatePieceClockwise", "Rotate building piece clockwise", Mandatory, a => valheim_RotatePieceClockwise = a),
            Boolean("/actions/Valheim/in/RotatePieceCounterclockwise", "Rotate building piece counterclockwise", Mandatory, a => valheim_RotatePieceCounterclockwise = a),
            Haptic("/actions/Valheim/out/Haptic", "Haptic", Mandatory, a => valheim_Haptic = a),
        };

        // Every action is resolved here, once, rather than where it is read: reads stay a plain static load.
        // The backend must have been given the catalog (IVRInputBackend.Declare) and be initialized by now.
        public static void Initialize(IVRInputBackend input)
        {
            foreach (var set in Sets)
            {
                set.resolve(input);
            }
            foreach (var action in Actions)
            {
                action.resolve?.Invoke(input);
            }
        }

        static VRActionDecl Boolean(string path, string name, VRActionRequirement requirement, Action<VRBooleanAction> assign)
        {
            return new VRActionDecl(path, VRActionKind.Boolean, name, requirement, input => assign(input.Boolean(path)));
        }
        static VRActionDecl Axis(string path, string name, VRActionRequirement requirement, Action<VRVector2Action> assign)
        {
            return new VRActionDecl(path, VRActionKind.Vector2, name, requirement, input => assign(input.Axis(path)));
        }
        static VRActionDecl Pose(string path, string name, VRActionRequirement requirement, Action<VRPoseAction> assign)
        {
            return new VRActionDecl(path, VRActionKind.Pose, name, requirement, input => assign(input.Pose(path)));
        }
        static VRActionDecl Haptic(string path, string name, VRActionRequirement requirement, Action<VRHapticAction> assign)
        {
            return new VRActionDecl(path, VRActionKind.Haptic, name, requirement, input => assign(input.Haptic(path)));
        }
        static VRActionDecl Unread(string path, VRActionKind kind, string name, VRActionRequirement requirement)
        {
            return new VRActionDecl(path, kind, name, requirement, null);
        }
    }

    public enum VRActionKind { Boolean, Single, Vector2, Pose, Skeleton, Haptic }

    // As in SteamVR's actions.json, where an action without a requirement is suggested.
    public enum VRActionRequirement { Mandatory, Suggested, Optional }

    public sealed class VRActionSetDecl
    {
        public readonly string Path;
        public readonly string LocalizedName;
        internal readonly Action<IVRInputBackend> resolve;
        internal VRActionSetDecl(string path, string localizedName, Action<IVRInputBackend> resolve)
        {
            Path = path;
            LocalizedName = localizedName;
            this.resolve = resolve;
        }
    }

    public sealed class VRActionDecl
    {
        public readonly string Path;
        public readonly VRActionKind Kind;
        public readonly string LocalizedName;
        public readonly VRActionRequirement Requirement;
        // Null for an action that is declared to the runtime but never read by the mod.
        internal readonly Action<IVRInputBackend> resolve;
        internal VRActionDecl(string path, VRActionKind kind, string localizedName, VRActionRequirement requirement,
                              Action<IVRInputBackend> resolve)
        {
            Path = path;
            Kind = kind;
            LocalizedName = localizedName;
            Requirement = requirement;
            this.resolve = resolve;
        }
        // "/actions/<set>/in/<name>" belongs to "/actions/<set>".
        public string SetPath => Path.Substring(0, Path.IndexOf('/', "/actions/".Length));
        public bool IsOutput => Kind == VRActionKind.Haptic;
    }
}
