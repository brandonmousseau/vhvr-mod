using System.Collections.Generic;
using UnityEngine;

namespace ValheimVRMod.VRCore.Backends
{
    // A binding VHVR suggests to the runtime for one of its own OpenXR actions: Input is the input's path on the hand,
    // e.g. "a/click" for /user/hand/right/input/a/click. The runtime may rebind it (e.g. in SteamVR's binding UI).
    sealed class XRSuggestedBinding
    {
        public string Action;
        public XRHand Hand;
        public string Input;
        public string Path => (Hand == XRHand.Left ? "/user/hand/left" : "/user/hand/right") + "/input/" + Input;
    }

    enum XRCompatKind
    {
        // A stick or pad direction used as a button (SteamVR's dpad mode).
        Dpad,
        // Two inputs held together (SteamVR's chords).
        Chord,
    }

    // A binding OpenXR has no way to express, worked out by the mod: SteamVR binds the action with a chord or a dpad
    // direction, and the profile has no suggested binding for it. It applies to its action on its hand only while the
    // runtime has that action unbound there, so a binding the player makes in the runtime's UI replaces it.
    sealed class XRCompatBinding
    {
        public string Action;
        // The hand the action is reported on: a chord's is its last input's.
        public XRHand Source;
        public XRCompatKind Kind;
        public XRCondition Condition;
    }

    // One OpenXR interaction profile and the mod's actions on it, written after the mod's SteamVR bindings for the same
    // controllers (StreamingAssets/SteamVR/bindings_*.json):
    // - Suggested: every binding of the SteamVR file that OpenXR can express (a button, a trigger or grip value, a
    //   stick or pad position, a touch sensor), suggested to the runtime for VHVR's own OpenXR actions;
    // - Compat: the chords and dpad directions, which OpenXR cannot express. They are made of the mod's own actions
    //   where one stands for the input (the grip is Grab, the trigger LeftClick, a face button Jump or RightClick, a
    //   stick Walk or PitchAndYaw), as worked out this frame: a chord then changes in the same frame and at the same
    //   threshold as the actions it is made of, and follows them when they are rebound. Only an input no action stands
    //   for is read from the controller's legacy XR device (OpenXRController).
    // SteamVR reports the buttons that make up a chord as well as the chord, and so does this (LaserPointerChords counts
    // on it). SteamVR's scroll mode (ContextScroll, HotbarScroll) has no counterpart.
    sealed class OpenXRProfile
    {
        public readonly string Name;
        public readonly string InteractionProfile;
        // The plugin's name for the profile's legacy XR device (kDeviceLocalizedName), should the runtime not say
        // which profile it uses.
        public readonly string DeviceName;
        // The SteamVR bindings file the profile is written after, e.g. "oculus_touch"; null for none.
        public readonly string SteamVRController;
        public readonly List<XRSuggestedBinding> Suggested = new List<XRSuggestedBinding>();
        public readonly List<XRCompatBinding> Compat = new List<XRCompatBinding>();

        public OpenXRProfile(string name, string interactionProfile, string deviceName, string steamVRController)
        {
            Name = name;
            InteractionProfile = interactionProfile;
            DeviceName = deviceName;
            SteamVRController = steamVRController;
        }

        public void Suggest(string action, XRHand hand, params string[] inputs)
        {
            foreach (string input in inputs)
            {
                Suggested.Add(new XRSuggestedBinding { Action = action, Hand = hand, Input = input });
            }
        }

        public void Dpad(string action, XRHand hand, XRCondition condition) =>
            Compat.Add(new XRCompatBinding { Action = action, Source = hand, Kind = XRCompatKind.Dpad, Condition = condition });

        // Reported on the hand of the button, the chord's last input.
        public void Chord(string action, XRCondition modifier, XRHand buttonHand, XRCondition button) =>
            Compat.Add(new XRCompatBinding
            {
                Action = action,
                Source = buttonHand,
                Kind = XRCompatKind.Chord,
                Condition = (l, r) => modifier(l, r) && button(l, r),
            });

        public bool IsSuggested(string action, XRHand hand)
        {
            foreach (XRSuggestedBinding binding in Suggested)
            {
                if (binding.Action == action && binding.Hand == hand) return true;
            }
            return false;
        }
    }

    // The interaction profiles OpenXRLoaderSetup enables, with their suggested and compat bindings.
    static class OpenXRProfiles
    {
        // Before the profiles: static fields are initialized in the order they are written, and the profiles use it.
        private static readonly XRHand[] Hands = { XRHand.Left, XRHand.Right };
        private const XRHand L = XRHand.Left;
        private const XRHand R = XRHand.Right;

        public static readonly OpenXRProfile Touch = touch();
        public static readonly OpenXRProfile Index = index();
        public static readonly OpenXRProfile Vive = vive();
        public static readonly OpenXRProfile WindowsMR = windowsMR();
        public static readonly OpenXRProfile Simple = simple();
        public static readonly OpenXRProfile[] All = { Touch, Index, Vive, WindowsMR, Simple };

        // The profile of an interaction profile path, else of a legacy XR device name; null for neither.
        public static OpenXRProfile Find(string interactionProfile, string deviceName)
        {
            foreach (OpenXRProfile profile in All)
            {
                if (interactionProfile != null && profile.InteractionProfile == interactionProfile) return profile;
            }
            foreach (OpenXRProfile profile in All)
            {
                if (deviceName != null && profile.DeviceName == deviceName) return profile;
            }
            return null;
        }

        // Every binding as plain rows, for the offline checks: profile path, SteamVR controller, "suggest" or the compat
        // kind, action path, hand, input path ("" for compat).
        internal static List<string[]> Rows()
        {
            var rows = new List<string[]>();
            foreach (OpenXRProfile profile in All)
            {
                foreach (XRSuggestedBinding binding in profile.Suggested)
                {
                    rows.Add(new[] { profile.InteractionProfile, profile.SteamVRController ?? "", "suggest", binding.Action,
                                     binding.Hand.ToString(), binding.Path });
                }
                foreach (XRCompatBinding binding in profile.Compat)
                {
                    rows.Add(new[] { profile.InteractionProfile, profile.SteamVRController ?? "", binding.Kind.ToString().ToLowerInvariant(),
                                     binding.Action, binding.Source.ToString(), "" });
                }
            }
            return rows;
        }

        private static string D(string name) => "/actions/default/in/" + name;
        private static string V(string name) => "/actions/Valheim/in/" + name;

        private static OpenXRController On(XRHand hand, OpenXRController l, OpenXRController r) => hand == L ? l : r;

        // One of the mod's actions as pressed on the hand this frame (by the runtime, or by an earlier compat binding).
        private static XRCondition Held(XRHand hand, string action) => (l, r) => On(hand, l, r).ActionHeld(action);
        private static XRCondition Grab(XRHand hand) => Held(hand, V("Grab"));
        private static XRCondition Trigger(XRHand hand) => Held(hand, V("LeftClick"));
        private static XRCondition Jump(XRHand hand) => Held(hand, V("Jump"));
        private static XRCondition RightClick(XRHand hand) => Held(hand, V("RightClick"));
        private static XRCondition AllOf(XRCondition a, XRCondition b) => (l, r) => a(l, r) && b(l, r);

        enum Dir { North, East, South, West, Any }

        // SteamVR's dpad: the position out of the deadzone (a fraction of its range) within the direction's quarter,
        // widened by the overlap (a fraction of a quarter). Any is every direction.
        private static bool InDirection(Vector2 v, Dir dir, float deadzone, float overlap)
        {
            if (v.magnitude <= deadzone)
            {
                return false;
            }
            if (dir == Dir.Any)
            {
                return true;
            }
            float center = dir == Dir.East ? 0 : dir == Dir.North ? 90 : dir == Dir.West ? 180 : -90;
            float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            return Mathf.Abs(Mathf.DeltaAngle(angle, center)) <= 45 * (1 + overlap);
        }

        // A direction of one of the mod's vector2 actions (Walk, PitchAndYaw) on the hand.
        private static XRCondition Toward(XRHand hand, string axisAction, Dir dir, float deadzone = 0.25f, float overlap = 0) =>
            (l, r) => InDirection(On(hand, l, r).ActionAxis(axisAction), dir, deadzone, overlap);
        private static XRCondition RightStick(Dir dir) => Toward(R, V("PitchAndYaw"), dir);

        // A direction of a 2D control of the hand's legacy XR device, for an input no action stands for.
        private static XRCondition DeviceToward(XRHand hand, string usage, Dir dir, float deadzone = 0.25f, float overlap = 0) =>
            (l, r) => InDirection(On(hand, l, r).Vector2(usage), dir, deadzone, overlap);
        private static XRCondition DeviceClick(XRHand hand, string usage) => (l, r) => On(hand, l, r).Button(usage);

        // The building and item chords that every profile with a grip and a trigger shares.
        private static void pieceChords(OpenXRProfile p, System.Func<Dir, XRCondition> direction)
        {
            p.Chord(V("FacePieceToward"), Grab(R), R, direction(Dir.South));
            p.Chord(V("FacePieceAway"), Grab(R), R, direction(Dir.North));
            p.Chord(V("RotatePieceClockwise"), Grab(R), R, direction(Dir.West));
            p.Chord(V("RotatePieceCounterclockwise"), Grab(R), R, direction(Dir.East));
        }

        private static void itemChords(OpenXRProfile p, bool bothHands)
        {
            foreach (XRHand h in bothHands ? Hands : new[] { R })
            {
                p.Chord(V("AddMapPin"), Grab(h), h, Trigger(h));
                p.Chord(V("DiscardItem"), Grab(h), h, Trigger(h));
            }
            p.Chord(V("SplitStack"), Grab(L), R, Trigger(R));
            if (bothHands)
            {
                p.Chord(V("SplitStack"), Grab(R), L, Trigger(L));
            }
        }

        // Oculus Touch, and the controllers runtimes give as Touch (Quest, Rift, Pico...): bindings_oculus_touch.json.
        private static OpenXRProfile touch()
        {
            var p = new OpenXRProfile("Oculus Touch", "/interaction_profiles/oculus/touch_controller", "Oculus Touch Controller OpenXR", "oculus_touch");
            // Triggers and grips are values: the runtime thresholds them.
            foreach (XRHand h in Hands)
            {
                p.Suggest(D("InteractUI"), h, "trigger/value");
                p.Suggest(D("GrabPinch"), h, "trigger/value");
                p.Suggest(D("GrabGrip"), h, "squeeze/value");
                p.Suggest(V("Use"), h, "trigger/value");
                p.Suggest(V("LeftClick"), h, "trigger/value");
                p.Suggest(V("Grab"), h, "squeeze/value");
                p.Suggest(V("StopGesturedLocomotion"), h, "trigger/value", "squeeze/value", "thumbstick/touch");
            }
            p.Suggest(V("Walk"), L, "thumbstick");
            p.Suggest(V("ToggleMap"), L, "thumbstick/click");
            p.Suggest(V("ToggleInventory"), L, "x/click");
            p.Suggest(V("QuickActions"), L, "y/click");
            p.Suggest(V("RightClick"), L, "y/click");
            p.Suggest(V("StopGesturedLocomotion"), L, "x/touch", "y/touch");
            p.Suggest(V("PitchAndYaw"), R, "thumbstick");
            p.Suggest(V("ToggleMenu"), R, "thumbstick/click");
            p.Suggest(V("Jump"), R, "a/click");
            p.Suggest(V("QuickSwitch"), R, "b/click");
            p.Suggest(V("RightClick"), R, "b/click");
            p.Suggest(V("StopGesturedLocomotion"), R, "a/touch", "b/touch");

            // The default set's teleport: either stick north. Walk and PitchAndYaw belong to the Valheim set, so the
            // stick is read from the device.
            foreach (XRHand h in Hands)
            {
                p.Dpad(D("Teleport"), h, DeviceToward(h, "Primary2DAxis", Dir.North, 0.25f, 0.3f));
            }
            p.Dpad(V("ToggleRun"), R, RightStick(Dir.North));
            p.Dpad(V("ToggleCrouch"), R, RightStick(Dir.South));
            pieceChords(p, RightStick);
            p.Chord(V("RemovePiece"), Grab(R), R, Jump(R));
            p.Chord(V("ScrollUp"), Grab(R), R, RightStick(Dir.North));
            p.Chord(V("ScrollDown"), Grab(R), R, RightStick(Dir.South));
            p.Chord(V("Dodge"), Trigger(L), R, Jump(R));
            foreach (XRHand h in Hands)
            {
                p.Chord(V("MiddleClick"), Grab(h), h, RightClick(h));
            }
            itemChords(p, bothHands: true);
            return p;
        }

        // Valve Index: bindings_knuckles.json, which has no default set bindings and binds Grab on the left grip only;
        // both are given here as on the other controllers.
        private static OpenXRProfile index()
        {
            var p = new OpenXRProfile("Valve Index", "/interaction_profiles/valve/index_controller", "Index Controller OpenXR", "knuckles");
            foreach (XRHand h in Hands)
            {
                p.Suggest(D("InteractUI"), h, "trigger/click");
                p.Suggest(D("GrabPinch"), h, "trigger/click");
                // SteamVR clicks the Index grip by force: its value follows the fingers resting on it.
                p.Suggest(D("GrabGrip"), h, "squeeze/force");
                p.Suggest(V("Use"), h, "trigger/click");
                p.Suggest(V("LeftClick"), h, "trigger/click");
                p.Suggest(V("Grab"), h, "squeeze/force");
                p.Suggest(V("RightClick"), h, "b/click");
                p.Suggest(V("StopGesturedLocomotion"), h,
                          "trigger/click", "trackpad/touch", "thumbstick/touch", "a/touch", "b/touch", "system/touch");
            }
            p.Suggest(V("Walk"), L, "thumbstick");
            p.Suggest(V("ToggleMenu"), L, "trackpad/force");
            p.Suggest(V("ToggleMap"), L, "a/click");
            p.Suggest(V("QuickActions"), L, "b/click");
            p.Suggest(V("PitchAndYaw"), R, "thumbstick");
            p.Suggest(V("ToggleInventory"), R, "trackpad/force");
            p.Suggest(V("Jump"), R, "a/click");
            p.Suggest(V("QuickSwitch"), R, "b/click");

            p.Dpad(V("ToggleRun"), R, RightStick(Dir.North));
            p.Dpad(V("ToggleCrouch"), R, RightStick(Dir.South));
            pieceChords(p, RightStick);
            p.Chord(V("RemovePiece"), Grab(R), R, Jump(R));
            p.Chord(V("Dodge"), Grab(L), R, Jump(R));
            foreach (XRHand h in Hands)
            {
                p.Chord(V("MiddleClick"), Grab(h), h, RightClick(h));
            }
            itemChords(p, bothHands: true);
            return p;
        }

        // HTC Vive wands: bindings_vive_controller.json, where the trackpads stand in for the sticks. That file binds no
        // Grab and LeftClick on the left trigger only; Grab is suggested on both grips here, as the chords are made of
        // it, and LeftClick on both triggers, as on the other controllers.
        private static OpenXRProfile vive()
        {
            var p = new OpenXRProfile("HTC Vive", "/interaction_profiles/htc/vive_controller", "HTC Vive Controller OpenXR", "vive_controller");
            foreach (XRHand h in Hands)
            {
                p.Suggest(D("InteractUI"), h, "trigger/click");
                p.Suggest(D("GrabPinch"), h, "trigger/value");
                p.Suggest(D("GrabGrip"), h, "squeeze/click");
                // SteamVR teleports on the pad's center, north and south clicked; the whole pad clicked here.
                p.Suggest(D("Teleport"), h, "trackpad/click");
                p.Suggest(V("Use"), h, "trigger/click");
                p.Suggest(V("LeftClick"), h, "trigger/click");
                p.Suggest(V("Grab"), h, "squeeze/click");
                p.Suggest(V("RightClick"), h, "menu/click");
                p.Suggest(V("StopGesturedLocomotion"), h, "trigger/click", "squeeze/click", "trackpad/touch");
            }
            p.Suggest(V("Walk"), L, "trackpad");
            p.Suggest(V("ToggleInventory"), L, "trackpad/click");
            p.Suggest(V("QuickActions"), L, "menu/click");
            p.Suggest(V("PitchAndYaw"), R, "trackpad");
            p.Suggest(V("Jump"), R, "trackpad/click");
            p.Suggest(V("QuickSwitch"), R, "menu/click");

            // The right pad's directions are clicked (SteamVR's click sub-mode): the pad click is Jump.
            XRCondition RightPadClick(Dir dir) => AllOf(Jump(R), RightStick(dir));
            p.Dpad(V("ToggleRun"), R, RightPadClick(Dir.North));
            p.Dpad(V("ToggleCrouch"), R, RightPadClick(Dir.South));
            // The left pad's are touched: Walk is zero while it is not.
            p.Dpad(V("ScrollUp"), L, Toward(L, V("Walk"), Dir.North, 0.5f));
            p.Dpad(V("ScrollDown"), L, Toward(L, V("Walk"), Dir.South, 0.5f));
            pieceChords(p, RightPadClick);
            p.Chord(V("RemovePiece"), Grab(R), R, Jump(R));
            p.Chord(V("ToggleMenu"), Grab(R), R, RightClick(R));
            p.Chord(V("ToggleMap"), Grab(L), L, RightClick(L));
            p.Chord(V("Dodge"), Grab(L), R, Jump(R));
            p.Chord(V("MiddleClick"), Grab(R), R, RightPadClick(Dir.South));
            itemChords(p, bothHands: true);
            return p;
        }

        // Windows Mixed Reality: bindings_holographic_controller.json. The stick moves and turns; the touchpad's
        // quarters, clicked, are buttons. No action stands for the touchpad, so it is read from the device, where the
        // plugin gives it as the secondary 2D axis.
        private static OpenXRProfile windowsMR()
        {
            var p = new OpenXRProfile("Windows Mixed Reality", "/interaction_profiles/microsoft/motion_controller", "Windows MR Controller OpenXR", "holographic_controller");
            foreach (XRHand h in Hands)
            {
                p.Suggest(D("InteractUI"), h, "trigger/value");
                p.Suggest(D("GrabPinch"), h, "trigger/value");
                p.Suggest(D("GrabGrip"), h, "squeeze/click");
                p.Suggest(D("Teleport"), h, "trackpad/click");
                p.Suggest(V("Use"), h, "trigger/value");
                p.Suggest(V("LeftClick"), h, "trigger/value");
                p.Suggest(V("Grab"), h, "squeeze/click");
            }
            p.Suggest(V("Walk"), L, "thumbstick");
            p.Suggest(V("ToggleMenu"), L, "menu/click");
            p.Suggest(V("PitchAndYaw"), R, "thumbstick");
            p.Suggest(V("ToggleInventory"), R, "menu/click");

            XRCondition Quarter(XRHand h, Dir dir) =>
                AllOf(DeviceClick(h, "Secondary2DAxisClick"), DeviceToward(h, "Secondary2DAxis", dir, 0.5f));
            foreach (XRHand h in Hands)
            {
                p.Dpad(V("QuickSwitch"), h, Quarter(h, Dir.East));
                p.Dpad(V("QuickActions"), h, Quarter(h, Dir.West));
            }
            p.Dpad(V("ToggleMap"), L, Quarter(L, Dir.North));
            p.Dpad(V("HotbarUse"), L, Quarter(L, Dir.South));
            p.Dpad(V("RightClick"), L, Quarter(L, Dir.West));
            p.Dpad(V("Jump"), R, Quarter(R, Dir.North));
            p.Dpad(V("Sit"), R, Quarter(R, Dir.South));
            p.Dpad(V("RightClick"), R, Quarter(R, Dir.East));
            p.Dpad(V("StopGesturedLocomotion"), R, RightStick(Dir.Any));
            // SteamVR also teleports on either stick north; Teleport is suggested on the touchpad click, so that has
            // no compat binding.
            pieceChords(p, RightStick);
            p.Chord(V("RemovePiece"), Grab(R), R, Quarter(R, Dir.North));
            p.Chord(V("MiddleClick"), Grab(L), R, Grab(R));
            itemChords(p, bothHands: false);
            return p;
        }

        // The Khronos simple controller, the last resort of a runtime with none of the others: a select and a menu
        // button. There is no SteamVR counterpart.
        private static OpenXRProfile simple()
        {
            var p = new OpenXRProfile("Khronos Simple", "/interaction_profiles/khr/simple_controller", "KHR Simple Controller OpenXR", null);
            foreach (XRHand h in Hands)
            {
                p.Suggest(D("InteractUI"), h, "select/click");
                p.Suggest(V("Use"), h, "select/click");
                p.Suggest(V("LeftClick"), h, "select/click");
            }
            p.Suggest(V("ToggleMenu"), L, "menu/click");
            p.Suggest(V("ToggleInventory"), R, "menu/click");
            return p;
        }
    }
}
