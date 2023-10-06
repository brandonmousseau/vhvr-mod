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
     * Eitr Panel Hierarchy:
        eitrpanel (UnityEngine.RectTransform;UnityEngine.Animator;)
           |Stamina (UnityEngine.RectTransform;) # As of the pre-release branch this is miss-named
           |   |darken (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
           |   |border (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
           |   |bkg (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
           |   |eitr_slow (UnityEngine.RectTransform;GuiBar;)
           |   |   |bar (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
           |   |eitr_fast (UnityEngine.RectTransform;GuiBar;)
           |   |   |bar (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
           |   |EitrText (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Text;UnityEngine.UI.Outline;)
     */
    public class EitrPanelElement : IVRHudElement
    {
        public string Placement => VHVRConfig.EitrPanelPlacement();
        public HudOrientation Orientation => HudOrientation.Horizontal;

        // Data class to store references to important eitr bar components
        private class EitrPanelComponents : IVRPanelComponent
        {
            public GameObject Root => eitrBarRoot;

            public GameObject eitrBarRoot; // public RectTransform m_eitrBarRoot; "eitrpanel" ROOT
            public GameObject eitrBarFast; // public GuiBar m_eitrBarFast; "eitr_fast"
            public GameObject eitrBarSlow; // public GuiBar m_eitrBarSlow; "eitr_slow"
            public GameObject eitrText; // public Text m_eitrText; "EitrText"
            public Animator eitrAnimator; // public Animator m_eitrAnimator; component of eitrBarRoot

            public void Clear()
            {
                eitrBarRoot = null;
                eitrBarFast = null;
                eitrBarSlow = null;
                eitrText = null;
                eitrAnimator = null;
            }
        }

        private EitrPanelComponents _original = new EitrPanelComponents();
        public IVRPanelComponent Original => _original;

        private EitrPanelComponents _clone = new EitrPanelComponents();
        public IVRPanelComponent Clone => _clone;

        public void Reset()
        {
            //Destroy clone
            GameObject.Destroy(_clone.Root);
            _clone.Clear();

            //Restore Hud to original
            if (_original.eitrBarRoot && !_original.eitrBarRoot.activeSelf)
            {
                _original.eitrBarRoot.SetActive(true);
                updateEitrPanelHudReferences(_original);
                _original.Clear();
            }
        }

        public void Update()
        {
            maybeCloneEitrPanelComponents();
            if (_original.eitrBarRoot)
            {
                _original.eitrBarRoot.SetActive(false);
            }
            updateEitrPanelHudReferences(_clone);
        }

        private void maybeCloneEitrPanelComponents()
        {
            if (_clone.eitrBarRoot)
            {
                // Already cloned
                return;
            }
            if (Hud.instance == null)
            {
                return;
            }
            if (Hud.instance.m_eitrBarRoot == null)
            {
                return;
            }
            cacheEitrPanelComponents(Hud.instance.m_eitrBarRoot.gameObject, _original);
            GameObject eitrPanelClone = GameObject.Instantiate(Hud.instance.m_eitrBarRoot.gameObject);
            cacheEitrPanelComponents(eitrPanelClone, _clone);

            var cloneTransform = _clone.Root.GetComponent<RectTransform>();
            cloneTransform.localPosition = Vector3.zero;
            cloneTransform.localRotation = Quaternion.identity;
        }

        private void cacheEitrPanelComponents(GameObject root, EitrPanelComponents cache)
        {
            if (!root)
            {
                LogError("Invalid root object while caching EitrPanel");
            }
            cache.eitrBarRoot = root;
            //TODO: This is named incorrectly in the current release, but will probably change in the future
            var baseComponent = root.transform.Find("Stamina") ?? root.transform.Find("Eitr"); 
            cache.eitrBarSlow = baseComponent.Find("eitr_slow").gameObject;
            cache.eitrBarFast = baseComponent.Find("eitr_fast").gameObject;
            cache.eitrText = baseComponent.Find("EitrText").gameObject;
            cache.eitrAnimator = root.GetComponent<Animator>();
        }

        private void updateEitrPanelHudReferences(EitrPanelComponents newComponents)
        {
            Hud.instance.m_eitrBarRoot = newComponents.eitrBarRoot.GetComponent<RectTransform>();
            Hud.instance.m_eitrAnimator = newComponents.eitrAnimator;
            Hud.instance.m_eitrBarFast = newComponents.eitrBarFast.GetComponent<GuiBar>();
            Hud.instance.m_eitrBarSlow = newComponents.eitrBarSlow.GetComponent<GuiBar>();
            Hud.instance.m_eitrText = newComponents.eitrText.GetComponent<TMPro.TMP_Text>();
        }
    }
}
