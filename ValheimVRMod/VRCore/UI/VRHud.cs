using UnityEngine;
using UnityEngine.UI;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore.UI.HudElements;
using Valve.VR.InteractionSystem;
using System.Collections.Generic;
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
        public const string BUILD = "Build";
        public const string CAMERA_LOCKED = "CameraLocked";
        public const string LEGACY = "Legacy";

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

        // Build Canvas
        private Canvas buildHudCanvas;
        private CanvasGroup buildHudCanvasGroup;
        private VerticalLayoutGroup buildHudVerticalLayout;
        private HorizontalLayoutGroup buildHudHorizontalLayout;
        private GameObject buildHudCanvasParent;

        private Vector3 buildHudPos = new Vector3(-0.019195370376110078f, 0.1886948049068451f, 0.09105824679136276f);
        private Quaternion buildHudRot = new Quaternion(0.4495787024497986f, -0.0009610052220523357f, 0.003949014004319906f, 0.8932315111160278f);


        // References to all the relevant UI components
        private IVRHudElement[] VRHudElements = new IVRHudElement[]
        {
            //This also gives the order of precedence
            new HealthPanelElement(), //Vertical START
            new StaminaPanelElement(), //Horizontal START
            new MinimapPanelElement(),
            new BuildSelectedInfoElement()
        };

        // Map of "Panel Component" -> "Current Position"
        IDictionary<IVRHudElement, string> hudElementToPositionMap = new Dictionary<IVRHudElement, string>();

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
            VRHudElements.ForEach(x => {
                x.Reset();
                hudElementToPositionMap[x] = LEGACY;
            });
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

                buildHudCanvas = null;
                buildHudCanvasGroup = null;
                GameObject.Destroy(buildHudCanvasParent);
                buildHudCanvasParent = null;

                hudCamera = null;
            }
        }

        private void updateHudPositionAndScale()
        {
            if (leftHudCanvasParent == null || !VRPlayer.attachedToPlayer || hudCamera == null)
            {
                return;
            }

            VRHudElements.ForEach(x => placePanelToHud(x.Placement, x));

            setCameraHudPosition();
            
            var vrik = VRPlayer.vrikRef;
            
            if (vrik == null)
            {
                LogError("handBone is null while setting vr hud panel positions");
                return;
            }

            setWristPosition(leftHudCanvasParent, leftHudCanvas, VRPlayer.leftHand.transform, VHVRConfig.LeftWristPos(), VHVRConfig.LeftWristRot());
            leftHudCanvasGroup.alpha = VHVRConfig.AllowHudFade() && ! SettingCallback.configRunning ? calculateHudCanvasAlpha(leftHudCanvasParent) : 1f;

            setWristPosition(rightHudCanvasParent, rightHudCanvas, VRPlayer.rightHand.transform, VHVRConfig.RightWristPos(), VHVRConfig.RightWristRot());
            rightHudCanvasGroup.alpha = VHVRConfig.AllowHudFade() && ! SettingCallback.configRunning ? calculateHudCanvasAlpha(rightHudCanvasParent) : 1f;

            setWristPosition(buildHudCanvasParent, buildHudCanvas, VRPlayer.rightHand.transform, buildHudPos, buildHudRot);
            buildHudCanvasGroup.alpha = VHVRConfig.AllowHudFade() && !SettingCallback.configRunning ? calculateHudCanvasAlpha(buildHudCanvasParent) : 1f;
        }

        private void placePanelToHud(string placement, IVRHudElement panelElement)
        {
            if (hudElementToPositionMap.ContainsKey(panelElement) && hudElementToPositionMap[panelElement] == placement)
            {
                // Return immediately if no change in placement
                return;
            }
            hudElementToPositionMap[panelElement] = placement;
            switch (placement) {
                case LEFT_WRIST:
                    placeInHorizontalVerticalHud(leftHudVerticalLayout.transform, leftHudHorizontalLayout.transform, panelElement.Clone.Root.transform, panelElement.Orientation);
                    break;
                
                case RIGHT_WRIST:
                    placeInHorizontalVerticalHud(rightHudVerticalLayout.transform, rightHudHorizontalLayout.transform, panelElement.Clone.Root.transform, panelElement.Orientation);
                    break;

                case BUILD:
                    placeInHorizontalVerticalHud(buildHudVerticalLayout.transform, buildHudHorizontalLayout.transform, panelElement.Clone.Root.transform, panelElement.Orientation);
                    break;
                case CAMERA_LOCKED:
                    panelElement.Clone.Root.transform.SetParent(cameraHudCanvas.GetComponent<RectTransform>(), false);
                    break;

                case LEGACY:
                    if (panelElement.Clone.Root) panelElement.Reset();
                    break;
            }
        }

        private void placeInHorizontalVerticalHud(Transform verticalHud, Transform horizontalHud, Transform panel, HudOrientation orientation)
        {
            panel.SetParent(orientation == HudOrientation.Horizontal ? verticalHud : horizontalHud, false);
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
            hudCanvasRect.localScale = Vector3.one * 0.1f /  hudCanvasRect.rect.width;
            
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
            if (!buildHudCanvasParent)
            {
                buildHudCanvasParent = new GameObject("BuildHudCanvasParent");
                buildHudCanvasParent.layer = LayerUtils.getWorldspaceUiLayer();
                GameObject.DontDestroyOnLoad(buildHudCanvasParent);
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

            buildHudCanvas = buildHudCanvasParent.AddComponent<Canvas>();
            buildHudCanvasGroup = buildHudCanvasParent.AddComponent<CanvasGroup>();
            buildHudCanvasGroup.interactable = false;
            buildHudCanvasGroup.blocksRaycasts = false;
            buildHudCanvasGroup.alpha = 1f;
            buildHudCanvas.renderMode = RenderMode.WorldSpace;
            buildHudCanvas.transform.position = buildHudCanvasParent.transform.position;
            buildHudCanvas.transform.rotation = buildHudCanvasParent.transform.rotation;
            buildHudCanvas.transform.localPosition = Vector3.zero;
            buildHudCanvas.transform.localRotation = Quaternion.identity;
            buildHudCanvas.worldCamera = hudCamera;

            //Setup layouts
            setupWristCanvas(ref rightHudHorizontalLayout, ref rightHudVerticalLayout, rightHudCanvasGroup.transform, false);
            setupWristCanvas(ref leftHudHorizontalLayout, ref leftHudVerticalLayout, leftHudCanvasGroup.transform, true);
            setupWristCanvas(ref buildHudHorizontalLayout, ref buildHudVerticalLayout, buildHudCanvasGroup.transform, false);

            return true;
        }

        private void setupWristCanvas(ref HorizontalLayoutGroup horizontalLayoutGroup, ref VerticalLayoutGroup verticalLayoutGroup, Transform hudCanvasGroup, bool isLeft)
        {
            var horizontalLayout = new GameObject("HorizontalLayout");
            horizontalLayout.transform.SetParent(hudCanvasGroup.transform, false);
            horizontalLayoutGroup = horizontalLayout.AddComponent<HorizontalLayoutGroup>();
            var verticalLayout = new GameObject("VerticalLayout");
            verticalLayout.transform.SetParent(horizontalLayout.transform, false);
            verticalLayoutGroup = verticalLayout.AddComponent<VerticalLayoutGroup>();
            var verticalLayoutElement = verticalLayout.AddComponent<LayoutElement>();
            verticalLayoutElement.flexibleWidth = 1f;
            verticalLayoutElement.flexibleHeight = 1f;
            setupLayoutGroup(verticalLayoutGroup);
            setupLayoutGroup(horizontalLayoutGroup);
        }

        private void setupLayoutGroup(HorizontalOrVerticalLayoutGroup layoutGroup)
        {
            layoutGroup.spacing = 1;
            if(layoutGroup is VerticalLayoutGroup)
                layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.childControlHeight = false;
            layoutGroup.childControlWidth = false;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childScaleHeight = false;
            layoutGroup.childScaleWidth = false;
            layoutGroup.transform.localPosition = Vector3.zero;
            layoutGroup.transform.localRotation = Quaternion.identity;
        }

        private bool ensureHudCamera()
        {
            if (hudCamera == null)
            {
                hudCamera = CameraUtils.getWorldspaceUiCamera();
            }
            return hudCamera != null;
        }

    }
}
