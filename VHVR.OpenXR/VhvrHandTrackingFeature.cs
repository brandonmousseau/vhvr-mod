using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
// One type for every VHVR feature's hook of the function lookup, which may be next in another's chain: see there.
using GetInstanceProcAddrFn = VHVR.OpenXR.VhvrActionsFeature.GetInstanceProcAddrFn;

namespace VHVR.OpenXR
{
    // The joints of the player's hands through XR_EXT_hand_tracking, for posing the fingers of the player character.
    //
    // A hand tracker is created for each hand with the session. Where the runtime has XR_EXT_hand_tracking_data_source,
    // the tracker asks for both sources: the optical hand tracking of a bare hand, and the joints the runtime works out
    // from a held controller (its touch sensors and buttons). Without that extension a runtime picks the source itself,
    // and some (Virtual Desktop's) then only give bare hands.
    //
    // The joints are located at the predicted display time of the last frame, read from xrWaitFrame through
    // HookGetInstanceProcAddr. Only plain types cross into the mod: the orientations as x, y, z, w in OpenXR's own
    // (right-handed) convention, in the order of XrHandJointEXT.
    public sealed unsafe class VhvrHandTrackingFeature : OpenXRFeature
    {
        public const string HandTrackingExtension = "XR_EXT_hand_tracking";
        public const string DataSourceExtension = "XR_EXT_hand_tracking_data_source";
        public const int JointCount = 26;
        // Where a hand's joints come from, as LocateHand() reports it.
        public const int SourceUnknown = 0;
        public const int SourceHand = 1;
        public const int SourceController = 2;

        // ---- OpenXR, as in openxr.h (64 bit) ----
        private const uint XR_TYPE_HAND_TRACKER_CREATE_INFO_EXT = 1000051001;
        private const uint XR_TYPE_HAND_JOINTS_LOCATE_INFO_EXT = 1000051002;
        private const uint XR_TYPE_HAND_JOINT_LOCATIONS_EXT = 1000051003;
        private const uint XR_TYPE_HAND_TRACKING_DATA_SOURCE_INFO_EXT = 1000428000;
        private const uint XR_TYPE_HAND_TRACKING_DATA_SOURCE_STATE_EXT = 1000428001;
        private const int XR_HAND_LEFT_EXT = 1;
        private const int XR_HAND_JOINT_SET_DEFAULT_EXT = 0;
        private const int XR_HAND_TRACKING_DATA_SOURCE_UNOBSTRUCTED_EXT = 1;
        private const int XR_HAND_TRACKING_DATA_SOURCE_CONTROLLER_EXT = 2;
        private const ulong XR_SPACE_LOCATION_ORIENTATION_VALID_BIT = 0x1;

        [StructLayout(LayoutKind.Sequential)]
        private struct XrHandTrackerCreateInfoEXT
        {
            public uint type;
            public IntPtr next;
            public int hand;
            public int handJointSet;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XrHandTrackingDataSourceInfoEXT
        {
            public uint type;
            public IntPtr next;
            public uint requestedDataSourceCount;
            public int* requestedDataSources;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XrHandJointsLocateInfoEXT
        {
            public uint type;
            public IntPtr next;
            public ulong baseSpace;
            public long time;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XrHandJointLocationEXT
        {
            public ulong locationFlags;
            public float qx, qy, qz, qw;
            public float px, py, pz;
            public float radius;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XrHandJointLocationsEXT
        {
            public uint type;
            public IntPtr next;
            public uint isActive;
            public uint jointCount;
            public XrHandJointLocationEXT* jointLocations;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XrHandTrackingDataSourceStateEXT
        {
            public uint type;
            public IntPtr next;
            public uint isActive;
            public int dataSource;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XrFrameState
        {
            public uint type;
            public IntPtr next;
            public long predictedDisplayTime;
            public long predictedDisplayPeriod;
            public uint shouldRender;
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int WaitFrameFn(ulong session, IntPtr frameWaitInfo, XrFrameState* frameState);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CreateHandTrackerFn(ulong session, XrHandTrackerCreateInfoEXT* createInfo, out ulong handTracker);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int DestroyHandTrackerFn(ulong handTracker);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int LocateHandJointsFn(ulong handTracker, XrHandJointsLocateInfoEXT* locateInfo, XrHandJointLocationsEXT* locations);

        // The hooks, kept alive for as long as the runtime may call them.
        private static readonly GetInstanceProcAddrFn hookGetInstanceProcAddr = hookedGetInstanceProcAddr;
        private static readonly WaitFrameFn hookWaitFrame = hookedWaitFrame;

        private static GetInstanceProcAddrFn nextGetInstanceProcAddr;
        private static WaitFrameFn realWaitFrame;
        private static CreateHandTrackerFn createHandTracker;
        private static DestroyHandTrackerFn destroyHandTracker;
        private static LocateHandJointsFn locateHandJoints;

        private static ulong instance;
        private static ulong session;
        private static ulong appSpace;
        private static bool hasDataSource;
        private static long predictedDisplayTime;
        private static readonly ulong[] trackers = new ulong[2];
        private static readonly XrHandJointLocationEXT[] locations = new XrHandJointLocationEXT[JointCount];

        // The plugin takes a feature's extensions from a field that a Unity project fills in from the feature's
        // attribute when it is built; this feature is created at runtime instead (see OpenXRLoaderSetup).
        protected override void OnEnable()
        {
            base.OnEnable();
            FieldInfo extensions = typeof(OpenXRFeature).GetField("openxrExtensionStrings", BindingFlags.Instance | BindingFlags.NonPublic);
            extensions?.SetValue(this, HandTrackingExtension + " " + DataSourceExtension);
        }

        // Whether a hand tracker exists for each hand, i.e. the runtime has hand tracking.
        public static bool IsAvailable => trackers[0] != 0 || trackers[1] != 0;

        // The joints of a hand (0 left, 1 right). orientations takes JointCount x, y, z, w quadruples, in the app space
        // and OpenXR's convention; valid[j] tells whether joint j's orientation is. source is one of the Source
        // constants. False, with nothing written, while the runtime does not track the hand.
        public static bool LocateHand(int hand, float[] orientations, bool[] valid, out int source)
        {
            source = SourceUnknown;
            ulong tracker = hand == 0 || hand == 1 ? trackers[hand] : 0;
            long time = Interlocked.Read(ref predictedDisplayTime);
            ulong space = appSpace != 0 ? appSpace : GetCurrentAppSpace();
            if (tracker == 0 || locateHandJoints == null || time == 0 || space == 0 ||
                orientations == null || orientations.Length < JointCount * 4 || valid == null || valid.Length < JointCount)
            {
                return false;
            }
            var info = new XrHandJointsLocateInfoEXT { type = XR_TYPE_HAND_JOINTS_LOCATE_INFO_EXT, baseSpace = space, time = time };
            var state = new XrHandTrackingDataSourceStateEXT { type = XR_TYPE_HAND_TRACKING_DATA_SOURCE_STATE_EXT };
            fixed (XrHandJointLocationEXT* joints = locations)
            {
                var result = new XrHandJointLocationsEXT
                {
                    type = XR_TYPE_HAND_JOINT_LOCATIONS_EXT,
                    next = hasDataSource ? (IntPtr)(&state) : IntPtr.Zero,
                    jointCount = JointCount,
                    jointLocations = joints,
                };
                if (locateHandJoints(tracker, &info, &result) < 0 || result.isActive == 0)
                {
                    return false;
                }
            }
            if (hasDataSource && state.isActive != 0)
            {
                source = state.dataSource == XR_HAND_TRACKING_DATA_SOURCE_CONTROLLER_EXT ? SourceController : SourceHand;
            }
            for (int j = 0; j < JointCount; j++)
            {
                orientations[j * 4] = locations[j].qx;
                orientations[j * 4 + 1] = locations[j].qy;
                orientations[j * 4 + 2] = locations[j].qz;
                orientations[j * 4 + 3] = locations[j].qw;
                valid[j] = (locations[j].locationFlags & XR_SPACE_LOCATION_ORIENTATION_VALID_BIT) != 0;
            }
            return true;
        }

        protected override IntPtr HookGetInstanceProcAddr(IntPtr func)
        {
            nextGetInstanceProcAddr = Marshal.GetDelegateForFunctionPointer<GetInstanceProcAddrFn>(func);
            return Marshal.GetFunctionPointerForDelegate(hookGetInstanceProcAddr);
        }

        private static int hookedGetInstanceProcAddr(ulong xrInstance, IntPtr name, out IntPtr function)
        {
            int result = nextGetInstanceProcAddr(xrInstance, name, out function);
            if (result < 0 || function == IntPtr.Zero)
            {
                return result;
            }
            try
            {
                if (Marshal.PtrToStringAnsi(name) == "xrWaitFrame")
                {
                    realWaitFrame = Marshal.GetDelegateForFunctionPointer<WaitFrameFn>(function);
                    function = Marshal.GetFunctionPointerForDelegate(hookWaitFrame);
                }
            }
            catch (Exception e)
            {
                VhvrActionsFeature.LogWarning("VHVR OpenXR hand tracking: " + e);
            }
            return result;
        }

        // May be called from the plugin's own thread: the time is only ever written whole.
        private static int hookedWaitFrame(ulong xrSession, IntPtr frameWaitInfo, XrFrameState* frameState)
        {
            int result = realWaitFrame(xrSession, frameWaitInfo, frameState);
            if (result >= 0 && frameState != null)
            {
                Interlocked.Exchange(ref predictedDisplayTime, frameState->predictedDisplayTime);
            }
            return result;
        }

        private static T Resolve<T>(string name) where T : Delegate
        {
            IntPtr namePtr = Marshal.StringToHGlobalAnsi(name);
            try
            {
                return nextGetInstanceProcAddr != null &&
                    nextGetInstanceProcAddr(instance, namePtr, out IntPtr function) >= 0 && function != IntPtr.Zero ?
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
            // The plugin goes on either way: without hand tracking the fingers keep their animation.
            return true;
        }

        protected override void OnAppSpaceChange(ulong xrSpace)
        {
            appSpace = xrSpace;
        }

        protected override void OnSessionCreate(ulong xrSession)
        {
            session = xrSession;
            try
            {
                createTrackers();
            }
            catch (Exception e)
            {
                VhvrActionsFeature.LogWarning("VHVR OpenXR hand tracking could not be set up: " + e);
            }
        }

        private static void createTrackers()
        {
            if (!OpenXRRuntime.IsExtensionEnabled(HandTrackingExtension))
            {
                VhvrActionsFeature.LogInfo("OpenXR hand tracking: the runtime does not have " + HandTrackingExtension + ".");
                return;
            }
            createHandTracker = Resolve<CreateHandTrackerFn>("xrCreateHandTrackerEXT");
            destroyHandTracker = Resolve<DestroyHandTrackerFn>("xrDestroyHandTrackerEXT");
            locateHandJoints = Resolve<LocateHandJointsFn>("xrLocateHandJointsEXT");
            if (createHandTracker == null || destroyHandTracker == null || locateHandJoints == null)
            {
                VhvrActionsFeature.LogWarning("OpenXR hand tracking: the runtime's hand tracking functions are missing.");
                return;
            }
            hasDataSource = OpenXRRuntime.IsExtensionEnabled(DataSourceExtension);
            int* sources = stackalloc int[2];
            sources[0] = XR_HAND_TRACKING_DATA_SOURCE_UNOBSTRUCTED_EXT;
            sources[1] = XR_HAND_TRACKING_DATA_SOURCE_CONTROLLER_EXT;
            var sourceInfo = new XrHandTrackingDataSourceInfoEXT
            {
                type = XR_TYPE_HAND_TRACKING_DATA_SOURCE_INFO_EXT,
                requestedDataSourceCount = 2,
                requestedDataSources = sources,
            };
            for (int hand = 0; hand < 2; hand++)
            {
                var info = new XrHandTrackerCreateInfoEXT
                {
                    type = XR_TYPE_HAND_TRACKER_CREATE_INFO_EXT,
                    next = hasDataSource ? (IntPtr)(&sourceInfo) : IntPtr.Zero,
                    hand = XR_HAND_LEFT_EXT + hand,
                    handJointSet = XR_HAND_JOINT_SET_DEFAULT_EXT,
                };
                int result = createHandTracker(session, &info, out ulong tracker);
                trackers[hand] = result >= 0 ? tracker : 0;
                if (result < 0)
                {
                    VhvrActionsFeature.LogWarning("OpenXR hand tracking: no tracker for the " + (hand == 0 ? "left" : "right") +
                        " hand (" + result + ").");
                }
            }
            if (IsAvailable)
            {
                VhvrActionsFeature.LogInfo("OpenXR hand tracking is on" +
                    (hasDataSource ? ", from bare hands and from controllers." : ", from whatever source the runtime picks."));
            }
        }

        private static void destroyTrackers()
        {
            for (int hand = 0; hand < 2; hand++)
            {
                if (trackers[hand] != 0 && destroyHandTracker != null)
                {
                    destroyHandTracker(trackers[hand]);
                }
                trackers[hand] = 0;
            }
        }

        protected override void OnSessionDestroy(ulong xrSession)
        {
            if (xrSession == session)
            {
                destroyTrackers();
                session = 0;
                appSpace = 0;
                Interlocked.Exchange(ref predictedDisplayTime, 0);
            }
        }

        protected override void OnInstanceDestroy(ulong xrInstance)
        {
            if (xrInstance == instance)
            {
                destroyTrackers();
                instance = 0;
                session = 0;
                appSpace = 0;
                Interlocked.Exchange(ref predictedDisplayTime, 0);
            }
        }
    }
}
