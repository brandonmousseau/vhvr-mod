using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Valve.VR;
using ValheimVRMod.VRCore.Backends;
using UnityEngine;

static class Program
{
    static int checks;
    static void Require(bool condition, string label)
    {
        if (!condition) throw new Exception("FAIL: " + label);
        checks++;
    }
    static void Main(string[] args)
    {
        if (args.Length != 3) throw new ArgumentException("Usage: BackendRefactor <game> <gameplay DLL> <SteamVR managed directory>");
        var folders = new[] { Path.GetDirectoryName(Path.GetFullPath(args[1]))!, Path.GetFullPath(args[2]),
            Path.Combine(args[0], "Valheim_Data/Managed"), Path.Combine(args[0], "BepInEx/core") };
        AssemblyLoadContext.Default.Resolving += (_, name) => {
            foreach (var folder in folders)
            {
                var file = Path.Combine(folder, name.Name + ".dll");
                if (File.Exists(file)) return AssemblyLoadContext.Default.LoadFromAssemblyPath(file);
            }
            return null;
        };
        Run();
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Run()
    {
        // The catalog builder stands in for the generated SteamVR_Actions.PreInitialize(). Its check against the
        // shipped actions.json needs the Unity player to find that file, so CheckManifest() runs it offline.
        Build();
        // Compare already-published managed snapshots. Unity's native frame clock and
        // hardware polling are unavailable in this process and are a separate gate.
        SteamVR_Action.startUpdatingSourceOnAccess = false;
        VRInputActions.Initialize(VRBackend.Active.Input);
        Require(!VRInputActions.Valheim.IsActive(), "native action-set lookup retains catalog capitalization");
        Require(VRInputActions.valheim_Grab.GetState(VRInputSource.Any) == NativeLookup.Action<SteamVR_Action_Boolean>("/actions/Valheim/in/Grab").GetState(SteamVR_Input_Sources.Any), "native grip read");
        CheckBuiltArrays();
        CheckManifest();
        CheckCatalog();
        CheckOpenXRActions();
        CheckCallbacks();
        CheckBoundary();
        CheckHapticIdentity();
        CheckRuntimeSelection();
        Console.WriteLine($"PASS: {checks} differential and contract assertions against the action catalog and original managed SteamVR SDK.");
        Console.WriteLine("Published input snapshots are simulated. Native polling, rig timing, rendering, haptics and headset acceptance remain separate gates.");
    }

    // A property rather than a static field, so that it is not resolved before Main() can locate the assemblies.
    static Type Catalog => typeof(SteamVRInputBackend).Assembly.GetType("ValheimVRMod.VRCore.Backends.SteamVRActionCatalog", true)!;
    static void Build() => Catalog.GetMethod("Build")!.Invoke(null, new object[] { VRInputActions.Sets, VRInputActions.Actions });

    // The property a catalog entry is read through: "/actions/Valheim/in/Grab" is valheim_Grab.
    static string PropertyName(string path)
    {
        var parts = path.Split('/');
        return parts[2].ToLowerInvariant() + "_" + parts[^1];
    }
    static object NativeFor(string path, VRActionKind kind) => kind switch
    {
        VRActionKind.Boolean => NativeLookup.Action<SteamVR_Action_Boolean>(path),
        VRActionKind.Single => NativeLookup.Action<SteamVR_Action_Single>(path),
        VRActionKind.Vector2 => NativeLookup.Action<SteamVR_Action_Vector2>(path),
        VRActionKind.Pose => NativeLookup.Action<SteamVR_Action_Pose>(path),
        VRActionKind.Skeleton => NativeLookup.Action<SteamVR_Action_Skeleton>(path),
        VRActionKind.Haptic => NativeLookup.Action<SteamVR_Action_Vibration>(path),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    // The arrays that SteamVR_Input.Initialize() and its per-frame updates walk, split by type as the generated
    // code split them.
    static void CheckBuiltArrays()
    {
        var decls = VRInputActions.Actions;
        string[] Paths(Func<VRActionDecl, bool> filter) => decls.Where(filter).Select(decl => decl.Path).ToArray();
        string[] Built(IEnumerable<object> actions) => actions.Select(action => ((SteamVR_Action)action).fullPath).ToArray();
        Require(SteamVR_Input.actionSets.Select(set => set.fullPath).SequenceEqual(VRInputActions.Sets.Select(set => set.Path)), "action sets in catalog order");
        Require(Built(SteamVR_Input.actions).SequenceEqual(Paths(decl => true)), "actions in catalog order");
        Require(decls.Select(decl => decl.Path).Distinct().Count() == decls.Count, "no duplicate actions");
        Require(Built(SteamVR_Input.actionsIn).SequenceEqual(Paths(decl => !decl.IsOutput)), "input actions");
        Require(Built(SteamVR_Input.actionsOut).SequenceEqual(Paths(decl => decl.IsOutput)), "output actions");
        Require(Built(SteamVR_Input.actionsBoolean).SequenceEqual(Paths(decl => decl.Kind == VRActionKind.Boolean)), "boolean actions");
        Require(Built(SteamVR_Input.actionsSingle).SequenceEqual(Paths(decl => decl.Kind == VRActionKind.Single)), "single actions");
        Require(Built(SteamVR_Input.actionsVector2).SequenceEqual(Paths(decl => decl.Kind == VRActionKind.Vector2)), "vector2 actions");
        Require(SteamVR_Input.actionsVector3.Length == 0, "no vector3 actions");
        Require(Built(SteamVR_Input.actionsPose).SequenceEqual(Paths(decl => decl.Kind == VRActionKind.Pose)), "pose actions");
        Require(Built(SteamVR_Input.actionsSkeleton).SequenceEqual(Paths(decl => decl.Kind == VRActionKind.Skeleton)), "skeleton actions");
        Require(Built(SteamVR_Input.actionsVibration).SequenceEqual(Paths(decl => decl.Kind == VRActionKind.Haptic)), "vibration actions");
        Require(Built(SteamVR_Input.actionsNonPoseNonSkeletonIn).SequenceEqual(Paths(decl =>
            !decl.IsOutput && decl.Kind != VRActionKind.Pose && decl.Kind != VRActionKind.Skeleton)), "non-pose, non-skeleton input actions");
        foreach (var decl in decls)
        {
            // SteamVR's own path lookup, which its components and serialized actions use, finds every built action.
            var native = (SteamVR_Action)NativeFor(decl.Path, decl.Kind);
            Require(native.fullPath == decl.Path && native.actionSet.fullPath == decl.SetPath, "SteamVR path lookup " + decl.Path);
        }
        var before = SteamVR_Input.actions;
        Build();
        Require(ReferenceEquals(before, SteamVR_Input.actions), "actions built only once");
        foreach (var path in new[] { "/actions/Valheim/in/NoSuchAction", "/actions/Valheim/in/Walk" })
        {
            bool threw = false;
            try { VRBackend.Active.Input.Boolean(path); }
            catch (ArgumentException) { threw = true; }
            Require(threw, "undeclared action or action of another kind rejected: " + path);
        }
        Console.WriteLine($"Built {SteamVR_Input.actionSets.Length} action sets and {SteamVR_Input.actions.Length} actions from the catalog.");
    }

    // The shipped actions.json has to list exactly the catalog's sets and actions, with the same types and requirements.
    static void CheckManifest()
    {
        string? manifest = null;
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null && manifest == null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Unity/ValheimVR/Assets/StreamingAssets/SteamVR/actions.json");
            if (File.Exists(candidate)) manifest = candidate;
        }
        Require(manifest != null, "actions.json found");
        var json = File.ReadAllText(manifest!);
        List<string> Compare(string text) => (List<string>)Catalog.GetMethod("CompareManifest", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { text, VRInputActions.Sets, VRInputActions.Actions })!;
        var problems = Compare(json);
        foreach (var problem in problems) Console.WriteLine("Drift: " + problem);
        Require(problems.Count == 0, "catalog matches actions.json");
        // A renamed action is reported on both sides.
        Require(Compare(json.Replace("\"/actions/Valheim/in/Jump\"", "\"/actions/Valheim/in/Leap\"")).Count == 2, "drift reported both ways");
    }

    static FieldInfo Field(Type type, string name)
    {
        for (Type? current = type; current != null; current = current.BaseType)
        {
            var field = current.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            if (field != null) return field;
        }
        throw new MissingFieldException(type.FullName, name);
    }
    static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
    static void Active(SteamVR_ActionSet actionSet, SteamVR_Input_Sources source, bool enabled)
    {
        var data = Field(typeof(SteamVR_ActionSet), "setData").GetValue(actionSet)!;
        var states = (bool[])Field(data.GetType(), "rawSetActive").GetValue(data)!;
        Array.Clear(states);
        states[(int)source] = enabled;
    }

    // The OpenXR input paths of each interaction profile VHVR suggests bindings for, as the OpenXR 1.0 specification
    // lists them (poses and haptics left out). Per hand where the hands differ.
    static readonly Dictionary<string, (string[] both, string[] left, string[] right)> OpenXRInputs = new()
    {
        ["/interaction_profiles/oculus/touch_controller"] = (
            new[] { "squeeze/value", "trigger/value", "trigger/touch", "thumbstick", "thumbstick/x", "thumbstick/y", "thumbstick/click", "thumbstick/touch", "thumbrest/touch" },
            new[] { "x/click", "x/touch", "y/click", "y/touch", "menu/click" },
            new[] { "a/click", "a/touch", "b/click", "b/touch", "system/click" }),
        ["/interaction_profiles/valve/index_controller"] = (
            new[] { "system/click", "system/touch", "a/click", "a/touch", "b/click", "b/touch", "squeeze/value", "squeeze/force",
                    "trigger/click", "trigger/value", "trigger/touch", "thumbstick", "thumbstick/x", "thumbstick/y", "thumbstick/click",
                    "thumbstick/touch", "trackpad", "trackpad/x", "trackpad/y", "trackpad/force", "trackpad/touch" },
            new string[0], new string[0]),
        ["/interaction_profiles/htc/vive_controller"] = (
            new[] { "system/click", "squeeze/click", "menu/click", "trigger/click", "trigger/value", "trackpad", "trackpad/x", "trackpad/y",
                    "trackpad/click", "trackpad/touch" },
            new string[0], new string[0]),
        ["/interaction_profiles/microsoft/motion_controller"] = (
            new[] { "menu/click", "squeeze/click", "trigger/value", "thumbstick", "thumbstick/x", "thumbstick/y", "thumbstick/click",
                    "trackpad", "trackpad/x", "trackpad/y", "trackpad/click", "trackpad/touch" },
            new string[0], new string[0]),
        ["/interaction_profiles/khr/simple_controller"] = (new[] { "select/click", "menu/click" }, new string[0], new string[0]),
    };

    // VHVR's OpenXR actions and bindings, offline: the manifest registered with the runtime, the suggested paths of
    // each profile, and each profile against the SteamVR bindings file it is written after.
    static void CheckOpenXRActions()
    {
        var mod = typeof(VRBackend).Assembly;
        object Get(object target, string name) =>
            target.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance)!.GetValue(target)!;

        // The manifest: one OpenXR set per catalog set, one action per boolean and vector2 action, with valid unique
        // names, and bindings to existing actions.
        var backend = new OpenXRInputBackend();
        backend.Declare(VRInputActions.Sets, VRInputActions.Actions);
        object manifest = typeof(OpenXRInputBackend).GetMethod("BuildManifest", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(backend, null)!;
        var sets = ((System.Collections.IEnumerable)Get(manifest, "Sets")).Cast<object>().ToList();
        var actions = ((System.Collections.IEnumerable)Get(manifest, "Actions")).Cast<object>().ToList();
        var bindings = ((System.Collections.IEnumerable)Get(manifest, "Bindings")).Cast<object>().ToList();
        var component = new System.Text.RegularExpressions.Regex("^[a-z0-9_.-]{1,63}$");
        Require(sets.Count == VRInputActions.Sets.Count, "an OpenXR set per catalog set");
        Require(sets.All(set => component.IsMatch((string)Get(set, "Name"))) &&
                sets.Select(set => (string)Get(set, "Name")).Distinct().Count() == sets.Count, "valid unique OpenXR set names");
        int readable = VRInputActions.Actions.Count(a => a.Kind == VRActionKind.Boolean || a.Kind == VRActionKind.Vector2);
        Require(actions.Count == readable, "an OpenXR action per boolean and vector2 catalog action");
        Require(actions.All(a => component.IsMatch((string)Get(a, "Name")) && ((string)Get(a, "LocalizedName")).Length < 128) &&
                actions.Select(a => (string)Get(a, "Name")).Distinct().Count() == actions.Count, "valid unique OpenXR action names");
        Require(bindings.All(b => (int)Get(b, "Action") >= 0 && (int)Get(b, "Action") < actions.Count), "bindings of existing actions");
        Require(bindings.Select(b => Get(b, "Profile") + " " + Get(b, "Action") + " " + Get(b, "Path")).Distinct().Count() == bindings.Count,
                "no binding suggested twice");

        // The tables: every suggested path is an input of its profile, of the action's type; no compat binding stands
        // for an action the profile suggests on the same hand.
        var rows = ((List<string[]>)mod.GetType("ValheimVRMod.VRCore.Backends.OpenXRProfiles", true)!
            .GetMethod("Rows", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!);
        var kinds = VRInputActions.Actions.ToDictionary(a => a.Path, a => a.Kind);
        foreach (var row in rows)
        {
            string profile = row[0], kind = row[2], action = row[3], hand = row[4], path = row[5];
            Require(kinds.ContainsKey(action), "bound action is in the catalog: " + action);
            if (kind == "suggest")
            {
                Require(OpenXRInputs.ContainsKey(profile), "known profile " + profile);
                var inputs = OpenXRInputs[profile];
                string prefix = hand == "Left" ? "/user/hand/left/input/" : "/user/hand/right/input/";
                Require(path.StartsWith(prefix), "suggested on its hand: " + path);
                string input = path.Substring(prefix.Length);
                Require(inputs.both.Contains(input) || (hand == "Left" ? inputs.left : inputs.right).Contains(input),
                        "valid input " + path + " for " + profile);
                bool vector2 = input == "thumbstick" || input == "trackpad";
                Require(vector2 == (kinds[action] == VRActionKind.Vector2), "input type matches the action: " + action + " " + path);
            }
            else
            {
                Require(kinds[action] == VRActionKind.Boolean, "compat binding of a boolean: " + action);
                Require(!rows.Any(r => r[0] == profile && r[2] == "suggest" && r[3] == action && r[4] == hand),
                        "compat only where the profile suggests nothing: " + action + " " + hand + " " + profile);
            }
        }

        // Every action the SteamVR bindings file binds on a hand is suggested or compat on that hand in the profile
        // written after it; the compat bindings are only chords and dpad directions. Scroll mode has no counterpart,
        // and the actions the catalog lacks (SnapTurn...) or the backend does not read (poses, skeletons, squeeze) are
        // left out.
        string? directory = null;
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null && directory == null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "Unity/ValheimVR/Assets/StreamingAssets/SteamVR");
            if (File.Exists(Path.Combine(candidate, "actions.json"))) directory = candidate;
        }
        Require(directory != null, "SteamVR bindings found");
        var paths = kinds.Where(k => k.Value == VRActionKind.Boolean || k.Value == VRActionKind.Vector2)
            .ToDictionary(k => k.Key.ToLowerInvariant(), k => k.Key);
        int compared = 0;
        foreach (var controller in rows.Select(r => r[1]).Where(c => c != "").Distinct())
        {
            string profile = rows.First(r => r[1] == controller)[0];
            using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(directory!, "bindings_" + controller + ".json")));
            var expected = new List<(string action, string hand, string how)>();
            foreach (var set in json.RootElement.GetProperty("bindings").EnumerateObject())
            {
                if (set.Value.TryGetProperty("sources", out var sources))
                foreach (var source in sources.EnumerateArray())
                {
                    string mode = source.GetProperty("mode").GetString()!;
                    string hand = source.GetProperty("path").GetString()!.StartsWith("/user/hand/left") ? "Left" : "Right";
                    if (mode == "scroll" || !source.TryGetProperty("inputs", out var inputs)) continue;
                    foreach (var input in inputs.EnumerateObject())
                    {
                        expected.Add((input.Value.GetProperty("output").GetString()!, hand, mode == "dpad" ? "dpad" : "plain"));
                    }
                }
                if (set.Value.TryGetProperty("chords", out var chords))
                foreach (var chord in chords.EnumerateArray())
                {
                    var parts = chord.GetProperty("inputs").EnumerateArray().ToList();
                    if (parts.Count == 0) continue;
                    string hand = parts[^1][0].GetString()!.StartsWith("/user/hand/left") ? "Left" : "Right";
                    expected.Add((chord.GetProperty("output").GetString()!, hand, "chord"));
                }
            }
            foreach (var (output, hand, how) in expected)
            {
                if (!paths.TryGetValue(output.ToLowerInvariant(), out var action)) continue;
                bool suggested = rows.Any(r => r[0] == profile && r[2] == "suggest" && r[3] == action && r[4] == hand);
                bool compat = rows.Any(r => r[0] == profile && r[2] != "suggest" && r[3] == action && r[4] == hand);
                Require(suggested || compat, "bindings_" + controller + ".json's " + action + " (" + how + ", " + hand + ") is in " + profile);
                Require(suggested || how != "plain", "a plain SteamVR binding is suggested, not compat: " + action + " " + hand + " " + profile);
                compared++;
            }
        }
        Console.WriteLine($"OpenXR: {actions.Count} actions in {sets.Count} sets, {bindings.Count} suggested bindings; {compared} SteamVR bindings compared.");
    }

    static void CheckCatalog()
    {
        int booleans = 0, axes = 0, poses = 0, sets = 0, haptics = 0;
        var properties = typeof(VRInputActions).GetProperties(BindingFlags.Public | BindingFlags.Static);
        // Skeleton and squeeze actions are declared for SteamVR's sake but not read through a property.
        var read = VRInputActions.Actions.Where(decl => decl.Kind != VRActionKind.Single && decl.Kind != VRActionKind.Skeleton)
            .ToDictionary(decl => PropertyName(decl.Path));
        var setPaths = VRInputActions.Sets.ToDictionary(set => set.Path == "/actions/default" ? "_default" : set.Path.Split('/')[^1], set => set.Path);
        Require(properties.Select(property => property.Name).OrderBy(name => name)
            .SequenceEqual(read.Keys.Concat(setPaths.Keys).OrderBy(name => name)), "a property for every read action and set");
        foreach (var property in properties)
        {
            object native = setPaths.TryGetValue(property.Name, out var setPath)
                ? SteamVR_Input.GetActionSetFromPath(setPath)
                : NativeFor(read[property.Name].Path, read[property.Name].Kind);
            Require(native != null, "catalog member " + property.Name);
            object adapter = property.GetValue(null)!;
            foreach (VRInputSource source in Enum.GetValues<VRInputSource>())
            {
                var hand = Enum.Parse<SteamVR_Input_Sources>(source.ToString());
                var mapped = (SteamVR_Input_Sources)typeof(SteamVRInputBackend).GetMethod("Native", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { source })!;
                Require(mapped == hand, "input source " + source);
                switch (adapter)
                {
                    case VRBooleanAction action:
                        var boolean = (SteamVR_Action_Boolean)native;
                        foreach (bool activeSet in new[] { false, true })
                        foreach (bool bound in new[] { false, true })
                        foreach (bool held in new[] { false, true })
                        foreach (bool changed in new[] { false, true })
                        {
                            Active(boolean.actionSet, hand, activeSet);
                            Set(boolean[hand], "actionData", new InputDigitalActionData_t { bActive = bound, bState = held, bChanged = changed });
                            Require(action.GetState(source) == boolean.GetState(hand), "held " + property.Name + " " + source);
                            Require(action.GetStateDown(source) == boolean.GetStateDown(hand), "press " + property.Name + " " + source);
                            Require(action.GetStateUp(source) == boolean.GetStateUp(hand), "release " + property.Name + " " + source);
                            Require(action.GetActiveBinding(source) == boolean.GetActiveBinding(hand), "binding " + property.Name + " " + source);
                            Require(action[source].activeBinding == boolean[hand].activeBinding, "indexed binding");
                            if (source == VRInputSource.Any)
                            {
                                Require(action.state == boolean.state && action.activeBinding == boolean.activeBinding, "Any defaults");
                            }
                        }
                        break;
                    case VRVector2Action action:
                        var axis = (SteamVR_Action_Vector2)native;
                        Set(axis[hand], "<axis>k__BackingField", new Vector2((int)source * .125f, -.75f));
                        Require(action.GetAxis(source).Equals(axis.GetAxis(hand)), "axis " + property.Name + " " + source);
                        Require(action.GetActiveBinding(source) == axis.GetActiveBinding(hand), "axis binding");
                        if (source == VRInputSource.Any) Require(action.axis.Equals(axis.axis), "axis Any default");
                        break;
                    case VRPoseAction action:
                        var pose = (SteamVR_Action_Pose)native;
                        Set(pose[hand], "<localPosition>k__BackingField", new Vector3((int)source, 2, -3));
                        Set(pose[hand], "<localRotation>k__BackingField", new Quaternion(.1f, .2f, .3f, .9f));
                        Set(pose[hand], "<velocity>k__BackingField", new Vector3(4, (int)source, 6));
                        Set(pose[hand], "<angularVelocity>k__BackingField", new Vector3(-1, -2, (int)source));
                        foreach (bool valid in new[] { false, true })
                        foreach (bool connected in new[] { false, true })
                        {
                            Set(pose[hand], "poseActionData", new InputPoseActionData_t { bActive = true, pose = new TrackedDevicePose_t { bPoseIsValid = valid, bDeviceIsConnected = connected } });
                            Require(action.GetLocalPosition(source).Equals(pose.GetLocalPosition(hand)), "pose position");
                            Require(action.GetLocalRotation(source).Equals(pose.GetLocalRotation(hand)), "pose rotation");
                            Require(action.GetVelocity(source).Equals(pose.GetVelocity(hand)), "pose velocity, including tracking loss");
                            Require(action.GetAngularVelocity(source).Equals(pose.GetAngularVelocity(hand)), "pose angular velocity");
                            Require(action.GetPoseIsValid(source) == pose.GetPoseIsValid(hand), "pose validity");
                            Require(action.GetDeviceIsConnected(source) == pose.GetDeviceIsConnected(hand), "pose connection");
                        }
                        if (source == VRInputSource.Any) Require(action.localPosition.Equals(pose.localPosition) && action.localRotation.Equals(pose.localRotation), "pose Any defaults");
                        break;
                    case VRActionSet action:
                        var set = (SteamVR_ActionSet)native;
                        foreach (bool active in new[] { false, true })
                        {
                            Active(set, hand, active);
                            Require(action.IsActive(source) == set.IsActive(hand), "action set " + property.Name + " " + source);
                        }
                        break;
                }
            }
            if (adapter is VRAction named) Require(named.GetShortName() == ((SteamVR_Action)native).GetShortName(), "binding prompt label");
            if (adapter is VRBooleanAction) booleans++;
            if (adapter is VRVector2Action) axes++;
            if (adapter is VRPoseAction) poses++;
            if (adapter is VRActionSet) sets++;
            if (adapter is VRHapticAction) haptics++;
        }
        int Count(VRActionKind kind) => VRInputActions.Actions.Count(decl => decl.Kind == kind);
        Require(sets == VRInputActions.Sets.Count && booleans == Count(VRActionKind.Boolean) && axes == Count(VRActionKind.Vector2)
            && poses == Count(VRActionKind.Pose) && haptics == Count(VRActionKind.Haptic), "catalog coverage");
        Console.WriteLine($"Catalog: {booleans} boolean, {axes} axis, {poses} pose, {haptics} haptic actions and {sets} sets; all 16 input sources.");
    }
    static void CheckCallbacks()
    {
        foreach (var source in new[] { VRInputSource.Any, VRInputSource.LeftHand, VRInputSource.RightHand })
        {
            var hand = Enum.Parse<SteamVR_Input_Sources>(source.ToString());
            var native = NativeLookup.Action<SteamVR_Action_Boolean>("/actions/Valheim/in/Grab");
            var action = VRInputActions.valheim_Grab;
            int down = 0, up = 0;
            action.AddOnStateDownListener((sender, actual) => { Require(ReferenceEquals(sender, action) && actual == source, "down payload"); down++; }, source);
            action.AddOnStateUpListener((sender, actual) => { Require(ReferenceEquals(sender, action) && actual == source, "up payload"); up++; }, source);
            ((SteamVR_Action_Boolean.StateDownHandler)Field(native[hand].GetType(), "onStateDown").GetValue(native[hand])!)(native, hand);
            ((SteamVR_Action_Boolean.StateUpHandler)Field(native[hand].GetType(), "onStateUp").GetValue(native[hand])!)(native, hand);
            Require(down == 1 && up == 1, "native SDK callbacks delivered once");
            var nativeAxis = NativeLookup.Action<SteamVR_Action_Vector2>("/actions/Valheim/in/Walk");
            int axisChanges = 0;
            VRInputActions.valheim_Walk.AddOnChangeListener((sender, actual, value, delta) => {
                Require(actual == source && value.x == .25f && delta.y == -.1f, "axis callback payload"); axisChanges++;
            }, source);
            ((SteamVR_Action_Vector2.ChangeHandler)Field(nativeAxis[hand].GetType(), "onChange").GetValue(nativeAxis[hand])!)(nativeAxis, hand, new Vector2(.25f, .5f), new Vector2(.1f, -.1f));
            Require(axisChanges == 1, "native axis callback delivered once");
        }
        int updates = 0;
        Action listener = () => updates++;
        VRInput.onNonVisualActionsUpdated += listener;
        VRInput.onNonVisualActionsUpdated += listener;
        var updateEvent = Field(typeof(SteamVR_Input), "onNonVisualActionsUpdated");
        ((Action)updateEvent.GetValue(null)!)();
        Require(updates == 2, "duplicate update listeners preserve SDK semantics");
        VRInput.onNonVisualActionsUpdated -= listener;
        ((Action)updateEvent.GetValue(null)!)();
        Require(updates == 3, "remove only one matching update listener");
        VRInput.onNonVisualActionsUpdated -= listener;
        Require(updateEvent.GetValue(null) == null, "update listener cleanup");
    }
    static void CheckBoundary()
    {
        foreach (var type in new[] { typeof(IVRBackend), typeof(IVRInputBackend), typeof(IVRRigBackend), typeof(IVRKeyboardBackend), typeof(IVROverlayBackend), typeof(VRHand), typeof(VRLaserPointer) })
        foreach (var method in type.GetMethods())
        {
            Require(!(method.ReturnType.FullName ?? method.ReturnType.Name).Contains("Valve."), "neutral return " + method.Name);
            foreach (var argument in method.GetParameters()) Require(!(argument.ParameterType.FullName ?? argument.ParameterType.Name).Contains("Valve."), "neutral argument " + method.Name);
        }
        Require(typeof(VRBackend).GetProperty(nameof(VRBackend.Active))!.SetMethod == null, "the backend is chosen once, not set from outside");
        CheckOpenXRConfinement();
        Require(VROverlay.InvalidHandle == OpenVR.k_ulOverlayHandleInvalid, "native invalid overlay handle");
    }
    // Nothing outside the bodies of OpenXRLoaderSetup's methods may name an OpenXR type: a field, signature, base type
    // or interface that did would load Unity.XR.OpenXR.dll with its declaring type, under SteamVR too, and fail where
    // the plugin is not installed. (check_boundary.py confines the method bodies to that one file.)
    static void CheckOpenXRConfinement()
    {
        var mod = typeof(VRBackend).Assembly;
        Require(mod.GetReferencedAssemblies().Any(a => a.Name == "Unity.XR.OpenXR"), "the OpenXR backend is built in");
        bool IsOpenXR(Type? type)
        {
            while (type != null && (type.HasElementType || type.IsGenericType && !type.IsGenericTypeDefinition))
            {
                if (type.IsGenericType && type.GetGenericArguments().Any(IsOpenXR)) return true;
                type = type.HasElementType ? type.GetElementType() : type.GetGenericTypeDefinition();
            }
            // VHVR.OpenXR's feature derives from the plugin's: naming it loads the plugin as well.
            string? name = type?.Assembly.GetName().Name;
            return name == "Unity.XR.OpenXR" || name == "VHVR.OpenXR";
        }
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var type in mod.GetTypes())
        {
            // Unity.XR.OpenXR.dll is not where this process looks (the game has it only once the mod is installed),
            // so naming one of its types usually shows as the assembly failing to load rather than as an OpenXR type.
            try
            {
                Require(!IsOpenXR(type.BaseType) && !type.GetInterfaces().Any(IsOpenXR), "no OpenXR base type: " + type.FullName);
                foreach (var field in type.GetFields(all)) Require(!IsOpenXR(field.FieldType), "no OpenXR field: " + type.FullName + "." + field.Name);
                foreach (var property in type.GetProperties(all)) Require(!IsOpenXR(property.PropertyType), "no OpenXR property: " + type.FullName + "." + property.Name);
                foreach (var method in type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all)))
                {
                    Require(!(method is MethodInfo info) || !IsOpenXR(info.ReturnType), "no OpenXR return: " + type.FullName + "." + method.Name);
                    foreach (var parameter in method.GetParameters()) Require(!IsOpenXR(parameter.ParameterType), "no OpenXR parameter: " + type.FullName + "." + method.Name);
                }
            }
            catch (FileNotFoundException e) when (e.FileName?.StartsWith("Unity.XR.OpenXR") == true || e.FileName?.StartsWith("VHVR.OpenXR") == true)
            {
                Require(false, "no OpenXR type in the members of " + type.FullName);
            }
        }
    }

    // VRRuntime picks the backend, and OpenXR turns the overlay GUI off whatever its setting. Runs last: it leaves
    // VRInputActions resolved against the OpenXR backend's input, inert until the runtime starts.
    static void CheckRuntimeSelection()
    {
        var config = typeof(VRBackend).Assembly.GetType("ValheimVRMod.Utilities.VHVRConfig", true)!;
        void Set(string name, object? value) => config.GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, value);
        bool Call(string name) => (bool)config.GetMethod(name, BindingFlags.Static | BindingFlags.Public)!.Invoke(null, null)!;
        var active = typeof(VRBackend).GetField("active", BindingFlags.Static | BindingFlags.NonPublic)!;

        Require(VRBackend.Active is SteamVRBackend, "SteamVR before VRRuntime is read, and by default");
        Set("flatScreenModeResolved", (bool?)false);
        Set("latchedUseVrControls", true);
        Set("latchedUseOverlayGui", true);
        Require(Call("UseVrControls"), "SteamVR keeps VR controls");
        Set("latchedUseVrControls", false);
        Require(Call("GetUseOverlayGui"), "SteamVR keeps the overlay GUI");

        Set("latchedVrRuntime", "OpenXR");
        Require(VRBackend.Active is SteamVRBackend, "the backend is kept once chosen");
        active.SetValue(null, null);
        var openXr = VRBackend.Active;
        Require(openXr is OpenXRBackend && openXr.RuntimeName == "OpenXR", "OpenXR chosen by VRRuntime");
        Require(!Call("GetUseOverlayGui"), "no overlay GUI under OpenXR");
        Set("latchedUseVrControls", true);
        Require(Call("UseVrControls"), "VR controls under OpenXR");
        Set("latchedUseVrControls", false);
        Require(!Call("UseVrControls"), "VR controls off by setting under OpenXR");
        Require(openXr.SupportsControllers && !openXr.SupportsOverlay && openXr.Overlay == null, "OpenXR capabilities");
        Require(!openXr.IsControllerConnected(VRInputSource.LeftHand) && !openXr.IsControllerConnected(VRInputSource.RightHand), "no controllers before the OpenXR runtime starts");
        Require(!openXr.GetConnectedTrackers().Any() && openXr.InitializeInput(), "no trackers or separate input system under OpenXR");
        Require(!openXr.Keyboard.IsAvailable && openXr.Keyboard.Show("") != null, "no system keyboard under OpenXR");
        Require(new SteamVRKeyboardBackend().IsAvailable, "SteamVR has a system keyboard");

        // Before the runtime starts, every catalog action resolves to an action that is not bound, pressed, moved,
        // valid or heard from, without touching Unity. Action sets keep their activation.
        openXr.Input.Declare(VRInputActions.Sets, VRInputActions.Actions);
        VRInputActions.Initialize(openXr.Input);
        int calls = 0;
        foreach (var property in typeof(VRInputActions).GetProperties(BindingFlags.Static | BindingFlags.Public))
        {
            var value = property.GetValue(null);
            Require(value != null, "inert action resolved: " + property.Name);
            foreach (var source in Enum.GetValues<VRInputSource>())
            {
                switch (value)
                {
                    case VRBooleanAction b:
                        Require(!b.state && !b.GetState(source) && !b.GetStateDown(source) && !b.GetStateUp(source) && !b.GetActiveBinding(source) && !b[source].activeBinding, "inert boolean " + property.Name);
                        b.AddOnStateDownListener((_, _) => calls++, source);
                        b.AddOnStateUpListener((_, _) => calls++, source);
                        break;
                    case VRVector2Action a:
                        Require(a.axis == Vector2.zero && a.GetAxis(source) == Vector2.zero && !a.activeBinding, "inert axis " + property.Name);
                        a.AddOnChangeListener((_, _, _, _) => calls++, source);
                        break;
                    case VRPoseAction p:
                        Require(!p.GetPoseIsValid(source) && !p.GetDeviceIsConnected(source) && p.GetLocalPosition(source) == Vector3.zero, "inert pose " + property.Name);
                        break;
                    case VRHapticAction h:
                        h.Execute(0, 1, 1, 1, source);
                        break;
                    case VRActionSet s:
                        s.Activate(source);
                        Require(s.IsActive(source), "action set activated " + property.Name);
                        s.Deactivate(source);
                        Require(!s.IsActive(source), "action set deactivated " + property.Name);
                        break;
                    default:
                        throw new Exception("FAIL: unexpected action type " + value!.GetType());
                }
            }
        }
        Action update = () => calls++;
        VRInput.onNonVisualActionsUpdated += update;
        VRInput.onNonVisualActionsUpdated -= update;
        Require(calls == 0, "no listener of the inert input is called");
    }
    static void CheckHapticIdentity()
    {
        var wrap = typeof(SteamVRInputBackend).GetMethod("Wrap", BindingFlags.Static | BindingFlags.NonPublic)!;
        Require(wrap.Invoke(null, new object?[] { null }) == null, "missing authored haptic action stays missing");
        // SteamVR overloads == null to also mean an action whose path/source is not
        // initialized. That object must still be forwarded, not replaced or dropped.
        foreach (var native in new[] { new SteamVR_Action_Vibration(), NativeLookup.Action<SteamVR_Action_Vibration>("/actions/default/out/Haptic"), NativeLookup.Action<SteamVR_Action_Vibration>("/actions/Valheim/out/Haptic") })
        {
            var wrapped = wrap.Invoke(null, new object[] { native })!;
            Require(wrapped != null, "retain authored haptic object before initialization");
            Require(ReferenceEquals(Field(wrapped!.GetType(), "action").GetValue(wrapped), native), "preserve per-hand haptic identity");
        }
    }
}

// Kept out of Program: a generic constraint on an SDK type there is resolved when Program loads, which is before
// Main() can tell the runtime where the SDK assemblies are.
static class NativeLookup
{
    public static T Action<T>(string path) where T : SteamVR_Action, new() => SteamVR_Input.GetActionFromPath<T>(path, caseSensitive: true);
}
