using UnityEngine;

namespace ValheimVRMod.Utilities
{
    // Lets work that is done in OnRenderObject, which Unity calls after every camera render (in VR about ten times a
    // frame: each eye of the VR, UI, world space UI and hands cameras, plus the GUI and mirror cameras), be done once
    // per frame instead.
    //
    // Such work is done in OnRenderObject because only there the hand bones are where they are drawn: after the
    // animation, VRIK (LateUpdate) and the controller poses (Application.onBeforeRender). All camera renders of a frame
    // come after those, and nothing moves the hands between them, so the first render's result is the frame's result.
    // A camera rendered by a script outside the render phase (Camera.Render() from Update or LateUpdate, before the
    // poses are final) does not count as the first one: it still runs the work, as before, and the render phase that
    // follows runs it again.
    static class RenderPhase
    {
        private static int beforeRenderFrame = -1;
        private static bool subscribed;

        // Call once at startup, so that the first frame's render phase is known as well.
        public static void Init()
        {
            if (subscribed)
            {
                return;
            }
            subscribed = true;
            Application.onBeforeRender += onBeforeRender;
        }

        private static void onBeforeRender()
        {
            beforeRenderFrame = Time.frameCount;
        }

        // Whether the OnRenderObject work tracked by lastFrame should run now. False only when it already ran in this
        // frame's render phase and RenderWorkOncePerFrame is on.
        public static bool ShouldRun(ref int lastFrame)
        {
            Init();
            if (!VHVRConfig.RenderWorkOncePerFrame())
            {
                return true;
            }
            int frame = Time.frameCount;
            if (beforeRenderFrame != frame)
            {
                // Not in the render phase (or its start was not seen): run as every call did before.
                return true;
            }
            if (lastFrame == frame)
            {
                return false;
            }
            lastFrame = frame;
            return true;
        }
    }
}
