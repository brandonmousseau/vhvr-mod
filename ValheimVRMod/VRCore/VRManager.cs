using ValheimVRMod.VRCore.Backends;

namespace ValheimVRMod.VRCore
{
    // Preserve the original entry points and call order; the provider owns the SDK.
    class VRManager
    {
        public static bool InitializeVR() => VRBackend.Active.InitializeVR();
        public static bool StartVR() => VRBackend.Active.StartVR();
        public static bool InitializeSteamVR() => VRBackend.Active.InitializeInput();
        public static void UpdateMirrorViewMode() => VRBackend.Active.UpdateMirrorViewMode();
        public static void UpdateMirrorSetup() => VRBackend.Active.UpdateMirrorSetup();
        public static void tryRecenter() => VRBackend.Active.tryRecenter();
    }
}
