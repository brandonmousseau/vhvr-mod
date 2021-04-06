using Unity.XR.OpenVR;
using HarmonyLib;
using Valve.VR;

namespace ValheimVRMod.Patches
{

    // This patch just forces IsUsingSteamVRInput to always return true.
    // Without this, there seems to be some problem with how the DLL namespacest
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

    // This method is responsible for pausing the game when the
    // steam dashboard is shown as well as sets render scale
    // to half while the dashboard is showing. Both of these
    // break the game - pausing ends up freezing most of the
    // game functions and the render scale doesn't get restored
    // properly. So this patch skips the function. Dashboard still
    // is functional.
    [HarmonyPatch(typeof(SteamVR_Render), "OnInputFocus")]
    class SteamVR_Render_OnInputFocus_Patch
    {
        public static bool Prefix()
        {
            return false;
        }
    }
}
