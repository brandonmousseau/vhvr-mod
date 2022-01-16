using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Valve.VR;

public class circleScript : MonoBehaviour
{
    
    private static readonly string STEAMVR_PREFAB_ASSETBUNDLE_NAME = "steamvr_player_prefabs";
    private static readonly string CUSTOM_RESOURCES_ASSETBUNDLE_NAME = "vhvr_custom";
    private static readonly string STEAM_VR_SHADERS = "steamvr_shaders";
    private static readonly string AMPLIFY_SHADERS = "amplify_resources";
    private static Dictionary<string, Object> _assets;

    // Start is called before the first frame update
    void Start()
    {
        Initialize();
        
        var texture = GetAsset<Sprite>("circle");
        
        transform.localScale *= 4;
        var standardRenderer = gameObject.AddComponent<SpriteRenderer>();
        standardRenderer.sprite = texture;
        standardRenderer.color = Color.green;
        standardRenderer.sortingOrder = 0;
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
    
    private static bool initialized = false;

        /**
         * Loads all the prefabs from disk and saves references
         * to them in local memory for quick access.
         */
        public static bool Initialize()
        {
            Debug.Log("Initializing VRAssetManager");
            if (initialized)
            {
                Debug.Log("VR assets already loaded.");
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
                Debug.LogError("Problem loading required SteamVR shaders.");
                initialized = false;
                return false;
            }
            if (!LoadAmplifyShaders())
            {
                Debug.LogError("Problem loading Amplify Occlusion shaders.");
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
                Debug.LogError("Problem loading AssetBundle from file: " + assetBundlePath);
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
                    Debug.LogWarning("Asset with duplicate name loaded: " + asset.name);
                }
            }
            return true;
        }

        public static bool LoadSteamVRShaders()
        {
            Debug.Log("Loading steamvr_shaders");
            return ShaderLoader.Initialize(Path.Combine(Application.streamingAssetsPath, STEAM_VR_SHADERS));
        }

        public static bool LoadAmplifyShaders()
        {
            Debug.Log("Loading Amplify Occlusion shaders");
            return ShaderLoader.Initialize(Path.Combine(Application.streamingAssetsPath, AMPLIFY_SHADERS));
        }

    
    public static T GetAsset<T>(string name) where T :Object
    {
        Debug.Log("Getting asset: " + name);
        if (!initialized)
        {
            Debug.LogError("GetAsset called before Initialize()");
            return default;
        }
        
        if (!_assets.ContainsKey(name))
        {
            Debug.LogError("No asset with name found: " + name);
        }
        var loadedAsset = _assets[name];
        if (loadedAsset == null)
        {
            Debug.LogError("Loaded asset is null!");
            return default;
        }
        if (!loadedAsset.GetType().IsAssignableFrom(typeof(T))) {
            Debug.LogError("Asset " + name + " is not assignable to type " + typeof(T));
            return default;
        }
        Debug.Log("Asset " + name + " successfully retrieved.");
        return loadedAsset as T;
    }
    
}
