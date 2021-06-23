using HarmonyLib;
using UnityEngine;
using ValheimVRMod.Scripts;

namespace ValheimVRMod.Patches
{
    class HarmonyPatcher
    {
        private static readonly Harmony harmony = new Harmony("com.valheimvrmod.patches");
        public static void DoPatching()
        {
            harmony.PatchAll();
        }

        public static void doNonVrPatching() {
            
            var method = AccessTools.Method(typeof(Player), "Start");
            System.Reflection.MethodInfo patchMethodPostfix = SymbolExtensions.GetMethodInfo((Player __instance) => playerPostfix(__instance));
            harmony.Patch(method, postfix: new HarmonyMethod(patchMethodPostfix));
            
            method = AccessTools.Method(typeof(VisEquipment), "SetLeftHandEquiped");
            System.Reflection.MethodInfo patchLeftEquipedPostfix = SymbolExtensions.GetMethodInfo((GameObject ___m_leftItemInstance) => equipPostfix(___m_leftItemInstance));
            harmony.Patch(method, postfix: new HarmonyMethod(patchLeftEquipedPostfix));
            
            method = AccessTools.Method(typeof(VisEquipment), "SetRightHandEquiped");
            System.Reflection.MethodInfo patchRightEquipedPostfix = SymbolExtensions.GetMethodInfo((GameObject ___m_rightItemInstance) => equipPostfix(___m_rightItemInstance));
            harmony.Patch(method, postfix: new HarmonyMethod(patchRightEquipedPostfix));
            
            
        }

        public static void playerPostfix(Player __instance) {
            if (__instance == Player.m_localPlayer) {
                return;
            }
            
            __instance.gameObject.AddComponent<VRPlayerSync>();
        }

        public static void equipPostfix(GameObject item) {
            if (item == null) {
                return;
            }

            MeshFilter meshFilter = item.GetComponentInChildren<MeshFilter>();

            if (meshFilter == null) {
                return;
            }

            Player player = item.GetComponentInParent<Player>();

            if (player == null || player == Player.m_localPlayer) {
                return;
            }
            
            player.GetComponent<VRPlayerSync>().currentLeftWeapon = meshFilter.gameObject;
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
