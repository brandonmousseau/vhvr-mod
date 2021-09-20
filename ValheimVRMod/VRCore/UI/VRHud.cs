using UnityEngine;
using UnityEngine.UI;
using ValheimVRMod.Utilities;

using static ValheimVRMod.Utilities.LogUtils;

/**
 * Adds a more custom-VR HUD by moving UI elements to world space canvases, e.g. Health Bar that is either
 * camera locked or attached to a player wrist.
 */
namespace ValheimVRMod.VRCore.UI
{

    class VRHud
    {
        public const string LEFT_WRIST = "LeftWrist";
        public const string RIGHT_WRIST = "RightWrist";
        public const string CAMERA_LOCKED = "CameraLocked";
        
        private const float FULL_ALPHA_ANGLE = 5f;
        private const float ZERO_ALPHA_ANGLE = 90f;

        private float debugXPos = -0.0009f;
        private float debugYPos = 0.0005f;
        private float debugZPos = -0.0005f;
        private float debugXRot = 0f;
        private float debugYRot = 0f;
        private float debugZRot = -100f;

        public static VRHud instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new VRHud();
                }
                return _instance;
            }
        }

        // Data class to encapsulate all the references needed to be maintained for Health panel
        private class HealthPanelComponents
        {
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
            public GameObject[] foodTimes; // Image Array
            public GameObject foodIcon; // Image
            public GameObject foodText; // Text

            public void clear()
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

        // Data class to store references to important stamina bar componentts
        private class StaminaPanelComponents
        {
            public GameObject staminaBarRoot; // public RectTransform m_staminaBar2Root; "staminapanel" ROOT
            public GameObject staminaBarFast; // public GuiBar m_staminaBar2Fast; "stamina_fast"
            public GameObject staminaBarSlow; // public GuiBar m_staminaBar2Slow; "stamina_slow"
            public GameObject staminaText; // public GuiBar m_staminaBar2Slow; "stamina_slow"
            public Animator staminaAnimator; // public Animator m_staminaAnimator; component of staminaBarRoot

            public void clear()
            {
                staminaBarRoot = null;
                staminaBarFast = null;
                staminaBarSlow = null;
                staminaText = null;
                staminaAnimator = null;
            }
        }

        private static VRHud _instance = null;

        private Camera hudCamera;

        // Camera Canvas
        private Canvas cameraHudCanvas;
        private CanvasGroup cameraHudCanvasGroup;
        private GameObject cameraHudCanvasParent;
        
        // Left Wrist Canvas
        private Canvas leftHudCanvas;
        private CanvasGroup leftHudCanvasGroup;
        private GameObject leftHudCanvasParent;

        // Right Wrist Canvas
        private Canvas rightHudCanvas;
        private CanvasGroup rightHudCanvasGroup;
        private GameObject rightHudCanvasParent;

        // References to all the relevant UI components
        private HealthPanelComponents healthPanelComponents;
        private HealthPanelComponents originalHealthPanelComponents;
        private StaminaPanelComponents staminaPanelComponents;
        private StaminaPanelComponents originalStaminaPanelComponents;

        private VRHud() {
            healthPanelComponents = new HealthPanelComponents();
            originalHealthPanelComponents = new HealthPanelComponents();
            staminaPanelComponents = new StaminaPanelComponents();
            originalStaminaPanelComponents = new StaminaPanelComponents();
        }

        public void Update()
        {
            if (!VRPlayer.attachedToPlayer || VHVRConfig.UseLegacyHud() || !VHVRConfig.UseVrControls())
            {
                revertToLegacyHud();
                return;
            }
            if (!ensureHudCanvas())
            {
                revertToLegacyHud();
                LogError("Hud Canvas not created!");
                return;
            }
            if (!ensureHudCamera())
            {
                revertToLegacyHud();
                LogError("Problem getting HUD camera.");
                return;
            }
            maybeCloneHealthPanelComponents();
            if (originalHealthPanelComponents.healthPanel)
            {
                originalHealthPanelComponents.healthPanel.SetActive(false);
            }
            updateHealthPanelHudReferences(healthPanelComponents);
            maybeCloneStaminaPanelComponents();
            if (originalStaminaPanelComponents.staminaBarRoot)
            {
                originalStaminaPanelComponents.staminaBarRoot.SetActive(false);
            }
            updateStaminaPanelHudReferences(staminaPanelComponents);
            // Set the cloned panel as active only when attached to player
            if (leftHudCanvasParent)
            {
                leftHudCanvasParent.SetActive(VRPlayer.attachedToPlayer);
            }
            updateHudPositionAndScale();
        }

        private void revertToLegacyHud()
        {
            if (leftHudCanvasParent)
            {
                leftHudCanvasParent.SetActive(false);
                GameObject.Destroy(healthPanelComponents.healthPanel);
                GameObject.Destroy(staminaPanelComponents.staminaBarRoot);
                cameraHudCanvas = null;
                cameraHudCanvasGroup = null;
                GameObject.Destroy(cameraHudCanvasParent);
                cameraHudCanvasParent = null;
                leftHudCanvas = null;
                leftHudCanvasGroup = null;
                GameObject.Destroy(leftHudCanvasParent);
                leftHudCanvasParent = null;
                rightHudCanvas = null;
                rightHudCanvasGroup = null;
                GameObject.Destroy(rightHudCanvasParent);
                rightHudCanvasParent = null;
                hudCamera = null;
                healthPanelComponents.clear();
                staminaPanelComponents.clear();
            }
            if (originalHealthPanelComponents.healthPanel && !originalHealthPanelComponents.healthPanel.activeSelf)
            {
                originalHealthPanelComponents.healthPanel.SetActive(true);
                updateHealthPanelHudReferences(originalHealthPanelComponents);
                originalHealthPanelComponents.clear();
            }
            if (originalStaminaPanelComponents.staminaBarRoot && !originalStaminaPanelComponents.staminaBarRoot.activeSelf)
            {
                originalStaminaPanelComponents.staminaBarRoot.SetActive(true);
                updateStaminaPanelHudReferences(originalStaminaPanelComponents);
                originalStaminaPanelComponents.clear();
            }
        }

        private void updateHudPositionAndScale()
        {
            if (leftHudCanvasParent == null || !VRPlayer.attachedToPlayer || hudCamera == null)
            {
                return;
            }
            
            placePanelToHud(VHVRConfig.HealthPanelPlacement(), healthPanelComponents.healthPanel.transform);
            placePanelToHud(VHVRConfig.StaminaPanelPlacement(), staminaPanelComponents.staminaBarRoot.transform);

            setCameraHudPosition();
            
            var vrik = VRPlayer.vrikRef;
            
            if (vrik == null)
            {
                LogError("handBone is null while setting vr hud panel positions");
                return;
            }

            setWristPosition(leftHudCanvasParent, leftHudCanvas, vrik.references.leftHand, VHVRConfig.LeftWristPos(), VHVRConfig.LeftWristRot());
            leftHudCanvasGroup.alpha = VHVRConfig.AllowHudFade() && ! SettingCallback.configRunning ? calculateHudCanvasAlpha(leftHudCanvasParent) : 1f;

            setWristPosition(rightHudCanvasParent, rightHudCanvas, vrik.references.rightHand, VHVRConfig.RightWristPos(), VHVRConfig.RightWristRot());
            rightHudCanvasGroup.alpha = VHVRConfig.AllowHudFade() && ! SettingCallback.configRunning ? calculateHudCanvasAlpha(rightHudCanvasParent) : 1f;

            // TODO @artum: Replace this function with horizontal and vertical layout ?
            // aparently stamina is rotated 90 degree if its on same panel as health bar so we need to find a way to handle this with layout 
            updateStaminaPanelLocalPosition(VHVRConfig.HealthPanelPlacement(), VHVRConfig.StaminaPanelPlacement());
        }

        private void placePanelToHud(string placement, Transform panelTransform)
        {

            switch (placement) {
                
                case LEFT_WRIST:
                    panelTransform.SetParent(leftHudCanvas.GetComponent<RectTransform>(), false);
                    break;
                
                case RIGHT_WRIST:
                    panelTransform.SetParent(rightHudCanvas.GetComponent<RectTransform>(), false);
                    break;
                
                case CAMERA_LOCKED:
                    panelTransform.SetParent(cameraHudCanvas.GetComponent<RectTransform>(), false);
                    break;
            }
        }

        private void updateStaminaPanelLocalPosition(string healthPanelPosition, string staminaPanelPosition)
        {
            if (healthPanelPosition.Equals(staminaPanelPosition))
            {
                Vector3 healthPanelLocation = healthPanelComponents.healthPanel.GetComponent<RectTransform>().localPosition;
                // Need to make sure stamina and healthbar are positioned on same canvas correctly
                float healthPanelWidth = healthPanelComponents.healthPanel.GetComponent<RectTransform>().sizeDelta.x;
                float staminaPanelWidth = staminaPanelComponents.staminaBarRoot.GetComponent<RectTransform>().sizeDelta.x;
                float staminaPanelHeight = staminaPanelComponents.staminaBarRoot.GetComponent<RectTransform>().sizeDelta.y;
                staminaPanelComponents.staminaBarRoot.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, 90f);
                staminaPanelComponents.staminaBarRoot.GetComponent<RectTransform>().localPosition =
                    new Vector3(healthPanelLocation.x + 0.5f * healthPanelWidth + 0.5f * staminaPanelHeight, healthPanelLocation.y + 0.5f * staminaPanelWidth, healthPanelLocation.z);
            }
            else
            {
                staminaPanelComponents.staminaBarRoot.GetComponent<RectTransform>().localRotation = Quaternion.identity;
                staminaPanelComponents.staminaBarRoot.GetComponent<RectTransform>().localPosition = Vector3.zero;
            }
        }

        private void setCameraHudPosition() {
            
            float canvasWidth = cameraHudCanvas.GetComponent<RectTransform>().rect.width;
            float scaleFactor = 0.1f / canvasWidth * VHVRConfig.CameraHudScale();
            cameraHudCanvasParent.transform.SetParent(hudCamera.gameObject.transform, false);
            cameraHudCanvasParent.transform.position = VRPlayer.instance.transform.position;
            float hudDistance = 1f;
            float hudVerticalOffset = -0.5f;
            cameraHudCanvasParent.transform.localPosition = new Vector3(VHVRConfig.CameraHudX() * 1000, hudVerticalOffset + VHVRConfig.CameraHudY() * 1000, hudDistance);
            cameraHudCanvas.GetComponent<RectTransform>().localScale = Vector3.one * scaleFactor * hudDistance;
            cameraHudCanvasParent.transform.localRotation = Quaternion.Euler(Vector3.zero);
            cameraHudCanvasGroup.alpha = 1f;
            
        }

        private void setWristPosition(GameObject hCanvasParent, Canvas hCanvas, Transform hand, Vector3 pos, Quaternion rot)
        {
            var hudCanvasRect = hCanvas.GetComponent<RectTransform>();
            hudCanvasRect.localScale = Vector3.one * 0.001f /  hudCanvasRect.rect.width;
            
            hCanvasParent.transform.SetParent(hand, false);
            hCanvasParent.transform.localPosition = pos;
            hCanvasParent.transform.localRotation = rot;
        }

        private float calculateHudCanvasAlpha(GameObject hCanvasParent)
        {
            var deltaAngle = Quaternion.Angle(hudCamera.transform.rotation, hCanvasParent.transform.rotation);
            if (deltaAngle <= FULL_ALPHA_ANGLE)
            {
                return 1f;
            } else if (deltaAngle > FULL_ALPHA_ANGLE && deltaAngle <= ZERO_ALPHA_ANGLE)
            {
                return 18f / 17f - deltaAngle / 85f;
            } else
            {
                return 0f;
            }
        }

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
        private void maybeCloneHealthPanelComponents()
        {
            if (healthPanelComponents.healthPanel)
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
            cacheHealthPanelComponents(Hud.instance.m_healthPanel.gameObject, originalHealthPanelComponents);
            GameObject healthPanelClone = GameObject.Instantiate(Hud.instance.m_healthPanel.gameObject);
            cacheHealthPanelComponents(healthPanelClone, healthPanelComponents);
        }


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
        private void maybeCloneStaminaPanelComponents()
        {
            if (staminaPanelComponents.staminaBarRoot)
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
            cacheStaminaPanelComponents(Hud.instance.m_staminaBar2Root.gameObject, originalStaminaPanelComponents);
            GameObject staminaPanelClone = GameObject.Instantiate(Hud.instance.m_staminaBar2Root.gameObject);
            staminaPanelClone.transform.SetParent(leftHudCanvas.GetComponent<RectTransform>(), false); // TODO: Move this maybe?
            cacheStaminaPanelComponents(staminaPanelClone, staminaPanelComponents);
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

        private void updateHealthPanelHudReferences(HealthPanelComponents newComponents)
        {
            if (newComponents == null || !newComponents.healthPanel || Hud.instance == null || Hud.instance.m_healthPanel.gameObject == newComponents.healthPanel)
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
            Hud.instance.m_healthText = newComponents.healthText.GetComponent<Text>();
            // Food
            Hud.instance.m_foodBarRoot = newComponents.foodBarRoot.GetComponent<RectTransform>();
            Hud.instance.m_foodBaseBar = newComponents.foodBaseBar.GetComponent<RectTransform>();
            Hud.instance.m_foodIcon = newComponents.foodIcon.GetComponent<Image>();
            Hud.instance.m_foodText = newComponents.foodText.GetComponent<Text>();
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
            Text[] foodTimes = new Text[newComponents.foodTimes.Length];
            for (int i = 0; i < foodTimes.Length; i++)
            {
                foodTimes[i] = newComponents.foodTimes[i].GetComponent<Text>();
            }
            Hud.instance.m_foodTime = foodTimes;
        }

        private void updateStaminaPanelHudReferences(StaminaPanelComponents newComponents)
        {
            Hud.instance.m_staminaBar2Root = newComponents.staminaBarRoot.GetComponent<RectTransform>();
            Hud.instance.m_staminaAnimator = newComponents.staminaAnimator;
            Hud.instance.m_staminaBar2Fast = newComponents.staminaBarFast.GetComponent<GuiBar>();
            Hud.instance.m_staminaBar2Slow = newComponents.staminaBarSlow.GetComponent<GuiBar>();
            Hud.instance.m_staminaText = newComponents.staminaText.GetComponent<Text>();
        }

        private bool ensureHudCanvas()
        {
            if (leftHudCanvas != null && leftHudCanvasParent && rightHudCanvas != null && rightHudCanvasParent)
            {
                return true;
            }
            if (!cameraHudCanvasParent)
            {
                cameraHudCanvasParent = new GameObject("CameraHudCanvasParent");
                cameraHudCanvasParent.layer = LayerUtils.getWorldspaceUiLayer();
                GameObject.DontDestroyOnLoad(cameraHudCanvasParent);
            }
            if (!leftHudCanvasParent)
            {
                leftHudCanvasParent = new GameObject("LeftHudCanvasParent");
                leftHudCanvasParent.layer = LayerUtils.getWorldspaceUiLayer();
                GameObject.DontDestroyOnLoad(leftHudCanvasParent);
            }
            if (!rightHudCanvasParent)
            {
                rightHudCanvasParent = new GameObject("RightHudCanvasParent");
                rightHudCanvasParent.layer = LayerUtils.getWorldspaceUiLayer();
                GameObject.DontDestroyOnLoad(rightHudCanvasParent);
            }
            if (!ensureHudCamera())
            {
                LogError("Error getting HUD Camera.");
                return false;
            }
            cameraHudCanvas = cameraHudCanvasParent.AddComponent<Canvas>();
            cameraHudCanvasGroup = cameraHudCanvasParent.AddComponent<CanvasGroup>();
            cameraHudCanvasGroup.interactable = false;
            cameraHudCanvasGroup.blocksRaycasts = false;
            cameraHudCanvasGroup.alpha = 1f;
            cameraHudCanvas.renderMode = RenderMode.WorldSpace;
            cameraHudCanvas.transform.position = cameraHudCanvasParent.transform.position;
            cameraHudCanvas.transform.rotation = cameraHudCanvasParent.transform.rotation;
            cameraHudCanvas.transform.localPosition = Vector3.zero;
            cameraHudCanvas.transform.localRotation = Quaternion.identity;
            cameraHudCanvas.worldCamera = hudCamera;
            
            leftHudCanvas = leftHudCanvasParent.AddComponent<Canvas>();
            leftHudCanvasGroup = leftHudCanvasParent.AddComponent<CanvasGroup>();
            leftHudCanvasGroup.interactable = false;
            leftHudCanvasGroup.blocksRaycasts = false;
            leftHudCanvasGroup.alpha = 1f;
            leftHudCanvas.renderMode = RenderMode.WorldSpace;
            leftHudCanvas.transform.position = leftHudCanvasParent.transform.position;
            leftHudCanvas.transform.rotation = leftHudCanvasParent.transform.rotation;
            leftHudCanvas.transform.localPosition = Vector3.zero;
            leftHudCanvas.transform.localRotation = Quaternion.identity;
            leftHudCanvas.worldCamera = hudCamera;
            rightHudCanvas = rightHudCanvasParent.AddComponent<Canvas>();
            rightHudCanvasGroup = rightHudCanvasParent.AddComponent<CanvasGroup>();
            rightHudCanvasGroup.interactable = false;
            rightHudCanvasGroup.blocksRaycasts = false;
            rightHudCanvasGroup.alpha = 1f;
            rightHudCanvas.renderMode = RenderMode.WorldSpace;
            rightHudCanvas.transform.position = rightHudCanvasParent.transform.position;
            rightHudCanvas.transform.rotation = rightHudCanvasParent.transform.rotation;
            rightHudCanvas.transform.localPosition = Vector3.zero;
            rightHudCanvas.transform.localRotation = Quaternion.identity;
            rightHudCanvas.worldCamera = hudCamera;
            return true;
        }

        private bool ensureHudCamera()
        {
            if (hudCamera == null)
            {
                hudCamera = CameraUtils.getWorldspaceUiCamera();
            }
            return hudCamera != null;
        }

        // Helper debug function to manually re-position during gameplay.
        private void updateWristDebugPositioning()
        {
            // Position
            if (!Input.GetKey(KeyCode.Keypad0) && Input.GetKeyDown(KeyCode.Keypad7))
            {
                debugXPos += 0.0001f;
            }
            if (!Input.GetKey(KeyCode.Keypad0) && Input.GetKeyDown(KeyCode.Keypad1))
            {
                debugXPos -= 0.0001f;
            }
            if (!Input.GetKey(KeyCode.Keypad0) && Input.GetKeyDown(KeyCode.Keypad8))
            {
                debugYPos += 0.0001f;
            }
            if (!Input.GetKey(KeyCode.Keypad0) && Input.GetKeyDown(KeyCode.Keypad2))
            {
                debugYPos -= 0.0001f;
            }
            if (!Input.GetKey(KeyCode.Keypad0) && Input.GetKeyDown(KeyCode.Keypad9))
            {
                debugZPos += 0.0001f;
            }
            if (!Input.GetKey(KeyCode.Keypad0) && Input.GetKeyDown(KeyCode.Keypad3))
            {
                debugZPos -= 0.0001f;
            }
            // Rotation
            if (Input.GetKey(KeyCode.Keypad0) && Input.GetKeyDown(KeyCode.Keypad7))
            {
                debugXRot += 5f;
            }
            if (Input.GetKey(KeyCode.Keypad0) && Input.GetKeyDown(KeyCode.Keypad1))
            {
                debugXRot -= 5f;
            }
            if (Input.GetKey(KeyCode.Keypad0) && Input.GetKeyDown(KeyCode.Keypad8))
            {
                debugYRot += 5f;
            }
            if (Input.GetKey(KeyCode.Keypad0) && Input.GetKeyDown(KeyCode.Keypad2))
            {
                debugYRot -= 5f;
            }
            if (Input.GetKey(KeyCode.Keypad0) && Input.GetKeyDown(KeyCode.Keypad9))
            {
                debugZRot += 5f;
            }
            if (Input.GetKey(KeyCode.Keypad0) && Input.GetKeyDown(KeyCode.Keypad3))
            {
                debugZRot -= 5f;
            }
            LogDebug("Local Position = ( " + debugXPos + ", " + debugYPos + ", " + debugZPos + " ) Local Rotation = ( " + debugXRot + ", " + debugYRot + ", " + debugZRot + " )");
        }

    }
}
