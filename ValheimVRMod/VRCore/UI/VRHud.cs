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
        
        private readonly Vector3 leftWristPositionHealth = new Vector3(0.0009f, -0.0005f, -0.0005f);
        private readonly Vector3 leftWristPositionStamina = new Vector3(0.0002f, 0.0003f, -0.0005f);
        private readonly Vector3 rightWristPositionHealth = new Vector3(-0.0009f, 0.0005f, -0.0005f);
        private readonly Vector3 rightWristPositionStamina = new Vector3(-0.0002f, 0.0003f, -0.0005f);
        private readonly Vector3 leftWristRotation = new Vector3(0f, 0f, 100f);
        private readonly Vector3 rightWristRotatation = new Vector3(0f, 0f, -100f);
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
                foodText = null;
            }

        }

        // Data class to store references to important stamina bar componentts
        private class StaminaPanelComponents
        {
            public GameObject staminaBarRoot; // public RectTransform m_staminaBar2Root; "staminapanel" ROOT
            public GameObject staminaBarFast; // public GuiBar m_staminaBar2Fast; "stamina_fast"
            public GameObject staminaBarSlow; // public GuiBar m_staminaBar2Slow; "stamina_slow"
            public Animator staminaAnimator; // public Animator m_staminaAnimator; component of staminaBarRoot

            public void clear()
            {
                staminaBarRoot = null;
                staminaBarFast = null;
                staminaBarSlow = null;
                staminaAnimator = null;
            }
        }

        private static VRHud _instance = null;

        private Camera hudCamera;

        // Main Canvas - health panel always goes here. Stamina maybe goes here.
        private Canvas hudCanvas;
        private CanvasGroup hudCanvasGroup;
        private GameObject hudCanvasParent;

        // Stamina goes here if user selects different HUD location for bar versus health
        private Canvas altHudCanvas;
        private CanvasGroup altHudCanvasGroup;
        private GameObject altHudCanvasParent;

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
            if (hudCanvasParent)
            {
                hudCanvasParent.SetActive(VRPlayer.attachedToPlayer);
            }
            updateHudPositionAndScale();
        }

        private void revertToLegacyHud()
        {
            if (hudCanvasParent)
            {
                hudCanvasParent.SetActive(false);
                GameObject.Destroy(healthPanelComponents.healthPanel);
                GameObject.Destroy(staminaPanelComponents.staminaBarRoot);
                hudCanvas = null;
                hudCanvasGroup = null;
                GameObject.Destroy(hudCanvasParent);
                hudCanvasParent = null;
                altHudCanvas = null;
                altHudCanvasGroup = null;
                GameObject.Destroy(altHudCanvasParent);
                altHudCanvasParent = null;
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
            if (hudCanvasParent == null || !VRPlayer.attachedToPlayer || hudCamera == null)
            {
                return;
            }
            string healthPanelPosition = VHVRConfig.HealthPanelPlacement();
            string staminaPanelPosition = VHVRConfig.StaminaPanelPlacement();
            parentHudComponents(healthPanelPosition, staminaPanelPosition);
            updateStaminaPanelLocalPosition(healthPanelPosition, staminaPanelPosition);
            if (healthPanelPosition.Equals(CAMERA_LOCKED))
            {
                setCameraLockedPositioning(hudCanvasParent, hudCanvas, hudCanvasGroup, VHVRConfig.HealthPanelCameraX(), VHVRConfig.HealthPanelCameraY(), VHVRConfig.HealthPanelScale());
            } else
            {
                Transform handBone = getHandParent(healthPanelPosition);
                if (handBone != null) {
                    setWristPosition(hudCanvasParent, hudCanvas, handBone, healthPanelPosition, leftWristPositionHealth, rightWristPositionHealth,
                        VHVRConfig.HealthPanelPos(), VHVRConfig.HealthPanelRot(), VHVRConfig.HealthPanelScale());
                    hudCanvasGroup.alpha = VHVRConfig.AllowHudFade() ? calculateHudCanvasAlpha(hudCanvasParent) : 1f;
                } else
                {
                    LogError("handBone is null while setting health panel position. Falling back to camera locked.");
                    setCameraLockedPositioning(hudCanvasParent, hudCanvas, hudCanvasGroup, VHVRConfig.HealthPanelCameraX(), VHVRConfig.HealthPanelCameraY(), VHVRConfig.HealthPanelScale());
                }
            }
            if (!healthPanelPosition.Equals(staminaPanelPosition))
            {
                // Need to position the altHudCanvas since the stamina bar is separated from health bar
                if (staminaPanelPosition.Equals(CAMERA_LOCKED))
                {
                    setCameraLockedPositioning(altHudCanvasParent, altHudCanvas, altHudCanvasGroup, VHVRConfig.StaminaPanelCameraX(), VHVRConfig.StaminaPanelCameraY(), VHVRConfig.StaminaPanelScale());
                } else
                {
                    Transform handBone = getHandParent(staminaPanelPosition);
                    if (handBone != null)
                    {
                        setWristPosition(altHudCanvasParent, altHudCanvas, handBone, staminaPanelPosition, leftWristPositionStamina, rightWristPositionStamina,
                            VHVRConfig.StaminaPanelPos(), VHVRConfig.StaminaPanelRot(), VHVRConfig.StaminaPanelScale());
                        altHudCanvasGroup.alpha = VHVRConfig.AllowHudFade() ? calculateHudCanvasAlpha(altHudCanvasParent) : 1f;
                    } else
                    {
                        LogError("handBone is null while setting stamina panel position. Falling back to camera locked.");
                        setCameraLockedPositioning(altHudCanvasParent, altHudCanvas, altHudCanvasGroup, VHVRConfig.StaminaPanelCameraX(), VHVRConfig.StaminaPanelCameraY(), VHVRConfig.StaminaPanelScale());
                    }
                }
            }
        }

        private void updateStaminaPanelLocalPosition(string healthPanelPosition, string staminaPanelPosition)
        {
            if (healthPanelPosition.Equals(staminaPanelPosition))
            {
                Vector3 healthPanelLocation = healthPanelComponents.healthPanel.GetComponent<RectTransform>().localPosition;
                // Need to make sure stamina and healthbar and positioned on same canvas correctly
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

        private void parentHudComponents(string healthPanelPosition, string staminaPanelPosition)
        {
            healthPanelComponents.healthPanel.transform.SetParent(hudCanvas.GetComponent<RectTransform>(), false);
            if (healthPanelPosition.Equals(staminaPanelPosition))
            {
                staminaPanelComponents.staminaBarRoot.transform.SetParent(hudCanvas.GetComponent<RectTransform>(), false);
            } else
            {
                staminaPanelComponents.staminaBarRoot.transform.SetParent(altHudCanvas.GetComponent<RectTransform>(), false);
            }
        }

        private Transform getHandParent(string position)
        {
            var vrik = VRPlayer.vrikRef;
            if (vrik == null)
            {
                return null;
            }
            return position == LEFT_WRIST ? vrik.references.leftHand : vrik.references.rightHand;
        }

        private void setCameraLockedPositioning(GameObject hCanvasParent, Canvas hCanvas, CanvasGroup group, float xOffset, float yOffset, float scalar)
        {
            float canvasWidth = hCanvas.GetComponent<RectTransform>().rect.width;
            float scaleFactor = 0.1f / canvasWidth * scalar;
            hCanvasParent.transform.SetParent(hudCamera.gameObject.transform, false);
            hCanvasParent.transform.position = VRPlayer.instance.transform.position;
            float hudDistance = 1f;
            float hudVerticalOffset = -0.5f;
            hCanvasParent.transform.localPosition = new Vector3(xOffset * 1000, hudVerticalOffset + yOffset * 1000, hudDistance);
            hCanvas.GetComponent<RectTransform>().localScale = Vector3.one * scaleFactor * hudDistance;
            hCanvasParent.transform.localRotation = Quaternion.Euler(Vector3.zero);
            group.alpha = 1f;
        }

        private void setWristPosition(GameObject hCanvasParent, Canvas hCanvas, Transform hand, string panelPosition, Vector3 leftWristPosition, Vector3 rightWristPosition, Vector3 offset, Quaternion rot, float scale)
        {
            var isLeftWrist = panelPosition == LEFT_WRIST;
            var wristHudOffset = isLeftWrist ? leftWristPosition : rightWristPosition;
            var wristHudRotation = isLeftWrist ? leftWristRotation : rightWristRotatation;

            // var xSign = isLeftWrist ? 1 : -1;
            // var ySign = isLeftWrist ? -1 : 1;
            //
            // offset = new Vector3(ySign * offset.y, xSign * offset.x, offset.z);

            float canvasWidth = hCanvas.GetComponent<RectTransform>().rect.width;
            hCanvasParent.transform.SetParent(hand, false);
            var hudCanvasRect = hCanvas.GetComponent<RectTransform>();
            hudCanvasRect.localScale = Vector3.one * scale * 0.001f / canvasWidth;
            hCanvasParent.transform.localPosition = wristHudOffset + offset; 
            hCanvasParent.transform.localRotation = Quaternion.Euler(wristHudRotation + rot.eulerAngles);
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
         Healthbar Panel Object: healthpanel(Clone) 
           Component: RectTransform 
           Component: Animator 
             Healthbar Panel Object: darken 
               Component: RectTransform 
               Component: CanvasRenderer 
               Component: Image 
             Healthbar Panel Object: Health 
               Component: RectTransform 
                 Healthbar Panel Object: border 
                   Component: RectTransform 
                   Component: CanvasRenderer 
                   Component: Image 
                 Healthbar Panel Object: bkg 
                   Component: RectTransform 
                   Component: CanvasRenderer 
                   Component: Image 
                 Healthbar Panel Object: slow 
                   Component: RectTransform 
                   Component: GuiBar 
                     Healthbar Panel Object: bar 
                       Component: RectTransform 
                       Component: CanvasRenderer 
                       Component: Image 
                 Healthbar Panel Object: fast 
                   Component: RectTransform 
                   Component: GuiBar 
                     Healthbar Panel Object: bar 
                       Component: RectTransform 
                       Component: CanvasRenderer 
                       Component: Image 
                         Healthbar Panel Object: HealthText 
                           Component: RectTransform 
                           Component: CanvasRenderer 
                           Component: UI.Text 
                           Component: UI.Outline 
             Healthbar Panel Object: Food 
               Component: RectTransform 
                 Healthbar Panel Object: border 
                   Component: RectTransform 
                   Component: CanvasRenderer 
                   Component: Image 
                 Healthbar Panel Object: bar1 
                   Component: RectTransform 
                   Component: CanvasRenderer 
                   Component: Image 
                 Healthbar Panel Object: bar2 
                   Component: RectTransform 
                   Component: CanvasRenderer 
                   Component: Image 
                 Healthbar Panel Object: bar3 
                   Component: RectTransform 
                   Component: CanvasRenderer 
                   Component: Image 
                 Healthbar Panel Object: baseBar 
                   Component: RectTransform 
                   Component: CanvasRenderer 
                   Component: Image 
                 Healthbar Panel Object: overlay 
                   Component: RectTransform 
                   Component: CanvasRenderer 
                   Component: Image 
             Healthbar Panel Object: FoodText 
               Component: RectTransform 
               Component: CanvasRenderer 
               Component: UI.Text 
               Component: UI.Outline 
             Healthbar Panel Object: MaxHealth 
               Component: RectTransform 
               Component: CanvasRenderer 
               Component: UI.Text 
               Component: UI.Outline 
             Healthbar Panel Object: healthicon 
               Component: RectTransform 
               Component: CanvasRenderer 
               Component: Image 
             Healthbar Panel Object: foodicon 
               Component: RectTransform 
               Component: CanvasRenderer 
               Component: Image 
             Healthbar Panel Object: food0 
               Component: RectTransform 
               Component: CanvasRenderer 
               Component: Image 
                 Healthbar Panel Object: foodicon0 
                   Component: RectTransform 
                   Component: CanvasRenderer 
                   Component: Image 
             Healthbar Panel Object: food1 
               Component: RectTransform 
               Component: CanvasRenderer 
               Component: Image 
                 Healthbar Panel Object: foodicon1 
                   Component: RectTransform 
                   Component: CanvasRenderer 
                   Component: Image 
             Healthbar Panel Object: food2 
               Component:UnityEngine.RectTransform 
               Component: CanvasRenderer 
               Component: Image 
                 Healthbar Panel Object: foodicon2 
                   Component: RectTransform 
                   Component: CanvasRenderer 
                   Component: Image 
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
            staminapanel
              Component: UnityEngine.RectTransform
              Component: UnityEngine.Animator
              Stamina
                Component: UnityEngine.RectTransform
                darken
                  Component: UnityEngine.RectTransform
                  Component: UnityEngine.CanvasRenderer
                  Component: UnityEngine.UI.Image
                border
                  Component: UnityEngine.RectTransform
                  Component: UnityEngine.CanvasRenderer
                  Component: UnityEngine.UI.Image
                bkg
                  Component: UnityEngine.RectTransform
                  Component: UnityEngine.CanvasRenderer
                  Component: UnityEngine.UI.Image
                stamina_slow
                  Component: UnityEngine.RectTransform
                  Component: GuiBar
                  bar
                    Component: UnityEngine.RectTransform
                    Component: UnityEngine.CanvasRenderer
                    Component: UnityEngine.UI.Image
                stamina_fast
                  Component: UnityEngine.RectTransform
                  Component: GuiBar
                  bar
                    Component: UnityEngine.RectTransform
                    Component: UnityEngine.CanvasRenderer
                    Component: UnityEngine.UI.Image
                HealthText
                  Component: UnityEngine.RectTransform
                  Component: UnityEngine.CanvasRenderer
                  Component: UnityEngine.UI.Text
                  Component: UnityEngine.UI.Outline
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
            staminaPanelClone.transform.SetParent(hudCanvas.GetComponent<RectTransform>(), false); // TODO: Move this maybe?
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
            for (int i = 0; i < foodIconLength; i++)
            {
                string foodName = "food" + i; // // food0..foodN
                string foodIconName = "foodicon" + i; // foodicon0..foodiconN
                cache.foodIcons[i] = cache.healthPanel.transform.Find(foodName).Find(foodIconName).gameObject;
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
        }

        private void updateStaminaPanelHudReferences(StaminaPanelComponents newComponents)
        {
            Hud.instance.m_staminaBar2Root = newComponents.staminaBarRoot.GetComponent<RectTransform>();
            Hud.instance.m_staminaAnimator = newComponents.staminaAnimator;
            Hud.instance.m_staminaBar2Fast = newComponents.staminaBarFast.GetComponent<GuiBar>();
            Hud.instance.m_staminaBar2Slow = newComponents.staminaBarSlow.GetComponent<GuiBar>();
        }

        private bool ensureHudCanvas()
        {
            if (hudCanvas != null && hudCanvasParent && altHudCanvas != null && altHudCanvasParent)
            {
                return true;
            }
            if (!hudCanvasParent)
            {
                hudCanvasParent = new GameObject("VRHudCanvasParent");
                hudCanvasParent.layer = LayerUtils.getWorldspaceUiLayer();
                GameObject.DontDestroyOnLoad(hudCanvasParent);
            }
            if (!altHudCanvasParent)
            {
                altHudCanvasParent = new GameObject("VRAltHudCanvasParent");
                altHudCanvasParent.layer = LayerUtils.getWorldspaceUiLayer();
                GameObject.DontDestroyOnLoad(altHudCanvasParent);
            }
            if (!ensureHudCamera())
            {
                LogError("Error getting HUD Camera.");
                return false;
            }
            hudCanvas = hudCanvasParent.AddComponent<Canvas>();
            hudCanvasGroup = hudCanvasParent.AddComponent<CanvasGroup>();
            hudCanvasGroup.interactable = false;
            hudCanvasGroup.blocksRaycasts = false;
            hudCanvasGroup.alpha = 1f;
            hudCanvas.renderMode = RenderMode.WorldSpace;
            hudCanvas.transform.position = hudCanvasParent.transform.position;
            hudCanvas.transform.rotation = hudCanvasParent.transform.rotation;
            hudCanvas.transform.localPosition = Vector3.zero;
            hudCanvas.transform.localRotation = Quaternion.identity;
            hudCanvas.worldCamera = hudCamera;
            altHudCanvas = altHudCanvasParent.AddComponent<Canvas>();
            altHudCanvasGroup = altHudCanvasParent.AddComponent<CanvasGroup>();
            altHudCanvasGroup.interactable = false;
            altHudCanvasGroup.blocksRaycasts = false;
            altHudCanvasGroup.alpha = 1f;
            altHudCanvas.renderMode = RenderMode.WorldSpace;
            altHudCanvas.transform.position = altHudCanvasParent.transform.position;
            altHudCanvas.transform.rotation = altHudCanvasParent.transform.rotation;
            altHudCanvas.transform.localPosition = Vector3.zero;
            altHudCanvas.transform.localRotation = Quaternion.identity;
            altHudCanvas.worldCamera = hudCamera;
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
