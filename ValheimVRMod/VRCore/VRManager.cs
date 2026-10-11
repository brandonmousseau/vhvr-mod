using ValheimVRMod.VRCore.Backends;

namespace ValheimVRMod.VRCore
{
    // Preserve the original entry points and call order; the provider owns the SDK.
    class VRManager
    {
        public static bool InitializeVR() => VRBackend.Active.InitializeVR() && InitializeActions();
        public static bool StartVR() => VRBackend.Active.StartVR();
        public static bool InitializeSteamVR() => VRBackend.Active.InitializeInput() && InitializeActions();

        // Only once the input system is up: actions resolved before that are not backed by the runtime.
        private static bool InitializeActions()
        {
            VRInputActions.Initialize(VRBackend.Active.Input);
            return true;
        }
        public static void UpdateMirrorViewMode() => VRBackend.Active.UpdateMirrorViewMode();
        public static void UpdateMirrorSetup() => VRBackend.Active.UpdateMirrorSetup();
        public static void tryRecenter() => VRBackend.Active.tryRecenter();
    }
}
