using System;
using System.Collections.Generic;
using UnityEngine;

namespace ValheimVRMod.VRCore.Backends
{
    // Stage 1 has exactly one provider. Runtime selection belongs to a later change.
    public interface IVRBackend
    {
        IVRInputBackend Input { get; }
        IVRRigBackend Rig { get; }
        IVRKeyboardBackend Keyboard { get; }
        IVROverlayBackend Overlay { get; }
        bool IsRuntimeRunning();
        bool IsControllerConnected(VRInputSource source);
        IEnumerable<VRTrackerInfo> GetConnectedTrackers();
        void Fade(Color color, float duration, bool fadeOverlay = false);
        Shader GetShader(string name);
        bool LoadShaders(string path);
        bool InitializeVR();
        bool StartVR();
        bool InitializeInput();
        void UpdateMirrorViewMode();
        void UpdateMirrorSetup();
        void tryRecenter();
    }

    public static class VRBackend
    {
        public static IVRBackend Active { get; } = new SteamVRBackend();
    }

    public enum VRInputSource
    {
        Any, LeftHand, RightHand, LeftFoot, RightFoot, LeftShoulder, RightShoulder,
        Waist, Chest, Head, Gamepad, Camera, Keyboard, Treadmill, LeftAnkle, RightAnkle
    }

    public interface IVRInputBackend
    {
        VRBooleanAction Boolean(string path);
        VRVector2Action Axis(string path);
        VRPoseAction Pose(string path);
        VRHapticAction Haptic(string path);
        VRActionSet ActionSet(string path);
        event Action onNonVisualActionsUpdated;
        void OpenBindingUI(VRActionSet actionSet);
    }

    public abstract class VRAction
    {
        public abstract string GetShortName();
        public abstract bool activeBinding { get; }
        public abstract bool GetActiveBinding(VRInputSource source);
    }

    public abstract class VRBooleanAction : VRAction
    {
        public abstract bool state { get; }
        public abstract bool GetState(VRInputSource source);
        public abstract bool GetStateDown(VRInputSource source);
        public abstract bool GetStateUp(VRInputSource source);
        public VRBooleanSource this[VRInputSource source] => new VRBooleanSource(this, source);
        public abstract void AddOnStateDownListener(Action<VRBooleanAction, VRInputSource> callback, VRInputSource source);
        public abstract void AddOnStateUpListener(Action<VRBooleanAction, VRInputSource> callback, VRInputSource source);
    }

    public readonly struct VRBooleanSource
    {
        readonly VRBooleanAction action;
        readonly VRInputSource source;
        internal VRBooleanSource(VRBooleanAction action, VRInputSource source) { this.action = action; this.source = source; }
        public bool activeBinding => action.GetActiveBinding(source);
    }

    public abstract class VRVector2Action : VRAction
    {
        public abstract Vector2 axis { get; }
        public abstract Vector2 GetAxis(VRInputSource source);
        public abstract void AddOnChangeListener(Action<VRVector2Action, VRInputSource, Vector2, Vector2> callback, VRInputSource source);
    }

    public abstract class VRPoseAction : VRAction
    {
        public abstract Vector3 localPosition { get; }
        public abstract Quaternion localRotation { get; }
        public abstract Vector3 GetLocalPosition(VRInputSource source);
        public abstract Quaternion GetLocalRotation(VRInputSource source);
        public abstract Vector3 GetVelocity(VRInputSource source);
        public abstract Vector3 GetAngularVelocity(VRInputSource source);
        public abstract bool GetPoseIsValid(VRInputSource source);
        public abstract bool GetDeviceIsConnected(VRInputSource source);
    }

    public abstract class VRHapticAction
    {
        public abstract void Execute(float delay, float duration, float frequency, float amplitude, VRInputSource source);
    }

    public abstract class VRActionSet
    {
        public abstract bool IsActive(VRInputSource source = VRInputSource.Any);
        public abstract void Activate(VRInputSource source = VRInputSource.Any, int priority = 0, bool disableAllOtherActionSets = false);
        public abstract void Deactivate(VRInputSource source = VRInputSource.Any);
    }

    public static class VRInput
    {
        public static event Action onNonVisualActionsUpdated
        {
            add { VRBackend.Active.Input.onNonVisualActionsUpdated += value; }
            remove { VRBackend.Active.Input.onNonVisualActionsUpdated -= value; }
        }
        public static void OpenBindingUI(VRActionSet set) { VRBackend.Active.Input.OpenBindingUI(set); }
    }
}
