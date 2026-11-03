using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.VRCore.Backends
{
    // The only code that names types from Unity.XR.OpenXR.dll, so that the assembly is loaded only when the OpenXR
    // runtime is chosen, and only once VR is being initialized.
    static class OpenXRLoaderSetup
    {
        // Creates, at runtime, the XR Plug-in Management settings that a Unity project would build into its player
        // for OpenXR: the SteamVR runtime loads its own from the xrmanager asset bundle instead. Both settings objects
        // register themselves as the runtime instance from Awake(), which runs inside CreateInstance().
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static XRManagerSettings CreateManager()
        {
            if (XRGeneralSettings.Instance != null)
            {
                LogError("XR Plug-in Management settings already exist; not creating the OpenXR ones.");
                return null;
            }

            OpenXRSettings openXrSettings = ScriptableObject.CreateInstance<OpenXRSettings>();
            // No features: KB+M play needs no interaction profiles, and every other feature is optional.
            // Multi pass, as with OpenVR (Open VR Settings.asset): Valheim's shaders are not instancing aware.
            openXrSettings.renderMode = OpenXRSettings.RenderMode.MultiPass;

            // The loader for runtimes without pre-initialization: the mod is loaded long after the graphics device
            // is created, so the runtime gets the device Unity already has.
            OpenXRLoaderNoPreInit loader = ScriptableObject.CreateInstance<OpenXRLoaderNoPreInit>();
            loader.name = "OpenXR Loader";

            XRManagerSettings manager = ScriptableObject.CreateInstance<XRManagerSettings>();
            manager.name = "OpenXR Manager Settings";
            manager.automaticLoading = false;
            manager.automaticRunning = false;
            // The loaders list is the one to change at runtime: TryAddLoader() only accepts loaders registered in
            // the editor, and InitializeLoaderSync() goes through this list without checking registration.
#pragma warning disable CS0618
            manager.loaders.Add(loader);
#pragma warning restore CS0618

            XRGeneralSettings general = ScriptableObject.CreateInstance<XRGeneralSettings>();
            general.name = "OpenXR General Settings";
            general.Manager = manager;
            // Kept for the session: Valheim unloads unused assets on every scene change.
            foreach (Object settings in new Object[] { openXrSettings, loader, manager, general })
            {
                settings.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            }

            if (XRGeneralSettings.Instance != general)
            {
                LogError("The OpenXR settings did not register as the XR Plug-in Management settings.");
                return null;
            }
            return manager;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void LogRuntime()
        {
            LogInfo("OpenXR runtime: " + UnityEngine.XR.OpenXR.OpenXRRuntime.name + " " +
                    UnityEngine.XR.OpenXR.OpenXRRuntime.version + ", API " + UnityEngine.XR.OpenXR.OpenXRRuntime.apiVersion +
                    ", plugin " + UnityEngine.XR.OpenXR.OpenXRRuntime.pluginVersion);
        }
    }
}
