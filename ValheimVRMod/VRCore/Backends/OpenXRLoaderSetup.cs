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
    // The only code that names types from Unity.XR.OpenXR.dll or VHVR.OpenXR.dll (the mod's own OpenXR feature, which
    // derives from the plugin's), so that they are loaded only when the OpenXR runtime is chosen, and only once VR is
    // being initialized.
    static class OpenXRLoaderSetup
    {
        // Creates, at runtime, the XR Plug-in Management settings that a Unity project would build into its player
        // for OpenXR: the SteamVR runtime loads its own from the xrmanager asset bundle instead. Both settings objects
        // register themselves as the runtime instance from Awake(), which runs inside CreateInstance().
        // actions: VHVR's own OpenXR actions, registered with the runtime by the VHVR.OpenXR feature.
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static XRManagerSettings CreateManager(OpenXRActionManifest actions)
        {
            if (XRGeneralSettings.Instance != null)
            {
                LogError("XR Plug-in Management settings already exist; not creating the OpenXR ones.");
                return null;
            }

            OpenXRSettings openXrSettings = ScriptableObject.CreateInstance<OpenXRSettings>();
            // Multi pass, as with OpenVR (Open VR Settings.asset): Valheim's shaders are not instancing aware.
            openXrSettings.renderMode = OpenXRSettings.RenderMode.MultiPass;
            configureActions(actions);
            var features = createFeatures();
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
        // controller as a legacy XR input device, which the mod reads the poses from (OpenXRController). None needs an
        // extension. The runtime picks the profile of the controllers in use, or remaps them to one of these; the
        // Khronos simple controller is the last resort. Then VHVR's own actions (VhvrActionsFeature), which join the
        // profiles' suggested bindings, attach and sync.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ScriptableObject[] createFeatures()
        {
            var types = new[]
            {
                typeof(OculusTouchControllerProfile),
                typeof(ValveIndexControllerProfile),
                typeof(HTCViveControllerProfile),
                typeof(MicrosoftMotionControllerProfile),
                typeof(KHRSimpleControllerProfile),
                typeof(VHVR.OpenXR.VhvrActionsFeature),
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

        // Hands VHVR's actions to the feature, which creates them with the runtime's instance.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void configureActions(OpenXRActionManifest manifest)
        {
            VHVR.OpenXR.VhvrActionsFeature.LogInfo = message => LogInfo(message);
            VHVR.OpenXR.VhvrActionsFeature.LogWarning = message => LogWarning(message);
            var sets = new VHVR.OpenXR.VhvrActionsFeature.SetSpec[manifest.Sets.Count];
            for (int i = 0; i < sets.Length; i++)
            {
                sets[i] = new VHVR.OpenXR.VhvrActionsFeature.SetSpec { Name = manifest.Sets[i].Name, LocalizedName = manifest.Sets[i].LocalizedName };
            }
            var actions = new VHVR.OpenXR.VhvrActionsFeature.ActionSpec[manifest.Actions.Count];
            for (int i = 0; i < actions.Length; i++)
            {
                OpenXRActionManifest.Action action = manifest.Actions[i];
                actions[i] = new VHVR.OpenXR.VhvrActionsFeature.ActionSpec
                {
                    Set = action.Set,
                    Name = action.Name,
                    LocalizedName = action.LocalizedName,
                    Kind = action.IsVector2 ? VHVR.OpenXR.VhvrActionsFeature.Vector2Action : VHVR.OpenXR.VhvrActionsFeature.BooleanAction,
                };
            }
            var bindings = new VHVR.OpenXR.VhvrActionsFeature.BindingSpec[manifest.Bindings.Count];
            for (int i = 0; i < bindings.Length; i++)
            {
                OpenXRActionManifest.Binding binding = manifest.Bindings[i];
                bindings[i] = new VHVR.OpenXR.VhvrActionsFeature.BindingSpec { Profile = binding.Profile, Action = binding.Action, Path = binding.Path };
            }
            VHVR.OpenXR.VhvrActionsFeature.Sets = sets;
            VHVR.OpenXR.VhvrActionsFeature.Actions = actions;
            VHVR.OpenXR.VhvrActionsFeature.Bindings = bindings;
        }

        // VHVR's actions as the runtime has them (see VhvrActionsFeature); only called once the runtime has started.
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool ActionsAttached() => VHVR.OpenXR.VhvrActionsFeature.IsAttached;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void SetActionSetActive(int set, bool active) => VHVR.OpenXR.VhvrActionsFeature.SetActive(set, active);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool ReadBoolean(int action, int hand, out bool state) =>
            VHVR.OpenXR.VhvrActionsFeature.GetBoolean(action, hand, out state);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool ReadVector2(int action, int hand, out Vector2 value)
        {
            bool bound = VHVR.OpenXR.VhvrActionsFeature.GetVector2(action, hand, out float x, out float y);
            value = new Vector2(x, y);
            return bound;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string CurrentInteractionProfile(int hand) => VHVR.OpenXR.VhvrActionsFeature.CurrentProfile(hand);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void LogRuntime()
        {
            LogInfo("OpenXR runtime: " + UnityEngine.XR.OpenXR.OpenXRRuntime.name + " " +
                    UnityEngine.XR.OpenXR.OpenXRRuntime.version + ", API " + UnityEngine.XR.OpenXR.OpenXRRuntime.apiVersion +
                    ", plugin " + UnityEngine.XR.OpenXR.OpenXRRuntime.pluginVersion);
        }
    }
}
