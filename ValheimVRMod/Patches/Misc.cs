using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using Unity.XR.OpenVR;
using HarmonyLib;
using Valve.VR;
using UnityEngine;
using ValheimVRMod.Utilities;

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

    // Disables camera culling for all materials currently loaded (trees, grass, etc.)
    [HarmonyPatch(typeof(Game), "Awake")]
    class Game_Awake_Patches
    {
        private static void Postfix()
        {
            if(!VHVRConfig.NonVrPlayer())
            {
                foreach(Material material in Resources.FindObjectsOfTypeAll<Material>())
                    material.SetInt("_CamCull", 0);
            }
        }
    }

    // This method updates FOV on a few cameras, but this
    // just throws an error while in VR and spams the logfile
    [HarmonyPatch(typeof(GameCamera), "UpdateCamera")]
    class GameCamera_UpdateCamera_Patch
    {

        private static MethodInfo fovMethod =
            AccessTools.Method(typeof(Camera), "set_fieldOfView");

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var original = new List<CodeInstruction>(instructions);
            for (int i = 0; i < original.Count; i++)
            {
                if (i + 4 < original.Count)
                {
                    // check if 4 instructions ahead is set_fieldOfView
                    var instruction = original[i + 4];
                    if (instruction.Calls(fovMethod))
                    {
                        // Replace these five instructions with NOPs
                        original[i].opcode = OpCodes.Nop;
                        original[i + 1].opcode = OpCodes.Nop;
                        original[i + 2].opcode = OpCodes.Nop;
                        original[i + 3].opcode = OpCodes.Nop;
                        original[i + 4].opcode = OpCodes.Nop;
                    }
                }
            }
            return original;
        }
    }

}
