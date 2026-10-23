using System.Collections.Generic;
using UnityEngine;
using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.Utilities
{
    // [Graphics] LeanUiCameras: makes the two UI cameras (the GUI panel camera VRUICamera and WorldSpaceUiCamera),
    // which are copies of the VR camera drawing only their UI layers over the world once per eye, cheaper on the main
    // thread:
    // - no depth texture or motion vectors: CopyFrom carries over the VR camera's depthTextureMode, which in forward
    //   rendering costs a depth (and motion vector) pass of its own per eye; UI shaders read neither;
    // - no occlusion culling, also copied from the VR camera;
    // - no shadows while they render (QualitySettings.shadows off from their OnPreCull to their OnPostRender), which
    //   skips the shadow caster culling and shadow map setup per eye; UI is unlit.
    // Applied every frame from VRPlayer.enableCameras(), so it switches live; turned off, each camera gets back the
    // values it had before.
    static class LeanUiCameras
    {
        private struct Original
        {
            public DepthTextureMode depthTextureMode;
            public bool useOcclusionCulling;
        }

        private static readonly Dictionary<Camera, Original> originals = new Dictionary<Camera, Original>();
        private static readonly List<Camera> leanCameras = new List<Camera>();
        private static bool subscribed;
        // The camera whose render turned shadows off, and the setting to put back.
        private static Camera shadowsOffFor;
        private static ShadowQuality shadowsBefore;
        private static bool shadowsOff;

        public static void Apply(Camera camera)
        {
            if (camera == null)
            {
                return;
            }
            bool lean = VHVRConfig.LeanUiCameras();
            if (lean)
            {
                if (!originals.ContainsKey(camera))
                {
                    originals[camera] = new Original
                    {
                        depthTextureMode = camera.depthTextureMode,
                        useOcclusionCulling = camera.useOcclusionCulling
                    };
                    LogInfo("LeanUiCameras: " + camera.name + " depthTextureMode " + camera.depthTextureMode +
                            " -> None, occlusion culling " + camera.useOcclusionCulling + " -> False, shadows off while it renders.");
                    pruneDestroyed();
                }
                if (camera.depthTextureMode != DepthTextureMode.None)
                {
                    camera.depthTextureMode = DepthTextureMode.None;
                }
                if (camera.useOcclusionCulling)
                {
                    camera.useOcclusionCulling = false;
                }
                if (!leanCameras.Contains(camera))
                {
                    leanCameras.Add(camera);
                }
                subscribe();
            }
            else if (originals.TryGetValue(camera, out Original original))
            {
                camera.depthTextureMode = original.depthTextureMode;
                camera.useOcclusionCulling = original.useOcclusionCulling;
                originals.Remove(camera);
                leanCameras.Remove(camera);
                LogInfo("LeanUiCameras: " + camera.name + " back to depthTextureMode " + original.depthTextureMode +
                        ", occlusion culling " + original.useOcclusionCulling + ".");
            }
        }

        private static void pruneDestroyed()
        {
            var destroyed = new List<Camera>();
            foreach (Camera camera in originals.Keys)
            {
                if (camera == null)
                {
                    destroyed.Add(camera);
                }
            }
            foreach (Camera camera in destroyed)
            {
                originals.Remove(camera);
            }
            leanCameras.RemoveAll(c => c == null);
        }

        private static void subscribe()
        {
            if (subscribed)
            {
                return;
            }
            subscribed = true;
            Camera.onPreCull += onPreCull;
            Camera.onPostRender += onPostRender;
        }

        private static void onPreCull(Camera camera)
        {
            // A render that never reached its OnPostRender must not leave the world without shadows.
            restoreShadows();
            if (camera == null || !VHVRConfig.LeanUiCameras() || !leanCameras.Contains(camera))
            {
                return;
            }
            shadowsBefore = QualitySettings.shadows;
            if (shadowsBefore == ShadowQuality.Disable)
            {
                return;
            }
            shadowsOffFor = camera;
            shadowsOff = true;
            QualitySettings.shadows = ShadowQuality.Disable;
        }

        private static void onPostRender(Camera camera)
        {
            if (shadowsOff && camera == shadowsOffFor)
            {
                restoreShadows();
            }
        }

        private static void restoreShadows()
        {
            if (!shadowsOff)
            {
                return;
            }
            shadowsOff = false;
            shadowsOffFor = null;
            QualitySettings.shadows = shadowsBefore;
        }
    }
}
