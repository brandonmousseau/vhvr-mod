using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using ValheimVRMod.Utilities;

using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.VRCore.UI
{
    /**
     * Shows cinematics played by CinematicsManager on the VRGUI.
     *
     * Vanilla plays a cinematic by rendering the video onto the CinematicsManager's own flat camera while
     * disabling the main camera, which in VR is the VR camera. Instead, the video is rendered into a
     * RenderTexture shown on a world space canvas in front of the VRGUI camera, so it ends up on the UI
     * panel (or overlay) like the rest of the GUI, and the VR camera is left alone.
     *
     * This component lives on the CinematicsManager's GameObject so the screen, and the subtitle canvas
     * that is moved onto it, are destroyed together with the CinematicsManager.
     *
     * The screen is drawn under the other GUI canvases, as vanilla draws the video under every canvas and
     * relies on CinematicsHider to take away what should not show over it. That way what vanilla shows over a
     * cinematic, such as the credits, shows over it on the VRGUI as well.
     */
    class VRCinematicScreen : MonoBehaviour
    {
        private const string SCREEN_NAME = "VRCinematicScreen";
        private const string DESKTOP_SCREEN_NAME = "VRCinematicDesktopScreen";
        // Draw under the other GUI canvases, which are also rendered by the VRGUI camera.
        private const int SORTING_ORDER = short.MinValue;

        private CinematicsManager cinematicsManager;
        private Canvas screenCanvas;
        private RawImage videoImage;
        private AspectRatioFitter videoAspectRatioFitter;
        private RenderTexture videoTexture;

        // While a cinematic plays, the VR camera draws nothing of the (paused) world, only black, so that the
        // video is seen in the dark. Neither does the world space UI camera, whose crosshairs and hover names
        // would otherwise float stale in the dark: vanilla hides those along with the HUD, which VHVR moves them
        // out of. The UI panel and the hands have cameras of their own and still show.
        private bool isBlackingOut;
        private Camera blackedOutCamera;
        private int savedCullingMask;
        private CameraClearFlags savedClearFlags;
        private Color savedBackgroundColor;

        // The desktop window shows the GUI full-window while a cinematic plays, as vanilla does on a flat screen,
        // rather than the black or the paused world around the panel that the mirror would show. A screen space
        // overlay canvas is drawn on the desktop window only, never in the headset. The whole GUI image is shown
        // rather than only the video, so that the subtitles and credits come along.
        private Canvas desktopCanvas;
        private RawImage desktopImage;
        private AspectRatioFitter desktopAspectRatioFitter;

        public static void OnPlay(CinematicsManager cinematicsManager, VideoClip clip)
        {
            var screen = cinematicsManager.GetComponent<VRCinematicScreen>();
            if (screen == null)
            {
                screen = cinematicsManager.gameObject.AddComponent<VRCinematicScreen>();
                screen.cinematicsManager = cinematicsManager;
            }
            screen.RenderVideoToScreen(clip);
            AddEndCreditsToGui();
        }

        // The end credits are shown right before the credits video is played, from a canvas of the location
        // they belong to, which VRGUI does not know of.
        private static void AddEndCreditsToGui()
        {
            foreach (var endCredits in FindObjectsOfType<EndCredits>())
            {
                GameObject creditsPanel = endCredits.m_creditsPanel;
                if (creditsPanel == null || !creditsPanel.activeInHierarchy)
                {
                    continue;
                }
                Canvas canvas = creditsPanel.GetComponentInParent<Canvas>()?.rootCanvas;
                if (canvas != null)
                {
                    // Screen space overlay draws whatever the layer, but the VRGUI camera only renders the UI layer.
                    SetLayerRecursively(canvas.transform, LayerMask.NameToLayer("UI"));
                }
                if (!VRGUI.addGuiCanvas(canvas))
                {
                    LogWarning("Could not show the end credits on the VRGUI.");
                }
            }
        }

        private void RenderVideoToScreen(VideoClip clip)
        {
            EnsureScreen();

            int width = clip.width > 0 ? (int)clip.width : (int)VRGUI.GUI_DIMENSIONS.x;
            int height = clip.height > 0 ? (int)clip.height : (int)VRGUI.GUI_DIMENSIONS.y;
            if (videoTexture == null || videoTexture.width != width || videoTexture.height != height)
            {
                if (videoTexture != null)
                {
                    videoTexture.Release();
                    Destroy(videoTexture);
                }
                videoTexture = new RenderTexture(width, height, 0);
                videoImage.texture = videoTexture;
            }
            videoAspectRatioFitter.aspectRatio = (float)width / height;

            VideoPlayer videoPlayer = cinematicsManager.m_videoPlayer;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = videoTexture;
        }

        private void EnsureScreen()
        {
            if (screenCanvas != null)
            {
                return;
            }

            int uiLayer = LayerMask.NameToLayer("UI");

            var screenObject = new GameObject(SCREEN_NAME, typeof(RectTransform));
            screenObject.layer = uiLayer;
            screenObject.transform.SetParent(transform, false);
            screenCanvas = screenObject.AddComponent<Canvas>();
            screenCanvas.renderMode = RenderMode.WorldSpace;
            screenCanvas.sortingOrder = SORTING_ORDER;

            // Letterbox the video in case its aspect ratio differs from the UI panel's.
            var background = CreateStretchedChild("Background", screenObject.transform, uiLayer).AddComponent<Image>();
            background.color = Color.black;
            background.raycastTarget = false;

            var videoObject = CreateStretchedChild("Video", screenObject.transform, uiLayer);
            videoImage = videoObject.AddComponent<RawImage>();
            videoImage.raycastTarget = false;
            videoAspectRatioFitter = videoObject.AddComponent<AspectRatioFitter>();
            videoAspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

            // Move the subtitles onto the screen too, since their canvas was set up for the flat cinematic camera.
            GameObject subtitleCanvas = cinematicsManager.m_subtitleCanvas;
            if (subtitleCanvas != null)
            {
                SetLayerRecursively(subtitleCanvas.transform, uiLayer);
                var subtitleTransform = subtitleCanvas.GetComponent<RectTransform>();
                subtitleTransform.SetParent(screenObject.transform, false);
                Stretch(subtitleTransform);
                subtitleTransform.SetAsLastSibling();
            }
            else
            {
                LogWarning("CinematicsManager has no subtitle canvas.");
            }

            screenObject.SetActive(false);

            var desktopObject = new GameObject(DESKTOP_SCREEN_NAME, typeof(RectTransform));
            desktopObject.transform.SetParent(transform, false);
            desktopCanvas = desktopObject.AddComponent<Canvas>();
            desktopCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            desktopCanvas.sortingOrder = short.MaxValue;
            var desktopBackground = CreateStretchedChild("Background", desktopObject.transform, desktopObject.layer).AddComponent<Image>();
            desktopBackground.color = Color.black;
            desktopBackground.raycastTarget = false;
            var desktopImageObject = CreateStretchedChild("Gui", desktopObject.transform, desktopObject.layer);
            desktopImage = desktopImageObject.AddComponent<RawImage>();
            desktopImage.raycastTarget = false;
            desktopAspectRatioFitter = desktopImageObject.AddComponent<AspectRatioFitter>();
            desktopAspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            desktopObject.SetActive(false);

            Camera.onPreCull += OnCameraPreCull;
            Camera.onPostRender += OnCameraPostRender;
        }

        // Only for the length of the render, so that what other code sets on the VR camera meanwhile (e. g. the
        // water layer while underwater) is left as it is.
        private void OnCameraPreCull(Camera camera)
        {
            if (!isBlackingOut || blackedOutCamera != null)
            {
                return;
            }
            bool isVrCamera = camera == CameraUtils.getCamera(CameraUtils.VR_CAMERA);
            // With [Graphics] MergeUiCameras the hands camera draws the world space UI: only that layer goes.
            bool isMergedUiCamera = camera == VRPlayer.mergedUiCamera;
            if (!isVrCamera && !isMergedUiCamera && camera != CameraUtils.getCamera(CameraUtils.WORLD_SPACE_UI_CAMERA))
            {
                return;
            }
            // Cameras render one after another, so one camera at a time is blacked out and restored.
            blackedOutCamera = camera;
            savedCullingMask = camera.cullingMask;
            savedClearFlags = camera.clearFlags;
            savedBackgroundColor = camera.backgroundColor;
            camera.cullingMask = isMergedUiCamera ? camera.cullingMask & ~LayerUtils.WORLDSPACE_UI_LAYER_MASK : 0;
            if (isVrCamera)
            {
                // The world space UI camera only clears depth, drawing over what the VR camera drew.
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
            }
        }

        private void OnCameraPostRender(Camera camera)
        {
            if (camera != blackedOutCamera)
            {
                return;
            }
            RestoreBlackedOutCamera();
        }

        private void RestoreBlackedOutCamera()
        {
            if (blackedOutCamera != null)
            {
                blackedOutCamera.cullingMask = savedCullingMask;
                blackedOutCamera.clearFlags = savedClearFlags;
                blackedOutCamera.backgroundColor = savedBackgroundColor;
            }
            blackedOutCamera = null;
        }

        void LateUpdate()
        {
            if (screenCanvas == null)
            {
                return;
            }

            bool isPlaying =
                CinematicsManager.IsStartedPlaying() &&
                cinematicsManager.m_videoPlayer.clip != null &&
                cinematicsManager.m_videoPlayer.targetTexture == videoTexture;
            Camera guiCamera = isPlaying ? CameraUtils.getCamera(CameraUtils.VRGUI_SCREENSPACE_CAM) : null;
            isBlackingOut = guiCamera != null;
            Texture guiTexture = VRGUI.guiTexture;
            bool showOnDesktop = guiCamera != null && guiTexture != null;
            desktopCanvas.gameObject.SetActive(showOnDesktop);
            if (showOnDesktop)
            {
                desktopImage.texture = guiTexture;
                desktopAspectRatioFitter.aspectRatio = (float)guiTexture.width / guiTexture.height;
            }
            if (guiCamera == null)
            {
                screenCanvas.gameObject.SetActive(false);
                return;
            }

            screenCanvas.gameObject.SetActive(true);
            screenCanvas.worldCamera = guiCamera;

            // Fill the view of the orthographic VRGUI camera, matching how VRGUI lays out the GUI canvases:
            // one world unit per pixel of the UI panel, centered in front of the camera.
            var screenTransform = screenCanvas.GetComponent<RectTransform>();
            Vector3 cameraPosition = guiCamera.transform.position;
            screenTransform.SetPositionAndRotation(new Vector3(cameraPosition.x, cameraPosition.y, 0), Quaternion.identity);
            Vector3 parentScale = transform.lossyScale;
            screenTransform.localScale = new Vector3(
                parentScale.x == 0 ? 1 : 1 / parentScale.x,
                parentScale.y == 0 ? 1 : 1 / parentScale.y,
                parentScale.z == 0 ? 1 : 1 / parentScale.z);
            screenTransform.sizeDelta = VRGUI.GUI_DIMENSIONS;
        }

        void OnDestroy()
        {
            Camera.onPreCull -= OnCameraPreCull;
            Camera.onPostRender -= OnCameraPostRender;
            RestoreBlackedOutCamera();
            if (videoTexture != null)
            {
                videoTexture.Release();
                Destroy(videoTexture);
            }
        }

        private static GameObject CreateStretchedChild(string name, Transform parent, int layer)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.layer = layer;
            var rectTransform = child.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            Stretch(rectTransform);
            return child;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localPosition = Vector3.zero;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;
        }

        private static void SetLayerRecursively(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform child in t)
            {
                SetLayerRecursively(child, layer);
            }
        }
    }
}
