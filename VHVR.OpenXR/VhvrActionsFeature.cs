using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine.XR.OpenXR.Features;

namespace VHVR.OpenXR
{
    // VHVR's own OpenXR action sets and actions (Jump, ToggleMenu, Use...), declared to the runtime as an OpenXR
    // application declares its actions, with suggested bindings for each interaction profile. A runtime with a binding
    // UI (SteamVR's) then lists them under the game and lets the player rebind them.
    //
    // The OpenXR plugin only creates, suggests, attaches and syncs its own action sets (those of its interaction
    // profiles). A runtime keeps one suggestion per profile (the last one) and takes one attach per session. So this
    // feature intercepts those calls through HookGetInstanceProcAddr, as the plugin's own features do:
    // - xrSuggestInteractionProfileBindings: the plugin's bindings for the profile are recorded, and VHVR's are added
    //   to the same call;
    // - xrAttachSessionActionSets: every profile that did not get VHVR's bindings yet (the plugin suggested it before
    //   VHVR's actions existed, or not at all) is suggested again, with the plugin's recorded bindings and VHVR's;
    //   then VHVR's sets are attached with the plugin's;
    // - xrSyncActions: VHVR's active sets are synced with the plugin's;
    // - xrPollEvent: an interaction profile change marks the current profiles to be read again.
    // Should the runtime refuse VHVR's part of a call, the plugin's call goes through as it was, and VHVR's actions read
    // as unbound: the mod then falls back on its own bindings.
    //
    // The mod fills in Sets, Actions and Bindings before the loader initializes, and reads the actions with
    // GetBoolean() and GetVector2(). Only plain types cross into the mod.
    public sealed unsafe class VhvrActionsFeature : OpenXRFeature
    {
        public const int BooleanAction = 1;
        public const int Vector2Action = 3;

        // An action set. Name is an OpenXR path component (lower case letters, digits, '-', '_', '.'), unique among
        // every set of the instance.
        public sealed class SetSpec
        {
            public string Name;
            public string LocalizedName;
        }

        // An action of Sets[Set], with both hands as subaction paths. Kind is BooleanAction or Vector2Action.
        public sealed class ActionSpec
        {
            public int Set;
            public string Name;
            public string LocalizedName;
            public int Kind;
        }

        // A suggested binding of Actions[Action]: Profile is an interaction profile path, Path a full input path, e.g.
        // /user/hand/right/input/a/click.
        public sealed class BindingSpec
        {
            public string Profile;
            public int Action;
            public string Path;
        }

        public static SetSpec[] Sets = new SetSpec[0];
        public static ActionSpec[] Actions = new ActionSpec[0];
        public static BindingSpec[] Bindings = new BindingSpec[0];
        public static Action<string> LogInfo = message => UnityEngine.Debug.Log(message);
        public static Action<string> LogWarning = message => UnityEngine.Debug.LogWarning(message);

        // The hands, as the actions' subaction paths: 0 left, 1 right.
        private static readonly string[] HandPathNames = { "/user/hand/left", "/user/hand/right" };

        // ---- OpenXR, as in openxr.h (64 bit) ----
        private const uint XR_TYPE_ACTION_STATE_BOOLEAN = 23;
        private const uint XR_TYPE_ACTION_STATE_VECTOR2F = 25;
        private const uint XR_TYPE_ACTION_SET_CREATE_INFO = 28;
        private const uint XR_TYPE_ACTION_CREATE_INFO = 29;
        private const uint XR_TYPE_INTERACTION_PROFILE_SUGGESTED_BINDING = 51;
        private const uint XR_TYPE_EVENT_DATA_INTERACTION_PROFILE_CHANGED = 52;
        private const uint XR_TYPE_INTERACTION_PROFILE_STATE = 53;
        private const uint XR_TYPE_ACTION_STATE_GET_INFO = 58;
        private const int XR_MAX_ACTION_SET_NAME_SIZE = 64;
        private const int XR_MAX_LOCALIZED_ACTION_SET_NAME_SIZE = 128;
        private const int XR_MAX_ACTION_NAME_SIZE = 64;
        private const int XR_MAX_LOCALIZED_ACTION_NAME_SIZE = 128;
        private const int XR_MAX_PATH_LENGTH = 256;

        [StructLayout(LayoutKind.Sequential)]
        private struct XrActionSetCreateInfo
        {
            public uint type;
            public IntPtr next;
            public fixed byte actionSetName[XR_MAX_ACTION_SET_NAME_SIZE];
            public fixed byte localizedActionSetName[XR_MAX_LOCALIZED_ACTION_SET_NAME_SIZE];
            public uint priority;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XrActionCreateInfo
        {
            public uint type;
            public IntPtr next;
            public fixed byte actionName[XR_MAX_ACTION_NAME_SIZE];
            public int actionType;
            public uint countSubactionPaths;
            public ulong* subactionPaths;
            public fixed byte localizedActionName[XR_MAX_LOCALIZED_ACTION_NAME_SIZE];
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XrActionSuggestedBinding
        {
            public ulong action;
            public ulong binding;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XrInteractionProfileSuggestedBinding
        {
            public uint type;
            public IntPtr next;
            public ulong interactionProfile;
            public uint countSuggestedBindings;
            public XrActionSuggestedBinding* suggestedBindings;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XrSessionActionSetsAttachInfo
        {
            public uint type;
            public IntPtr next;
            public uint countActionSets;
            public ulong* actionSets;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XrActiveActionSet
        {
            public ulong actionSet;
            public ulong subactionPath;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XrActionsSyncInfo
        {
            public uint type;
            public IntPtr next;
            public uint countActiveActionSets;
            public XrActiveActionSet* activeActionSets;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XrActionStateGetInfo
        {
            public uint type;
            public IntPtr next;
            public ulong action;
            public ulong subactionPath;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XrActionStateBoolean
        {
            public uint type;
            public IntPtr next;
            public uint currentState;
            public uint changedSinceLastSync;
            public long lastChangeTime;
            public uint isActive;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XrActionStateVector2f
        {
            public uint type;
            public IntPtr next;
            public float x;
            public float y;
            public uint changedSinceLastSync;
            public long lastChangeTime;
            public uint isActive;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XrInteractionProfileState
        {
            public uint type;
            public IntPtr next;
            public ulong interactionProfile;
        }

        // The header every XrEventDataBuffer starts with.
        [StructLayout(LayoutKind.Sequential)]
        private struct XrEventDataBaseHeader
        {
            public uint type;
            public IntPtr next;
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int GetInstanceProcAddrFn(ulong instance, IntPtr name, out IntPtr function);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int StringToPathFn(ulong instance, [MarshalAs(UnmanagedType.LPStr)] string path, out ulong xrPath);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int PathToStringFn(ulong instance, ulong path, uint capacity, out uint count, byte* buffer);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CreateActionSetFn(ulong instance, XrActionSetCreateInfo* info, out ulong actionSet);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CreateActionFn(ulong actionSet, XrActionCreateInfo* info, out ulong action);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int SuggestFn(ulong instance, XrInteractionProfileSuggestedBinding* info);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int AttachFn(ulong session, XrSessionActionSetsAttachInfo* info);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int SyncFn(ulong session, XrActionsSyncInfo* info);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int PollEventFn(ulong instance, XrEventDataBaseHeader* eventData);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int GetBooleanFn(ulong session, XrActionStateGetInfo* info, XrActionStateBoolean* state);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int GetVector2Fn(ulong session, XrActionStateGetInfo* info, XrActionStateVector2f* state);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int GetCurrentInteractionProfileFn(ulong session, ulong topLevelUserPath, XrInteractionProfileState* state);

        // The hooks, kept alive for as long as the runtime may call them.
        private static readonly GetInstanceProcAddrFn hookGetInstanceProcAddr = hookedGetInstanceProcAddr;
        private static readonly SuggestFn hookSuggest = hookedSuggest;
        private static readonly AttachFn hookAttach = hookedAttach;
        private static readonly SyncFn hookSync = hookedSync;
        private static readonly PollEventFn hookPollEvent = hookedPollEvent;

        private static GetInstanceProcAddrFn nextGetInstanceProcAddr;
        private static SuggestFn realSuggest;
        private static AttachFn realAttach;
        private static SyncFn realSync;
        private static PollEventFn realPollEvent;
        private static StringToPathFn stringToPath;
        private static PathToStringFn pathToString;
        private static CreateActionSetFn createActionSet;
        private static CreateActionFn createAction;
        private static GetBooleanFn getBoolean;
        private static GetVector2Fn getVector2;
        private static GetCurrentInteractionProfileFn getCurrentInteractionProfile;

        private static ulong instance;
        private static ulong session;
        private static bool created;
        private static bool attached;
        private static ulong[] setHandles = new ulong[0];
        private static ulong[] actionHandles = new ulong[0];
        private static readonly ulong[] handPaths = new ulong[2];
        private static bool[] setActive = new bool[0];
        // VHVR's suggested bindings per profile path, the plugin's last ones, and the profiles VHVR's are in.
        private static readonly Dictionary<ulong, XrActionSuggestedBinding[]> ourBindings = new Dictionary<ulong, XrActionSuggestedBinding[]>();
        private static readonly Dictionary<ulong, XrActionSuggestedBinding[]> pluginBindings = new Dictionary<ulong, XrActionSuggestedBinding[]>();
        private static readonly Dictionary<ulong, string> profileNames = new Dictionary<ulong, string>();
        private static readonly HashSet<ulong> merged = new HashSet<ulong>();
        // The interaction profile of each hand, read again after a change.
        private static readonly string[] currentProfiles = new string[2];
        private static bool profilesDirty = true;

        // Whether VHVR's actions are attached to the session, so that reading them means anything.
        public static bool IsAttached => attached && session != 0;

        // Which sets are synced, as the mod activates them; the actions of a set not synced read as unbound.
        public static void SetActive(int set, bool active)
        {
            if (set >= 0 && set < setActive.Length)
            {
                setActive[set] = active;
            }
        }

        // An action on a hand (0 left, 1 right): false when the runtime has it bound to no input of that hand.
        public static bool GetBoolean(int action, int hand, out bool state)
        {
            state = false;
            if (!IsAttached || getBoolean == null || action < 0 || action >= actionHandles.Length || actionHandles[action] == 0)
            {
                return false;
            }
            var info = new XrActionStateGetInfo { type = XR_TYPE_ACTION_STATE_GET_INFO, action = actionHandles[action], subactionPath = handPaths[hand] };
            var value = new XrActionStateBoolean { type = XR_TYPE_ACTION_STATE_BOOLEAN };
            if (getBoolean(session, &info, &value) < 0 || value.isActive == 0)
            {
                return false;
            }
            state = value.currentState != 0;
            return true;
        }

        public static bool GetVector2(int action, int hand, out float x, out float y)
        {
            x = y = 0;
            if (!IsAttached || getVector2 == null || action < 0 || action >= actionHandles.Length || actionHandles[action] == 0)
            {
                return false;
            }
            var info = new XrActionStateGetInfo { type = XR_TYPE_ACTION_STATE_GET_INFO, action = actionHandles[action], subactionPath = handPaths[hand] };
            var value = new XrActionStateVector2f { type = XR_TYPE_ACTION_STATE_VECTOR2F };
            if (getVector2(session, &info, &value) < 0 || value.isActive == 0)
            {
                return false;
            }
            x = value.x;
            y = value.y;
            return true;
        }

        // The interaction profile path the runtime uses for a hand, e.g. /interaction_profiles/oculus/touch_controller;
        // null while it has none. Read from the runtime again only after it reports a change.
        public static string CurrentProfile(int hand)
        {
            if (!IsAttached || getCurrentInteractionProfile == null || pathToString == null)
            {
                return null;
            }
            if (profilesDirty)
            {
                profilesDirty = false;
                for (int i = 0; i < 2; i++)
                {
                    currentProfiles[i] = readCurrentProfile(i);
                }
            }
            return currentProfiles[hand];
        }

        private static string readCurrentProfile(int hand)
        {
            var state = new XrInteractionProfileState { type = XR_TYPE_INTERACTION_PROFILE_STATE };
            if (getCurrentInteractionProfile(session, handPaths[hand], &state) < 0 || state.interactionProfile == 0)
            {
                return null;
            }
            byte* buffer = stackalloc byte[XR_MAX_PATH_LENGTH];
            if (pathToString(instance, state.interactionProfile, XR_MAX_PATH_LENGTH, out uint count, buffer) < 0 || count == 0)
            {
                return null;
            }
            return Encoding.UTF8.GetString(buffer, (int)count - 1);
        }

        protected override IntPtr HookGetInstanceProcAddr(IntPtr func)
        {
            nextGetInstanceProcAddr = Marshal.GetDelegateForFunctionPointer<GetInstanceProcAddrFn>(func);
            return Marshal.GetFunctionPointerForDelegate(hookGetInstanceProcAddr);
        }

        // Every function the plugin and the runtime look up goes through here; the ones VHVR joins in are swapped.
        private static int hookedGetInstanceProcAddr(ulong xrInstance, IntPtr name, out IntPtr function)
        {
            int result = nextGetInstanceProcAddr(xrInstance, name, out function);
            if (result < 0 || function == IntPtr.Zero)
            {
                return result;
            }
            try
            {
                switch (Marshal.PtrToStringAnsi(name))
                {
                    case "xrSuggestInteractionProfileBindings":
                        realSuggest = Marshal.GetDelegateForFunctionPointer<SuggestFn>(function);
                        function = Marshal.GetFunctionPointerForDelegate(hookSuggest);
                        break;
                    case "xrAttachSessionActionSets":
                        realAttach = Marshal.GetDelegateForFunctionPointer<AttachFn>(function);
                        function = Marshal.GetFunctionPointerForDelegate(hookAttach);
                        break;
                    case "xrSyncActions":
                        realSync = Marshal.GetDelegateForFunctionPointer<SyncFn>(function);
                        function = Marshal.GetFunctionPointerForDelegate(hookSync);
                        break;
                    case "xrPollEvent":
                        realPollEvent = Marshal.GetDelegateForFunctionPointer<PollEventFn>(function);
                        function = Marshal.GetFunctionPointerForDelegate(hookPollEvent);
                        break;
                }
            }
            catch (Exception e)
            {
                LogWarning("VHVR OpenXR actions: " + e);
            }
            return result;
        }

        private static T Resolve<T>(string name) where T : Delegate
        {
            IntPtr namePtr = Marshal.StringToHGlobalAnsi(name);
            try
            {
                return nextGetInstanceProcAddr(instance, namePtr, out IntPtr function) >= 0 && function != IntPtr.Zero ?
                    Marshal.GetDelegateForFunctionPointer<T>(function) : null;
            }
            finally
            {
                Marshal.FreeHGlobal(namePtr);
            }
        }

        protected override bool OnInstanceCreate(ulong xrInstance)
        {
            instance = xrInstance;
            created = false;
            attached = false;
            session = 0;
            merged.Clear();
            try
            {
                createActions();
            }
            catch (Exception e)
            {
                LogWarning("VHVR OpenXR actions could not be created: " + e);
            }
            // The plugin goes on either way: without these actions the mod uses its own bindings.
            return true;
        }

        private static void createActions()
        {
            if (nextGetInstanceProcAddr == null)
            {
                LogWarning("VHVR OpenXR actions: the function lookup was not hooked.");
                return;
            }
            stringToPath = Resolve<StringToPathFn>("xrStringToPath");
            pathToString = Resolve<PathToStringFn>("xrPathToString");
            createActionSet = Resolve<CreateActionSetFn>("xrCreateActionSet");
            createAction = Resolve<CreateActionFn>("xrCreateAction");
            getBoolean = Resolve<GetBooleanFn>("xrGetActionStateBoolean");
            getVector2 = Resolve<GetVector2Fn>("xrGetActionStateVector2f");
            getCurrentInteractionProfile = Resolve<GetCurrentInteractionProfileFn>("xrGetCurrentInteractionProfile");
            realSuggest = realSuggest ?? Resolve<SuggestFn>("xrSuggestInteractionProfileBindings");
            realAttach = realAttach ?? Resolve<AttachFn>("xrAttachSessionActionSets");
            realSync = realSync ?? Resolve<SyncFn>("xrSyncActions");
            if (stringToPath == null || createActionSet == null || createAction == null || getBoolean == null || getVector2 == null ||
                realSuggest == null)
            {
                LogWarning("VHVR OpenXR actions: the runtime lacks an action function.");
                return;
            }
            for (int hand = 0; hand < 2; hand++)
            {
                if (stringToPath(instance, HandPathNames[hand], out handPaths[hand]) < 0)
                {
                    LogWarning("VHVR OpenXR actions: no path for " + HandPathNames[hand] + ".");
                    return;
                }
            }

            setHandles = new ulong[Sets.Length];
            setActive = new bool[Sets.Length];
            for (int i = 0; i < Sets.Length; i++)
            {
                var info = new XrActionSetCreateInfo { type = XR_TYPE_ACTION_SET_CREATE_INFO };
                write(info.actionSetName, XR_MAX_ACTION_SET_NAME_SIZE, Sets[i].Name);
                write(info.localizedActionSetName, XR_MAX_LOCALIZED_ACTION_SET_NAME_SIZE, Sets[i].LocalizedName);
                int result = createActionSet(instance, &info, out setHandles[i]);
                if (result < 0)
                {
                    LogWarning("VHVR OpenXR actions: action set " + Sets[i].Name + " not created (" + result + ").");
                    return;
                }
            }

            actionHandles = new ulong[Actions.Length];
            ulong* subactions = stackalloc ulong[2];
            subactions[0] = handPaths[0];
            subactions[1] = handPaths[1];
            int count = 0;
            for (int i = 0; i < Actions.Length; i++)
            {
                ActionSpec spec = Actions[i];
                var info = new XrActionCreateInfo
                {
                    type = XR_TYPE_ACTION_CREATE_INFO,
                    actionType = spec.Kind,
                    countSubactionPaths = 2,
                    subactionPaths = subactions,
                };
                write(info.actionName, XR_MAX_ACTION_NAME_SIZE, spec.Name);
                write(info.localizedActionName, XR_MAX_LOCALIZED_ACTION_NAME_SIZE, spec.LocalizedName);
                int result = createAction(setHandles[spec.Set], &info, out actionHandles[i]);
                if (result < 0)
                {
                    LogWarning("VHVR OpenXR actions: action " + spec.Name + " not created (" + result + ").");
                    actionHandles[i] = 0;
                    continue;
                }
                count++;
            }

            ourBindings.Clear();
            profileNames.Clear();
            var lists = new Dictionary<ulong, List<XrActionSuggestedBinding>>();
            foreach (BindingSpec binding in Bindings)
            {
                if (binding.Action < 0 || binding.Action >= actionHandles.Length || actionHandles[binding.Action] == 0)
                {
                    continue;
                }
                if (stringToPath(instance, binding.Profile, out ulong profile) < 0 || stringToPath(instance, binding.Path, out ulong path) < 0)
                {
                    LogWarning("VHVR OpenXR actions: invalid binding " + binding.Profile + " " + binding.Path + ".");
                    continue;
                }
                if (!lists.TryGetValue(profile, out List<XrActionSuggestedBinding> list))
                {
                    lists[profile] = list = new List<XrActionSuggestedBinding>();
                    profileNames[profile] = binding.Profile;
                }
                list.Add(new XrActionSuggestedBinding { action = actionHandles[binding.Action], binding = path });
            }
            foreach (KeyValuePair<ulong, List<XrActionSuggestedBinding>> entry in lists)
            {
                ourBindings[entry.Key] = entry.Value.ToArray();
            }
            created = true;
            LogInfo("VHVR OpenXR actions: " + count + " actions in " + Sets.Length + " sets, bindings for " + lists.Count + " profiles.");
        }

        private static void write(byte* buffer, int size, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? "");
            int length = Math.Min(bytes.Length, size - 1);
            for (int i = 0; i < length; i++)
            {
                buffer[i] = bytes[i];
            }
            buffer[length] = 0;
        }

        private static string profileName(ulong profile) =>
            profileNames.TryGetValue(profile, out string name) ? name : "profile " + profile;

        // The plugin's bindings and VHVR's for a profile, in one suggestion. False when the runtime refuses them, which
        // leaves the profile's previous suggestion as it was.
        private static int suggestMerged(ulong xrInstance, ulong profile, XrActionSuggestedBinding[] theirs, XrActionSuggestedBinding[] ours)
        {
            int total = theirs.Length + ours.Length;
            XrActionSuggestedBinding* all = stackalloc XrActionSuggestedBinding[total];
            for (int i = 0; i < theirs.Length; i++)
            {
                all[i] = theirs[i];
            }
            for (int i = 0; i < ours.Length; i++)
            {
                all[theirs.Length + i] = ours[i];
            }
            var info = new XrInteractionProfileSuggestedBinding
            {
                type = XR_TYPE_INTERACTION_PROFILE_SUGGESTED_BINDING,
                interactionProfile = profile,
                countSuggestedBindings = (uint)total,
                suggestedBindings = all,
            };
            return realSuggest(xrInstance, &info);
        }

        private static int hookedSuggest(ulong xrInstance, XrInteractionProfileSuggestedBinding* info)
        {
            try
            {
                if (info != null && xrInstance == instance)
                {
                    // Recorded whatever happens next, for when VHVR's bindings are added to the profile later.
                    var theirs = new XrActionSuggestedBinding[info->countSuggestedBindings];
                    for (int i = 0; i < theirs.Length; i++)
                    {
                        theirs[i] = info->suggestedBindings[i];
                    }
                    pluginBindings[info->interactionProfile] = theirs;
                    if (created && ourBindings.TryGetValue(info->interactionProfile, out XrActionSuggestedBinding[] ours))
                    {
                        int result = suggestMerged(xrInstance, info->interactionProfile, theirs, ours);
                        if (result >= 0)
                        {
                            merged.Add(info->interactionProfile);
                            return result;
                        }
                        LogWarning("VHVR OpenXR actions: the runtime refused the bindings for " + profileName(info->interactionProfile) +
                                   " (" + result + "); suggesting the plugin's alone.");
                        merged.Remove(info->interactionProfile);
                    }
                }
            }
            catch (Exception e)
            {
                LogWarning("VHVR OpenXR actions: " + e);
            }
            return realSuggest(xrInstance, info);
        }

        // Before the sets are attached, after the plugin's last suggestion: every profile of VHVR's that is not merged
        // yet gets the plugin's bindings (if any) and VHVR's.
        private static void suggestRemaining()
        {
            foreach (KeyValuePair<ulong, XrActionSuggestedBinding[]> entry in ourBindings)
            {
                if (merged.Contains(entry.Key))
                {
                    continue;
                }
                pluginBindings.TryGetValue(entry.Key, out XrActionSuggestedBinding[] theirs);
                int result = suggestMerged(instance, entry.Key, theirs ?? new XrActionSuggestedBinding[0], entry.Value);
                if (result >= 0)
                {
                    merged.Add(entry.Key);
                }
                else
                {
                    // E.g. a profile whose extension the runtime has not enabled.
                    LogInfo("VHVR OpenXR actions: no bindings for " + profileName(entry.Key) + " (" + result + ").");
                }
            }
        }

        private static int hookedAttach(ulong xrSession, XrSessionActionSetsAttachInfo* info)
        {
            try
            {
                if (created && info != null && setHandles.Length > 0)
                {
                    suggestRemaining();
                    int theirs = (int)info->countActionSets;
                    ulong* all = stackalloc ulong[theirs + setHandles.Length];
                    for (int i = 0; i < theirs; i++)
                    {
                        all[i] = info->actionSets[i];
                    }
                    for (int i = 0; i < setHandles.Length; i++)
                    {
                        all[theirs + i] = setHandles[i];
                    }
                    XrSessionActionSetsAttachInfo copy = *info;
                    copy.countActionSets = (uint)(theirs + setHandles.Length);
                    copy.actionSets = all;
                    int result = realAttach(xrSession, &copy);
                    if (result >= 0)
                    {
                        session = xrSession;
                        attached = true;
                        profilesDirty = true;
                        LogInfo("VHVR OpenXR actions attached to the session (bindings merged for " + merged.Count + " of " +
                                ourBindings.Count + " profiles).");
                        return result;
                    }
                    LogWarning("VHVR OpenXR actions: the runtime refused to attach them (" + result + "); attaching the plugin's alone.");
                }
            }
            catch (Exception e)
            {
                LogWarning("VHVR OpenXR actions: " + e);
            }
            return realAttach(xrSession, info);
        }

        private static int hookedSync(ulong xrSession, XrActionsSyncInfo* info)
        {
            try
            {
                if (attached && xrSession == session && info != null)
                {
                    int ours = 0;
                    for (int i = 0; i < setActive.Length; i++)
                    {
                        if (setActive[i]) ours++;
                    }
                    if (ours > 0)
                    {
                        int theirs = (int)info->countActiveActionSets;
                        XrActiveActionSet* all = stackalloc XrActiveActionSet[theirs + ours];
                        for (int i = 0; i < theirs; i++)
                        {
                            all[i] = info->activeActionSets[i];
                        }
                        int n = theirs;
                        for (int i = 0; i < setActive.Length; i++)
                        {
                            if (setActive[i])
                            {
                                all[n++] = new XrActiveActionSet { actionSet = setHandles[i], subactionPath = 0 };
                            }
                        }
                        XrActionsSyncInfo copy = *info;
                        copy.countActiveActionSets = (uint)n;
                        copy.activeActionSets = all;
                        return realSync(xrSession, &copy);
                    }
                }
            }
            catch (Exception e)
            {
                LogWarning("VHVR OpenXR actions: " + e);
            }
            return realSync(xrSession, info);
        }

        private static int hookedPollEvent(ulong xrInstance, XrEventDataBaseHeader* eventData)
        {
            int result = realPollEvent(xrInstance, eventData);
            // XR_SUCCESS: an event was returned (XR_EVENT_UNAVAILABLE is positive too, but leaves the buffer alone).
            if (result == 0 && eventData != null && eventData->type == XR_TYPE_EVENT_DATA_INTERACTION_PROFILE_CHANGED)
            {
                profilesDirty = true;
            }
            return result;
        }

        protected override void OnSessionDestroy(ulong xrSession)
        {
            if (xrSession == session)
            {
                session = 0;
                attached = false;
            }
        }

        protected override void OnInstanceDestroy(ulong xrInstance)
        {
            if (xrInstance == instance)
            {
                instance = 0;
                created = false;
                attached = false;
                session = 0;
                pluginBindings.Clear();
                merged.Clear();
            }
        }
    }
}
