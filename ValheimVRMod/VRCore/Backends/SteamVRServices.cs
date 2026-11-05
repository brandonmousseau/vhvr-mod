using UnityEngine;
using Valve.VR;

namespace ValheimVRMod.VRCore.Backends
{
    public sealed partial class SteamVRBackend
    {
        public IVRRigBackend Rig { get; } = new SteamVRRigBackend();
        public bool InitializeInput() => InitializeSteamVR();
        public IVRKeyboardBackend Keyboard { get; } = new SteamVRKeyboardBackend();
        CVROverlay nativeOverlay;
        IVROverlayBackend overlay;
        public IVROverlayBackend Overlay
        {
            get
            {
                var current = OpenVR.Overlay;
                if (!ReferenceEquals(current, nativeOverlay))
                {
                    nativeOverlay = current;
                    overlay = current == null ? null : new SteamVROverlayBackend(current);
                }
                return overlay;
            }
        }
        public void Fade(Color color, float duration, bool fadeOverlay = false) => SteamVRAssets.Fade(color, duration, fadeOverlay);
        public Shader GetShader(string name) => SteamVRAssets.GetShader(name);
        public bool LoadShaders(string path) => SteamVRAssets.LoadShaders(path);
    }

    // SteamVR plugin helpers that never reach the OpenVR runtime, so that a backend for another runtime can use them
    // too: ShaderLoader only reads an asset bundle, and SteamVR_Fade (on the rig's camera) only listens for a managed
    // event and draws a quad, unless asked to fade the SteamVR overlay as well.
    static class SteamVRAssets
    {
        public static void Fade(Color color, float duration, bool fadeOverlay) => SteamVR_Fade.Start(color, duration, fadeOverlay);
        public static Shader GetShader(string name) => ShaderLoader.GetShader(name);
        public static bool LoadShaders(string path) => ShaderLoader.Initialize(path);
    }
}
