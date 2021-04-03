using Unity.XR.OpenVR;
using HarmonyLib;

namespace ValheimVRMod.Patches
{

    // This patch just forces IsUsingSteamVRInput to always return true.
    // Without this, there seems to be some problem with how the DLL namespaces
    // are loaded when using certain other mods. Normally this method will result
    // in a call to the Assembly.GetTypes method, but for whatever reason, this
    // ends up throwing an exception and crashes the mod. The mod I know that causes
    // this conflict is ConfigurationManager, which is installed by default for
    // anyone using Vortex mod installer, but it likely will happen for others too.
    [HarmonyPatch(typeof(OpenVRHelpers), nameof(OpenVRHelpers.IsUsingSteamVRInput))]
    class OpenVRHelpers_IsUsingSteamVRInput_Patch
    {
        public static bool Prefix(ref bool __result)
        {
            __result = true;
            return false;
        }
    }
}
