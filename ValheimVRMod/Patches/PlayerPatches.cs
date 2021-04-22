using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace ValheimVRMod.Patches {


    // The UpdateHuds method is responsible for updating values
    // of any active Enemy huds (ie, health, level, alert status etc) as
    // well as removing any huds that should no longer active. Rather
    // than duplicate this logic for our mirror hud, we'll insert some
    // method calls to our EnemyHudManager class to update the values
    // at the right points. This requires the use of a transpiler to insert
    // the method calls at the right place in the code.
    [HarmonyPatch(typeof(Attack), "Start")]
    class Attack_Patch {

        private static MethodInfo GetStaminaUsageMethod =
            AccessTools.Method(typeof(Attack), "GetStaminaUsage");

        // Need to insert method calls to UpdateHudCoordinates, RemoveEnemyHud, UpdateHealth,
        // UpdateLevel, UpdateAlerted, and UpdateAware and SetActive.
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            var original = new List<CodeInstruction>(instructions);
            var patched = new List<CodeInstruction>();
            for (int i = 0; i < original.Count; i++) {
                var instruction = original[i];
                patched.Add(instruction);

                if (instruction.Calls(GetStaminaUsageMethod)) {
                    patched.Add(new CodeInstruction(OpCodes.Pop));
                    patched.Add(new CodeInstruction(OpCodes.Ldc_R4, 0.0f));
                }
            }
            return patched;
        }
    }
   
}
