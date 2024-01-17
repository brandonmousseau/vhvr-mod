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
     Health Panel Hierarchy:
     healthpanel(Clone) (UnityEngine.RectTransform;UnityEngine.Animator;)
        |darken (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
        |Health (UnityEngine.RectTransform;)
        |   |border (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
        |   |bkg (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
        |   |slow (UnityEngine.RectTransform;GuiBar;)
        |   |   |bar (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
        |   |fast (UnityEngine.RectTransform;GuiBar;)
        |   |   |bar (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
        |   |   |   |HealthText (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Text;UnityEngine.UI.Outline;)
        |healthicon (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
        |Food (UnityEngine.RectTransform;)
        |   |border (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
        |   |bar1 (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
        |   |bar2 (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
        |   |bar3 (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
        |   |baseBar (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
        |   |overlay (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
        |FoodText (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Text;UnityEngine.UI.Outline;)
        |foodicon (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
        |foodicon (1) (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
        |food2 (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
        |   |foodicon2 (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
        |   |time (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Text;UnityEngine.UI.Outline;)
        |food1 (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
        |   |foodicon1 (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
        |   |time (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Text;UnityEngine.UI.Outline;)
        |food0 (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
        |   |foodicon0 (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Image;)
        |   |time (UnityEngine.RectTransform;UnityEngine.CanvasRenderer;UnityEngine.UI.Text;UnityEngine.UI.Outline;)
     */
    public class HealthPanelElement : IVRHudElement
    {
        public string Placement => VHVRConfig.HealthPanelPlacement();
        public HudOrientation Orientation => HudOrientation.Veritcal;

        public IVRPanelComponent Original => _original;
        private HealthPanelComponents _original = new HealthPanelComponents();

        public IVRPanelComponent Clone => _clone;
        private HealthPanelComponents _clone = new HealthPanelComponents();

        // Data class to encapsulate all the references needed to be maintained for Health panel
        private class HealthPanelComponents : IVRPanelComponent
        {
            public GameObject Root => healthPanel;

            // Root Component
            public GameObject healthPanel; // RectTransform ROOT
            public Animator healthPanelAnimator;
            // HealthBar
            public GameObject healthbarRoot; // RectTransform
            public GameObject healthBarFast; // GuiBar
            public GameObject healthBarSlow; // GuiBar
            public GameObject healthText; // Text
            // Food
            public GameObject foodBarRoot; // RectTransform
            public GameObject foodBaseBar; // RectTransform
            public GameObject[] foodBars; // Image Array
            public GameObject[] foodIcons; // Image Array
            public GameObject[] foodTimes; // Text Array
            public GameObject foodIcon; // Image
            public GameObject foodText; // Text

            public void Clear()
            {
                healthPanel = null;
                healthPanelAnimator = null;
                healthbarRoot = null;
                healthBarFast = null;
                healthBarSlow = null;
                healthText = null;
                foodBarRoot = null;
                foodBaseBar = null;
                foodBars = null;
                foodIcons = null;
                foodIcon = null;
                foodTimes = null;
                foodText = null;
            }

        }

        public void Update()
        {
            maybeCloneHealthPanelComponents();
            if (_original.healthPanel)
            {
                _original.healthPanel.SetActive(false);
            }
            updateHealthPanelHudReferences(_clone);
        }

        public void Reset()
        {
            //Destroy clone
            GameObject.Destroy(_clone.Root);
            _clone.Clear();

            //Restore Hud to original
            if (_original.healthPanel && !_original.healthPanel.activeSelf)
            {
                _original.healthPanel.SetActive(true);
                updateHealthPanelHudReferences(_original);
                _original.Clear();
            }
        }

        private void maybeCloneHealthPanelComponents()
        {
            if (_clone.healthPanel)
            {
                // Already cloned
                return;
            }
            if (Hud.instance == null)
            {
                return;
            }
            if (Hud.instance.m_healthPanel == null)
            {
                // Early exit since health panel doesn't exist yet
                return;
            }
            cacheHealthPanelComponents(Hud.instance.m_healthPanel.gameObject, _original);
            GameObject healthPanelClone = GameObject.Instantiate(Hud.instance.m_healthPanel.gameObject);
            cacheHealthPanelComponents(healthPanelClone, _clone);

            //Declare sizes for the auto-layout
            var layoutElement = healthPanelClone.AddComponent<LayoutElement>();
            var panelTransform = healthPanelClone.GetComponent<RectTransform>();
            panelTransform.localPosition = Vector3.zero;
            panelTransform.localRotation = Quaternion.identity;
            layoutElement.preferredWidth = panelTransform.sizeDelta.x;
            layoutElement.minWidth = panelTransform.sizeDelta.x;
            layoutElement.preferredHeight = panelTransform.sizeDelta.y;
            layoutElement.minHeight = panelTransform.sizeDelta.y;
        }

        // Cache references to all the important game objects in the health panel
        private void cacheHealthPanelComponents(GameObject root, HealthPanelComponents cache)
        {
            cache.healthPanel = root;
            cache.healthPanelAnimator = root.GetComponent<Animator>();
            // HealthBar
            cache.healthbarRoot = cache.healthPanel.transform.Find("Health").gameObject;
            cache.healthBarFast = cache.healthbarRoot.transform.Find("fast").gameObject;
            cache.healthBarSlow = cache.healthbarRoot.transform.Find("slow").gameObject;
            cache.healthText = cache.healthBarFast.transform.Find("bar").Find("HealthText").gameObject;
            // Food
            cache.foodBarRoot = cache.healthPanel.transform.Find("Food").gameObject;
            cache.foodBaseBar = cache.foodBarRoot.transform.Find("baseBar").gameObject;
            int foodBarsLength = Hud.instance.m_foodBars.Length;
            cache.foodBars = new GameObject[foodBarsLength];
            for (int i = 0; i < foodBarsLength; i++)
            {
                string foodBarName = "bar" + (i + 1); // bar1...barN+1
                cache.foodBars[i] = cache.foodBarRoot.transform.Find(foodBarName).gameObject;
            }
            int foodIconLength = Hud.instance.m_foodIcons.Length;
            cache.foodIcons = new GameObject[foodIconLength];
            cache.foodTimes = new GameObject[foodIconLength];
            for (int i = 0; i < foodIconLength; i++)
            {
                string foodName = "food" + i; // // food0..foodN
                string foodIconName = "foodicon" + i; // foodicon0..foodiconN
                cache.foodIcons[i] = cache.healthPanel.transform.Find(foodName).Find(foodIconName).gameObject;
                cache.foodTimes[i] = cache.healthPanel.transform.Find(foodName).Find("time").gameObject;
            }
            cache.foodIcon = cache.healthPanel.transform.Find("foodicon").gameObject;
            cache.foodText = cache.healthPanel.transform.Find("FoodText").gameObject;
        }

        private void updateHealthPanelHudReferences(HealthPanelComponents newComponents)
        {
            if (newComponents == null 
                || newComponents.healthPanel == null
                || newComponents.healthPanel.gameObject == null
                || Hud.instance == null
                || Hud.instance.m_healthPanel == null
                || Hud.instance.m_healthPanel.gameObject == newComponents.healthPanel)
            {
                return;
            }
            // HealthBar
            Hud.instance.m_healthPanel = newComponents.healthPanel.GetComponent<RectTransform>();
            Hud.instance.m_healthAnimator = newComponents.healthPanelAnimator;
            Hud.instance.m_healthBarRoot = newComponents.healthbarRoot.GetComponent<RectTransform>();
            Hud.instance.m_healthAnimator = newComponents.healthPanel.GetComponent<Animator>();
            Hud.instance.m_healthBarFast = newComponents.healthBarFast.GetComponent<GuiBar>();
            Hud.instance.m_healthBarSlow = newComponents.healthBarSlow.GetComponent<GuiBar>();
            Hud.instance.m_healthText = newComponents.healthText.GetComponent<TMPro.TMP_Text>();
            // Food
            Hud.instance.m_foodBarRoot = newComponents.foodBarRoot.GetComponent<RectTransform>();
            Hud.instance.m_foodBaseBar = newComponents.foodBaseBar.GetComponent<RectTransform>();
            Hud.instance.m_foodIcon = newComponents.foodIcon.GetComponent<Image>();
            Hud.instance.m_foodText = newComponents.foodText.GetComponent<TMPro.TMP_Text>();
            Image[] foodBarImages = new Image[newComponents.foodBars.Length];
            for (int i = 0; i < foodBarImages.Length; i++)
            {
                foodBarImages[i] = newComponents.foodBars[i].GetComponent<Image>();
            }
            Hud.instance.m_foodBars = foodBarImages;
            Image[] foodIconImages = new Image[newComponents.foodIcons.Length];
            for (int i = 0; i < foodIconImages.Length; i++)
            {
                foodIconImages[i] = newComponents.foodIcons[i].GetComponent<Image>();
            }
            Hud.instance.m_foodIcons = foodIconImages;
            TMPro.TMP_Text[] foodTimes = new TMPro.TMP_Text[newComponents.foodTimes.Length];
            for (int i = 0; i < foodTimes.Length; i++)
            {
                foodTimes[i] = newComponents.foodTimes[i].GetComponent<TMPro.TMP_Text>();
            }
            Hud.instance.m_foodTime = foodTimes;
        }
    }
}
