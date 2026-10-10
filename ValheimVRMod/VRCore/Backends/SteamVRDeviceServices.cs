using System;
using System.Collections.Generic;
using System.Text;
using Valve.VR;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.VRCore.Backends
{
    public sealed partial class SteamVRBackend
    {
        // vrserver is the runtime; vrmonitor is the status window started alongside it.
        private static readonly string[] k_steamVrProcessNames = { "vrserver", "vrmonitor" };

        // Keep the process probe: querying OpenVR here would launch the runtime.
        public bool IsRuntimeRunning()
        {
            foreach (string processName in k_steamVrProcessNames)
            {
                System.Diagnostics.Process[] processes;
                try
                {
                    processes = System.Diagnostics.Process.GetProcessesByName(processName);
                }
                catch (Exception e)
                {
                    LogUtils.LogWarning("Could not check whether " + processName + " is running: " + e.Message);
                    continue;
                }

                try
                {
                    if (processes.Length > 0)
                    {
                        return true;
                    }
                }
                finally
                {
                    foreach (System.Diagnostics.Process process in processes)
                    {
                        process.Dispose();
                    }
                }
            }
            return false;
        }

        static Unity.XR.OpenVR.OpenVRSettings.MirrorViewModes NativeMirrorMode(VRMirrorViewMode mode)
        {
            switch (mode)
            {
                case VRMirrorViewMode.None: return Unity.XR.OpenVR.OpenVRSettings.MirrorViewModes.None;
                case VRMirrorViewMode.Left: return Unity.XR.OpenVR.OpenVRSettings.MirrorViewModes.Left;
                case VRMirrorViewMode.Right: return Unity.XR.OpenVR.OpenVRSettings.MirrorViewModes.Right;
                case VRMirrorViewMode.OpenVR: return Unity.XR.OpenVR.OpenVRSettings.MirrorViewModes.OpenVR;
                default: throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }

        public bool IsControllerConnected(VRInputSource source)
        {
            var role = source == VRInputSource.LeftHand ? ETrackedControllerRole.LeftHand : ETrackedControllerRole.RightHand;
            if (OpenVR.System == null)
            {
                return false;
            }
            uint deviceIndex = OpenVR.System.GetTrackedDeviceIndexForControllerRole(role);
            return deviceIndex != OpenVR.k_unTrackedDeviceIndexInvalid && OpenVR.System.IsTrackedDeviceConnected(deviceIndex);
        }

        private static string GetTrackerTitle(CVRSystem system, uint deviceIndex)
        {
            var title = new StringBuilder("#" + deviceIndex);
            string model = GetStringProperty(system, deviceIndex, ETrackedDeviceProperty.Prop_ModelNumber_String);
            if (model != "")
            {
                title.Append("  ").Append(model);
            }
            string serial = GetStringProperty(system, deviceIndex, ETrackedDeviceProperty.Prop_SerialNumber_String);
            if (serial != "")
            {
                title.Append("  ").Append(serial);
            }
            // SteamVR reports the role assigned to a tracker as its controller type, e. g. vive_tracker_left_foot.
            const string rolePrefix = "vive_tracker_";
            string controllerType = GetStringProperty(system, deviceIndex, ETrackedDeviceProperty.Prop_ControllerType_String);
            if (controllerType.StartsWith(rolePrefix) && controllerType != rolePrefix + "handed")
            {
                title.Append("  (").Append(controllerType.Substring(rolePrefix.Length).Replace('_', ' ')).Append(')');
            }
            return title.ToString();
        }

        private static string GetStringProperty(CVRSystem system, uint deviceIndex, ETrackedDeviceProperty property)
        {
            var error = ETrackedPropertyError.TrackedProp_Success;
            uint capacity = system.GetStringTrackedDeviceProperty(deviceIndex, property, null, 0, ref error);
            if (capacity <= 1)
            {
                return "";
            }
            var result = new StringBuilder((int)capacity);
            system.GetStringTrackedDeviceProperty(deviceIndex, property, result, capacity, ref error);
            return error == ETrackedPropertyError.TrackedProp_Success ? result.ToString().Trim() : "";
        }

        public IEnumerable<VRTrackerInfo> GetConnectedTrackers()
        {
            var trackers = new List<VRTrackerInfo>();
            var system = OpenVR.System;
            if (system != null)
            {
                for (uint i = 1; i < OpenVR.k_unMaxTrackedDeviceCount; i++)
                {
                    if (system.GetTrackedDeviceClass(i) != ETrackedDeviceClass.GenericTracker || !system.IsTrackedDeviceConnected(i))
                    {
                        continue;
                    }
                    trackers.Add(new VRTrackerInfo { Index = (int)i, Title = GetTrackerTitle(system, i) });
                }
            }
            return trackers;
        }
    }
}
