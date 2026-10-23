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
     * Action Progress Panel Hierarchy:
       hudroot
        | action_progress
            | darken
            | bkg
            | bar
            | Text
     */
    public class ActionProgressPanelElement : IVRHudElement
    {
        public string Placement => VHVRConfig.ActionProgressPanelPlacement();
        public HudOrientation Orientation => HudOrientation.Horizontal;

        // Data class to store references to important action progress bar components
        private class ActionProgressPanelComponents : IVRPanelComponent
        {
            public GameObject Root => actionBarRoot;

            public GameObject actionBarRoot; // public RectTransform m_actionBarRoot; "hudroot" ROOT
            public GameObject actionName; // public Text m_actionName; "Text"

            public void Clear()
            {
                actionBarRoot = null;
                actionName = null;
            }
        }

        private ActionProgressPanelComponents _original = new ActionProgressPanelComponents();
        public IVRPanelComponent Original => _original;

        private ActionProgressPanelComponents _clone = new ActionProgressPanelComponents();
        public IVRPanelComponent Clone => _clone;

        public void Reset()
        {
            //Destroy clone
            GameObject.Destroy(_clone.Root);
            _clone.Clear();

            //Restore Hud to original
            if (_original.actionBarRoot && !_original.actionBarRoot.activeSelf)
            {
                _original.actionBarRoot.SetActive(true);
                updateActionProgressPanelHudReferences(_original);
                _original.Clear();
            }
        }

        public void Update()
        {
            maybeCloneActionProgressPanelComponents();
            if (_original.actionBarRoot)
            {
                _original.actionBarRoot.SetActive(false);
            }
            updateActionProgressPanelHudReferences(_clone);
        }

        private void maybeCloneActionProgressPanelComponents()
        {
            if (_clone.actionBarRoot)
            {
                // Already cloned
                return;
            }
            if (Hud.instance == null)
            {
                return;
            }
            if (Hud.instance.m_actionBarRoot == null)
            {
                return;
            }
            cacheActionProgressPanelComponents(Hud.instance.m_actionBarRoot.gameObject, _original);
            GameObject actionProgressPanelClone = GameObject.Instantiate(Hud.instance.m_actionBarRoot.gameObject);
            cacheActionProgressPanelComponents(actionProgressPanelClone, _clone);

            var cloneTransform = _clone.Root.GetComponent<RectTransform>();
            cloneTransform.localPosition = Vector3.zero;
            cloneTransform.localRotation = Quaternion.identity;
        }

        private void cacheActionProgressPanelComponents(GameObject root, ActionProgressPanelComponents cache)
        {
            if (!root)
            {
                LogError("Invalid root object while caching Action Progress Panel");
            }
            cache.actionBarRoot = root;
            cache.actionName = root.transform.Find("Text").gameObject;
        }

        private void updateActionProgressPanelHudReferences(ActionProgressPanelComponents newComponents)
        {
            Hud.instance.m_actionBarRoot = newComponents.actionBarRoot.GetComponent<GameObject>();
            Hud.instance.m_actionProgress = newComponents.actionBarRoot.GetComponent<GuiBar>();
            Hud.instance.m_actionName = newComponents.actionName.GetComponent<TMPro.TMP_Text>();
        }
    }
}
