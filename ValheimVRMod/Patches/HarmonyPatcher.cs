using HarmonyLib;
using System;

namespace ValheimVRMod.Patches
{
    class HarmonyPatcher
    {
        private static readonly Harmony harmony = new Harmony("com.valheimvrmod.patches");
        public static void DoPatching()
        {
            harmony.PatchAll();

            if (AccessTools.TypeByName("SteamManager") != null)
            {
                DoSteamPatching();
            }
        }

        private static void DoSteamPatching()
        {
            harmony.Patch(AccessTools.Method("SteamManager:LoadAPPID"), prefix: new HarmonyMethod(typeof(SteamManager_LoadAppId_Patch), nameof(SteamManager_LoadAppId_Patch.Prefix)));
        }
        
        /** Example of how to patch hidden classes if needed
        private static void DoCustomPatching()
        {
            var type = AccessTools.TypeByName("MultipleDisplayUtilities");
            var method = AccessTools.Method("UnityEngine.UI.MultipleDisplayUtilities:GetMousePositionRelativeToMainDisplayResolution");
            System.Reflection.MethodInfo patchMethodPostfix = SymbolExtensions.GetMethodInfo((Vector2 __result) => Postfix(ref __result));
            harmony.Patch(method, postfix: new HarmonyMethod(patchMethodPostfix));
        }

        public static void Postfix(ref Vector2 __result)
        {
        }
        */
    }
}
