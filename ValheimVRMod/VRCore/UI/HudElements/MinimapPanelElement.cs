using UnityEngine;
using UnityEngine.UI;
using ValheimVRMod.Utilities;
using static ValheimVRMod.VRCore.UI.VRHud;
using static ValheimVRMod.Utilities.LogUtils;
using TMPro;

namespace ValheimVRMod.VRCore.UI.HudElements
{
    public class MinimapPanelElement : IVRHudElement
    {
        public string Placement => VHVRConfig.MinimapPanelPlacement();
        public HudOrientation Orientation => HudOrientation.Horizontal;

        private bool toggledOn = true;
        private bool wasTogglingMap;
        // Set when the vanilla small map is not laid out as expected, e. g. because another mod replaced it. It is
        // then left alone, to whatever took its place.
        private bool smallMapUnavailable;

        //Data class to store references to the small minimap elements
        private class MinimapPanelComponents : IVRPanelComponent
        {
            public GameObject Root => mapRoot;

            public GameObject mapRoot;          //GameObject        "small"
            public GameObject mapBiomeName;     //Text              "small/small_biome"
            public GameObject map;              //GameObject        "small/map"
            public GameObject mapPinsRoot;      //RectTransform     "small/map/small_mapPin_root"
            public GameObject mapMarker;        //RectTransform     "small/map/player_marker"
            public GameObject mapWindMarker;    //RectTransform     "small/map/wind_marker"
            public GameObject mapShipMarker;    //RectTransform     "small/map/ship_marker"

            public void Clear()
            {
                mapRoot = null;
                map = null;
                mapBiomeName = null;
                mapMarker = null;
                mapShipMarker = null;
                mapWindMarker = null;
                mapPinsRoot = null;
            }
        }

        private MinimapPanelComponents _original = new MinimapPanelComponents();
        public IVRPanelComponent Original => _original;

        private MinimapPanelComponents _clone = new MinimapPanelComponents();
        public IVRPanelComponent Clone => _clone;

        public void Reset()
        {
            //Destroy clone
            GameObject.Destroy(_clone.Root);
            _clone.Clear();

            //Restore Hud to original
            if (_original.mapRoot && !_original.mapRoot.activeSelf)
            {
                _original.mapRoot.SetActive(true);
                updateSmallMinimapPanelHudReferences(_original, true);
                _original.Clear();
            }
        }

        public void Update()
        {
            if (GetButtonPatchUtils.GetButtonPatched(VRControls.ToggleMiniMap))
            {
                if (!wasTogglingMap)
                {
                    toggledOn = !toggledOn;
                }
                wasTogglingMap = true;
            }
            else
            {
                wasTogglingMap = false;
            }

            if (smallMapUnavailable || Minimap.instance == null)
            {
                return;
            }

            maybeCloneSmallMinimapPanelComponents();
            if (!_clone.mapRoot)
            {
                return;
            }
            if (_original.mapRoot)
            {
                _original.mapRoot.SetActive(false);
            }

            _clone.Root.SetActive(toggledOn && Minimap.instance.m_mode == Minimap.MapMode.Small);

            updateSmallMinimapPanelHudReferences(_clone, false);
        }

        private void maybeCloneSmallMinimapPanelComponents()
        {
            if (_clone.mapRoot)
            {
                // Already cloned
                return;
            }
            if (Minimap.instance == null)
            {
                return;
            }
            if (Minimap.instance.m_smallRoot == null)
            {
                return;
            }
            if (!cacheSmallMinimapPanelComponents(Minimap.instance.m_smallRoot.gameObject, _original))
            {
                LogWarning("The small minimap is not laid out as expected, possibly replaced by another mod. It will not be shown on the VR HUD.");
                _original.Clear();
                smallMapUnavailable = true;
                return;
            }
            GameObject smallMinimapPanelClone = GameObject.Instantiate(Minimap.instance.m_smallRoot.gameObject);
            cacheSmallMinimapPanelComponents(smallMinimapPanelClone, _clone);

            var cloneTransform = _clone.Root.GetComponent<RectTransform>();
            _clone.Root.AddComponent<LayoutElement>();
            cloneTransform.localPosition = Vector3.zero;
            cloneTransform.localRotation = Quaternion.identity;

            // This being enabled this causes PlayerMarker to not be visible when moving
            // the canvas around to different HUD locations. It doesn't seem like
            // anything is broken by just disabling it.
            _clone.map.GetComponent<RectMask2D>().enabled = false;
        }

        // Returns false if any of the expected parts is missing, leaving the cache incomplete.
        private bool cacheSmallMinimapPanelComponents(GameObject root, MinimapPanelComponents cache)
        {
            if (!root)
            {
                LogError("Invalid root object while caching SmallMinimapPanel");
                return false;
            }
            cache.mapRoot = root;
            cache.mapBiomeName = findChild(root.transform, "small_biome");
            cache.map = findChild(root.transform, "map");
            if (!cache.map)
            {
                return false;
            }
            cache.mapPinsRoot = findChild(cache.map.transform, "small_mapPin_root");
            cache.mapMarker = findChild(cache.map.transform, "player_marker");
            cache.mapWindMarker = findChild(cache.map.transform, "wind_marker");
            cache.mapShipMarker = findChild(cache.map.transform, "ship_marker");
            return cache.mapBiomeName && cache.mapPinsRoot && cache.mapMarker && cache.mapWindMarker && cache.mapShipMarker &&
                cache.map.GetComponent<RawImage>() && cache.map.GetComponent<RectMask2D>() && cache.mapBiomeName.GetComponent<TMP_Text>();
        }

        private static GameObject findChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            return child ? child.gameObject : null;
        }

        private void updateSmallMinimapPanelHudReferences(MinimapPanelComponents newComponents, bool isOriginal)
        {
            Minimap.instance.m_smallRoot = newComponents.mapRoot;
            Minimap.instance.m_mapSmall = newComponents.map;
            Minimap.instance.m_mapImageSmall = newComponents.map.GetComponent<RawImage>();
            Minimap.instance.m_biomeNameSmall = newComponents.mapBiomeName.GetComponent<TMP_Text>();
            if (isOriginal)
            {
                // Use the original pinRoot
                Minimap.instance.m_pinRootSmall = newComponents.mapPinsRoot.GetComponent<RectTransform>();
            } else
            {
                // Use the map as the pin root for the VR HUD (for...reasons...? can't make them render
                // otherwise but no idea why xD ....)
                Minimap.instance.m_pinRootSmall = newComponents.map.GetComponent<RectTransform>();
                //Move the player marker to above the pin
                newComponents.mapMarker.transform.SetParent(newComponents.map.transform.parent);

                //make sure hud windmarker is on the right layer
                newComponents.mapWindMarker.gameObject.layer = LayerUtils.getWorldspaceUiLayer();
                if (EnvMan.instance != null)
                {
                    Quaternion quaternion = Quaternion.LookRotation(EnvMan.instance.GetWindDir());
                    newComponents.mapWindMarker.transform.localRotation = Quaternion.Euler(0f, 0f, -quaternion.eulerAngles.y);
                }
            }
            Minimap.instance.m_smallMarker = newComponents.mapMarker.GetComponent<RectTransform>();
            Minimap.instance.m_smallShipMarker = newComponents.mapShipMarker.GetComponent<RectTransform>();
            Minimap.instance.m_windMarker = newComponents.mapWindMarker.GetComponent<RectTransform>();
        }
    }
}
