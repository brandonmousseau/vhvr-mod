﻿using UnityEngine;
using UnityEngine.UI;
using ValheimVRMod.Utilities;
using static ValheimVRMod.VRCore.UI.VRHud;
using static ValheimVRMod.Utilities.LogUtils;
using Valve.VR;

namespace ValheimVRMod.VRCore.UI.HudElements
{
    /**
     * Small Minimap Panel Hierarchy:
        small(Clone) (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
          |map (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.RawImage;UnityEngine.UI.RectMask2D;)
          |   |pin_root (UnityEngine.RectTransform;UnityEngine.Canvas;)
          |   |   |MapPin(Clone) (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
          |   |   |   |Checked (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
          |   |   |   |Name (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Text;UnityEngine.UI.Outline;)
          |   |   |....
          |   |ship_marker (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
          |   |player_marker (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
          |   |wind_marker (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;UnityEngine.Canvas;)
          |biome (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Text;UnityEngine.UI.Outline;UnityEngine.Animator;)
          |Key tip (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Text;UnityEngine.UI.Outline;)
     */
    public class MinimapPanelElement : IVRHudElement
    {
        public string Placement => VHVRConfig.MinimapPanelPlacement();
        public HudOrientation Orientation => HudOrientation.Horizontal;

        private bool bToggle;
        private bool toggledOn = true;

        //Data class to store references to the small minimap elements
        private class MinimapPanelComponents : IVRPanelComponent
        {
            public GameObject Root => mapRoot;

            public GameObject mapRoot;          //GameObject        "small"
            public GameObject mapBiomeName;     //Text              "small/biome"
            public GameObject map;              //GameObject        "small/map"
            public GameObject mapPinsRoot;      //RectTransform     "small/map/pin_root"
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
            if (!bToggle && SteamVR_Actions.valheim_QuickActions.GetState(SteamVR_Input_Sources.Any)
                && SteamVR_Actions.valheim_QuickSwitch.GetState(SteamVR_Input_Sources.Any))
            {
                bToggle = true;
                toggledOn = !toggledOn;
            }
            else if (bToggle && !SteamVR_Actions.valheim_QuickActions.GetState(SteamVR_Input_Sources.Any)
                && !SteamVR_Actions.valheim_QuickSwitch.GetState(SteamVR_Input_Sources.Any))
            {
                bToggle = false;
            }
            maybeCloneSmallMinimapPanelComponents();
            if (_original.mapRoot)
            {
                _original.mapRoot.SetActive(false);
            }
            if (Minimap.m_instance.m_mode == Minimap.MapMode.Small)
            {
                _clone.Root.SetActive(toggledOn);
            } else
            {
                _clone.Root.SetActive(false);
            }
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
            cacheSmallMinimapPanelComponents(Minimap.instance.m_smallRoot.gameObject, _original);
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

        private void cacheSmallMinimapPanelComponents(GameObject root, MinimapPanelComponents cache)
        {
            if (!root)
            {
                LogError("Invalid root object while caching SmallMinimapPanel");
            }
            cache.mapRoot = root;
            cache.mapBiomeName = root.transform.Find("biome").gameObject;
            cache.map = root.transform.Find("map").gameObject;
            cache.mapPinsRoot = cache.map.transform.Find("pin_root").gameObject;
            cache.mapMarker = cache.map.transform.Find("player_marker").gameObject;
            cache.mapWindMarker = cache.map.transform.Find("wind_marker").gameObject;
            cache.mapShipMarker = cache.map.transform.Find("ship_marker").gameObject;
        }

        private void updateSmallMinimapPanelHudReferences(MinimapPanelComponents newComponents, bool isOriginal)
        {
            Minimap.instance.m_smallRoot = newComponents.mapRoot;
            Minimap.instance.m_mapSmall = newComponents.map;
            Minimap.instance.m_mapImageSmall = newComponents.map.GetComponent<RawImage>();
            Minimap.instance.m_biomeNameSmall = newComponents.mapBiomeName.GetComponent<Text>();
            if (isOriginal)
            {
                // Use the original pinRoot
                Minimap.instance.m_pinRootSmall = newComponents.mapPinsRoot.GetComponent<RectTransform>();
            } else
            {
                // Use the map as the pin root for the VR HUD (for...reasons...? can't make them render
                // otherwise but no idea why xD ....)
                Minimap.instance.m_pinRootSmall = newComponents.map.GetComponent<RectTransform>();
            }
            Minimap.instance.m_smallMarker = newComponents.mapMarker.GetComponent<RectTransform>();
            Minimap.instance.m_smallShipMarker = newComponents.mapShipMarker.GetComponent<RectTransform>();
            Minimap.instance.m_windMarker = newComponents.mapWindMarker.GetComponent<RectTransform>();
        }
    }
}
