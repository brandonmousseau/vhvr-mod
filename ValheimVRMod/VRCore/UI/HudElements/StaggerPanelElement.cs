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
     * Stagger Bar Hierarchy:
        staggerpanel (UnityEngine.RectTransform;)
           |staggerbar (UnityEngine.RectTransform;UnityEngine.Animator;)
           |   |darken (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
           |   |bkg (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
           |   |bar (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
           |   |StaggerIcon (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
     */
    public class StaggerPanelElement : IVRHudElement
    {
        public string Placement => VHVRConfig.StaggerPanelPlacement();
        public HudOrientation Orientation => HudOrientation.Horizontal;

        // Data class to store references to important stagger bar components
        private class StaggerPanelComponents : IVRPanelComponent
        {
            public GameObject Root => staggerBarRoot;


            public GameObject staggerBarRoot; // "staggerpanel" ROOT
            public GameObject staggerBar; // public GuiBar m_staggerBar; "bar";
            public Animator staggerAnimator; // public Animator m_staggerAnimator; component of staggerBarRoot

            public void Clear()
            {
                staggerBarRoot = null;
                staggerBar = null;
                staggerAnimator = null;
            }
        }

        private StaggerPanelComponents _original = new StaggerPanelComponents();
        public IVRPanelComponent Original => _original;

        private StaggerPanelComponents _clone = new StaggerPanelComponents();
        public IVRPanelComponent Clone => _clone;

        public void Reset()
        {
            //Destroy clone
            GameObject.Destroy(_clone.Root);
            _clone.Clear();

            //Restore Hud to original
            if (_original.staggerBarRoot && !_original.staggerBarRoot.activeSelf)
            {
                _original.staggerBarRoot.SetActive(true);
                updateStaggerPanelHudReferences(_original);
                _original.Clear();
            }
        }

        public void Update()
        {
            maybeCloneStaggerPanelComponents();
            if (_original.staggerBarRoot)
            {
                _original.staggerBarRoot.SetActive(false);
            }
            updateStaggerPanelHudReferences(_clone);
        }

        private void maybeCloneStaggerPanelComponents()
        {
            if (_clone.staggerBarRoot)
            {
                // Already cloned
                return;
            }
            if (Hud.instance == null)
            {
                return;
            }
            if (Hud.instance.m_staggerAnimator == null)
            {
                return;
            }
            cacheStaggerPanelComponents(Hud.instance.m_staggerAnimator.gameObject, _original);
            GameObject staggerPanelClone = GameObject.Instantiate(Hud.instance.m_staggerAnimator.gameObject);
            cacheStaggerPanelComponents(staggerPanelClone, _clone);

            var cloneTransform = _clone.Root.GetComponent<RectTransform>();
            cloneTransform.localPosition = Vector3.zero;
            cloneTransform.localRotation = Quaternion.identity;
        }

        private void cacheStaggerPanelComponents(GameObject root, StaggerPanelComponents cache)
        {
            if (!root)
            {
                LogError("Invalid root object while caching StaggerPanel");
            }
            cache.staggerBarRoot = root;
            cache.staggerBar = root.transform.Find("staggerbar").gameObject;
            cache.staggerAnimator = root.GetComponent<Animator>();
        }

        private void updateStaggerPanelHudReferences(StaggerPanelComponents newComponents)
        {
            Hud.instance.m_staggerAnimator = newComponents.staggerAnimator;
            Hud.instance.m_staggerProgress = newComponents.staggerBar.GetComponent<GuiBar>();
        }
    }
}
