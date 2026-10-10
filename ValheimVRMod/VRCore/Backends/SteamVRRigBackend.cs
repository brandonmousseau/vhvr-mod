using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Valve.VR;
using Valve.VR.Extras;
using Valve.VR.InteractionSystem;
using ValheimVRMod.VRCore.BodyTracking;

namespace ValheimVRMod.VRCore.Backends
{
    public sealed class SteamVRRigBackend : IVRRigBackend
    {
        readonly ConditionalWeakTable<Hand, HandAdapter> hands = new ConditionalWeakTable<Hand, HandAdapter>();
        readonly ConditionalWeakTable<Hand, HandAdapter>.CreateValueCallback createHand;
        readonly ConditionalWeakTable<SteamVR_LaserPointer, PointerAdapter> pointers = new ConditionalWeakTable<SteamVR_LaserPointer, PointerAdapter>();
        readonly ConditionalWeakTable<Valve.VR.InteractionSystem.Player, PlayerAdapter> players = new ConditionalWeakTable<Valve.VR.InteractionSystem.Player, PlayerAdapter>();
        public SteamVRRigBackend() { createHand = hand => new HandAdapter(hand, this); }
        public VRRigPlayer Player
        {
            get
            {
                var player = Valve.VR.InteractionSystem.Player.instance;
                return ReferenceEquals(player, null) ? null : players.GetValue(player, p => new PlayerAdapter(p));
            }
        }
        public GameObject CreateRig(GameObject template) => UnityEngine.Object.Instantiate(template);
        public VRRigPlayer GetPlayer(GameObject root)
        {
            var player = root.GetComponent<Valve.VR.InteractionSystem.Player>();
            return ReferenceEquals(player, null) ? null : players.GetValue(player, p => new PlayerAdapter(p));
        }
        VRHand Wrap(Hand hand) => ReferenceEquals(hand, null) ? null : hands.GetValue(hand, createHand);
        public VRHand[] GetHands(GameObject root)
        {
            var native = root.GetComponentsInChildren<Hand>();
            var result = new VRHand[native.Length];
            for (int i = 0; i < native.Length; i++) result[i] = Wrap(native[i]);
            return result;
        }
        public VRLaserPointer GetPointer(GameObject hand)
        {
            var pointer = hand.GetComponent<SteamVR_LaserPointer>();
            return ReferenceEquals(pointer, null) ? null : pointers.GetValue(pointer, p => new PointerAdapter(p));
        }
        public VRTrackingProvider AddBodyTracking(GameObject root) => new TrackingAdapter(root.AddComponent<SteamVRBodyTrackingProvider>());
        public VRTrackedObject AddTrackedObject(GameObject root) => new TrackedObjectAdapter(root.AddComponent<SteamVR_TrackedObject>());

        sealed class PlayerAdapter : VRRigPlayer
        {
            readonly Valve.VR.InteractionSystem.Player player;
            public PlayerAdapter(Valve.VR.InteractionSystem.Player player) : base(player) { this.player = player; }
            public override Transform hmdTransform => player.hmdTransform;
            public override float eyeHeight => player.eyeHeight;
        }
        sealed class HandAdapter : VRHand
        {
            readonly Hand hand;
            readonly SteamVRRigBackend rig;
            SteamVR_Action_Vibration nativeHaptic;
            VRHapticAction haptic;
            public HandAdapter(Hand hand, SteamVRRigBackend rig) : base(hand) { this.hand = hand; this.rig = rig; }
            public override bool isActive => hand.isActive;
            public override bool isPoseValid => hand.isPoseValid;
            public override VRHand otherHand => rig.Wrap(hand.otherHand);
            public override VRHapticAction hapticAction
            {
                get
                {
                    // Preserve the authored per-hand action, including a missing action.
                    if (!ReferenceEquals(nativeHaptic, hand.hapticAction))
                    {
                        nativeHaptic = hand.hapticAction;
                        haptic = SteamVRInputBackend.Wrap(nativeHaptic);
                    }
                    return haptic;
                }
            }
            public override Vector3 GetTrackedObjectVelocity() => hand.GetTrackedObjectVelocity();
            public override Vector3 GetTrackedObjectAngularVelocity() => hand.GetTrackedObjectAngularVelocity();
            public override void SetVisibility(bool visible) => hand.SetVisibility(visible);
        }
        sealed class PointerAdapter : VRLaserPointer
        {
            readonly SteamVR_LaserPointer native;
            // Keep one native subscription per add, including duplicate delegates.
            readonly Dictionary<VRPointerEventHandler, Stack<PointerEventHandler>> listeners = new Dictionary<VRPointerEventHandler, Stack<PointerEventHandler>>();
            public PointerAdapter(SteamVR_LaserPointer pointer) : base(pointer) { native = pointer; }
            public override event VRPointerEventHandler PointerTracking
            {
                add
                {
                    if (value == null) return;
                    PointerEventHandler listener = (sender, e) => value(this, new VRPointerEventArgs {
                        fromInputSource = SteamVRInputBackend.Source(e.fromInputSource), flags = e.flags,
                        distance = e.distance, target = e.target, position = e.position,
                        buttonStateLeft = e.buttonStateLeft, buttonStateRight = e.buttonStateRight });
                    if (!listeners.TryGetValue(value, out var stack)) listeners[value] = stack = new Stack<PointerEventHandler>();
                    stack.Push(listener);
                    native.PointerTracking += listener;
                }
                remove
                {
                    if (value == null || !listeners.TryGetValue(value, out var stack)) return;
                    native.PointerTracking -= stack.Pop();
                    if (stack.Count == 0) listeners.Remove(value);
                }
            }
            public override int raycastLayerMask { get => native.raycastLayerMask; set => native.raycastLayerMask = value; }
            public override Vector3 rayStartingPosition { get => native.rayStartingPosition; set => native.rayStartingPosition = value; }
            public override Quaternion rayDirection { get => native.rayDirection; set => native.rayDirection = value; }
            public override GameObject holder => native.holder;
            public override GameObject pointer => native.pointer;
            public override void setUsePointer(bool active) => native.setUsePointer(active);
            public override void setVisible(bool visible) => native.setVisible(visible);
            public override bool pointerIsActive() => native.pointerIsActive();
        }
        sealed class TrackingAdapter : VRTrackingProvider
        {
            readonly SteamVRBodyTrackingProvider provider;
            public TrackingAdapter(SteamVRBodyTrackingProvider provider) : base(provider) { this.provider = provider; }
            public override void Initialize(Transform trackingOrigin) => provider.Initialize(trackingOrigin);
            public override bool IsAvailable => provider.IsAvailable;
            public override bool IsJointActive(BodyJoint joint) => provider.IsJointActive(joint);
            public override Transform GetJointTransform(BodyJoint joint) => provider.GetJointTransform(joint);
        }
        sealed class TrackedObjectAdapter : VRTrackedObject
        {
            readonly SteamVR_TrackedObject tracker;
            public TrackedObjectAdapter(SteamVR_TrackedObject tracker) : base(tracker) { this.tracker = tracker; }
            public override bool isValid => tracker.isValid;
            public override int index { get => (int)tracker.index; set => tracker.index = (SteamVR_TrackedObject.EIndex)value; }
        }
    }
}
