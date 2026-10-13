using System;
using UnityEngine;
using ValheimVRMod.VRCore.BodyTracking;

namespace ValheimVRMod.VRCore.Backends
{
    public interface IVRRigBackend
    {
        VRRigPlayer Player { get; }
        VRRigPlayer GetPlayer(GameObject root);
        GameObject CreateRig(GameObject template);
        VRHand[] GetHands(GameObject root);
        VRLaserPointer GetPointer(GameObject hand);
        VRTrackingProvider AddBodyTracking(GameObject root);
        VRTrackedObject AddTrackedObject(GameObject root);
    }

    // These are views of existing components, not new behaviours. Equality and truth
    // tests retain Unity's destroyed-object semantics; the native update order is untouched.
    public abstract class VRComponent
    {
        protected readonly Component component;
        protected VRComponent(Component component) { this.component = component; }
        public GameObject gameObject => component.gameObject;
        public Transform transform => component.transform;
        public string name => component.name;
        public bool enabled { get => ((Behaviour)component).enabled; set => ((Behaviour)component).enabled = value; }
        public T GetComponent<T>() => component.GetComponent<T>();
        public T GetComponentInChildren<T>() => component.GetComponentInChildren<T>();
        public T[] GetComponentsInChildren<T>() => component.GetComponentsInChildren<T>();
        public static implicit operator bool(VRComponent value) => !ReferenceEquals(value, null) && value.component;
        public static bool operator ==(VRComponent a, VRComponent b) =>
            (ReferenceEquals(a, null) ? null : a.component) == (ReferenceEquals(b, null) ? null : b.component);
        public static bool operator !=(VRComponent a, VRComponent b) => !(a == b);
        public override bool Equals(object obj) => obj == null ? this == null : obj is VRComponent other && this == other;
        public override int GetHashCode() => component.GetHashCode();
    }

    public abstract class VRRigPlayer : VRComponent
    {
        protected VRRigPlayer(Component component) : base(component) { }
        public abstract Transform hmdTransform { get; }
        public abstract float eyeHeight { get; }
    }
    public abstract class VRHand : VRComponent
    {
        protected VRHand(Component component) : base(component) { }
        public abstract bool isActive { get; }
        public abstract bool isPoseValid { get; }
        public abstract VRHand otherHand { get; }
        public abstract VRHapticAction hapticAction { get; }
        public abstract Vector3 GetTrackedObjectVelocity();
        public abstract Vector3 GetTrackedObjectAngularVelocity();
        public abstract void SetVisibility(bool visible);
    }
    public struct VRPointerEventArgs
    {
        public VRInputSource fromInputSource;
        public uint flags;
        public float distance;
        public Transform target;
        public Vector3 position;
        public bool buttonStateLeft, buttonStateRight;
    }
    public delegate void VRPointerEventHandler(object sender, VRPointerEventArgs args);
    public abstract class VRLaserPointer : VRComponent
    {
        protected VRLaserPointer(Component component) : base(component) { }
        public abstract event VRPointerEventHandler PointerTracking;
        public abstract int raycastLayerMask { get; set; }
        public abstract Vector3 rayStartingPosition { get; set; }
        public abstract Quaternion rayDirection { get; set; }
        public abstract GameObject holder { get; }
        public abstract GameObject pointer { get; }
        public abstract void setUsePointer(bool active);
        public abstract void setVisible(bool visible);
        public abstract bool pointerIsActive();
    }
    public abstract class VRTrackedObject : VRComponent
    {
        protected VRTrackedObject(Component component) : base(component) { }
        public abstract bool isValid { get; }
        public abstract int index { get; set; }
    }
    public abstract class VRTrackingProvider : VRComponent, IBodyTrackingProvider
    {
        protected VRTrackingProvider(Component component) : base(component) { }
        public abstract void Initialize(Transform trackingOrigin);
        public abstract bool IsAvailable { get; }
        public abstract bool IsJointActive(BodyJoint joint);
        public abstract Transform GetJointTransform(BodyJoint joint);
    }
}
