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
        public void Fade(Color color, float duration, bool fadeOverlay = false) => SteamVR_Fade.Start(color, duration, fadeOverlay);
        public Shader GetShader(string name) => ShaderLoader.GetShader(name);
        public bool LoadShaders(string path) => ShaderLoader.Initialize(path);
    }
}
