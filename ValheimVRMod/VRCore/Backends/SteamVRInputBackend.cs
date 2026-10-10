using System;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;

namespace ValheimVRMod.VRCore.Backends
{
    // Reads and callbacks are forwarded at the original call sites. No new polling loop,
    // synthesized edges, action-set policy, or additional SDK reads are introduced.
    public sealed class SteamVRInputBackend : IVRInputBackend
    {
        readonly Dictionary<string, VRBooleanAction> booleans = new Dictionary<string, VRBooleanAction>();
        readonly Dictionary<string, VRVector2Action> axes = new Dictionary<string, VRVector2Action>();
        readonly Dictionary<string, VRPoseAction> poses = new Dictionary<string, VRPoseAction>();
        readonly Dictionary<string, VRHapticAction> haptics = new Dictionary<string, VRHapticAction>();
        readonly Dictionary<string, VRActionSet> sets = new Dictionary<string, VRActionSet>();

        internal static SteamVR_Input_Sources Native(VRInputSource source)
        {
            switch (source)
            {
                case VRInputSource.Any: return SteamVR_Input_Sources.Any;
                case VRInputSource.LeftHand: return SteamVR_Input_Sources.LeftHand;
                case VRInputSource.RightHand: return SteamVR_Input_Sources.RightHand;
                case VRInputSource.LeftFoot: return SteamVR_Input_Sources.LeftFoot;
                case VRInputSource.RightFoot: return SteamVR_Input_Sources.RightFoot;
                case VRInputSource.LeftShoulder: return SteamVR_Input_Sources.LeftShoulder;
                case VRInputSource.RightShoulder: return SteamVR_Input_Sources.RightShoulder;
                case VRInputSource.Waist: return SteamVR_Input_Sources.Waist;
                case VRInputSource.Chest: return SteamVR_Input_Sources.Chest;
                case VRInputSource.Head: return SteamVR_Input_Sources.Head;
                case VRInputSource.Gamepad: return SteamVR_Input_Sources.Gamepad;
                case VRInputSource.Camera: return SteamVR_Input_Sources.Camera;
                case VRInputSource.Keyboard: return SteamVR_Input_Sources.Keyboard;
                case VRInputSource.Treadmill: return SteamVR_Input_Sources.Treadmill;
                case VRInputSource.LeftAnkle: return SteamVR_Input_Sources.LeftAnkle;
                case VRInputSource.RightAnkle: return SteamVR_Input_Sources.RightAnkle;
                default: throw new ArgumentOutOfRangeException(nameof(source));
            }
        }
        internal static VRInputSource Source(SteamVR_Input_Sources source)
        {
            switch (source)
            {
                case SteamVR_Input_Sources.Any: return VRInputSource.Any;
                case SteamVR_Input_Sources.LeftHand: return VRInputSource.LeftHand;
                case SteamVR_Input_Sources.RightHand: return VRInputSource.RightHand;
                case SteamVR_Input_Sources.LeftFoot: return VRInputSource.LeftFoot;
                case SteamVR_Input_Sources.RightFoot: return VRInputSource.RightFoot;
                case SteamVR_Input_Sources.LeftShoulder: return VRInputSource.LeftShoulder;
                case SteamVR_Input_Sources.RightShoulder: return VRInputSource.RightShoulder;
                case SteamVR_Input_Sources.Waist: return VRInputSource.Waist;
                case SteamVR_Input_Sources.Chest: return VRInputSource.Chest;
                case SteamVR_Input_Sources.Head: return VRInputSource.Head;
                case SteamVR_Input_Sources.Gamepad: return VRInputSource.Gamepad;
                case SteamVR_Input_Sources.Camera: return VRInputSource.Camera;
                case SteamVR_Input_Sources.Keyboard: return VRInputSource.Keyboard;
                case SteamVR_Input_Sources.Treadmill: return VRInputSource.Treadmill;
                case SteamVR_Input_Sources.LeftAnkle: return VRInputSource.LeftAnkle;
                case SteamVR_Input_Sources.RightAnkle: return VRInputSource.RightAnkle;
                default: throw new ArgumentOutOfRangeException(nameof(source));
            }
        }

        public VRBooleanAction Boolean(string path)
        {
            if (!booleans.TryGetValue(path, out var value))
                booleans[path] = value = new BooleanAction(SteamVR_Input.GetActionFromPath<SteamVR_Action_Boolean>(path));
            return value;
        }
        public VRVector2Action Axis(string path)
        {
            if (!axes.TryGetValue(path, out var value))
                axes[path] = value = new AxisAction(SteamVR_Input.GetActionFromPath<SteamVR_Action_Vector2>(path));
            return value;
        }
        public VRPoseAction Pose(string path)
        {
            if (!poses.TryGetValue(path, out var value))
                poses[path] = value = new PoseAction(SteamVR_Input.GetActionFromPath<SteamVR_Action_Pose>(path));
            return value;
        }
        public VRHapticAction Haptic(string path)
        {
            if (!haptics.TryGetValue(path, out var value))
                haptics[path] = value = Wrap(SteamVR_Input.GetActionFromPath<SteamVR_Action_Vibration>(path));
            return value;
        }
        public VRActionSet ActionSet(string path)
        {
            if (!sets.TryGetValue(path, out var value))
                sets[path] = value = new ActionSetAdapter(SteamVR_Input.GetActionSetFromPath(path));
            return value;
        }
        public event Action onNonVisualActionsUpdated
        {
            add { SteamVR_Input.onNonVisualActionsUpdated += value; }
            remove { SteamVR_Input.onNonVisualActionsUpdated -= value; }
        }
        public void OpenBindingUI(VRActionSet actionSet)
        {
            SteamVR_Input.OpenBindingUI(((ActionSetAdapter)actionSet).Action);
        }
        internal static VRHapticAction Wrap(SteamVR_Action_Vibration action)
        {
            return ReferenceEquals(action, null) ? null : new HapticAction(action);
        }

        sealed class BooleanAction : VRBooleanAction
        {
            readonly SteamVR_Action_Boolean action;
            public BooleanAction(SteamVR_Action_Boolean action) { this.action = action; }
            public override string GetShortName() => action.GetShortName();
            public override bool activeBinding => action.activeBinding;
            public override bool GetActiveBinding(VRInputSource source) => action.GetActiveBinding(Native(source));
            public override bool state => action.state;
            public override bool GetState(VRInputSource source) => action.GetState(Native(source));
            public override bool GetStateDown(VRInputSource source) => action.GetStateDown(Native(source));
            public override bool GetStateUp(VRInputSource source) => action.GetStateUp(Native(source));
            public override void AddOnStateDownListener(Action<VRBooleanAction, VRInputSource> callback, VRInputSource source)
            { action.AddOnStateDownListener((a, hand) => callback(this, Source(hand)), Native(source)); }
            public override void AddOnStateUpListener(Action<VRBooleanAction, VRInputSource> callback, VRInputSource source)
            { action.AddOnStateUpListener((a, hand) => callback(this, Source(hand)), Native(source)); }
        }
        sealed class AxisAction : VRVector2Action
        {
            readonly SteamVR_Action_Vector2 action;
            public AxisAction(SteamVR_Action_Vector2 action) { this.action = action; }
            public override string GetShortName() => action.GetShortName();
            public override bool activeBinding => action.activeBinding;
            public override bool GetActiveBinding(VRInputSource source) => action.GetActiveBinding(Native(source));
            public override Vector2 axis => action.axis;
            public override Vector2 GetAxis(VRInputSource source) => action.GetAxis(Native(source));
            public override void AddOnChangeListener(Action<VRVector2Action, VRInputSource, Vector2, Vector2> callback, VRInputSource source)
            { action.AddOnChangeListener((a, hand, value, delta) => callback(this, Source(hand), value, delta), Native(source)); }
        }
        sealed class PoseAction : VRPoseAction
        {
            readonly SteamVR_Action_Pose action;
            public PoseAction(SteamVR_Action_Pose action) { this.action = action; }
            public override string GetShortName() => action.GetShortName();
            public override bool activeBinding => action.activeBinding;
            public override bool GetActiveBinding(VRInputSource source) => action.GetActiveBinding(Native(source));
            public override Vector3 localPosition => action.localPosition;
            public override Quaternion localRotation => action.localRotation;
            public override Vector3 GetLocalPosition(VRInputSource source) => action.GetLocalPosition(Native(source));
            public override Quaternion GetLocalRotation(VRInputSource source) => action.GetLocalRotation(Native(source));
            public override Vector3 GetVelocity(VRInputSource source) => action.GetVelocity(Native(source));
            public override Vector3 GetAngularVelocity(VRInputSource source) => action.GetAngularVelocity(Native(source));
            public override bool GetPoseIsValid(VRInputSource source) => action.GetPoseIsValid(Native(source));
            public override bool GetDeviceIsConnected(VRInputSource source) => action.GetDeviceIsConnected(Native(source));
        }
        sealed class HapticAction : VRHapticAction
        {
            readonly SteamVR_Action_Vibration action;
            public HapticAction(SteamVR_Action_Vibration action) { this.action = action; }
            public override void Execute(float delay, float duration, float frequency, float amplitude, VRInputSource source)
            { action.Execute(delay, duration, frequency, amplitude, Native(source)); }
        }
        sealed class ActionSetAdapter : VRActionSet
        {
            internal readonly SteamVR_ActionSet Action;
            public ActionSetAdapter(SteamVR_ActionSet action) { Action = action; }
            public override bool IsActive(VRInputSource source = VRInputSource.Any) => Action.IsActive(Native(source));
            public override void Activate(VRInputSource source = VRInputSource.Any, int priority = 0, bool disableAllOtherActionSets = false)
            { Action.Activate(Native(source), priority, disableAllOtherActionSets); }
            public override void Deactivate(VRInputSource source = VRInputSource.Any) { Action.Deactivate(Native(source)); }
        }
    }
}
