using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SpatialTracking;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore.BodyTracking;
using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.VRCore.Backends
{
    // The SteamVR player prefab with its SteamVR components taken out, leaving the transforms, the cameras and the
    // TrackedPoseDriver that moves the head with any XR input subsystem. Several of those components start SteamVR as
    // soon as they are enabled (SteamVR_Behaviour, SteamVR_Render, SteamVR_Behaviour_Pose, Hand...), so the prefab is
    // instantiated under an inactive parent and stripped before anything in it is enabled.
    //
    // Nothing is tracked but the head. The hands are kept as inert stand-ins on the prefab's hand objects, never
    // active and with nothing to vibrate, as much of the mod expects VRPlayer.leftHand and rightHand to exist even
    // without VR controls, as they do under SteamVR. There are no laser pointers (GetPointer() is null), and the body
    // tracking and tracked objects track nothing.
    public sealed class OpenXRRigBackend : IVRRigBackend
    {
        // Components of the SteamVR plugin that never reach the runtime and that the mod relies on: the fade drawn
        // by the camera (see OpenXRBackend.Fade) and the Interaction System's input module, part of the EventSystem
        // that VRGUI installs its own input module on.
        private static readonly HashSet<string> keptSteamVRComponents = new HashSet<string>
        {
            "Valve.VR.SteamVR_Fade",
            "Valve.VR.InteractionSystem.InputModule",
        };

        private OpenXRRigPlayer player;

        public VRRigPlayer Player => player ? player : null;

        public GameObject CreateRig(GameObject template)
        {
            var holder = new GameObject("VHVR_OpenXRRigHolder");
            holder.SetActive(false);
            GameObject rig = UnityEngine.Object.Instantiate(template, holder.transform);
            Transform hmd = findHmd(rig);
            var handObjects = new List<GameObject>();
            foreach (var component in rig.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component != null && component.GetType().FullName == "Valve.VR.InteractionSystem.Hand")
                {
                    handObjects.Add(component.gameObject);
                }
            }
            stripSteamVR(rig);
            foreach (var handObject in handObjects)
            {
                handObject.AddComponent<OpenXRInertHand>();
            }
            var marker = rig.AddComponent<OpenXRRigPlayerMarker>();
            marker.hmdTransform = hmd;
            rig.transform.SetParent(null, false);
            UnityEngine.Object.Destroy(holder);
            player = new OpenXRRigPlayer(marker);
            if (hmd == null)
            {
                LogError("The rig has no VR camera: the head will not be tracked.");
            }
            return rig;
        }

        public VRRigPlayer GetPlayer(GameObject root)
        {
            var marker = root.GetComponent<OpenXRRigPlayerMarker>();
            if (marker == null)
            {
                return null;
            }
            return player != null && player.Marker == marker ? player : new OpenXRRigPlayer(marker);
        }

        public VRHand[] GetHands(GameObject root)
        {
            var native = root.GetComponentsInChildren<OpenXRInertHand>();
            var result = new VRHand[native.Length];
            for (int i = 0; i < native.Length; i++)
            {
                result[i] = Wrap(native[i]);
            }
            return result;
        }

        private readonly Dictionary<OpenXRInertHand, InertHand> hands = new Dictionary<OpenXRInertHand, InertHand>();

        private InertHand Wrap(OpenXRInertHand hand)
        {
            if (hand == null)
            {
                return null;
            }
            if (!hands.TryGetValue(hand, out var wrapped))
            {
                hands[hand] = wrapped = new InertHand(hand, this);
            }
            return wrapped;
        }

        public VRLaserPointer GetPointer(GameObject hand) => null;
        public VRTrackingProvider AddBodyTracking(GameObject root) => new NullTrackingProvider(root.AddComponent<OpenXRInertTracking>());
        public VRTrackedObject AddTrackedObject(GameObject root) => new NullTrackedObject(root.AddComponent<OpenXRInertTracking>());

        // The VR camera, the SteamVR Player's first hmdTransform, given a TrackedPoseDriver for the head. The prefab
        // has none: SteamVR moves the camera through Unity's implicit tracking of stereo cameras, which the OpenXR
        // plugin does not do, leaving the view fixed. The hands, GUI panel and world space UI cameras are children
        // of this camera and follow it.
        private static Transform findHmd(GameObject rig)
        {
            Camera camera = null;
            foreach (var candidate in rig.GetComponentsInChildren<Camera>(true))
            {
                if (candidate.name == CameraUtils.VR_CAMERA)
                {
                    camera = candidate;
                    break;
                }
                if (camera == null && candidate.stereoTargetEye != StereoTargetEyeMask.None)
                {
                    camera = candidate;
                }
            }
            if (camera == null)
            {
                return null;
            }
            var driver = camera.GetComponent<TrackedPoseDriver>() ?? camera.gameObject.AddComponent<TrackedPoseDriver>();
            driver.SetPoseSource(TrackedPoseDriver.DeviceType.GenericXRDevice, TrackedPoseDriver.TrackedPose.Head);
            driver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            driver.UseRelativeTransform = false;
            LogInfo("Head tracking: TrackedPoseDriver on " + camera.name + ".");
            return camera.transform;
        }

        private static void stripSteamVR(GameObject rig)
        {
            // Destroying a component that another one requires fails, so this goes round until nothing more goes.
            var removed = new SortedDictionary<string, int>();
            for (int pass = 0; pass < 4; pass++)
            {
                bool progress = false;
                foreach (var component in rig.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component == null)
                    {
                        continue;
                    }
                    Type type = component.GetType();
                    string ns = type.Namespace ?? "";
                    if (!ns.StartsWith("Valve.VR", StringComparison.Ordinal) || keptSteamVRComponents.Contains(type.FullName))
                    {
                        continue;
                    }
                    UnityEngine.Object.DestroyImmediate(component);
                    if (component == null)
                    {
                        removed.TryGetValue(type.Name, out int count);
                        removed[type.Name] = count + 1;
                        progress = true;
                    }
                }
                if (!progress)
                {
                    break;
                }
            }
            var summary = new List<string>();
            foreach (var entry in removed)
            {
                summary.Add(entry.Value > 1 ? entry.Key + " x" + entry.Value : entry.Key);
            }
            LogInfo("Removed SteamVR components from the rig: " + string.Join(", ", summary.ToArray()));
            foreach (var component in rig.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component != null && (component.GetType().Namespace ?? "").StartsWith("Valve.VR", StringComparison.Ordinal) &&
                    !keptSteamVRComponents.Contains(component.GetType().FullName))
                {
                    LogError("Could not remove " + component.GetType().Name + " from " + component.name + ": it may start SteamVR.");
                }
            }
        }

        sealed class OpenXRRigPlayer : VRRigPlayer
        {
            internal readonly OpenXRRigPlayerMarker Marker;
            public OpenXRRigPlayer(OpenXRRigPlayerMarker marker) : base(marker) { Marker = marker; }
            public override Transform hmdTransform =>
                Marker.hmdTransform != null && Marker.hmdTransform.gameObject.activeInHierarchy ? Marker.hmdTransform : null;
            // As the SteamVR Player computes it, with the rig's root as the tracking origin.
            public override float eyeHeight
            {
                get
                {
                    Transform hmd = hmdTransform;
                    if (hmd == null)
                    {
                        return 0;
                    }
                    Transform origin = Marker.transform;
                    Vector3 eyeOffset = Vector3.Project(hmd.position - origin.position, origin.up);
                    return eyeOffset.magnitude / origin.lossyScale.x;
                }
            }
        }

        sealed class InertHand : VRHand
        {
            readonly OpenXRInertHand hand;
            readonly OpenXRRigBackend rig;
            public InertHand(OpenXRInertHand hand, OpenXRRigBackend rig) : base(hand) { this.hand = hand; this.rig = rig; }
            public override bool isActive => false;
            public override bool isPoseValid => false;
            public override VRHand otherHand
            {
                get
                {
                    foreach (var other in hand.transform.root.GetComponentsInChildren<OpenXRInertHand>(true))
                    {
                        if (other != hand)
                        {
                            return rig.Wrap(other);
                        }
                    }
                    return null;
                }
            }
            public override VRHapticAction hapticAction => VRInputActions.valheim_Haptic;
            public override Vector3 GetTrackedObjectVelocity() => Vector3.zero;
            public override Vector3 GetTrackedObjectAngularVelocity() => Vector3.zero;
            public override void SetVisibility(bool visible) { }
        }

        sealed class NullTrackingProvider : VRTrackingProvider
        {
            public NullTrackingProvider(Component component) : base(component) { }
            public override void Initialize(Transform trackingOrigin) { }
            public override bool IsAvailable => false;
            public override bool IsJointActive(BodyJoint joint) => false;
            public override Transform GetJointTransform(BodyJoint joint) => null;
        }

        sealed class NullTrackedObject : VRTrackedObject
        {
            public NullTrackedObject(Component component) : base(component) { }
            public override bool isValid => false;
            public override int index { get; set; }
        }
    }

    // Marks the root of an OpenXR rig and holds its head, standing in for the SteamVR Player.
    public sealed class OpenXRRigPlayerMarker : MonoBehaviour
    {
        public Transform hmdTransform;
    }

    // Where an OpenXR rig's stand-in for a hand hangs (see OpenXRRigBackend.InertHand).
    public sealed class OpenXRInertHand : MonoBehaviour { }

    // What the body tracking and tracked objects of an OpenXR rig hang on: they track nothing.
    public sealed class OpenXRInertTracking : MonoBehaviour { }
}
