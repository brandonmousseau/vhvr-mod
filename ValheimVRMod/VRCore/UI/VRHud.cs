using UnityEngine;
using UnityEngine.UI;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore.UI.HudElements;
using Valve.VR.InteractionSystem;
using static ValheimVRMod.Utilities.LogUtils;

/**
 * Adds a more custom-VR HUD by moving UI elements to world space canvases, e.g. Health Bar that is either
 * camera locked or attached to a player wrist.
 */
namespace ValheimVRMod.VRCore.UI
{

    public class VRHud
    {
        public const string LEFT_WRIST = "LeftWrist";
        public const string RIGHT_WRIST = "RightWrist";
        public const string CAMERA_LOCKED = "CameraLocked";
        public const string LEGACY = "Legacy";

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

        public enum HudOrientation
        {
            Horizontal,
            Veritcal
        }

        public interface IVRPanelComponent
        {
            GameObject Root { get; }
            void Clear();
        }

        /// <summary>
        /// Interface for cloned UI Elements, offers parameters and methods to move UI Elements around
        /// </summary>
        public interface IVRHudElement
        {
            string Placement { get; }
            HudOrientation Orientation { get; }
            IVRPanelComponent Original { get; }
            IVRPanelComponent Clone { get; }

            void Update();
            void Reset();
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
        private VerticalLayoutGroup leftHudVerticalLayout;
        private HorizontalLayoutGroup leftHudHorizontalLayout;
        private GameObject leftHudCanvasParent;

        // Right Wrist Canvas
        private Canvas rightHudCanvas;
        private CanvasGroup rightHudCanvasGroup;
        private VerticalLayoutGroup rightHudVerticalLayout;
        private HorizontalLayoutGroup rightHudHorizontalLayout;
        private GameObject rightHudCanvasParent;

        // References to all the relevant UI components
        private IVRHudElement[] VRHudElements = new IVRHudElement[]
        {
            //This also gives the order of precedence
            new HealthPanelElement(), //Vertical START
            new StaminaPanelElement(), //Horizontal START
            new MinimapPanelElement()
        };

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
            foreach (var hudElement in VRHudElements)
            {
                //Only update elements that aren't on the legacy hud
                if(hudElement.Placement != LEGACY) hudElement.Update();
            }
            // Set the cloned panel as active only when attached to player
            if (leftHudCanvasParent)
            {
                leftHudCanvasParent.SetActive(VRPlayer.attachedToPlayer);
            }
            updateHudPositionAndScale();
        }

        private void revertToLegacyHud()
        {
            VRHudElements.ForEach(x => x.Reset());
            if (leftHudCanvasParent)
            {
                leftHudCanvasParent.SetActive(false);
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
            }
        }

        private void updateHudPositionAndScale()
        {
            if (leftHudCanvasParent == null || !VRPlayer.attachedToPlayer || hudCamera == null)
            {
                return;
            }

            int order = 0;
            VRHudElements.ForEach(x => placePanelToHud(x.Placement, x, order++));

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
        }

        private void placePanelToHud(string placement, IVRHudElement panelElement, int siblingIndex)
        {
            //TODO: Maybe do this on changes and not every frame
            switch (placement) {
                
                case LEFT_WRIST:
                    placeInHorizontalVerticalHud(leftHudVerticalLayout.transform, leftHudHorizontalLayout.transform, panelElement.Clone.Root.transform, panelElement.Orientation);
                    break;
                
                case RIGHT_WRIST:
                    placeInHorizontalVerticalHud(rightHudVerticalLayout.transform, rightHudHorizontalLayout.transform, panelElement.Clone.Root.transform, panelElement.Orientation);
                    break;
                
                case CAMERA_LOCKED:
                    panelElement.Clone.Root.transform.SetParent(cameraHudCanvas.GetComponent<RectTransform>(), false);
                    break;

                case LEGACY:
                    if (panelElement.Clone.Root) panelElement.Reset();
                    break;
            }
            panelElement.Clone?.Root?.transform.SetSiblingIndex(siblingIndex);
        }

        private void placeInHorizontalVerticalHud(Transform verticalHud, Transform horizontalHud, Transform panel, HudOrientation orientation)
        {
            panel.SetParent(orientation == HudOrientation.Horizontal ? verticalHud : horizontalHud, false);
        }

        //private void updateStaminaPanelLocalPosition(string healthPanelPosition, string staminaPanelPosition)
        //{
        //    if (healthPanelPosition.Equals(staminaPanelPosition))
        //    {
        //        Vector3 healthPanelLocation = healthPanelComponents.healthPanel.GetComponent<RectTransform>().localPosition;
        //        // Need to make sure stamina and healthbar are positioned on same canvas correctly
        //        float healthPanelWidth = healthPanelComponents.healthPanel.GetComponent<RectTransform>().sizeDelta.x;
        //        float staminaPanelWidth = staminaPanelComponents.staminaBarRoot.GetComponent<RectTransform>().sizeDelta.x;
        //        float staminaPanelHeight = staminaPanelComponents.staminaBarRoot.GetComponent<RectTransform>().sizeDelta.y;
        //        staminaPanelComponents.staminaBarRoot.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, 90f);
        //        staminaPanelComponents.staminaBarRoot.GetComponent<RectTransform>().localPosition =
        //            new Vector3(healthPanelLocation.x + 0.5f * healthPanelWidth + 0.5f * staminaPanelHeight, healthPanelLocation.y + 0.5f * staminaPanelWidth, healthPanelLocation.z);
        //    }
        //    else
        //    {
        //        staminaPanelComponents.staminaBarRoot.GetComponent<RectTransform>().localRotation = Quaternion.identity;
        //        staminaPanelComponents.staminaBarRoot.GetComponent<RectTransform>().localPosition = Vector3.zero;
        //    }
        //}

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

            //Setup layouts
            rightHudHorizontalLayout = rightHudCanvasParent.AddComponent<HorizontalLayoutGroup>();
            var rightHudCanvasVerticalLayout = new GameObject("VerticalLayout");
            rightHudCanvasVerticalLayout.transform.SetParent(rightHudCanvas.transform, false);
            rightHudCanvasVerticalLayout.transform.SetSiblingIndex(99); //Always first
            rightHudVerticalLayout = rightHudCanvasVerticalLayout.AddComponent<VerticalLayoutGroup>();
            setupLayoutGroup(rightHudVerticalLayout);
            setupLayoutGroup(rightHudHorizontalLayout);
            leftHudHorizontalLayout = leftHudCanvasParent.AddComponent<HorizontalLayoutGroup>();
            var leftHudCanvasVerticalLayout = new GameObject("VerticalLayout");
            leftHudCanvasVerticalLayout.transform.SetParent(leftHudCanvas.transform, false);
            leftHudCanvasVerticalLayout.transform.SetSiblingIndex(-99); //Always last
            leftHudVerticalLayout = leftHudCanvasVerticalLayout.AddComponent<VerticalLayoutGroup>();
            setupLayoutGroup(leftHudVerticalLayout);
            setupLayoutGroup(leftHudHorizontalLayout);


            rightHudCanvas.worldCamera = hudCamera;
            return true;
        }

        private void setupLayoutGroup(HorizontalOrVerticalLayoutGroup layoutGroup)
        {
            layoutGroup.spacing = 4;
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.childControlHeight = false;
            layoutGroup.childControlWidth = false;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childScaleHeight = false;
            layoutGroup.childScaleWidth = false;
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
