using static ValheimVRMod.Utilities.LogUtils;

using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;
using Valve.VR;

/**
 * Manages assets required for mod
 */
namespace ValheimVRMod.Utilities
{
    static class VRAssetManager
    {
        private static readonly string STEAMVR_PREFAB_ASSETBUNDLE_NAME = "steamvr_player_prefabs";
        private static readonly string CUSTOM_RESOURCES_ASSETBUNDLE_NAME = "vhvr_custom";
        private static readonly string STEAM_VR_SHADERS = "steamvr_shaders";
        private static readonly string AMPLIFY_SHADERS = "amplify_resources";
        private static Dictionary<string, Object> _assets;
        private static bool initialized = false;

        /**
         * Loads the assets a flat screen session needs and saves references to them in local memory for
         * quick access. That is the custom resource bundle alone: it holds the bow bending material, which
         * is needed to render a remote VR player's bow, while the SteamVR bundles and shaders are of no use
         * without a headset and cannot even be loaded without the VR assemblies installed.
         */
        public static bool InitializeFlatScreenAssets()
        {
            LogDebug("Initializing VRAssetManager for flat screen mode");
            if (initialized)
            {
                LogDebug("Assets already loaded.");
                return true;
            }
            _assets = new Dictionary<string, Object>();
            // GetAsset() refuses to hand anything out until this is set, so it has to be set even though
            // only one of the bundles was loaded.
            initialized = LoadAssets(CUSTOM_RESOURCES_ASSETBUNDLE_NAME);
            return initialized;
        }

        /**
         * Loads all the prefabs from disk and saves references
         * to them in local memory for quick access.
         */
        [VrOnly]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool InitializeVrAssets()
        {
            LogDebug("Initializing VRAssetManager");
            if (initialized)
            {
                LogDebug("VR assets already loaded.");
                return true;
            }
            _assets = new Dictionary<string, Object>();
            bool loadResult = true;
            foreach (var assetBundleName in new string[]
                { STEAMVR_PREFAB_ASSETBUNDLE_NAME, CUSTOM_RESOURCES_ASSETBUNDLE_NAME })
            {
                loadResult &= LoadAssets(assetBundleName);
            }
            if (!LoadSteamVRShaders())
            {
                LogError("Problem loading required SteamVR shaders.");
                initialized = false;
                return false;
            }
            if (!LoadAmplifyShaders())
            {
                LogError("Problem loading Amplify Occlusion shaders.");
                return false;
            }
            initialized = loadResult;
            return loadResult;
        }

        // The flat screen package installs nothing into the game folder, so its bundles sit next to the
        // plugin. StreamingAssets stays the fallback, which is where the full VR package puts them.
        private static string ResolveBundlePath(string assetBundleName)
        {
            string pluginDirectory = Path.GetDirectoryName(typeof(VRAssetManager).Assembly.Location);
            if (pluginDirectory != null)
            {
                string localPath = Path.Combine(pluginDirectory, assetBundleName);
                if (File.Exists(localPath))
                {
                    return localPath;
                }
            }
            return Path.Combine(Application.streamingAssetsPath, assetBundleName);
        }

        private static bool LoadAssets(string assetBundleName)
        {
            string assetBundlePath = ResolveBundlePath(assetBundleName);
            AssetBundle prefabAssetBundle = AssetBundle.LoadFromFile(assetBundlePath);
            if (prefabAssetBundle == null)
            {
                LogError("Problem loading AssetBundle from file: " + assetBundlePath);
                return false;
            }
            foreach (var asset in prefabAssetBundle.LoadAllAssets())
            {
                if (!_assets.ContainsKey(asset.name))
                {
                    _assets.Add(asset.name, asset);
                }
                else
                {
                    LogWarning("Asset with duplicate name loaded: " + asset.name);
                }
            }
            return true;
        }

        // ShaderLoader lives in SteamVR.dll, so these two are only ever called from InitializeVrAssets().
        [VrOnly]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool LoadSteamVRShaders()
        {
            LogDebug("Loading steamvr_shaders");
            return ShaderLoader.Initialize(ResolveBundlePath(STEAM_VR_SHADERS));
        }

        [VrOnly]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool LoadAmplifyShaders()
        {
            LogDebug("Loading Amplify Occlusion shaders");
            return ShaderLoader.Initialize(ResolveBundlePath(AMPLIFY_SHADERS));
        }

        /**
         * Return an asset of type T from the loaded
         * asset bundles.
         */
        public static T GetAsset<T>(string name) where T :Object
        {
            LogDebug("Getting asset: " + name);
            if (!initialized)
            {
                LogError("GetAsset called before Initialize()");
                return default;
            }
            if (!_assets.ContainsKey(name))
            {
                LogError("No asset with name found: " + name);
            }
            var loadedAsset = _assets[name];
            if (loadedAsset == null)
            {
                LogError("Loaded asset is null!");
                return default;
            }
            if (!loadedAsset.GetType().IsAssignableFrom(typeof(T))) {
                LogError("Asset " + name + " is not assignable to type " + typeof(T));
                return default;
            }
            LogDebug("Asset " + name + " successfully retrieved.");
            return loadedAsset as T;
        }

    }
}
