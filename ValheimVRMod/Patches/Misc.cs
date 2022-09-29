using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using System.IO;
using System;
using Unity.XR.OpenVR;
using HarmonyLib;
using Valve.VR;
using UnityEngine;
using ValheimVRMod.Utilities;

using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.Patches
{

    // STEAM ONLY
    // Ensure there are no problems parsing the environment variable. One user had some issue
    // where the environment variable had an invalid format, causing the steam app id to not load
    // properly. Normally without the mod installed, the game uses steam_appid.txt, but SteamVR
    // is setting the environment variable. This patch just does some additional error checking
    // instead and falls back to the steam_appid.txt file if there is a problem with the
    // environment variable.
    class SteamManager_LoadAppId_Patch
    {
        public static bool Prefix(ref uint __result)
        {
            string environmentVariable = Environment.GetEnvironmentVariable("SteamAppId");
            if (environmentVariable != null)
            {
                ZLog.Log(string.Concat("Using environment steamid ", environmentVariable));
                try
                {
                    __result = uint.Parse(environmentVariable);
                    return false;
                } catch
                {
                    LogError("Error parsing 'SteamAppId' environment variable. Using steam_appid.txt instead.");
                }
            }
            try
            {
                string str = File.ReadAllText("steam_appid.txt");
                ZLog.Log("Using steam_appid.txt");
                __result = uint.Parse(str);
            }
            catch
            {
                ZLog.LogWarning("Failed to find APPID");
                __result = (uint)0;
            }
            return false;
        }
    }

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

    // Removes calls to set the targetFrameRate of the game
    [HarmonyPatch(typeof(Game), nameof(Game.Update))]
    [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.Update))]
    class Application_FrameRate_Patch
    {
        private static void Nop(int ignore) { }
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            if (VHVRConfig.NonVrPlayer()) return instructions;

            var original = new List<CodeInstruction>(instructions);
            for (int i = 0; i < original.Count; i++)
            {
                if (original[i].Calls(AccessTools.Method(typeof(Application), "set_targetFrameRate", new[] { typeof(Int32) })))
                {
                    var changed = CodeInstruction.Call(typeof(Application_FrameRate_Patch), nameof(Nop), new[] { typeof(Int32) });
                    changed.labels = original[i].labels;
                    original[i] = changed;
                }
            }
            return original;
        }
    }

    // Prevents the character from spinning while paused
    [HarmonyPatch(typeof(Game), nameof(Game.UpdatePause))]
    class Prevent_Pause_Character_Spin_Patch
    {

        private static MethodInfo rotateMethod = AccessTools.Method(typeof(Transform), nameof(Transform.Rotate), new Type[] { typeof(Vector3), typeof(float) });
        private static MethodInfo setLookDirMethod = AccessTools.Method(typeof(Character), nameof(Player.SetLookDir));

        private static void FakeRotate(Transform t, Vector3 axis, float angle) {}

        private static void FakeSetLookDir(Character c, Vector3 v, float f) {}

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var original = new List<CodeInstruction>(instructions);
            var patched = new List<CodeInstruction>();
            for (int i = 0; i < original.Count; i++)
            {
                var instruction = original[i];
                if (i == 0)
                {
                    patched.Add(instruction);
                    continue;
                }
                if (instruction.Calls(rotateMethod) && original[i - 1].opcode == OpCodes.Mul)
                {
                    patched.Add(CodeInstruction.Call(typeof(Prevent_Pause_Character_Spin_Patch), nameof(Prevent_Pause_Character_Spin_Patch.FakeRotate)));
                }
                else if (instruction.Calls(setLookDirMethod))
                {
                    patched.Add(CodeInstruction.Call(typeof(Prevent_Pause_Character_Spin_Patch), nameof(Prevent_Pause_Character_Spin_Patch.FakeSetLookDir)));
                }
                else
                {
                    patched.Add(instruction);
                }
            }
            return patched;
        }
    }

}
