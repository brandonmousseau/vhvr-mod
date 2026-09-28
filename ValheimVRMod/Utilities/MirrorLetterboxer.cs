using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;
using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.Utilities
{
    // Shows the eye mirror of the Left, Right and OpenVR mirror modes at its true aspect, with black bars, instead
    // of stretched to the shape of the game window.
    //
    // The image is still the one the OpenVR display provider chooses for its mirror blit: Unity's automatic blit to
    // the window is turned off and the same blit is instead drawn into a centered rectangle at the end of the frame.
    // Whenever that cannot be done the automatic blit is turned back on, so the window never goes black, at worst
    // it is stretched again.
    class MirrorLetterboxer : MonoBehaviour
    {
        // Giving up for good after the automatic blit had to be restored this often, rather than flickering between
        // letterboxed and stretched frames on a setup where the manual blit only works now and then.
        private const int MAX_FALLBACKS = 5;

        private static MirrorLetterboxer instance;

        private readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
        private XRDisplaySubsystem display;
        private CommandBuffer commandBuffer;
        private bool suppressingAutomaticBlit;
        private int fallbackCount;
        private bool givenUp;
        private bool loggedSuccess;

        public static void EnsureCreated()
        {
            if (instance != null)
            {
                return;
            }
            GameObject gameObject = new GameObject("VHVRMirrorLetterboxer");
            DontDestroyOnLoad(gameObject);
            instance = gameObject.AddComponent<MirrorLetterboxer>();
        }

        void OnEnable()
        {
            StartCoroutine(drawAtEndOfFrames());
        }

        void OnDisable()
        {
            setAutomaticBlitSuppressed(false);
        }

        void OnDestroy()
        {
            commandBuffer?.Release();
            commandBuffer = null;
        }

        private IEnumerator drawAtEndOfFrames()
        {
            var endOfFrame = new WaitForEndOfFrame();
            while (true)
            {
                yield return endOfFrame;
                if (givenUp || VHVRConfig.NonVrPlayer() ||
                    VHVRConfig.GetMirrorViewMode() == Unity.XR.OpenVR.OpenVRSettings.MirrorViewModes.None)
                {
                    // The window is either not drawn from the eye mirror at all, or left to the automatic blit.
                    setAutomaticBlitSuppressed(false);
                    continue;
                }

                if (tryDrawLetterboxed())
                {
                    setAutomaticBlitSuppressed(true);
                }
                else if (suppressingAutomaticBlit)
                {
                    setAutomaticBlitSuppressed(false);
                    if (++fallbackCount >= MAX_FALLBACKS)
                    {
                        LogWarning("Could not letterbox the mirror view, showing it stretched to the window instead.");
                        givenUp = true;
                    }
                }
            }
        }

        private bool tryDrawLetterboxed()
        {
            XRDisplaySubsystem display = getDisplay();
            if (display == null ||
                !display.GetMirrorViewBlitDesc(null, out var desc, XRMirrorViewBlitMode.Default) ||
                desc.blitParamsCount < 1)
            {
                return false;
            }
            desc.GetBlitParameter(0, out var blit);
            Rect srcRect = blit.srcRect;
            Texture source = blit.srcTex;
            // Without a texture of its own (the OpenVR mode shows the compositor's mirror image) the source is still
            // an eye image of the size the eye textures have.
            float sourceWidth = source != null ? source.width : XRSettings.eyeTextureWidth;
            float sourceHeight = source != null ? source.height : XRSettings.eyeTextureHeight;
            float imageWidth = Mathf.Abs(srcRect.width) * sourceWidth;
            float imageHeight = Mathf.Abs(srcRect.height) * sourceHeight;
            if (imageWidth <= 0 || imageHeight <= 0 || Screen.width <= 0 || Screen.height <= 0)
            {
                return false;
            }

            Rect target = getLetterboxRect(imageWidth / imageHeight);
            bool drawn =
                source != null && blit.srcTexArraySlice == 0 && desc.blitParamsCount == 1 ?
                drawSourceDirectly(source, srcRect, target) :
                drawThroughNativeBlit(display, target);
            if (drawn && !loggedSuccess)
            {
                loggedSuccess = true;
                LogInfo("Letterboxing the mirror view: image " + imageWidth + "x" + imageHeight + " into " + target +
                        (source != null ? " from " + source.name : " through the native blit"));
            }
            return drawn;
        }

        // The largest rectangle of the image's aspect that fits in the window, centered, in pixels.
        private static Rect getLetterboxRect(float imageAspect)
        {
            float width = Screen.width;
            float height = Screen.height;
            if (width / height > imageAspect)
            {
                width = Mathf.Round(height * imageAspect);
            }
            else
            {
                height = Mathf.Round(width / imageAspect);
            }
            return new Rect(Mathf.Floor((Screen.width - width) / 2), Mathf.Floor((Screen.height - height) / 2), width, height);
        }

        // Draws the part of the source texture that the native blit would, into the letterbox rectangle. The render
        // target is put back afterwards, so that nothing rendered later inherits the window as its target.
        private static bool drawSourceDirectly(Texture source, Rect srcRect, Rect target)
        {
            RenderTexture previousTarget = RenderTexture.active;
            RenderTexture.active = null;
            GL.Viewport(new Rect(0, 0, Screen.width, Screen.height));
            GL.Clear(true, true, Color.black);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);
            // The pixel matrix counts rows from the top while the source rectangle counts them from the bottom, so the
            // rectangle is flipped vertically to keep the image upright.
            Rect flipped = new Rect(srcRect.x, srcRect.y + srcRect.height, srcRect.width, -srcRect.height);
            Graphics.DrawTexture(target, source, flipped, 0, 0, 0, 0);
            GL.PopMatrix();
            RenderTexture.active = previousTarget;
            return true;
        }

        // For a source the provider does not expose as a texture: the native blit fills a temporary texture of the
        // letterbox size, which is then drawn into the letterbox rectangle.
        private bool drawThroughNativeBlit(XRDisplaySubsystem display, Rect target)
        {
            RenderTexture temporary = RenderTexture.GetTemporary((int)target.width, (int)target.height, 0);
            try
            {
                if (commandBuffer == null)
                {
                    commandBuffer = new CommandBuffer { name = "VHVR mirror letterbox" };
                }
                commandBuffer.Clear();
                commandBuffer.SetRenderTarget(temporary);
                commandBuffer.ClearRenderTarget(true, true, Color.black);
                if (!display.AddGraphicsThreadMirrorViewBlit(commandBuffer, true, XRMirrorViewBlitMode.Default))
                {
                    return false;
                }
                Graphics.ExecuteCommandBuffer(commandBuffer);
                return drawSourceDirectly(temporary, new Rect(0, 0, 1, 1), target);
            }
            finally
            {
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private void setAutomaticBlitSuppressed(bool suppressed)
        {
            if (suppressed == suppressingAutomaticBlit)
            {
                return;
            }
            XRDisplaySubsystem display = getDisplay();
            if (display == null)
            {
                return;
            }
            display.SetPreferredMirrorBlitMode(suppressed ? XRMirrorViewBlitMode.None : XRMirrorViewBlitMode.Default);
            suppressingAutomaticBlit = suppressed;
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
