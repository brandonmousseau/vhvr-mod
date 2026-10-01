using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.XR;
using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.Utilities
{
    // Saves a screenshot of both eye images as rendered for the headset rather than whatever
    // ScreenCapture.CaptureScreenshot() picks in VR (it asks for the left eye, which the XR display may or may not
    // honor): side by side with the left eye on the left, whole, undistorted and at full resolution, in every mirror
    // mode. Follow, Spectator and Stabilized also save the flat screen window, which shows the view of the mod's own
    // flat screen camera those modes exist for, next to it with a _flat suffix.
    // The eye images are tens of megapixels, so they are read back from the GPU asynchronously, and the images are
    // encoded and written in the background, rather than stalling the frame.
    static class VRScreenshot
    {
        // Gives up on a capture or a read back that never happens, e.g. because the capture camera did not render.
        private const int MAX_FRAMES = 10;

        private static readonly XRNode[] EYES = { XRNode.LeftEye, XRNode.RightEye };

        // Calls onDone with null once the screenshot has been saved to path, and the window to path with a _flat
        // suffix where the mode has a flat screen camera, or with why one of them could not be.
        public static IEnumerator Capture(string path, Action<string> onDone)
        {
            string failure = null;
            Task<string> windowSave = null;
            string flatPath = null;
            if (VHVRConfig.UseSeparateFlatscreenCamera())
            {
                flatPath = Path.Combine(
                    Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + "_flat" + Path.GetExtension(path));
                // After everything, including any GUI, has been drawn into the window.
                yield return new WaitForEndOfFrame();
                Image window = null;
                failure = tryRun(() => readWindow(out window));
                if (window != null)
                {
                    windowSave = saveInBackground(flatPath, window);
                }
            }

            string eyeFailure = null;
            EyeReadback readback = null;
            EyeCaptureCamera.Create(() => eyeFailure = tryRun(() => startEyeReadback(EYES, out readback)));
            int frames = 0;
            for (; readback == null && eyeFailure == null && frames < MAX_FRAMES; frames++)
            {
                yield return null;
            }
            if (readback == null && eyeFailure == null)
            {
                eyeFailure = "the eye image was not captured";
            }
            // The read backs take a few frames.
            for (frames = 0; readback != null && !readback.isDone && frames < MAX_FRAMES; frames++)
            {
                yield return null;
            }
            Task<string> eyeSave = null;
            if (readback != null)
            {
                eyeFailure = !readback.isDone ? "the eye image was not read back" : readback.failure;
                if (eyeFailure == null)
                {
                    eyeSave = Task.Run(() => saveEyes(path, readback));
                }
            }

            while ((windowSave != null && !windowSave.IsCompleted) || (eyeSave != null && !eyeSave.IsCompleted))
            {
                yield return null;
            }
            failure = failure ?? windowSave?.Result;
            eyeFailure = eyeFailure ?? eyeSave?.Result;
            if (failure != null)
            {
                LogWarning("Could not save the screenshot to " + flatPath + ": " + failure);
            }
            if (eyeFailure != null)
            {
                LogWarning("Could not save the screenshot to " + path + ": " + eyeFailure);
            }
            onDone(eyeFailure ?? failure);
        }

        private static string tryRun(Func<string> run)
        {
            try
            {
                return run();
            }
            catch (Exception e)
            {
                return e.ToString();
            }
        }

        // Pixels in the layout ImageConversion expects: RGB24, bottom row first.
        private class Image
        {
            public byte[] rgb;
            public int width;
            public int height;
        }

        private static string readWindow(out Image window)
        {
            window = null;
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return "window " + Screen.width + "x" + Screen.height;
            }
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = null;
            Texture2D image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            try
            {
                // Window sized, so waiting for the GPU here is short.
                image.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                window = new Image { rgb = image.GetRawTextureData<byte>().ToArray(), width = Screen.width, height = Screen.height };
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.Destroy(image);
            }
            return null;
        }

        private static Task<string> saveInBackground(string path, Image image)
        {
            return Task.Run(() => tryRun(() => save(path, image)));
        }

        // Safe off the main thread: ImageConversion.EncodeArrayToPNG() doesn't touch any Unity object.
        private static string save(string path, Image image)
        {
            byte[] png = ImageConversion.EncodeArrayToPNG(
                image.rgb, GraphicsFormat.R8G8B8_UNorm, (uint)image.width, (uint)image.height);
            if (png == null)
            {
                return "could not encode the image";
            }
            File.WriteAllBytes(path, png);
            return null;
        }

        // The eye images being read back from the GPU, each as RGBA32, bottom row first like ReadPixels().
        private class EyeReadback
        {
            public int eyeWidth;
            public int height;
            public byte[][] eyes;
            public int pending;
            public string failure;
            public bool isDone { get { return pending == 0; } }
        }

        // Called right after the stereo cameras have rendered this frame's eye images. Starts reading back the given
        // eyes, to be saved next to each other, in order from left to right.
        private static string startEyeReadback(XRNode[] eyes, out EyeReadback readback)
        {
            readback = null;
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
            var passes = new XRDisplaySubsystem.XRRenderPass[eyes.Length];
            for (int i = 0; i < eyes.Length; i++)
            {
                display.GetRenderPass(eyes[i] == XRNode.LeftEye ? 0 : 1, out passes[i]);
            }
            // Both eyes share one resolution, so the first eye's stands for all of them.
            RenderTextureDescriptor eyeDesc = passes[0].renderTargetDesc;
            if (eyeDesc.width <= 0 || eyeDesc.height <= 0)
            {
                return "eye texture " + eyeDesc.width + "x" + eyeDesc.height;
            }

            var result = new EyeReadback
            {
                eyeWidth = eyeDesc.width,
                height = eyeDesc.height,
                eyes = new byte[eyes.Length][],
                pending = eyes.Length,
            };
            using (CommandBuffer commandBuffer = new CommandBuffer { name = "VHVR screenshot" })
            {
                for (int i = 0; i < eyes.Length; i++)
                {
                    // Keeps the eye's color encoding, so that the copy only flips the image and the read back gets the
                    // encoded colors a PNG expects. Released once read back.
                    RenderTexture flipped = RenderTexture.GetTemporary(
                        eyeDesc.width, eyeDesc.height, 0, RenderTextureFormat.ARGB32,
                        eyeDesc.sRGB ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
                    // The eye image is stored upside down relative to what the read back gets, so it is flipped
                    // vertically while copying it: its top row goes to the bottom of the copy and so on.
                    commandBuffer.Blit(passes[i].renderTarget, flipped, new Vector2(1, -1), new Vector2(0, 1));
                    int eye = i;
                    commandBuffer.RequestAsyncReadback(flipped, 0, TextureFormat.RGBA32, request =>
                    {
                        if (request.hasError)
                        {
                            result.failure = "could not read back the " + eyes[eye] + " image";
                        }
                        else
                        {
                            // The data is only valid during this callback.
                            result.eyes[eye] = request.GetData<byte>().ToArray();
                        }
                        RenderTexture.ReleaseTemporary(flipped);
                        result.pending--;
                    });
                }
                Graphics.ExecuteCommandBuffer(commandBuffer);
            }
            readback = result;
            return null;
        }

        // Runs in the background.
        private static string saveEyes(string path, EyeReadback readback)
        {
            return tryRun(() =>
            {
                var stopwatch = Stopwatch.StartNew();
                int eyeCount = readback.eyes.Length;
                int width = readback.eyeWidth * eyeCount;
                int height = readback.height;
                var image = new Image { rgb = new byte[width * height * 3], width = width, height = height };
                for (int y = 0; y < height; y++)
                {
                    for (int eye = 0; eye < eyeCount; eye++)
                    {
                        byte[] source = readback.eyes[eye];
                        int sourceIndex = y * readback.eyeWidth * 4;
                        int destinationIndex = (y * width + eye * readback.eyeWidth) * 3;
                        // Drops alpha, which the eye images don't keep at opaque.
                        for (int x = 0; x < readback.eyeWidth; x++)
                        {
                            image.rgb[destinationIndex++] = source[sourceIndex++];
                            image.rgb[destinationIndex++] = source[sourceIndex++];
                            image.rgb[destinationIndex++] = source[sourceIndex++];
                            sourceIndex++;
                        }
                    }
                }
                string failure = save(path, image);
                if (failure == null)
                {
                    LogInfo("Saved the eye images " + readback.eyeWidth + "x" + height + ", side by side as " + width +
                            "x" + height + ", in " + stopwatch.ElapsedMilliseconds + " ms in the background");
                }
                return failure;
            });
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
