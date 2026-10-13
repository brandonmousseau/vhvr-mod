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
        SteamVR_Actions.PreInitialize();
        // Compare already-published managed snapshots. Unity's native frame clock and
        // hardware polling are unavailable in this process and are a separate gate.
        SteamVR_Action.startUpdatingSourceOnAccess = false;
        Require(!VRInputActions.Valheim.IsActive(), "native action-set lookup retains catalog capitalization");
        Require(VRInputActions.valheim_Grab.GetState(VRInputSource.Any) == SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.Any), "native grip read");
        CheckCatalog();
        CheckCallbacks();
        CheckBoundary();
        CheckHapticIdentity();
        Console.WriteLine($"PASS: {checks} differential and contract assertions against the production DLL and original managed SteamVR SDK.");
        Console.WriteLine("Published input snapshots are simulated. Native polling, rig timing, rendering, haptics and headset acceptance remain separate gates.");
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
    static void CheckCatalog()
    {
        int booleans = 0, axes = 0, poses = 0, sets = 0, haptics = 0;
        foreach (var property in typeof(VRInputActions).GetProperties(BindingFlags.Public | BindingFlags.Static))
        {
            var nativeProperty = typeof(SteamVR_Actions).GetProperty(property.Name)!;
            Require(nativeProperty != null, "catalog member " + property.Name);
            object native = nativeProperty!.GetValue(null)!;
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
        Require(sets == 2 && booleans == 32 && axes == 4 && poses == 4 && haptics == 2, "catalog coverage");
        Console.WriteLine($"Catalog: {booleans} boolean, {axes} axis, {poses} pose, {haptics} haptic actions and {sets} sets; all 16 input sources.");
    }
    static void CheckCallbacks()
    {
        foreach (var source in new[] { VRInputSource.Any, VRInputSource.LeftHand, VRInputSource.RightHand })
        {
            var hand = Enum.Parse<SteamVR_Input_Sources>(source.ToString());
            var native = SteamVR_Actions.valheim_Grab;
            var action = VRInputActions.valheim_Grab;
            int down = 0, up = 0;
            action.AddOnStateDownListener((sender, actual) => { Require(ReferenceEquals(sender, action) && actual == source, "down payload"); down++; }, source);
            action.AddOnStateUpListener((sender, actual) => { Require(ReferenceEquals(sender, action) && actual == source, "up payload"); up++; }, source);
            ((SteamVR_Action_Boolean.StateDownHandler)Field(native[hand].GetType(), "onStateDown").GetValue(native[hand])!)(native, hand);
            ((SteamVR_Action_Boolean.StateUpHandler)Field(native[hand].GetType(), "onStateUp").GetValue(native[hand])!)(native, hand);
            Require(down == 1 && up == 1, "native SDK callbacks delivered once");
            var nativeAxis = SteamVR_Actions.valheim_Walk;
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
        Require(!typeof(VRBackend).Assembly.GetReferencedAssemblies().Any(a => a.Name!.Contains("OpenXR", StringComparison.OrdinalIgnoreCase)), "no OpenXR runtime dependency");
        Require(typeof(VRBackend).GetProperty(nameof(VRBackend.Active))!.SetMethod == null, "stage 1 has no runtime selection UI or mutable backend");
        Require(VROverlay.InvalidHandle == OpenVR.k_ulOverlayHandleInvalid, "native invalid overlay handle");
    }
    static void CheckHapticIdentity()
    {
        var wrap = typeof(SteamVRInputBackend).GetMethod("Wrap", BindingFlags.Static | BindingFlags.NonPublic)!;
        Require(wrap.Invoke(null, new object?[] { null }) == null, "missing authored haptic action stays missing");
        // SteamVR overloads == null to also mean an action whose path/source is not
        // initialized. That object must still be forwarded, not replaced or dropped.
        foreach (var native in new[] { new SteamVR_Action_Vibration(), SteamVR_Actions.default_Haptic, SteamVR_Actions.valheim_Haptic })
        {
            var wrapped = wrap.Invoke(null, new object[] { native })!;
            Require(wrapped != null, "retain authored haptic object before initialization");
            Require(ReferenceEquals(Field(wrapped!.GetType(), "action").GetValue(wrapped), native), "preserve per-hand haptic identity");
        }
    }
}
