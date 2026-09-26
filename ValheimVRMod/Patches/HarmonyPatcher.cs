using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Patches
{
    class HarmonyPatcher
    {
        private static readonly Harmony harmony = new Harmony("com.valheimvrmod.patches");

        /// <summary>
        /// Every patch class that is installed when the VR assemblies may be missing from Managed.
        /// Anything absent from this list is VR only and is installed by <see cref="DoVrPatching"/>.
        ///
        /// This is a hardcoded list rather than an attribute scan because a scan means Assembly.GetTypes(),
        /// which throws ReflectionTypeLoadException in flat screen mode: the types whose fields are typed
        /// with SteamVR or FinalIK classes genuinely cannot load. Harmony swallows that exception but logs
        /// it, which is the error spam the flat screen package exists to avoid. Listing the types instead
        /// means flat screen mode never enumerates the assembly at all.
        ///
        /// Leaving a new patch class out of this list makes it VR only, which is the safe default: the
        /// cost of forgetting is a feature missing for flat screen players, not a crash. The classes here
        /// carry [FlatScreenSafe] and FlatScreenGuard checks that the two agree.
        /// </summary>
        private static readonly Type[] FlatScreenSafePatches =
        {
            typeof(PatchPlayerAwake),                               // attaches VRPlayerSync to remote players
            typeof(PatchSetRightHandEquipped),                      // remote weapon wield sync
            typeof(PatchSetLeftHandEquipped),                       // remote weapon wield sync
            typeof(PatchAttachItem),                                // remote left handed item placement
            typeof(PlayerOnDeathPatch),                             // tears down a remote player's VRIK
            typeof(GraphicsSettingsUpdateSettingAvailabilityPatch), // vanilla null crash workaround
            typeof(AoeOnCollisionEnterPatch),                       // vanilla collision handling replacement
            typeof(AoeOnCollisionStayPatch),                        // vanilla collision handling replacement
        };

        /// <summary>
        /// Whether the VR patches were installed. Note this is not simply the negation of
        /// VHVRConfig.NonVrPlayer(): when VR initialization fails after patching, the mod falls back to
        /// flat screen mode with the VR patches already in place, relying on their own runtime guards.
        /// </summary>
        public static bool VrPatchesInstalled { get; private set; }

        public static void DoFlatScreenSafePatching()
        {
            foreach (var type in FlatScreenSafePatches)
            {
                harmony.CreateClassProcessor(type).Patch();
            }
            DoSteamPatching();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void DoVrPatching()
        {
            // Equivalent to harmony.PatchAll() for everything the flat screen pass did not already install.
            // Patching a type without a Harmony attribute is a no-op, so enumerating the whole assembly is
            // what PatchAll does too.
            var alreadyPatched = new HashSet<Type>(FlatScreenSafePatches);
            foreach (var type in AccessTools.GetTypesFromAssembly(typeof(HarmonyPatcher).Assembly))
            {
                if (alreadyPatched.Contains(type))
                {
                    continue;
                }
                harmony.CreateClassProcessor(type).Patch();
            }
            VrPatchesInstalled = true;
        }

        private static void DoSteamPatching()
        {
            var loadAppIdMethod = AccessTools.Method("SteamManager:LoadAPPID");
            if (loadAppIdMethod != null)
            {
                harmony.Patch(loadAppIdMethod, prefix: new HarmonyMethod(typeof(SteamManager_LoadAppId_Patch), nameof(SteamManager_LoadAppId_Patch.Prefix)));
            }
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
