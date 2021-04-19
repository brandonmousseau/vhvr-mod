using UnityEngine;
using ValheimVRMod.VRCore.UI;
using HarmonyLib;

namespace ValheimVRMod.Patches
{
    // These patches are used to inject the VR inputs into the game's control system
    class ControlPatches
    {

        [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetButtonDown))]
        class ZInput_GetButtonDown_Patch
        {
            static void Postfix(string name, ref bool __result)
            {
                if (VRControls.instance != null)
                {
                    __result = __result || VRControls.instance.GetButtonDown(name);
                }
            }
        }

        [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetButtonUp))]
        class ZInput_GetButtonUp_Patch
        {
            static void Postfix(string name, ref bool __result)
            {
                if (VRControls.instance != null)
                {
                    __result = __result || VRControls.instance.GetButtonUp(name);
                }
            }
        }

        [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetButton))]
        class ZInput_GetButton_Patch
        {
            static void Postfix(string name, ref bool __result)
            {
                if (VRControls.instance != null)
                {
                    __result = __result || VRControls.instance.GetButton(name);
                }
            }
        }

        [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetJoyLeftStickX))]
        class ZInput_GetJoyLeftStickX_Patch
        {
            static void Postfix(ref float __result)
            {
                if (VRControls.instance != null)
                {
                    __result = __result + VRControls.instance.GetJoyLeftStickX();
                }
            }
        }

        [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetJoyLeftStickY))]
        class ZInput_GetJoyLeftStickY_Patch
        {
            static void Postfix(ref float __result)
            {
                if (VRControls.instance != null)
                {
                    __result = __result + VRControls.instance.GetJoyLeftStickY();
                }
            }
        }

        [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetJoyRightStickX))]
        class ZInput_GetJoyRightStickX_Patch
        {
            static void Postfix(ref float __result)
            {
                if (VRControls.instance != null)
                {
                    __result = __result + VRControls.instance.GetJoyRightStickX();
                }
            }
        }

        [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetJoyRightStickY))]
        class ZInput_GetJoyRightStickY_Patch
        {
            static void Postfix(ref float __result)
            {
                if (VRControls.instance != null)
                {
                    __result = __result + VRControls.instance.GetJoyRightStickY();
                }
            }
        }

    }
}
