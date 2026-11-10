using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;
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
            // Multi pass, as with OpenVR (Open VR Settings.asset): Valheim's shaders are not instancing aware.
            openXrSettings.renderMode = OpenXRSettings.RenderMode.MultiPass;
            var features = createInteractionProfiles();
            // OpenXRSettings.features is internal: a Unity project fills it in the editor, from the features found in
            // the project. The loader reads it when it initializes, so it must be set before.
            FieldInfo featuresField = typeof(OpenXRSettings).GetField("features", BindingFlags.Instance | BindingFlags.NonPublic);
            if (featuresField == null)
            {
                LogError("OpenXRSettings.features not found: the controllers will not be available.");
            }
            else
            {
                // Typed in this method's body only: no signature of the mod may name an OpenXR type.
                var typed = new OpenXRFeature[features.Length];
                for (int i = 0; i < features.Length; i++)
                {
                    typed[i] = (OpenXRFeature)features[i];
                }
                featuresField.SetValue(openXrSettings, typed);
            }

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
            foreach (ScriptableObject feature in features)
            {
                feature.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            }

            if (XRGeneralSettings.Instance != general)
            {
                LogError("The OpenXR settings did not register as the XR Plug-in Management settings.");
                return null;
            }
            return manager;
        }

        // The interaction profiles, each a feature that suggests its controller's bindings to the runtime and gives the
        // controller as a legacy XR input device, which is what the mod reads (OpenXRController). None needs an
        // extension. The runtime picks the profile of the controllers in use, or remaps them to one of these; the
        // Khronos simple controller is the last resort.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ScriptableObject[] createInteractionProfiles()
        {
            var types = new[]
            {
                typeof(OculusTouchControllerProfile),
                typeof(ValveIndexControllerProfile),
                typeof(HTCViveControllerProfile),
                typeof(MicrosoftMotionControllerProfile),
                typeof(KHRSimpleControllerProfile),
            };
            // Filled in from the feature's attribute when a project is built, and only used to sort and log.
            FieldInfo nameUi = typeof(OpenXRFeature).GetField("nameUi", BindingFlags.Instance | BindingFlags.NonPublic);
            var features = new ScriptableObject[types.Length];
            for (int i = 0; i < types.Length; i++)
            {
                var feature = (OpenXRFeature)ScriptableObject.CreateInstance(types[i]);
                feature.name = types[i].Name;
                nameUi?.SetValue(feature, types[i].Name);
                feature.enabled = true;
                features[i] = feature;
            }
            return features;
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
