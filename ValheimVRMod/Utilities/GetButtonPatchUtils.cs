using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using ValheimVRMod.VRCore.UI;

namespace ValheimVRMod.Utilities
{
    // Util methods for pacthing and transpiling callers of ZInput.GetButton(), ZInput.GetButtonDown(), and ZInput.GetButtonUp()
    // in case the prefix or postfix of those methods get unpatched and stop working.
    public class GetButtonPatchUtils
    {
        public static readonly MethodInfo GetButtonOriginal =
             AccessTools.Method(typeof(ZInput), nameof(ZInput.GetButton), new[] { typeof(string) });
        public static readonly MethodInfo GetButtonDownOriginal =
             AccessTools.Method(typeof(ZInput), nameof(ZInput.GetButtonDown), new[] { typeof(string) });
        public static readonly MethodInfo GetButtonUpOriginal =
            AccessTools.Method(typeof(ZInput), nameof(ZInput.GetButtonUp), new[] { typeof(string) });

        public static bool GetButtonPatched(string name)
        {
            return (VRControls.mainControlsActive && VRControls.instance.GetButton(name)) ||
                ZInput.GetButton(name);
        }

        public static bool GetButtonDownPatched(string name)
        {
            return (VRControls.mainControlsActive && VRControls.instance.GetButtonDown(name)) ||
                ZInput.GetButtonDown(name);
        }

        public static bool GetButtonUpPatched(string name)
        {
            return (VRControls.mainControlsActive && VRControls.instance.GetButtonUp(name)) ||
                ZInput.GetButtonUp(name);
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var original = new List<CodeInstruction>(instructions);
            var patched = new List<CodeInstruction>();
            foreach (var instruction in original)
            {
                if (instruction.Calls(GetButtonOriginal))
                {
                    patched.Add(
                        CodeInstruction.Call(
                            typeof(GetButtonPatchUtils),
                            nameof(GetButtonPatched),
                            new[] { typeof(string) }));
                }
                else if (instruction.Calls(GetButtonDownOriginal))
                {
                    patched.Add(
                        CodeInstruction.Call(
                            typeof(GetButtonPatchUtils),
                            nameof(GetButtonDownPatched),
                            new[] { typeof(string) }));
                }
                else if (instruction.Calls(GetButtonUpOriginal))
                {
                    patched.Add(
                        CodeInstruction.Call(
                            typeof(GetButtonPatchUtils),
                            nameof(GetButtonUpPatched),
                            new[] { typeof(string) }));
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
