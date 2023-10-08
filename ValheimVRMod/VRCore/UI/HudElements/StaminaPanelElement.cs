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
     * Stamina Panel Hierarchy:
        staminapanel (UnityEngine.RectTransform;UnityEngine.Animator;)
           |Stamina (UnityEngine.RectTransform;)
           |   |darken (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
           |   |border (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
           |   |bkg (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
           |   |stamina_slow (UnityEngine.RectTransform;GuiBar;)
           |   |   |bar (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
           |   |stamina_fast (UnityEngine.RectTransform;GuiBar;)
           |   |   |bar (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
           |   |StaminaText (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Text;UnityEngine.UI.Outline;)
     */
    public class StaminaPanelElement : IVRHudElement
    {
        public string Placement => VHVRConfig.StaminaPanelPlacement();
        public HudOrientation Orientation => HudOrientation.Horizontal;

        // Data class to store references to important stamina bar components
        private class StaminaPanelComponents : IVRPanelComponent
        {
            public GameObject Root => staminaBarRoot;

            public GameObject staminaBarRoot; // public RectTransform m_staminaBar2Root; "staminapanel" ROOT
            public GameObject staminaBarFast; // public GuiBar m_staminaBar2Fast; "stamina_fast"
            public GameObject staminaBarSlow; // public GuiBar m_staminaBar2Slow; "stamina_slow"
            public GameObject staminaText; // public Text m_staminaText; "StaminaText"
            public Animator staminaAnimator; // public Animator m_staminaAnimator; component of staminaBarRoot

            public void Clear()
            {
                staminaBarRoot = null;
                staminaBarFast = null;
                staminaBarSlow = null;
                staminaText = null;
                staminaAnimator = null;
            }
        }

        private StaminaPanelComponents _original = new StaminaPanelComponents();
        public IVRPanelComponent Original => _original;

        private StaminaPanelComponents _clone = new StaminaPanelComponents();
        public IVRPanelComponent Clone => _clone;

        public void Reset()
        {
            //Destroy clone
            GameObject.Destroy(_clone.Root);
            _clone.Clear();

            //Restore Hud to original
            if (_original.staminaBarRoot && !_original.staminaBarRoot.activeSelf)
            {
                _original.staminaBarRoot.SetActive(true);
                updateStaminaPanelHudReferences(_original);
                _original.Clear();
            }
        }

        public void Update()
        {
            maybeCloneStaminaPanelComponents();
            if (_original.staminaBarRoot)
            {
                _original.staminaBarRoot.SetActive(false);
            }
            updateStaminaPanelHudReferences(_clone);
        }

        private void maybeCloneStaminaPanelComponents()
        {
            if (_clone.staminaBarRoot)
            {
                // Already cloned
                return;
            }
            if (Hud.instance == null)
            {
                return;
            }
            if (Hud.instance.m_staminaBar2Root == null)
            {
                return;
            }
            cacheStaminaPanelComponents(Hud.instance.m_staminaBar2Root.gameObject, _original);
            GameObject staminaPanelClone = GameObject.Instantiate(Hud.instance.m_staminaBar2Root.gameObject);
            cacheStaminaPanelComponents(staminaPanelClone, _clone);

            var cloneTransform = _clone.Root.GetComponent<RectTransform>();
            cloneTransform.localPosition = Vector3.zero;
            cloneTransform.localRotation = Quaternion.identity;
        }

        private void cacheStaminaPanelComponents(GameObject root, StaminaPanelComponents cache)
        {
            if (!root)
            {
                LogError("Invalid root object while caching StaminaPanel");
            }
            cache.staminaBarRoot = root;
            cache.staminaBarSlow = root.transform.Find("Stamina").Find("stamina_slow").gameObject;
            cache.staminaBarFast = root.transform.Find("Stamina").Find("stamina_fast").gameObject;
            cache.staminaText = root.transform.Find("Stamina").Find("StaminaText").gameObject;
            cache.staminaAnimator = root.GetComponent<Animator>();
        }

        private void updateStaminaPanelHudReferences(StaminaPanelComponents newComponents)
        {
            Hud.instance.m_staminaBar2Root = newComponents.staminaBarRoot.GetComponent<RectTransform>();
            Hud.instance.m_staminaAnimator = newComponents.staminaAnimator;
            Hud.instance.m_staminaBar2Fast = newComponents.staminaBarFast.GetComponent<GuiBar>();
            Hud.instance.m_staminaBar2Slow = newComponents.staminaBarSlow.GetComponent<GuiBar>();
            Hud.instance.m_staminaText = newComponents.staminaText.GetComponent<TMPro.TMP_Text>();
        }
    }
}
