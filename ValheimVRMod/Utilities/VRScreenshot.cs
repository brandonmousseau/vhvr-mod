using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;
using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.Utilities
{
    // Saves a screenshot chosen for the mirror mode rather than whatever ScreenCapture.CaptureScreenshot() picks in
    // VR (it asks for the left eye, which the XR display may or may not honor):
    // - Follow, Spectator and Stabilized: the flat screen window, which shows the view of the mod's own flat screen
    //   camera those modes exist for.
    // - Every other mode: one eye's image as rendered for the headset, undistorted, across its full width and
    //   cropped at the top and bottom to 16:9 like FullWidthMirror does for the window. Right, Left and OpenVR would
    //   otherwise give the stretched eye mirror, and None a black window. The eye is the mirror's one where the mode
    //   has one, otherwise the right eye.
    static class VRScreenshot
    {
        private const float ASPECT = 16f / 9f;
        // Gives up on a capture that never happens, e.g. because the capture camera did not render.
        private const int MAX_FRAMES = 10;

        // Calls onDone with null once the screenshot has been saved to path, or with why it could not be.
        public static IEnumerator Capture(string path, Action<string> onDone)
        {
            string failure = null;
            if (VHVRConfig.UseSeparateFlatscreenCamera())
            {
                // After everything, including any GUI, has been drawn into the window.
                yield return new WaitForEndOfFrame();
                failure = tryRun(() => saveWindow(path));
            }
            else
            {
                XRNode eye = VHVRConfig.GetMirrorViewMode() == Unity.XR.OpenVR.OpenVRSettings.MirrorViewModes.Left ||
                    (VHVRConfig.UseFullWidthMirror(out XRNode fullWidthEye) && fullWidthEye == XRNode.LeftEye) ?
                    XRNode.LeftEye : XRNode.RightEye;
                bool done = false;
                EyeCaptureCamera.Create(() =>
                {
                    failure = tryRun(() => saveEye(path, eye));
                    done = true;
                });
                for (int frames = 0; !done && frames < MAX_FRAMES; frames++)
                {
                    yield return null;
                }
                if (!done)
                {
                    failure = "the eye image was not captured";
                }
            }
            if (failure != null)
            {
                LogWarning("Could not save the screenshot to " + path + ": " + failure);
            }
            onDone(failure);
        }

        private static string tryRun(Func<string> save)
        {
            try
            {
                return save();
            }
            catch (Exception e)
            {
                return e.ToString();
            }
        }

        private static string saveWindow(string path)
        {
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return "window " + Screen.width + "x" + Screen.height;
            }
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = null;
            Texture2D image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            try
            {
                image.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.Destroy(image);
            }
            return null;
        }

        // Called right after the stereo cameras have rendered this frame's eye images.
        private static string saveEye(string path, XRNode eye)
        {
            XRDisplaySubsystem display = getDisplay();
            if (display == null)
            {
                return "no running XR display";
            }
            // With multi pass stereo, which the mod uses, each eye has a render pass and texture of its own.
            if (display.GetRenderPassCount() < 2)
            {
                return "expected a render pass per eye, found " + display.GetRenderPassCount();
            }
            display.GetRenderPass(eye == XRNode.LeftEye ? 0 : 1, out var pass);
            RenderTextureDescriptor eyeDesc = pass.renderTargetDesc;
            if (eyeDesc.width <= 0 || eyeDesc.height <= 0)
            {
                return "eye texture " + eyeDesc.width + "x" + eyeDesc.height;
            }

            // The full width and the centered band of the height that makes the image 16:9, or the whole image if
            // it is already wider than that.
            float sourceHeight = Mathf.Min(1, (float)eyeDesc.width / eyeDesc.height / ASPECT);
            int width = eyeDesc.width;
            int height = Mathf.RoundToInt(eyeDesc.height * sourceHeight);

            // Keeps the eye's color encoding, so that the copy only crops the image and ReadPixels() below gets the
            // encoded colors a PNG expects.
            RenderTexture cropped = RenderTexture.GetTemporary(
                width, height, 0, RenderTextureFormat.ARGB32,
                eyeDesc.sRGB ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                using (CommandBuffer commandBuffer = new CommandBuffer { name = "VHVR screenshot" })
                {
                    // The eye image is stored upside down relative to what ReadPixels() below expects, so the band is
                    // also flipped vertically while copying it: its top row goes to the bottom of the copy and so on.
                    commandBuffer.Blit(
                        pass.renderTarget, cropped, new Vector2(1, -sourceHeight), new Vector2(0, (1 + sourceHeight) / 2));
                    Graphics.ExecuteCommandBuffer(commandBuffer);
                }
                RenderTexture.active = cropped;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(cropped);
                UnityEngine.Object.Destroy(image);
            }
            LogInfo("Saved the " + eye + " image " + eyeDesc.width + "x" + eyeDesc.height + ", cropped to " + width + "x" + height);
            return null;
        }

        private static XRDisplaySubsystem getDisplay()
        {
            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetInstances(displays);
            foreach (XRDisplaySubsystem display in displays)
            {
                if (display.running)
                {
                    return display;
                }
            }
            return null;
        }

        // A camera that renders nothing, only to run the capture once every stereo camera has rendered this frame's
        // eye images, while they are still the display's current ones. Destroys itself after one capture.
        private class EyeCaptureCamera : MonoBehaviour
        {
            // After every stereo camera (the highest of which have depth 4) and FullWidthMirror's camera.
            private const float CAMERA_DEPTH = 101;

            private Action onCaptured;

            public static void Create(Action onCaptured)
            {
                // Built on an inactive object so that the camera is never a stereo camera, which a camera added to an
                // active object would be until stereoTargetEye is set.
                GameObject gameObject = new GameObject("VHVRScreenshotCamera");
                gameObject.SetActive(false);
                Camera camera = gameObject.AddComponent<Camera>();
                camera.stereoTargetEye = StereoTargetEyeMask.None;
                camera.targetTexture = null;
                camera.depth = CAMERA_DEPTH;
                // Draws nothing at all, not even a clear, so the window is left as it is.
                camera.cullingMask = 0;
                camera.clearFlags = CameraClearFlags.Nothing;
                camera.renderingPath = RenderingPath.Forward;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.useOcclusionCulling = false;
                camera.depthTextureMode = DepthTextureMode.None;
                gameObject.AddComponent<EyeCaptureCamera>().onCaptured = onCaptured;
                gameObject.SetActive(true);
            }

            void OnPostRender()
            {
                if (onCaptured == null)
                {
                    return;
                }
                Action capture = onCaptured;
                onCaptured = null;
                capture();
                Destroy(gameObject);
            }
        }
    }
}
