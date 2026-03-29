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
     * Adrenaline Panel Hierarchy:
        adrenalinepanel 
           |Stamina 

     */
    public class AdrenalinePanelElement : IVRHudElement
    {
        public string Placement => VHVRConfig.AdrenalinePanelPlacement();
        public HudOrientation Orientation => HudOrientation.Horizontal;

        // Data class to store references to important adrenaline bar components
        private class AdrenalinePanelComponents : IVRPanelComponent
        {
            public GameObject Root => adrenalineBarRoot;

            public GameObject adrenalineBarRoot; 
            public GameObject adrenalineBarFast; 
            public GameObject adrenalineBarSlow; 
            public GameObject adrenalineText; 
            public Animator adrenalineAnimator;

            public void Clear()
            {
                adrenalineBarRoot = null;
                adrenalineBarFast = null;
                adrenalineBarSlow = null;
                adrenalineText = null;
                adrenalineAnimator = null;
            }
        }

        private AdrenalinePanelComponents _original = new AdrenalinePanelComponents();
        public IVRPanelComponent Original => _original;

        private AdrenalinePanelComponents _clone = new AdrenalinePanelComponents();
        public IVRPanelComponent Clone => _clone;

        public void Reset()
        {
            //Destroy clone
            GameObject.Destroy(_clone.Root);
            _clone.Clear();

            //Restore Hud to original
            if (_original.adrenalineBarRoot && !_original.adrenalineBarRoot.activeSelf)
            {
                _original.adrenalineBarRoot.SetActive(true);
                updateAdrenalinePanelHudReferences(_original);
                _original.Clear();
            }
        }

        public void Update()
        {
            maybeCloneAdrenalinePanelComponents();
            if (_original.adrenalineBarRoot)
            {
                _original.adrenalineBarRoot.SetActive(false);
            }
            updateAdrenalinePanelHudReferences(_clone);
        }

        private void maybeCloneAdrenalinePanelComponents()
        {
            if (_clone.adrenalineBarRoot)
            {
                // Already cloned
                return;
            }
            if (Hud.instance == null)
            {
                return;
            }
            if (Hud.instance.m_adrenalineBarRoot == null)
            {
                return;
            }
            cacheAdrenalinePanelComponents(Hud.instance.m_adrenalineBarRoot.gameObject, _original);
            GameObject adrenalinePanelClone = GameObject.Instantiate(Hud.instance.m_adrenalineBarRoot.gameObject);
            cacheAdrenalinePanelComponents(adrenalinePanelClone, _clone);

            var cloneTransform = _clone.Root.GetComponent<RectTransform>();
            cloneTransform.localPosition = Vector3.zero;
            cloneTransform.localRotation = Quaternion.identity;
        }

        private void cacheAdrenalinePanelComponents(GameObject root, AdrenalinePanelComponents cache)
        {
            if (!root)
            {
                LogError("Invalid root object while caching AdrenalinePanel");
            }
            cache.adrenalineBarRoot = root;
            //TODO: This is named incorrectly in the current release, but will probably change in the future
            var baseComponent = root.transform.Find("Stamina") ?? root.transform.Find("Adrenaline");
            cache.adrenalineBarSlow = baseComponent.Find("adrenaline_slow").gameObject;
            cache.adrenalineBarFast = baseComponent.Find("adrenaline_fast").gameObject;
            cache.adrenalineText = baseComponent.Find("adrenalineText").gameObject;
            cache.adrenalineAnimator = root.GetComponent<Animator>();
        }

        private void updateAdrenalinePanelHudReferences(AdrenalinePanelComponents newComponents)
        {
            Hud.instance.m_adrenalineBarRoot = newComponents.adrenalineBarRoot.GetComponent<RectTransform>();
            Hud.instance.m_adrenalineAnimator = newComponents.adrenalineAnimator;
            Hud.instance.m_adrenalineBarFast = newComponents.adrenalineBarFast.GetComponent<GuiBar>();
            Hud.instance.m_adrenalineBarSlow = newComponents.adrenalineBarSlow.GetComponent<GuiBar>();
            Hud.instance.m_adrenalineText = newComponents.adrenalineText.GetComponent<TMPro.TMP_Text>();
        }
    }
}
