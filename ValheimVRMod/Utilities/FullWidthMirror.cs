using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;
using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.Utilities
{
    // Draws the flat screen of the FullWidthRight and FullWidthLeft mirror modes: one eye's image undistorted,
    // across the full width of the window and cropped at the top and bottom, instead of the eye mirror stretched to
    // the shape of the window. That keeps the eye's whole horizontal field of view, which is wider than the native
    // mirror's, and fills the window (see getFullWidthLayout).
    //
    // Like the flat screen camera modes, these modes turn the provider's eye mirror off and own the window with a
    // camera of their own. That camera renders no scene though: it only clears the window to black and then copies
    // the finished eye image into the window. Rendering after every stereo camera, it gets the image with
    // the post effects, hands and world space GUI already in it, just as the eye mirror would show it. Its cost is
    // about that of the eye mirror blit it replaces, and, being a camera, it draws before any GUI rather than over it.
    //
    // The SBS mode draws both eye images the same way, each whole and at its own aspect, next to each other with the
    // left eye on the left. The pair is letterboxed as one (see getSideBySideLayout). That costs a second copy but
    // no more pixels written than a full width eye, as the two halves together cover at most the window.
    //
    // In the None mode it only clears the window. Nothing else draws to it then: once the provider has shown its
    // mirror, turning the mirror off merely stops drawing it, which would leave its last image frozen in the window.
    class FullWidthMirror : MonoBehaviour
    {
        // After every stereo camera (the highest of which have depth 4), so that the eye image is complete.
        private const float CAMERA_DEPTH = 100;

        private static FullWidthMirror instance;

        private readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
        private XRDisplaySubsystem display;
        private Camera mirrorCamera;
        private CommandBuffer commandBuffer;
        // Per eye (left, right), the shown part of the eye image scaled to its size in the window, for
        // Graphics.DrawTexture, which takes a texture rather than the render target identifier the display hands out
        // for the eye.
        private readonly RenderTexture[] scaledEyes = new RenderTexture[2];
        // The layout last logged, so that each mode logs once when it starts showing.
        private string loggedLayout;
        private bool loggedFailure;

        public static void EnsureCreated()
        {
            if (instance != null)
            {
                return;
            }
            // Built on an inactive object so that the camera is never a stereo camera, which a camera added to an
            // active object would be until stereoTargetEye is set.
            GameObject gameObject = new GameObject("VHVRFullWidthMirror");
            gameObject.SetActive(false);
            DontDestroyOnLoad(gameObject);
            Camera camera = gameObject.AddComponent<Camera>();
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.targetTexture = null;
            camera.depth = CAMERA_DEPTH;
            // Renders nothing of the scene, and so needs none of the buffers or passes that rendering one would.
            camera.cullingMask = 0;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.renderingPath = RenderingPath.Forward;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.useOcclusionCulling = false;
            camera.depthTextureMode = DepthTextureMode.None;
            camera.enabled = false;
            instance = gameObject.AddComponent<FullWidthMirror>();
            instance.mirrorCamera = camera;
            gameObject.SetActive(true);
        }

        void Update()
        {
            mirrorCamera.enabled =
                !VHVRConfig.NonVrPlayer() && (VHVRConfig.UseFullWidthMirror(out _) || VHVRConfig.UseSideBySideMirror() || VHVRConfig.UseNoFlatscreenView());
        }

        void OnDestroy()
        {
            commandBuffer?.Release();
            commandBuffer = null;
            releaseScaledEye(0);
            releaseScaledEye(1);
        }

        void OnPostRender()
        {
            string failure;
            if (VHVRConfig.UseSideBySideMirror())
            {
                failure = tryDrawSideBySide();
            }
            else if (VHVRConfig.UseFullWidthMirror(out XRNode eye))
            {
                failure = tryDrawEye(eye);
            }
            else
            {
                return;
            }
            if (failure != null && !loggedFailure)
            {
                loggedFailure = true;
                LogWarning("Cannot show the eye image, leaving the window black: " + failure);
            }
        }

        // Returns why the eye image could not be drawn, or null once it is.
        private string tryDrawEye(XRNode eye)
        {
            string failure = tryGetEyePass(eye, out var pass);
            if (failure != null)
            {
                return failure;
            }
            RenderTextureDescriptor eyeDesc = pass.renderTargetDesc;
            getFullWidthLayout((float)eyeDesc.width / eyeDesc.height, out Rect target, out float sourceHeight);
            int slot = eyeSlot(eye);
            drawEye(pass, slot, target, sourceHeight);
            // The other eye's copy is only used by the SBS mode.
            releaseScaledEye(1 - slot);

            logLayoutOnce("Showing the " + eye + " image " + eyeDesc.width + "x" + eyeDesc.height + ", " +
                          sourceHeight * 100 + "% of its height, in " + target);
            return null;
        }

        // Returns why the eye images could not be drawn, or null once they are.
        private string tryDrawSideBySide()
        {
            string failure = tryGetEyePass(XRNode.LeftEye, out var leftPass);
            if (failure != null)
            {
                return failure;
            }
            failure = tryGetEyePass(XRNode.RightEye, out var rightPass);
            if (failure != null)
            {
                return failure;
            }
            // Both eyes share one resolution, so the left eye's aspect stands for the pair.
            RenderTextureDescriptor eyeDesc = leftPass.renderTargetDesc;
            getSideBySideLayout((float)eyeDesc.width / eyeDesc.height, out Rect leftTarget, out Rect rightTarget);
            drawEye(leftPass, 0, leftTarget, 1);
            drawEye(rightPass, 1, rightTarget, 1);

            logLayoutOnce("Showing both eye images " + eyeDesc.width + "x" + eyeDesc.height + " side by side in " +
                          leftTarget + " and " + rightTarget);
            return null;
        }

        // Returns why the eye's render pass cannot be shown, or null once pass holds it.
        private string tryGetEyePass(XRNode eye, out XRDisplaySubsystem.XRRenderPass pass)
        {
            pass = default;
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
            display.GetRenderPass(eyeSlot(eye), out pass);
            RenderTextureDescriptor eyeDesc = pass.renderTargetDesc;
            if (eyeDesc.width <= 0 || eyeDesc.height <= 0 || Screen.width <= 0 || Screen.height <= 0)
            {
                return "eye texture " + eyeDesc.width + "x" + eyeDesc.height + ", window " + Screen.width + "x" + Screen.height;
            }
            return null;
        }

        // The render pass, and scaledEyes slot, of an eye.
        private static int eyeSlot(XRNode eye)
        {
            return eye == XRNode.LeftEye ? 0 : 1;
        }

        // Draws the given fraction of the eye image's height, centered, into target in the window.
        private void drawEye(XRDisplaySubsystem.XRRenderPass pass, int slot, Rect target, float sourceHeight)
        {
            RenderTexture scaledEye = ensureScaledEye(slot, (int)target.width, (int)target.height, pass.renderTargetDesc.sRGB);
            // Executing the copy moves the render target, so the camera's own is put back before drawing into it.
            RenderTexture previousTarget = RenderTexture.active;
            if (commandBuffer == null)
            {
                commandBuffer = new CommandBuffer { name = "VHVR full width mirror" };
            }
            commandBuffer.Clear();
            // Copies the horizontal band of the eye image that is shown, centered vertically.
            commandBuffer.Blit(
                pass.renderTarget, scaledEye, new Vector2(1, sourceHeight), new Vector2(0, (1 - sourceHeight) / 2));
            Graphics.ExecuteCommandBuffer(commandBuffer);
            RenderTexture.active = previousTarget;

            GL.PushMatrix();
            GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);
            // The pixel matrix counts rows from the top while textures count them from the bottom, so the source
            // rectangle is flipped vertically to keep the image upright.
            Graphics.DrawTexture(target, scaledEye, new Rect(0, 1, 1, -1), 0, 0, 0, 0);
            GL.PopMatrix();
        }

        private void logLayoutOnce(string layout)
        {
            if (layout != loggedLayout)
            {
                loggedLayout = layout;
                LogInfo(layout);
            }
        }

        // Where the image goes in the window, in pixels, and which fraction of its height, centered, is shown there.
        // The image keeps its full width, the whole horizontal field of view, and is cropped at the top and bottom
        // to the window's aspect. Only a window narrower than the image leaves nothing to crop: the whole image is
        // then shown across the window's width, between bars at the top and bottom.
        private static void getFullWidthLayout(float imageAspect, out Rect target, out float sourceHeight)
        {
            float windowAspect = (float)Screen.width / Screen.height;
            if (windowAspect >= imageAspect)
            {
                target = new Rect(0, 0, Screen.width, Screen.height);
                sourceHeight = imageAspect / windowAspect;
                return;
            }
            float height = Mathf.Round(Screen.width / imageAspect);
            target = new Rect(0, Mathf.Floor((Screen.height - height) / 2), Screen.width, height);
            sourceHeight = 1;
        }

        // Where the two whole eye images go in the window, in pixels: next to each other, each at its own aspect,
        // with the pair as large as fits and centered, between bars at the top and bottom or at the sides.
        private static void getSideBySideLayout(float imageAspect, out Rect left, out Rect right)
        {
            float windowAspect = (float)Screen.width / Screen.height;
            float width;
            float height;
            if (windowAspect >= imageAspect * 2)
            {
                height = Screen.height;
                width = Mathf.Floor(height * imageAspect);
            }
            else
            {
                width = Mathf.Floor(Screen.width / 2f);
                height = Mathf.Round(width / imageAspect);
            }
            float x = Mathf.Floor((Screen.width - width * 2) / 2);
            float y = Mathf.Floor((Screen.height - height) / 2);
            left = new Rect(x, y, width, height);
            right = new Rect(x + width, y, width, height);
        }

        // Keeps the eye's color encoding so that the copy changes only the size of the image.
        private RenderTexture ensureScaledEye(int slot, int width, int height, bool sRGB)
        {
            RenderTexture scaledEye = scaledEyes[slot];
            if (scaledEye != null && scaledEye.width == width && scaledEye.height == height && scaledEye.sRGB == sRGB)
            {
                return scaledEye;
            }
            releaseScaledEye(slot);
            scaledEye = new RenderTexture(
                width, height, 0, RenderTextureFormat.ARGB32, sRGB ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
            scaledEye.name = "VHVR full width eye";
            scaledEye.filterMode = FilterMode.Bilinear;
            scaledEye.Create();
            scaledEyes[slot] = scaledEye;
            return scaledEye;
        }

        private void releaseScaledEye(int slot)
        {
            if (scaledEyes[slot] != null)
            {
                scaledEyes[slot].Release();
                Destroy(scaledEyes[slot]);
                scaledEyes[slot] = null;
            }
        }

        private XRDisplaySubsystem getDisplay()
        {
            if (display != null && display.running)
            {
                return display;
            }
            display = null;
            SubsystemManager.GetInstances(displays);
            foreach (XRDisplaySubsystem candidate in displays)
            {
                if (candidate.running)
                {
                    display = candidate;
                    break;
                }
            }
            return display;
        }
    }
}
