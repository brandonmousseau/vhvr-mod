using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using ValheimVRMod.Utilities;
using static ValheimVRMod.VRCore.UI.VRHud;
using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.VRCore.UI.HudElements
{
    /**
    SelectedInfo Hierarchy:
    |SelectedInfo (UnityEngine.RectTransform;)
    |   |Bkg2 (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
    |   |Bkg2 (1) (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
    |   |selected_piece (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;)
    |   |   |piece_icon (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
    |   |   |piece_name (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Text;UnityEngine.UI.Outline;)
    |   |   |piece_description (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Text;UnityEngine.UI.Outline;)
    |   |requirements (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
    |   |   |res_bkg (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;UITooltip;)
    |   |   |   |res_icon (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
    |   |   |   |res_name (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Text;UnityEngine.UI.Outline;)
    |   |   |   |res_amount (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Text;UnityEngine.UI.Outline;)
    |   |   |res_bkg (1) (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;UITooltip;)
    |   |   |   |res_icon (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
    |   |   |   |res_name (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Text;UnityEngine.UI.Outline;)
    |   |   |   |res_amount (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Text;UnityEngine.UI.Outline;)
    |   |   |res_bkg (2) (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;UITooltip;)
    |   |   |   |res_icon (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
    |   |   |   |res_name (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Text;UnityEngine.UI.Outline;)
    |   |   |   |res_amount (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Text;UnityEngine.UI.Outline;)
    |   |   |res_bkg (3) (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;UITooltip;)
    |   |   |   |res_icon (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
    |   |   |   |res_name (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Text;UnityEngine.UI.Outline;)
    |   |   |   |res_amount (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Text;UnityEngine.UI.Outline;)
    |   |   |res_bkg (4) (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;UITooltip;)
    |   |   |   |res_icon (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
    |   |   |   |res_name (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Text;UnityEngine.UI.Outline;)
    |   |   |   |res_amount (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Text;UnityEngine.UI.Outline;)
    |   |   |res_bkg (5) (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;UITooltip;)
    |   |   |   |res_icon (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
    |   |   |   |res_name (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Text;UnityEngine.UI.Outline;)
    |   |   |   |res_amount (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Text;UnityEngine.UI.Outline;)
    |   |build_menu_help (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Text;UnityEngine.UI.Outline;)
    |   |help_Text_short (1) (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Text;UnityEngine.UI.Outline;)
     */
    class BuildSelectedInfoElement : IVRHudElement
    {
        public string Placement => "Build";
        public HudOrientation Orientation => HudOrientation.Horizontal;
        private class BuildSelectedInfoComponents : IVRPanelComponent
        {
            public GameObject Root => selectedInfoRoot;

            public GameObject selectedInfoRoot;
            public TMPro.TMP_Text buildSelection;
            public TMPro.TMP_Text pieceDescription;
            public Image buildIcon;
            public GameObject[] requirementItems;

            public void Clear()
            {
                selectedInfoRoot = null;
                buildSelection = null;
                pieceDescription = null;
                buildIcon = null;
                requirementItems = null;
            }
        }
        private BuildSelectedInfoComponents _original = new BuildSelectedInfoComponents();
        public IVRPanelComponent Original => _original;

        private BuildSelectedInfoComponents _clone = new BuildSelectedInfoComponents();
        public IVRPanelComponent Clone => _clone;
        public void Reset()
        {
            //Destroy clone
            GameObject.Destroy(_clone.Root);
            _clone.Clear();

            //Restore Hud to original
            if (_original.selectedInfoRoot && !_original.selectedInfoRoot.activeSelf)
            {
                _original.selectedInfoRoot.SetActive(true);
                updateBuildSelectedInfoReferences(_original);
                _original.Clear();
            }
        }

        public void Update()
        {
            maybeCloneBuildSelectedInfoComponents();
            if (_original.selectedInfoRoot)
            {
                _original.selectedInfoRoot.SetActive(false);
            }
            updateBuildSelectedInfoReferences(_clone);
        }

        private void maybeCloneBuildSelectedInfoComponents()
        {
            if (_clone.selectedInfoRoot)
            {
                // Already cloned
                return;
            }
            if (Hud.instance == null)
            {
                return;
            }
            if (Hud.instance.m_buildHud.transform == null)
            {
                return;
            }
            
            var selectedInfoCheck = Hud.instance.m_buildHud.transform.Find("SelectedInfo");
            if (selectedInfoCheck == null)
            {
                return;
            }
            cacheBuildSelectedInfoComponents(selectedInfoCheck.gameObject, _original);
            GameObject buildSelectedInfoClone = GameObject.Instantiate(selectedInfoCheck.gameObject);
            cacheBuildSelectedInfoComponents(buildSelectedInfoClone, _clone);
            var cloneTransform = _clone.Root.GetComponent<RectTransform>();
            cloneTransform.localPosition = Vector3.zero;
            cloneTransform.localRotation = Quaternion.identity;
            cloneTransform.localScale *= 0.5f;
        }

        private void cacheBuildSelectedInfoComponents(GameObject root, BuildSelectedInfoComponents cache)
        {
            if (!root)
            {
                LogError("Invalid root object while caching SelectedInfo");
            }
            cache.selectedInfoRoot = root;
            cache.buildSelection = root.transform.Find("selected_piece/piece_name").GetComponent<TMPro.TMP_Text>();
            cache.pieceDescription = root.transform.Find("selected_piece/piece_description").GetComponent<TMPro.TMP_Text>();
            cache.buildIcon = root.transform.Find("selected_piece/piece_icon").GetComponent<Image>();
            var requirementLength = Hud.instance.m_requirementItems.Length;
            cache.requirementItems = new GameObject[requirementLength];
            for (int i=0; i< requirementLength; i++)
            {
                string reqname = i == 0 ? "res_bkg" : "res_bkg (" + i + ")"; 
                cache.requirementItems[i] = cache.selectedInfoRoot.transform.Find("requirements").Find(reqname).gameObject;
            }
        }

        private void updateBuildSelectedInfoReferences(BuildSelectedInfoComponents newComponents)
        {
            Hud.instance.m_buildSelection = newComponents.buildSelection;
            Hud.instance.m_pieceDescription = newComponents.pieceDescription;
            Hud.instance.m_buildIcon = newComponents.buildIcon;
            Hud.instance.m_requirementItems = newComponents.requirementItems;
            if (Player.m_localPlayer && Player.m_localPlayer.InPlaceMode())
            {
                newComponents.selectedInfoRoot.SetActive(true);
            }
            else
            {
                newComponents.selectedInfoRoot.SetActive(false);
            }
        }


    }
}
