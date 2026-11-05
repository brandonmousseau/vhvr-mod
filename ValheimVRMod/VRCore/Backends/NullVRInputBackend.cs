using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ValheimVRMod.VRCore.Backends
{
    // The input of a backend without controllers (see IVRBackend.SupportsControllers): every action exists, so that
    // VRInputActions resolves as usual and nothing that reads an action has to check for one, but none is ever bound
    // or pressed, no pose is valid, haptics do nothing and listeners are never called.
    public sealed class NullVRInputBackend : IVRInputBackend
    {
        public void Declare(IReadOnlyList<VRActionSetDecl> sets, IReadOnlyList<VRActionDecl> actions) { }
        public VRBooleanAction Boolean(string path) => NullBooleanAction.Instance;
        public VRVector2Action Axis(string path) => NullAxisAction.Instance;
        public VRPoseAction Pose(string path) => NullPoseAction.Instance;
        public VRHapticAction Haptic(string path) => NullHapticAction.Instance;
        public VRActionSet ActionSet(string path) => NullActionSet.Instance;
        // Never raised: there is no input to update.
        public event Action onNonVisualActionsUpdated { add { } remove { } }
        public void OpenBindingUI(VRActionSet actionSet) { }

        sealed class NullBooleanAction : VRBooleanAction
        {
            public static readonly NullBooleanAction Instance = new NullBooleanAction();
            public override string GetShortName() => "";
            public override bool activeBinding => false;
            public override bool GetActiveBinding(VRInputSource source) => false;
            public override bool state => false;
            public override bool GetState(VRInputSource source) => false;
            public override bool GetStateDown(VRInputSource source) => false;
            public override bool GetStateUp(VRInputSource source) => false;
            public override void AddOnStateDownListener(Action<VRBooleanAction, VRInputSource> callback, VRInputSource source) { }
            public override void AddOnStateUpListener(Action<VRBooleanAction, VRInputSource> callback, VRInputSource source) { }
        }
        sealed class NullAxisAction : VRVector2Action
        {
            public static readonly NullAxisAction Instance = new NullAxisAction();
            public override string GetShortName() => "";
            public override bool activeBinding => false;
            public override bool GetActiveBinding(VRInputSource source) => false;
            public override Vector2 axis => Vector2.zero;
            public override Vector2 GetAxis(VRInputSource source) => Vector2.zero;
            public override void AddOnChangeListener(Action<VRVector2Action, VRInputSource, Vector2, Vector2> callback, VRInputSource source) { }
        }
        sealed class NullPoseAction : VRPoseAction
        {
            public static readonly NullPoseAction Instance = new NullPoseAction();
            public override string GetShortName() => "";
            public override bool activeBinding => false;
            public override bool GetActiveBinding(VRInputSource source) => false;
            public override Vector3 localPosition => Vector3.zero;
            public override Quaternion localRotation => Quaternion.identity;
            public override Vector3 GetLocalPosition(VRInputSource source) => Vector3.zero;
            public override Quaternion GetLocalRotation(VRInputSource source) => Quaternion.identity;
            public override Vector3 GetVelocity(VRInputSource source) => Vector3.zero;
            public override Vector3 GetAngularVelocity(VRInputSource source) => Vector3.zero;
            public override bool GetPoseIsValid(VRInputSource source) => false;
            public override bool GetDeviceIsConnected(VRInputSource source) => false;
        }
        sealed class NullHapticAction : VRHapticAction
        {
            public static readonly NullHapticAction Instance = new NullHapticAction();
            public override void Execute(float delay, float duration, float frequency, float amplitude, VRInputSource source) { }
        }
        sealed class NullActionSet : VRActionSet
        {
            public static readonly NullActionSet Instance = new NullActionSet();
            public override bool IsActive(VRInputSource source = VRInputSource.Any) => false;
            public override void Activate(VRInputSource source = VRInputSource.Any, int priority = 0, bool disableAllOtherActionSets = false) { }
            public override void Deactivate(VRInputSource source = VRInputSource.Any) { }
        }
    }

    // The keyboard of a backend without a system keyboard. Text input only asks for it with VR controls on
    // (see TextInputPatches), which such a backend never has, so this is only reached if that changes; the physical
    // keyboard is used instead.
    public sealed class NullVRKeyboardBackend : IVRKeyboardBackend
    {
        public void ListenClosed(Action listener) { }
        public void ListenInput(Action<VRKeyboardInput> listener) { }
        public void ListenDone(Action listener) { }
        public string Show(string existingText) => "NotSupported";
        public void Hide() { }
        public void ReadText(StringBuilder text, uint capacity) { }
    }
}
