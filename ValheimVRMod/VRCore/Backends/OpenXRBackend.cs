using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using ValheimVRMod.Utilities;
using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.VRCore.Backends
{
    // The headset and controllers through the system's OpenXR runtime instead of SteamVR: head tracking, stereo
    // rendering, the hands, laser pointers, and VHVR's actions registered with the runtime, which suggests their
    // bindings and may let the player rebind them in its own UI (see OpenXRInputBackend). No haptics, body trackers,
    // finger tracking, overlays or system keyboard, so GetUseOverlayGui() is false whatever the settings say and text
    // is typed on the physical keyboard.
    //
    // SteamVR must never start under this backend. Nothing here calls into OpenVR, and the SteamVR components of the
    // rig prefab, some of which initialize SteamVR when enabled, are removed before the rig is enabled (see
    // OpenXRRigBackend). The OpenXR types themselves are only named in OpenXRLoaderSetup.
    public sealed class OpenXRBackend : IVRBackend
    {
        public string RuntimeName => "OpenXR";
        public bool SupportsControllers => true;
        public bool SupportsOverlay => false;
        public IVRInputBackend Input => input;
        private readonly OpenXRInputBackend input = new OpenXRInputBackend();
        public IVRRigBackend Rig { get; } = new OpenXRRigBackend();
        public IVRKeyboardBackend Keyboard { get; } = new NullVRKeyboardBackend();
        public IVROverlayBackend Overlay => null;

        // Set once the loader is up, as SteamVRBackend's xrSdkInitialized.
        private bool loaderInitialized;
        private readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();

        public bool InitializeVR()
        {
            LogInfo("Initializing VR through OpenXR...");
            XRManagerSettings manager;
            try
            {
                manager = OpenXRLoaderSetup.CreateManager(input.BuildManifest());
            }
            catch (Exception e) when (e is FileNotFoundException || e is TypeLoadException)
            {
                LogError("The OpenXR plugin (Unity.XR.OpenXR.dll and VHVR.OpenXR.dll in valheim_Data\\Managed) could not be loaded: " + e.Message);
                return false;
            }
            if (manager == null || !XRManagementHelper.InitializeLoaders(manager))
            {
                LogError("Failed to initialize OpenXR. Is the headset connected and its OpenXR runtime set as the active one?");
                return false;
            }
            OpenXRLoaderSetup.LogRuntime();
            loaderInitialized = true;
            UpdateMirrorViewMode();
            return true;
        }

        public bool StartVR()
        {
            LogInfo("Starting VR...");
            if (!XRManagementHelper.StartSubsystems())
            {
                return false;
            }
            useFloorOrigin();
            input.Start();
            return true;
        }

        // A standing (floor level) origin, as SteamVR gives the OpenVR loader, rather than OpenXR's default of the
        // headset's position at startup: VRPlayer's height calibration and eye height assume one.
        private void useFloorOrigin()
        {
            var inputs = new List<XRInputSubsystem>();
            SubsystemManager.GetInstances(inputs);
            foreach (XRInputSubsystem input in inputs)
            {
                if ((input.GetSupportedTrackingOriginModes() & TrackingOriginModeFlags.Floor) == 0)
                {
                    LogWarning("The OpenXR runtime has no floor level origin; the head height is relative to where it started.");
                    continue;
                }
                if (!input.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor))
                {
                    LogWarning("Could not set a floor level origin.");
                }
            }
        }

        // There is no separate input system to bring up: the actions come with the runtime (StartVR()).
        public bool InitializeInput() => true;

        // An OpenXR runtime has no notion of running before an application connects to it, and connecting would
        // start it. What can be told without that is whether one is installed as the active runtime: the
        // XR_RUNTIME_JSON override or the ActiveRuntime registry value that the OpenXR loader itself reads.
        public bool IsRuntimeRunning()
        {
            string manifest = Environment.GetEnvironmentVariable("XR_RUNTIME_JSON");
            if (string.IsNullOrEmpty(manifest))
            {
                try
                {
                    using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                    using (var key = hklm.OpenSubKey(@"SOFTWARE\Khronos\OpenXR\1"))
                    {
                        manifest = key?.GetValue("ActiveRuntime") as string;
                    }
                }
                catch (Exception e)
                {
                    LogWarning("Could not read the active OpenXR runtime from the registry: " + e.Message);
                    return false;
                }
            }
            bool installed = !string.IsNullOrEmpty(manifest) && File.Exists(Environment.ExpandEnvironmentVariables(manifest));
            LogInfo("Active OpenXR runtime: " + (installed ? manifest : "none"));
            return installed;
        }

        public bool IsControllerConnected(VRInputSource source)
        {
            switch (source)
            {
                case VRInputSource.LeftHand: return input.IsConnected(XRHand.Left);
                case VRInputSource.RightHand: return input.IsConnected(XRHand.Right);
                case VRInputSource.Any: return input.IsConnected(XRHand.Left) || input.IsConnected(XRHand.Right);
                default: return false;
            }
        }
        public IEnumerable<VRTrackerInfo> GetConnectedTrackers() => new VRTrackerInfo[0];

        // The rig keeps SteamVR_Fade on its camera: it draws the fade itself and only reaches OpenVR to fade the
        // SteamVR overlay, which there is none of here.
        public void Fade(Color color, float duration, bool fadeOverlay = false) => SteamVRAssets.Fade(color, duration, false);
        public Shader GetShader(string name) => SteamVRAssets.GetShader(name);
        public bool LoadShaders(string path) => SteamVRAssets.LoadShaders(path);

        // OpenXR has no mirror modes of its own; the display subsystem's mirror blit modes cover the native ones.
        public void UpdateMirrorViewMode()
        {
            if (!loaderInitialized || VHVRConfig.NonVrPlayer())
            {
                return;
            }
            if (VHVRConfig.UseFullWidthMirror(out _) || VHVRConfig.UseSideBySideMirror() || VHVRConfig.UseNoFlatscreenView())
            {
                // Turns itself on and off with the mode from then on.
                FullWidthMirror.EnsureCreated();
            }
            VRMirrorViewMode mode = VHVRConfig.GetMirrorViewMode();
            int blitMode = MirrorBlitMode(mode);
            LogInfo("Mirror View Mode: " + mode);
            SubsystemManager.GetInstances(displays);
            foreach (XRDisplaySubsystem display in displays)
            {
                display.SetPreferredMirrorBlitMode(blitMode);
            }
        }

        // The OpenVR display provider's trouble with turning the mirror off early (see SteamVRBackend) is its own.
        public void UpdateMirrorSetup() { }

        private static int MirrorBlitMode(VRMirrorViewMode mode)
        {
            switch (mode)
            {
                case VRMirrorViewMode.None: return XRMirrorViewBlitMode.None;
                case VRMirrorViewMode.Left: return XRMirrorViewBlitMode.LeftEye;
                case VRMirrorViewMode.Right: return XRMirrorViewBlitMode.RightEye;
                // The runtime's own choice, as OpenVR's mirror is SteamVR's.
                case VRMirrorViewMode.OpenVR: return XRMirrorViewBlitMode.Default;
                default: throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }

        public void tryRecenter() => XRManagementHelper.Recenter();
    }
}
