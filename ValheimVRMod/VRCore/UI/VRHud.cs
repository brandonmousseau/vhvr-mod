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
        private const string LEFT_WRIST = "LeftWrist";
        private readonly Vector3 leftWristPosition = new Vector3(0.0009f, -0.0005f, -0.0005f);
        private readonly Vector3 leftWristRotation = new Vector3(0f, 0f, 100f);
        private readonly Vector3 rightWristPosition = new Vector3(-0.0009f, 0.0005f, -0.0005f);
        private readonly Vector3 rightWristRotatation = new Vector3(0f, 0f, -100f);
        private const float FULL_ALPHA_ANGLE = 5f;
        private const float ZERO_ALPHA_ANGLE = 90f;

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

        private static VRHud _instance = null;

        private Canvas hudCanvas;
        private CanvasGroup hudCanvasGroup;
        private GameObject hudCanvasParent;
        private Camera hudCamera;
        private HealthPanelComponents healthPanelComponents;
        private HealthPanelComponents originalHealthPanelComponents;

        private float debugXPos = -0.0014f;
        private float debugYPos = 0.0005f;
        private float debugZPos = -0.0005f;
        private float debugXRot = 0f;
        private float debugYRot = 0f;
        private float debugZRot = -100f;

        private VRHud() {
            healthPanelComponents = new HealthPanelComponents();
            originalHealthPanelComponents = new HealthPanelComponents();
        }

        public void Update()
        {
            if (!VRPlayer.attachedToPlayer || VHVRConfig.UseLegacyHud() || !VHVRConfig.UseVrControls())
            {
                if (hudCanvasParent && hudCanvasParent.activeSelf)
                {
                    hudCanvasParent.SetActive(false);
                    hudCanvas = null;
                    GameObject.Destroy(hudCanvasParent);
                    hudCanvasParent = null;
                    hudCanvasGroup = null;
                    hudCamera = null;
                    healthPanelComponents.clear();
                } 
                if (originalHealthPanelComponents.healthPanel && !originalHealthPanelComponents.healthPanel.activeSelf)
                {
                    originalHealthPanelComponents.healthPanel.SetActive(true);
                    updateHealthPanelHudReferences(originalHealthPanelComponents);
                    originalHealthPanelComponents.clear();
                }
                return;
            }
            if (!ensureHudCanvas())
            {
                LogError("Hud Canvas not created!");
                return;
            }
            if (!ensureHudCamera())
            {
                LogError("Problem getting HUD camera.");
                return;
            }
            maybeCloneHealthBarComponents();
            if (originalHealthPanelComponents.healthPanel)
            {
                originalHealthPanelComponents.healthPanel.SetActive(false);
            }
            // Set the cloned panel as active only when attached to player
            if (hudCanvasParent)
            {
                hudCanvasParent.SetActive(VRPlayer.attachedToPlayer);
            }
            updateHealthPanelHudReferences(healthPanelComponents);
            updateHudPositionAndScale();
        }

        private void updateHudPositionAndScale()
        {
            if (hudCanvasParent == null || !VRPlayer.attachedToPlayer || hudCamera == null)
            {
                return;
            }
            string position = VHVRConfig.HudPanelPosition();
            if (position == "CameraLocked")
            {
                setCameraLockedPositioning();
            } else
            {
                var wristHudOffset = position == LEFT_WRIST ? leftWristPosition : rightWristPosition;
                var wristHudRotation = position == LEFT_WRIST ? leftWristRotation : rightWristRotatation;
                var xSign = position == LEFT_WRIST ? 1 : -1;
                var ySign = position == LEFT_WRIST ? -1 : 1;
                Transform handBone = getHandParent(position);
                if (handBone != null) {
                    setWristPosition(handBone, wristHudOffset, wristHudRotation, xSign, ySign);
                    //updateWristDebugPositioning();
                    //setWristPosition(handBone, new Vector3(debugXPos, debugYPos, debugZPos), new Vector3(debugXRot, debugYRot, debugZRot), xSign, ySign);
                    hudCanvasGroup.alpha = VHVRConfig.AllowHudFade() ? calculateHudCanvasAlpha() : 1f;
                } else
                {
                    LogError("handBone is null while setting health panel position. Falling back to camera locked.");
                    setCameraLockedPositioning();
                }
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

        private void setCameraLockedPositioning()
        {
            float canvasWidth = hudCanvas.GetComponent<RectTransform>().rect.width;
            float scaleFactor = 0.1f / canvasWidth * VHVRConfig.HudPanelScale(); ;
            hudCanvasParent.transform.SetParent(hudCamera.gameObject.transform, false);
            hudCanvasParent.transform.position = VRPlayer.instance.transform.position;
            float hudDistance = 1f;
            float hudVerticalOffset = -0.5f;
            hudCanvasParent.transform.localPosition = new Vector3(VHVRConfig.HudPanelXOffset() * 1000, hudVerticalOffset + VHVRConfig.HudPanelYOffset() * 1000, hudDistance);
            hudCanvas.GetComponent<RectTransform>().localScale = Vector3.one * scaleFactor * hudDistance;
            hudCanvasParent.transform.localRotation = Quaternion.Euler(Vector3.zero);
            hudCanvasGroup.alpha = 1f;
        }

        private void setWristPosition(Transform hand, Vector3 pos, Vector3 rot, int xSign, int ySign)
        {
            float canvasWidth = hudCanvas.GetComponent<RectTransform>().rect.width;
            float scaleFactor = 0.001f / canvasWidth * VHVRConfig.HudPanelScale();
            hudCanvasParent.transform.SetParent(hand, false);
            var hudCanvasRect = hudCanvas.GetComponent<RectTransform>();
            hudCanvasRect.localScale = Vector3.one * scaleFactor;
            hudCanvasParent.transform.position = hand.position;
            hudCanvasParent.transform.localPosition = pos + new Vector3(ySign * VHVRConfig.HudPanelYOffset(), xSign * VHVRConfig.HudPanelXOffset(), VHVRConfig.HudPanelZOffset()); // X/Y swapped so it makes sense in game
            hudCanvasParent.transform.localRotation = Quaternion.Euler(rot + new Vector3(VHVRConfig.HudPanelYRotationOffset(), VHVRConfig.HudPanelXRotationOffset(), VHVRConfig.HudPanelZRotationOffset()));
        }

        private float calculateHudCanvasAlpha()
        {
            var deltaAngle = Quaternion.Angle(hudCamera.transform.rotation, hudCanvasParent.transform.rotation);
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

        private void updateWristDebugPositioning()
        {
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
        private void maybeCloneHealthBarComponents()
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
            originalHealthPanelComponents.healthPanel = Hud.instance.m_healthPanel.gameObject; // Save reference to original health panel
            cacheHealthPanelComponents(originalHealthPanelComponents);
            healthPanelComponents.healthPanel = GameObject.Instantiate(Hud.instance.m_healthPanel.gameObject);
            healthPanelComponents.healthPanel.transform.SetParent(hudCanvas.GetComponent<RectTransform>(), false); // TODO: Move this maybe?
            cacheHealthPanelComponents(healthPanelComponents);
        }

        // Cache references to all the important game objects in the health panel
        private void cacheHealthPanelComponents(HealthPanelComponents cache)
        {
            if (cache.healthPanel)
            {
                // HealthBar
                cache.healthbarRoot = cache.healthPanel.transform.Find("Health").gameObject;
                cache.healthBarFast = cache.healthbarRoot.transform.Find("fast").gameObject;
                cache.healthBarSlow = cache.healthbarRoot.transform.Find("slow").gameObject;
                cache.healthMaxText = cache.healthPanel.transform.Find("MaxHealth").gameObject;
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
        }

        private void updateHealthPanelHudReferences(HealthPanelComponents newComponents)
        {
            if (newComponents == null || !newComponents.healthPanel || Hud.instance == null || Hud.instance.m_healthPanel.gameObject == newComponents.healthPanel)
            {
                // Return early if the local healthPanel is null, the Hud instance is null, or the Hud instance reference is already
                // the same as the locally cached clone.
                return;
            }
            // HealthBar
            Hud.instance.m_healthPanel = newComponents.healthPanel.GetComponent<RectTransform>();
            Hud.instance.m_healthBarRoot = newComponents.healthbarRoot.GetComponent<RectTransform>();
            Hud.instance.m_healthAnimator = newComponents.healthPanel.GetComponent<Animator>();
            Hud.instance.m_healthBarFast = newComponents.healthBarFast.GetComponent<GuiBar>();
            Hud.instance.m_healthBarSlow = newComponents.healthBarSlow.GetComponent<GuiBar>();
            Hud.instance.m_healthText = newComponents.healthText.GetComponent<Text>();
            Hud.instance.m_healthMaxText = newComponents.healthMaxText.GetComponent<Text>();
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

        private bool ensureHudCanvas()
        {
            if (hudCanvas != null && hudCanvasParent)
            {
                return true;
            }
            if (!hudCanvasParent)
            {
                hudCanvasParent = new GameObject("VRHudCanvasParent");
                hudCanvasParent.layer = LayerUtils.getWorldspaceUiLayer();
                GameObject.DontDestroyOnLoad(hudCanvasParent);
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
            hudCanvasGroup.alpha = 1;
            hudCanvas.renderMode = RenderMode.WorldSpace;
            hudCanvas.transform.position = hudCanvasParent.transform.position;
            hudCanvas.transform.rotation = hudCanvasParent.transform.rotation;
            hudCanvas.transform.localPosition = Vector3.zero;
            hudCanvas.transform.localRotation = Quaternion.identity;
            hudCanvas.worldCamera = hudCamera;
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

        // Data class to encapsulate all the references needed to be maintained for Health panel
        private class HealthPanelComponents
        {
            // Root Component
            public GameObject healthPanel; // RectTransform
            // HealthBar
            public GameObject healthbarRoot; // RectTransform
            public GameObject healthBarFast; // GuiBar
            public GameObject healthBarSlow; // GuiBar
            public GameObject healthText; // Text
            public GameObject healthMaxText; // Text
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
                healthbarRoot = null;
                healthBarFast = null;
                healthBarSlow = null;
                healthText = null;
                healthMaxText = null;
                foodBarRoot = null;
                foodBaseBar = null;
                foodBars = null;
                foodIcons = null;
                foodIcon = null;
                foodText = null;
            }

        }

    }
}
