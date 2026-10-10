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
     */
    class VRCinematicScreen : MonoBehaviour
    {
        private const string SCREEN_NAME = "VRCinematicScreen";
        // Draw on top of the other GUI canvases, which are also rendered by the VRGUI camera.
        private const int SORTING_ORDER = short.MaxValue;

        private CinematicsManager cinematicsManager;
        private Canvas screenCanvas;
        private RawImage videoImage;
        private AspectRatioFitter videoAspectRatioFitter;
        private RenderTexture videoTexture;

        public static void OnPlay(CinematicsManager cinematicsManager, VideoClip clip)
        {
            var screen = cinematicsManager.GetComponent<VRCinematicScreen>();
            if (screen == null)
            {
                screen = cinematicsManager.gameObject.AddComponent<VRCinematicScreen>();
                screen.cinematicsManager = cinematicsManager;
            }
            screen.RenderVideoToScreen(clip);
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
