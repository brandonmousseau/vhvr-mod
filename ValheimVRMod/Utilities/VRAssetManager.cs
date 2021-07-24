using static ValheimVRMod.Utilities.LogUtils;

using System.Collections.Generic;
using System.IO;
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
        private static readonly string GAME_FIXES = "game_fixes";
        private static readonly string STEAM_VR_SHADERS = "steamvr_shaders";
        private static readonly string AMPLIFY_SHADERS = "amplify_resources";
        private static Dictionary<string, Object> _assets;
        private static bool initialized = false;

        /**
         * Loads all the prefabs from disk and saves references
         * to them in local memory for quick access.
         */
        public static bool Initialize()
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
                { STEAMVR_PREFAB_ASSETBUNDLE_NAME, CUSTOM_RESOURCES_ASSETBUNDLE_NAME, GAME_FIXES })
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

        private static bool LoadAssets(string assetBundleName)
        {
            string assetBundlePath = Path.Combine(Application.streamingAssetsPath,
                assetBundleName);
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

        public static bool LoadSteamVRShaders()
        {
            LogDebug("Loading steamvr_shaders");
            return ShaderLoader.Initialize(Path.Combine(Application.streamingAssetsPath, STEAM_VR_SHADERS));
        }

        public static bool LoadAmplifyShaders()
        {
            LogDebug("Loading Amplify Occlusion shaders");
            return ShaderLoader.Initialize(Path.Combine(Application.streamingAssetsPath, AMPLIFY_SHADERS));
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
