using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.XR;
using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.VRCore.Backends
{
    // The mod's actions (VRInputActions) under OpenXR, worked out once per frame with the same down and up edges,
    // listeners and action sets as SteamVR's. Per action and hand, in this order:
    // - VHVR's own OpenXR actions (BuildManifest(), registered with the runtime by VHVR.OpenXR's VhvrActionsFeature):
    //   the runtime reads them through the bindings suggested for the controller's interaction profile, or the
    //   player's own from the runtime's binding UI (SteamVR's). Whatever the runtime has bound wins.
    // - Otherwise the profile's compat bindings (OpenXRProfiles): the chords and dpad directions OpenXR cannot express.
    // - Should the runtime not have taken VHVR's actions at all, the generic legacy bindings (OpenXRBindings).
    // The poses come from the controllers' legacy XR devices (OpenXRController). There is no haptics yet.
    // Inert until Start(), when the runtime is up: every action is unbound and released, and nothing here touches
    // Unity, so the backend can be made and its actions resolved outside the game.
    public sealed class OpenXRInputBackend : IVRInputBackend
    {
        private const string DefaultSet = "/actions/default";

        private readonly Dictionary<string, BooleanAction> booleans = new Dictionary<string, BooleanAction>();
        private readonly Dictionary<string, AxisAction> axes = new Dictionary<string, AxisAction>();
        private readonly Dictionary<string, PoseAction> poses = new Dictionary<string, PoseAction>();
        private readonly Dictionary<string, XRActionSet> sets = new Dictionary<string, XRActionSet>();
        private readonly OpenXRController left = new OpenXRController();
        private readonly OpenXRController right = new OpenXRController();
        private string loggedDevices;
        private bool started;
        private int evaluatedFrame = -1;
        private int dispatchedFrame = -1;
        private bool loggedBindingUI;

        private IReadOnlyList<VRActionSetDecl> declaredSets = new VRActionSetDecl[0];
        private IReadOnlyList<VRActionDecl> declaredActions = new VRActionDecl[0];
        // The OpenXR action of each mod action path, and the mod set path of each OpenXR set, by index.
        private readonly Dictionary<string, int> runtimeActions = new Dictionary<string, int>();
        private readonly List<string> runtimeSets = new List<string>();
        private string loggedProfiles;
        private int? loggedRuntimeSignature;
        private bool loggedLegacyFallback;

        public event Action onNonVisualActionsUpdated;

        public OpenXRInputBackend()
        {
            left.ActionHeld = action => booleans.TryGetValue(action, out BooleanAction a) && a.PressedOn(XRHand.Left);
            right.ActionHeld = action => booleans.TryGetValue(action, out BooleanAction a) && a.PressedOn(XRHand.Right);
            left.ActionAxis = action => axes.TryGetValue(action, out AxisAction a) ? a.ValueOn(XRHand.Left) : Vector2.zero;
            right.ActionAxis = action => axes.TryGetValue(action, out AxisAction a) ? a.ValueOn(XRHand.Right) : Vector2.zero;
        }

        // Kept for BuildManifest(): VHVR's OpenXR actions are created with the runtime's instance, before the mod's
        // actions are resolved.
        public void Declare(IReadOnlyList<VRActionSetDecl> sets, IReadOnlyList<VRActionDecl> actions)
        {
            declaredSets = sets;
            declaredActions = actions;
        }

        // VHVR's OpenXR action sets and actions, one per declared set and per declared boolean or vector2 action, and
        // the bindings each profile suggests for them. Poses come from the controllers' devices, and there is no
        // haptics yet.
        internal OpenXRActionManifest BuildManifest()
        {
            var manifest = new OpenXRActionManifest();
            runtimeActions.Clear();
            runtimeSets.Clear();
            var setNames = new HashSet<string>();
            foreach (VRActionSetDecl set in declaredSets)
            {
                runtimeSets.Add(set.Path);
                manifest.Sets.Add(new OpenXRActionManifest.Set
                {
                    Name = unique(setNames, "vhvr_" + pathComponent(ShortName(set.Path))),
                    LocalizedName = "VHVR " + set.LocalizedName,
                });
            }
            var actionNames = new HashSet<string>();
            foreach (VRActionDecl action in declaredActions)
            {
                int set = runtimeSets.IndexOf(action.SetPath);
                if ((action.Kind != VRActionKind.Boolean && action.Kind != VRActionKind.Vector2) || set < 0)
                {
                    continue;
                }
                runtimeActions[action.Path] = manifest.Actions.Count;
                manifest.Actions.Add(new OpenXRActionManifest.Action
                {
                    Set = set,
                    // Unique per set is enough for OpenXR; per instance keeps the runtime's UI unambiguous.
                    Name = unique(actionNames, pathComponent(ShortName(action.Path))),
                    LocalizedName = action.LocalizedName,
                    IsVector2 = action.Kind == VRActionKind.Vector2,
                });
            }
            foreach (OpenXRProfile profile in OpenXRProfiles.All)
            {
                foreach (XRSuggestedBinding binding in profile.Suggested)
                {
                    if (runtimeActions.TryGetValue(binding.Action, out int index))
                    {
                        manifest.Bindings.Add(new OpenXRActionManifest.Binding
                        {
                            Profile = profile.InteractionProfile,
                            Action = index,
                            Path = binding.Path,
                        });
                    }
                }
            }
            return manifest;
        }

        // An OpenXR path component: lower case letters, digits, '-', '_' and '.'.
        private static string pathComponent(string name)
        {
            var result = new System.Text.StringBuilder();
            foreach (char c in name.ToLowerInvariant())
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.')
                {
                    result.Append(c);
                }
            }
            return result.Length == 0 ? "action" : result.ToString();
        }

        private static string unique(HashSet<string> taken, string name)
        {
            string candidate = name;
            for (int i = 2; !taken.Add(candidate); i++)
            {
                candidate = name + "_" + i;
            }
            return candidate;
        }

        public VRBooleanAction Boolean(string path)
        {
            if (!booleans.TryGetValue(path, out BooleanAction action))
            {
                booleans[path] = action = new BooleanAction(this, path, ActionSetFor(path));
            }
            return action;
        }

        public VRVector2Action Axis(string path)
        {
            if (!axes.TryGetValue(path, out AxisAction action))
            {
                axes[path] = action = new AxisAction(this, path, ActionSetFor(path));
            }
            return action;
        }

        public VRPoseAction Pose(string path)
        {
            if (!poses.TryGetValue(path, out PoseAction action))
            {
                // As the SteamVR bindings have it: the default pose on both hands, PoseL and PoseR on their own.
                // There are no body trackers.
                bool leftHand = path == "/actions/default/in/Pose" || path == "/actions/Valheim/in/PoseL";
                bool rightHand = path == "/actions/default/in/Pose" || path == "/actions/Valheim/in/PoseR";
                poses[path] = action = new PoseAction(this, path, ActionSetFor(path), leftHand, rightHand);
            }
            return action;
        }

        public VRHapticAction Haptic(string path) => NoHaptic.Instance;

        public VRActionSet ActionSet(string path) => ActionSetFor(path);

        public void OpenBindingUI(VRActionSet actionSet)
        {
            if (!loggedBindingUI)
            {
                loggedBindingUI = true;
                LogWarning("VHVR cannot open the binding UI under OpenXR. Its actions can be rebound in the OpenXR " +
                           "runtime's own binding UI, if it has one (SteamVR: Settings > Controllers > Manage Controller Bindings).");
            }
        }

        private XRActionSet ActionSetFor(string actionPath)
        {
            int end = actionPath.IndexOf('/', "/actions/".Length);
            string setPath = end < 0 ? actionPath : actionPath.Substring(0, end);
            if (!sets.TryGetValue(setPath, out XRActionSet set))
            {
                // SteamVR activates the first set (default) on start; the mod activates its own.
                sets[setPath] = set = new XRActionSet(this, setPath == DefaultSet);
            }
            return set;
        }

        // The runtime is up: from now on the actions follow the controllers. Called by OpenXRBackend.StartVR().
        internal void Start()
        {
            if (started)
            {
                return;
            }
            started = true;
            var driver = new GameObject("VHVR_OpenXRInput");
            UnityEngine.Object.DontDestroyOnLoad(driver);
            driver.AddComponent<OpenXRInputDriver>().Backend = this;
        }

        // Early in the frame (OpenXRInputDriver), before other scripts read the actions: the actions are worked out
        // and their listeners called, once per frame. A read before that works them out too.
        internal void Tick()
        {
            if (!started)
            {
                return;
            }
            Refresh();
            if (dispatchedFrame == evaluatedFrame)
            {
                return;
            }
            dispatchedFrame = evaluatedFrame;
            foreach (BooleanAction action in booleans.Values)
            {
                action.Dispatch();
            }
            foreach (AxisAction action in axes.Values)
            {
                action.Dispatch();
            }
            onNonVisualActionsUpdated?.Invoke();
        }

        private void Refresh()
        {
            if (started)
            {
                refreshStarted();
            }
        }

        // Apart from Refresh(), which is called before the runtime starts too: a method that calls into Unity cannot
        // even be compiled outside the game.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void refreshStarted()
        {
            if (evaluatedFrame == Time.frameCount)
            {
                return;
            }
            evaluatedFrame = Time.frameCount;
            left.SetDevice(InputDevices.GetDeviceAtXRNode(XRNode.LeftHand), XRHand.Left);
            right.SetDevice(InputDevices.GetDeviceAtXRNode(XRNode.RightHand), XRHand.Right);
            string devices = (left.Name ?? "none") + "/" + (right.Name ?? "none");
            if (devices != loggedDevices)
            {
                loggedDevices = devices;
                LogInfo("OpenXR controllers (left/right): " + devices + ".");
                logXRDevices();
            }

            foreach (BooleanAction action in booleans.Values)
            {
                action.BeginFrame();
            }
            foreach (AxisAction action in axes.Values)
            {
                action.BeginFrame();
            }
            if (OpenXRLoaderSetup.ActionsAttached())
            {
                readRuntimeActions();
                OpenXRProfile leftProfile = profileOf(XRHand.Left);
                OpenXRProfile rightProfile = profileOf(XRHand.Right);
                string profiles = (leftProfile?.Name ?? "none") + " / " + (rightProfile?.Name ?? "none");
                if (profiles != loggedProfiles)
                {
                    loggedProfiles = profiles;
                    LogInfo("OpenXR interaction profiles (left / right): " + profiles + ".");
                }
                // The dpad directions first, the chords last: a chord may be made of an action a dpad gives.
                bindCompat(leftProfile, XRHand.Left, XRCompatKind.Dpad);
                bindCompat(rightProfile, XRHand.Right, XRCompatKind.Dpad);
                bindCompat(leftProfile, XRHand.Left, XRCompatKind.Chord);
                bindCompat(rightProfile, XRHand.Right, XRCompatKind.Chord);
            }
            else
            {
                bindLegacy();
            }
            foreach (BooleanAction action in booleans.Values)
            {
                action.EndFrame();
            }
            foreach (AxisAction action in axes.Values)
            {
                action.EndFrame();
            }
        }

        private OpenXRController Controller(XRHand hand) => hand == XRHand.Left ? left : right;

        private static readonly XRHand[] Hands = { XRHand.Left, XRHand.Right };

        // VHVR's OpenXR actions, as the runtime binds them: the sets the mod has active are synced, and each action of
        // an active set takes the runtime's state on every hand the runtime has it bound on.
        private void readRuntimeActions()
        {
            for (int i = 0; i < runtimeSets.Count; i++)
            {
                OpenXRLoaderSetup.SetActionSetActive(i, ActionSetFor(runtimeSets[i]).active);
            }
            foreach (BooleanAction action in booleans.Values)
            {
                if (!action.Set.active || !runtimeActions.TryGetValue(action.Path, out int index))
                {
                    continue;
                }
                foreach (XRHand hand in Hands)
                {
                    if (OpenXRLoaderSetup.ReadBoolean(index, (int)hand, out bool state))
                    {
                        action.BindFromRuntime(hand, state);
                    }
                }
            }
            foreach (AxisAction action in axes.Values)
            {
                if (!action.Set.active || !runtimeActions.TryGetValue(action.Path, out int index))
                {
                    continue;
                }
                foreach (XRHand hand in Hands)
                {
                    if (OpenXRLoaderSetup.ReadVector2(index, (int)hand, out Vector2 axis))
                    {
                        action.BindFromRuntime(hand, axis);
                    }
                }
            }
            logRuntimeBindings();
        }

        // The profile the runtime uses for the hand, else the one of the hand's device.
        private OpenXRProfile profileOf(XRHand hand)
        {
            OpenXRController controller = Controller(hand);
            if (!controller.IsConnected)
            {
                return null;
            }
            return OpenXRProfiles.Find(OpenXRLoaderSetup.CurrentInteractionProfile((int)hand), controller.Name);
        }

        // The hand's compat bindings of one kind, for the actions the runtime has unbound on that hand.
        private void bindCompat(OpenXRProfile profile, XRHand hand, XRCompatKind kind)
        {
            if (profile == null)
            {
                return;
            }
            foreach (XRCompatBinding binding in profile.Compat)
            {
                if (binding.Kind == kind && binding.Source == hand &&
                    booleans.TryGetValue(binding.Action, out BooleanAction action) && action.Set.active && !action.FromRuntime(hand))
                {
                    action.Bind(hand, binding.Condition(left, right));
                }
            }
        }

        // Without VHVR's OpenXR actions: the generic bindings on the legacy XR devices.
        private void bindLegacy()
        {
            if (!loggedLegacyFallback)
            {
                loggedLegacyFallback = true;
                LogWarning("VHVR's OpenXR actions are not attached; using the generic legacy XR bindings, which cannot be rebound.");
            }
            foreach (XRBooleanBinding binding in OpenXRBindings.Booleans)
            {
                if (booleans.TryGetValue(binding.Action, out BooleanAction action) && action.Set.active)
                {
                    action.Bind(binding.Source, Controller(binding.Source).IsConnected && binding.Condition(left, right));
                }
            }
            foreach (XRAxisBinding binding in OpenXRBindings.Axes)
            {
                if (axes.TryGetValue(binding.Action, out AxisAction action) && action.Set.active)
                {
                    action.Bind(binding.Source, Controller(binding.Source).IsConnected ? binding.Read(left, right) : Vector2.zero);
                }
            }
        }

        // Which actions the runtime has bound on each hand, logged when that changes; the others follow the compat
        // bindings, if any.
        private void logRuntimeBindings()
        {
            // Checked every frame without allocating; the names are only listed when it changed.
            int signature = 17;
            foreach (XRHand hand in Hands)
            {
                foreach (BooleanAction action in booleans.Values)
                {
                    signature = signature * 31 + (action.FromRuntime(hand) ? 1 : 0);
                }
                foreach (AxisAction action in axes.Values)
                {
                    signature = signature * 31 + (action.FromRuntime(hand) ? 1 : 0);
                }
            }
            if (loggedRuntimeSignature == signature)
            {
                return;
            }
            loggedRuntimeSignature = signature;
            foreach (XRHand hand in Hands)
            {
                var names = new List<string>();
                foreach (BooleanAction action in booleans.Values)
                {
                    if (action.FromRuntime(hand)) names.Add(ShortName(action.Path));
                }
                foreach (AxisAction action in axes.Values)
                {
                    if (action.FromRuntime(hand)) names.Add(ShortName(action.Path));
                }
                LogInfo("OpenXR actions bound by the runtime on the " + hand + " hand: " +
                        (names.Count == 0 ? "none" : string.Join(", ", names.ToArray())) + ".");
            }
        }

        // Every XR input device and the hands' features, for when a hand is not found or not the expected controller.
        private void logXRDevices()
        {
            var devices = new List<InputDevice>();
            InputDevices.GetDevices(devices);
            var lines = new List<string>();
            foreach (InputDevice device in devices)
            {
                lines.Add("\"" + device.name + "\" (" + device.characteristics + ")");
            }
            LogInfo("XR input devices: " + (lines.Count == 0 ? "none" : string.Join("; ", lines.ToArray())) + ".");
            foreach (OpenXRController controller in new[] { left, right })
            {
                if (controller.IsConnected)
                {
                    LogInfo("Features of \"" + controller.Name + "\": " + controller.Features() + ".");
                }
            }
        }

        // Why a hand has or has no pose, for the log (see OpenXRHand).
        internal string PoseStatus(XRHand hand)
        {
            if (!started)
            {
                return "the runtime has not started";
            }
            Refresh();
            OpenXRController controller = Controller(hand);
            if (!controller.IsConnected)
            {
                return "no " + hand + " hand controller";
            }
            if (!controller.Has("IsTracked") || !controller.Has("DevicePosition"))
            {
                return controller.Name + " has no IsTracked or DevicePosition feature";
            }
            return controller.Button("IsTracked") ?
                controller.Name + " tracked at " + controller.Vector3("DevicePosition").ToString("F2") :
                controller.Name + " not tracked";
        }

        internal bool IsConnected(XRHand hand)
        {
            if (!started)
            {
                return false;
            }
            Refresh();
            return Controller(hand).IsConnected;
        }

        // The SteamVR raw pose of the hand's controller in tracking space (see OpenXRBindings.GripToRaw), read as it
        // is now: the rig's hands read it again before rendering. False when the controller is not tracked.
        internal bool TryGetRawPose(XRHand hand, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (!started)
            {
                return false;
            }
            Refresh();
            OpenXRController controller = Controller(hand);
            if (!controller.IsConnected || !controller.Button("IsTracked"))
            {
                return false;
            }
            controller.GripToRaw(controller.Vector3("DevicePosition"), controller.Rotation("DeviceRotation"), out position, out rotation);
            return true;
        }

        // The velocities of the raw pose in tracking space, from those the runtime gives for the grip pose; zero when
        // it gives none (see OpenXRHand, which then works them out itself).
        internal void GetRawVelocity(XRHand hand, out Vector3 velocity, out Vector3 angularVelocity)
        {
            velocity = angularVelocity = Vector3.zero;
            if (!TryGetRawPose(hand, out _, out Quaternion rawRotation))
            {
                return;
            }
            OpenXRController controller = Controller(hand);
            angularVelocity = controller.Vector3("DeviceAngularVelocity");
            Vector3 gripVelocity = controller.Vector3("DeviceVelocity");
            // The raw pose is a point on the same rigid body as the grip pose.
            Vector3 gripToRaw = -controller.GripOffset(rawRotation);
            velocity = gripVelocity + Vector3.Cross(angularVelocity, gripToRaw);
        }

        private static int Index(VRInputSource source)
        {
            switch (source)
            {
                case VRInputSource.Any: return 0;
                case VRInputSource.LeftHand: return 1;
                case VRInputSource.RightHand: return 2;
                default: return -1;
            }
        }

        private static readonly VRInputSource[] Sources = { VRInputSource.Any, VRInputSource.LeftHand, VRInputSource.RightHand };

        private static string ShortName(string path) => path.Substring(path.LastIndexOf('/') + 1);

        sealed class XRActionSet : VRActionSet
        {
            private readonly OpenXRInputBackend backend;
            internal bool active;
            public XRActionSet(OpenXRInputBackend backend, bool active) { this.backend = backend; this.active = active; }
            // Sets apply to both hands at once; the source is ignored, as the mod only ever passes Any.
            public override bool IsActive(VRInputSource source = VRInputSource.Any) => active;
            public override void Activate(VRInputSource source = VRInputSource.Any, int priority = 0, bool disableAllOtherActionSets = false)
            {
                active = true;
                if (disableAllOtherActionSets)
                {
                    foreach (XRActionSet set in backend.sets.Values)
                    {
                        set.active = set == this;
                    }
                }
            }
            public override void Deactivate(VRInputSource source = VRInputSource.Any) { active = false; }
        }

        // State per source: Any (both hands combined, with its own edges), left, right.
        sealed class BooleanAction : VRBooleanAction
        {
            private readonly OpenXRInputBackend backend;
            private readonly string path;
            internal readonly XRActionSet Set;
            private readonly bool[] pressed = new bool[3];
            private readonly bool[] previous = new bool[3];
            private readonly bool[] bound = new bool[3];
            // Per hand (left, right): the runtime gave the state this frame.
            private readonly bool[] runtime = new bool[2];
            private readonly List<KeyValuePair<int, Action<VRBooleanAction, VRInputSource>>> downListeners =
                new List<KeyValuePair<int, Action<VRBooleanAction, VRInputSource>>>();
            private readonly List<KeyValuePair<int, Action<VRBooleanAction, VRInputSource>>> upListeners =
                new List<KeyValuePair<int, Action<VRBooleanAction, VRInputSource>>>();

            public BooleanAction(OpenXRInputBackend backend, string path, XRActionSet set) { this.backend = backend; this.path = path; Set = set; }

            internal string Path => path;

            internal void BeginFrame()
            {
                for (int i = 0; i < 3; i++)
                {
                    previous[i] = pressed[i];
                    pressed[i] = bound[i] = false;
                }
                runtime[0] = runtime[1] = false;
            }
            internal bool FromRuntime(XRHand hand) => runtime[(int)hand];
            // Pressed on the hand so far this frame, while the bindings are being worked out.
            internal bool PressedOn(XRHand hand) => pressed[hand == XRHand.Left ? 1 : 2];
            internal void BindFromRuntime(XRHand hand, bool value)
            {
                runtime[(int)hand] = true;
                Bind(hand, value);
            }
            internal void Bind(XRHand hand, bool value)
            {
                int i = hand == XRHand.Left ? 1 : 2;
                bound[i] = true;
                pressed[i] |= value;
            }
            internal void EndFrame()
            {
                pressed[0] = pressed[1] || pressed[2];
                bound[0] = bound[1] || bound[2];
            }
            internal void Dispatch()
            {
                foreach (var listener in downListeners.ToArray())
                {
                    if (pressed[listener.Key] && !previous[listener.Key]) listener.Value(this, Sources[listener.Key]);
                }
                foreach (var listener in upListeners.ToArray())
                {
                    if (!pressed[listener.Key] && previous[listener.Key]) listener.Value(this, Sources[listener.Key]);
                }
            }

            private bool Read(bool[] values, VRInputSource source)
            {
                int i = Index(source);
                if (i < 0) return false;
                backend.Refresh();
                return values[i];
            }

            public override string GetShortName() => ShortName(path);
            public override bool activeBinding => Read(bound, VRInputSource.Any);
            public override bool GetActiveBinding(VRInputSource source) => Read(bound, source);
            public override bool state => Read(pressed, VRInputSource.Any);
            public override bool GetState(VRInputSource source) => Read(pressed, source);
            public override bool GetStateDown(VRInputSource source) => Read(pressed, source) && !Read(previous, source);
            public override bool GetStateUp(VRInputSource source) => !Read(pressed, source) && Read(previous, source);
            public override void AddOnStateDownListener(Action<VRBooleanAction, VRInputSource> callback, VRInputSource source)
            {
                if (Index(source) >= 0) downListeners.Add(new KeyValuePair<int, Action<VRBooleanAction, VRInputSource>>(Index(source), callback));
            }
            public override void AddOnStateUpListener(Action<VRBooleanAction, VRInputSource> callback, VRInputSource source)
            {
                if (Index(source) >= 0) upListeners.Add(new KeyValuePair<int, Action<VRBooleanAction, VRInputSource>>(Index(source), callback));
            }
        }

        sealed class AxisAction : VRVector2Action
        {
            private readonly OpenXRInputBackend backend;
            private readonly string path;
            internal readonly XRActionSet Set;
            private readonly Vector2[] value = new Vector2[3];
            private readonly Vector2[] previous = new Vector2[3];
            private readonly bool[] bound = new bool[3];
            private readonly bool[] runtime = new bool[2];
            private readonly List<KeyValuePair<int, Action<VRVector2Action, VRInputSource, Vector2, Vector2>>> listeners =
                new List<KeyValuePair<int, Action<VRVector2Action, VRInputSource, Vector2, Vector2>>>();

            public AxisAction(OpenXRInputBackend backend, string path, XRActionSet set) { this.backend = backend; this.path = path; Set = set; }

            internal string Path => path;

            internal void BeginFrame()
            {
                for (int i = 0; i < 3; i++)
                {
                    previous[i] = value[i];
                    value[i] = Vector2.zero;
                    bound[i] = false;
                }
                runtime[0] = runtime[1] = false;
            }
            internal bool FromRuntime(XRHand hand) => runtime[(int)hand];
            // The hand's value so far this frame, while the bindings are being worked out.
            internal Vector2 ValueOn(XRHand hand) => value[hand == XRHand.Left ? 1 : 2];
            internal void BindFromRuntime(XRHand hand, Vector2 axis)
            {
                runtime[(int)hand] = true;
                Bind(hand, axis);
            }
            internal void Bind(XRHand hand, Vector2 axis)
            {
                int i = hand == XRHand.Left ? 1 : 2;
                bound[i] = true;
                if (axis.sqrMagnitude > value[i].sqrMagnitude) value[i] = axis;
            }
            internal void EndFrame()
            {
                // Any is the hand that is pushed furthest.
                value[0] = value[1].sqrMagnitude >= value[2].sqrMagnitude ? value[1] : value[2];
                bound[0] = bound[1] || bound[2];
            }
            internal void Dispatch()
            {
                foreach (var listener in listeners.ToArray())
                {
                    int i = listener.Key;
                    if (value[i] != previous[i]) listener.Value(this, Sources[i], value[i], value[i] - previous[i]);
                }
            }

            public override string GetShortName() => ShortName(path);
            public override bool activeBinding => GetActiveBinding(VRInputSource.Any);
            public override bool GetActiveBinding(VRInputSource source)
            {
                int i = Index(source);
                if (i < 0) return false;
                backend.Refresh();
                return bound[i];
            }
            public override Vector2 axis => GetAxis(VRInputSource.Any);
            public override Vector2 GetAxis(VRInputSource source)
            {
                int i = Index(source);
                if (i < 0) return Vector2.zero;
                backend.Refresh();
                return value[i];
            }
            public override void AddOnChangeListener(Action<VRVector2Action, VRInputSource, Vector2, Vector2> callback, VRInputSource source)
            {
                if (Index(source) >= 0) listeners.Add(new KeyValuePair<int, Action<VRVector2Action, VRInputSource, Vector2, Vector2>>(Index(source), callback));
            }
        }

        sealed class PoseAction : VRPoseAction
        {
            private readonly OpenXRInputBackend backend;
            private readonly string path;
            private readonly XRActionSet set;
            private readonly bool leftHand;
            private readonly bool rightHand;

            public PoseAction(OpenXRInputBackend backend, string path, XRActionSet set, bool leftHand, bool rightHand)
            {
                this.backend = backend;
                this.path = path;
                this.set = set;
                this.leftHand = leftHand;
                this.rightHand = rightHand;
            }

            // The hand a source reads; Any is the left hand where the pose is on both, as the mod only asks Any of
            // the one-handed poses.
            private bool TryHand(VRInputSource source, out XRHand hand)
            {
                hand = XRHand.Left;
                switch (source)
                {
                    case VRInputSource.LeftHand: return leftHand;
                    case VRInputSource.RightHand: hand = XRHand.Right; return rightHand;
                    case VRInputSource.Any:
                        if (!leftHand) hand = XRHand.Right;
                        return leftHand || rightHand;
                    default: return false;
                }
            }

            private bool Bound(VRInputSource source, out XRHand hand) => TryHand(source, out hand) && set.active && backend.IsConnected(hand);

            public override string GetShortName() => ShortName(path);
            public override bool activeBinding => GetActiveBinding(VRInputSource.Any);
            public override bool GetActiveBinding(VRInputSource source) => Bound(source, out _);
            public override Vector3 localPosition => GetLocalPosition(VRInputSource.Any);
            public override Quaternion localRotation => GetLocalRotation(VRInputSource.Any);
            public override Vector3 GetLocalPosition(VRInputSource source)
            {
                return Bound(source, out XRHand hand) && backend.TryGetRawPose(hand, out Vector3 position, out _) ? position : Vector3.zero;
            }
            public override Quaternion GetLocalRotation(VRInputSource source)
            {
                return Bound(source, out XRHand hand) && backend.TryGetRawPose(hand, out _, out Quaternion rotation) ? rotation : Quaternion.identity;
            }
            public override Vector3 GetVelocity(VRInputSource source)
            {
                if (!Bound(source, out XRHand hand)) return Vector3.zero;
                backend.GetRawVelocity(hand, out Vector3 velocity, out _);
                return velocity;
            }
            public override Vector3 GetAngularVelocity(VRInputSource source)
            {
                if (!Bound(source, out XRHand hand)) return Vector3.zero;
                backend.GetRawVelocity(hand, out _, out Vector3 angularVelocity);
                return angularVelocity;
            }
            public override bool GetPoseIsValid(VRInputSource source) => Bound(source, out XRHand hand) && backend.TryGetRawPose(hand, out _, out _);
            public override bool GetDeviceIsConnected(VRInputSource source) => TryHand(source, out XRHand hand) && backend.IsConnected(hand);
        }

        sealed class NoHaptic : VRHapticAction
        {
            public static readonly NoHaptic Instance = new NoHaptic();
            public override void Execute(float delay, float duration, float frequency, float amplitude, VRInputSource source) { }
        }
    }

    // Works out the OpenXR actions early each frame.
    [DefaultExecutionOrder(-30000)]
    sealed class OpenXRInputDriver : MonoBehaviour
    {
        internal OpenXRInputBackend Backend;
        private void Update() => Backend?.Tick();
    }

    // VHVR's OpenXR action sets, actions and suggested bindings, in plain types (OpenXRLoaderSetup hands them to the
    // VHVR.OpenXR feature that registers them with the runtime).
    sealed class OpenXRActionManifest
    {
        public sealed class Set
        {
            public string Name;
            public string LocalizedName;
        }

        public sealed class Action
        {
            public int Set;
            public string Name;
            public string LocalizedName;
            public bool IsVector2;
        }

        public sealed class Binding
        {
            public string Profile;
            public int Action;
            public string Path;
        }

        public readonly List<Set> Sets = new List<Set>();
        public readonly List<Action> Actions = new List<Action>();
        public readonly List<Binding> Bindings = new List<Binding>();
    }
}
