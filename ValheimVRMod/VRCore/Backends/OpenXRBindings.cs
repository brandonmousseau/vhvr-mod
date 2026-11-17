using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace ValheimVRMod.VRCore.Backends
{
    enum XRHand { Left = 0, Right = 1 }

    // One hand's controller, read through the legacy XR input (UnityEngine.XR, the API the head's TrackedPoseDriver
    // reads), which the OpenXR plugin fills from the interaction profile in use (OpenXRLoaderSetup enables them): its
    // pose, and the few controls the compat bindings read directly (OpenXRProfiles); the actions themselves are VHVR's
    // own OpenXR actions. Controls are read by feature usage name (CommonUsages: "TriggerButton", "Primary2DAxis"...);
    // one the controller lacks reads as released or zero.
    sealed class OpenXRController
    {
        private readonly List<InputFeatureUsage> features = new List<InputFeatureUsage>();
        private InputDevice device;

        public bool IsConnected => device.isValid;
        public string Name => device.isValid ? device.name : null;

        // The mod's own actions on this hand as worked out so far this frame (set by OpenXRInputBackend), for the
        // compat bindings that are made of them (see OpenXRProfiles).
        internal System.Func<string, bool> ActionHeld = action => false;
        internal System.Func<string, Vector2> ActionAxis = action => UnityEngine.Vector2.zero;

        // Where the SteamVR raw pose of the controller (what the hands of the rig follow, and what the mod's held
        // item offsets and laser pointer are made for) is from the OpenXR grip pose.
        private Vector3 rawPositionInGrip;
        private Quaternion rawRotationInGrip = Quaternion.identity;
        // The OpenXR grip pose in the raw pose.
        private Vector3 gripPosition;

        public void SetDevice(InputDevice newDevice, XRHand hand)
        {
            if (newDevice == device)
            {
                return;
            }
            device = newDevice;
            if (device.isValid)
            {
                setGrip(device.name, hand);
            }
        }

        public bool Button(string usage)
        {
            if (device.TryGetFeatureValue(new InputFeatureUsage<bool>(usage), out bool pressed)) return pressed;
            return device.TryGetFeatureValue(new InputFeatureUsage<float>(usage), out float value) && value >= 0.5f;
        }

        public float Axis(string usage)
        {
            if (device.TryGetFeatureValue(new InputFeatureUsage<float>(usage), out float value)) return value;
            return device.TryGetFeatureValue(new InputFeatureUsage<bool>(usage), out bool pressed) && pressed ? 1 : 0;
        }

        public Vector2 Vector2(string usage) =>
            device.TryGetFeatureValue(new InputFeatureUsage<Vector2>(usage), out Vector2 value) ? value : UnityEngine.Vector2.zero;

        public Vector3 Vector3(string usage) =>
            device.TryGetFeatureValue(new InputFeatureUsage<Vector3>(usage), out Vector3 value) ? value : UnityEngine.Vector3.zero;

        public Quaternion Rotation(string usage) =>
            device.TryGetFeatureValue(new InputFeatureUsage<Quaternion>(usage), out Quaternion value) ? value : Quaternion.identity;

        public bool Has(string usage)
        {
            if (!device.TryGetFeatureUsages(features)) return false;
            foreach (InputFeatureUsage feature in features)
            {
                if (feature.name == usage) return true;
            }
            return false;
        }

        // The device's features, for the log.
        public string Features()
        {
            if (!device.TryGetFeatureUsages(features)) return "none";
            var names = new List<string>();
            foreach (InputFeatureUsage feature in features) names.Add(feature.name);
            return string.Join(", ", names.ToArray());
        }

        public void GripToRaw(Vector3 gripPos, Quaternion gripRot, out Vector3 rawPos, out Quaternion rawRot)
        {
            rawRot = gripRot * rawRotationInGrip;
            rawPos = gripPos + gripRot * rawPositionInGrip;
        }

        // Where the grip pose is from the raw pose, in tracking space, for the velocity of the raw pose.
        public Vector3 GripOffset(Quaternion rawRot) => rawRot * gripPosition;

        // SteamVR's render model files (resources/rendermodels/*/*.json) place the OpenXR grip pose relative to the
        // raw pose as the "openxr_grip" component, in OpenVR's right-handed coordinates; these are those values. The
        // Touch offsets stand in for the controllers SteamVR's files have none for (it shares them across Quest,
        // Rift, Pico and Vive Focus controllers); the Index and Vive models have no openxr_grip, so their "grip"
        // component is used. The plugin names a device after its profile (kDeviceLocalizedName).
        private void setGrip(string deviceName, XRHand hand)
        {
            Vector3 position;
            Vector3 euler;
            switch (deviceName)
            {
                case "Index Controller OpenXR":
                    position = new Vector3(0, 0.003f, 0.097f);
                    euler = new Vector3(5.037f, 0, 0);
                    break;
                case "HTC Vive Controller OpenXR":
                    position = new Vector3(0, -0.015f, 0.097f);
                    euler = new Vector3(5.037f, 0, 0);
                    break;
                default:
                    position = new Vector3(hand == XRHand.Left ? 0.007f : -0.007f, -0.00182941f, 0.1019482f);
                    euler = new Vector3(20.6f, 0, 0);
                    break;
            }
            // Into Unity's coordinates: z negated, and with it the sense of rotations about x and y.
            position.z = -position.z;
            Quaternion rotation = Quaternion.Euler(-euler.x, -euler.y, euler.z);
            gripPosition = position;
            // grip = raw * (position, rotation), so raw = grip * inverse(position, rotation).
            rawRotationInGrip = Quaternion.Inverse(rotation);
            rawPositionInGrip = -(Quaternion.Inverse(rotation) * position);
        }
    }

    delegate bool XRCondition(OpenXRController left, OpenXRController right);
    delegate Vector2 XRAxisReader(OpenXRController left, OpenXRController right);

    sealed class XRBooleanBinding
    {
        public string Action;
        public XRHand Source;
        public XRCondition Condition;
    }

    sealed class XRAxisBinding
    {
        public string Action;
        public XRHand Source;
        public XRAxisReader Read;
    }

    // The last resort, for when the runtime did not take VHVR's own OpenXR actions (see OpenXRProfiles for the normal
    // case): one generic set of bindings for every controller, on the legacy XR usages. These are the mod's Touch
    // bindings (StreamingAssets/SteamVR/bindings_oculus_touch.json), chords and stick directions included, which suit
    // any controller with a stick, two face buttons, a trigger and a grip. SteamVR reports the buttons that make up a
    // chord as well as the chord, and so does this (LaserPointerChords counts on it).
    static class OpenXRBindings
    {
        public static readonly List<XRBooleanBinding> Booleans = new List<XRBooleanBinding>();
        public static readonly List<XRAxisBinding> Axes = new List<XRAxisBinding>();

        private const XRHand L = XRHand.Left;
        private const XRHand R = XRHand.Right;
        private static readonly XRHand[] Hands = { L, R };

        private static string D(string name) => "/actions/default/in/" + name;
        private static string V(string name) => "/actions/Valheim/in/" + name;

        private static void Bool(string action, XRHand source, XRCondition condition) =>
            Booleans.Add(new XRBooleanBinding { Action = action, Source = source, Condition = condition });
        private static void Axis(string action, XRHand source, XRAxisReader read) =>
            Axes.Add(new XRAxisBinding { Action = action, Source = source, Read = read });

        // A chord is reported on the hand of its last input, which is the button of a modifier + button chord.
        private static void Chord(string action, XRCondition modifier, XRHand buttonHand, XRCondition button) =>
            Bool(action, buttonHand, (l, r) => modifier(l, r) && button(l, r));

        private static OpenXRController On(XRHand hand, OpenXRController l, OpenXRController r) => hand == L ? l : r;
        private static XRCondition Click(XRHand hand, string usage) => (l, r) => On(hand, l, r).Button(usage);
        private static XRCondition AtLeast(XRHand hand, string usage, float threshold) => (l, r) => On(hand, l, r).Axis(usage) >= threshold;
        private static XRCondition AnyOf(params XRCondition[] conditions) => (l, r) =>
        {
            foreach (XRCondition condition in conditions)
            {
                if (condition(l, r)) return true;
            }
            return false;
        };

        // The grip as a button: the grip button where there is one, else the grip squeezed most of the way.
        private static XRCondition Grip(XRHand hand) => AnyOf(Click(hand, "GripButton"), AtLeast(hand, "Grip", 0.8f));
        private static XRCondition Trigger(XRHand hand) => Click(hand, "TriggerButton");

        enum Dir { North, East, South, West }

        // A SteamVR dpad direction: the stick out of the deadzone (a fraction of its range) within the direction's
        // quarter, widened by the overlap (a fraction of a quarter).
        private static XRCondition Stick(XRHand hand, Dir dir, float overlap = 0)
        {
            const float deadzone = 0.25f;
            float center = dir == Dir.East ? 0 : dir == Dir.North ? 90 : dir == Dir.West ? 180 : -90;
            float halfWidth = 45 * (1 + overlap);
            return (l, r) =>
            {
                Vector2 v = On(hand, l, r).Vector2("Primary2DAxis");
                if (v.magnitude <= deadzone)
                {
                    return false;
                }
                float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
                return Mathf.Abs(Mathf.DeltaAngle(angle, center)) <= halfWidth;
            };
        }

        private static XRAxisReader StickAxis(XRHand hand) => (l, r) => On(hand, l, r).Vector2("Primary2DAxis");

        static OpenXRBindings()
        {
            foreach (XRHand h in Hands)
            {
                Bool(D("InteractUI"), h, Trigger(h));
                Bool(D("GrabGrip"), h, Grip(h));
                Bool(D("GrabPinch"), h, AtLeast(h, "Trigger", 0.8f));
                Bool(D("Teleport"), h, Stick(h, Dir.North, 0.3f));
                Bool(V("Use"), h, Trigger(h));
                Bool(V("LeftClick"), h, Trigger(h));
                Bool(V("Grab"), h, Grip(h));
                Bool(V("StopGesturedLocomotion"), h, AnyOf(
                    Trigger(h), Grip(h), Click(h, "Primary2DAxisTouch"), Click(h, "PrimaryTouch"), Click(h, "SecondaryTouch")));
            }
            Bool(V("ToggleMap"), L, Click(L, "Primary2DAxisClick"));
            Axis(V("Walk"), L, StickAxis(L));
            Bool(V("ToggleInventory"), L, Click(L, "PrimaryButton"));
            Bool(V("QuickActions"), L, Click(L, "SecondaryButton"));
            Bool(V("RightClick"), L, Click(L, "SecondaryButton"));

            Bool(V("ToggleMenu"), R, Click(R, "Primary2DAxisClick"));
            Axis(V("PitchAndYaw"), R, StickAxis(R));
            Bool(V("Jump"), R, Click(R, "PrimaryButton"));
            Bool(V("QuickSwitch"), R, Click(R, "SecondaryButton"));
            Bool(V("RightClick"), R, Click(R, "SecondaryButton"));
            Bool(V("ToggleRun"), R, Stick(R, Dir.North));
            Bool(V("ToggleCrouch"), R, Stick(R, Dir.South));

            Chord(V("FacePieceToward"), Grip(R), R, Stick(R, Dir.South));
            Chord(V("FacePieceAway"), Grip(R), R, Stick(R, Dir.North));
            Chord(V("RotatePieceClockwise"), Grip(R), R, Stick(R, Dir.West));
            Chord(V("RotatePieceCounterclockwise"), Grip(R), R, Stick(R, Dir.East));
            Chord(V("RemovePiece"), Grip(R), R, Click(R, "PrimaryButton"));
            Chord(V("ScrollUp"), Grip(R), R, Stick(R, Dir.North));
            Chord(V("ScrollDown"), Grip(R), R, Stick(R, Dir.South));
            Chord(V("Dodge"), Trigger(L), R, Click(R, "PrimaryButton"));
            foreach (XRHand h in Hands)
            {
                Chord(V("MiddleClick"), Grip(h), h, Click(h, "SecondaryButton"));
                Chord(V("AddMapPin"), Grip(h), h, Trigger(h));
                Chord(V("DiscardItem"), Grip(h), h, Trigger(h));
            }
            Chord(V("SplitStack"), Grip(L), R, Trigger(R));
            Chord(V("SplitStack"), Grip(R), L, Trigger(L));
        }
    }
}
