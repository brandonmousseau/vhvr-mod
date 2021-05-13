using UnityEngine;
using System.Collections.Generic;
using ValheimVRMod.VRCore.UI;
using HarmonyLib;
using System.Reflection;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Patches
{
    // These patches are used to inject the VR inputs into the game's control system
    class ControlPatches
    {

        [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetButtonDown))]
        class ZInput_GetButtonDown_Patch
        {

            static bool Prefix(string name, ref bool __result)
            {
                // Need to bypass original function for any required ZInputs that begin
                // with "Joy" to ensure the VR Controls still work when
                // Gamepad is disabled.
                if (VRControls.mainControlsActive && !ZInput.IsGamepadEnabled() && isUsedJoyZinput(name))
                {
                    __result = VRControls.instance.GetButtonDown(name);
                    return false;
                }
                return true;
            }

            private static bool isUsedJoyZinput(string name)
            {
                return name == "JoyMenu" ||
                       name == "JoyPlace" ||
                       name == "JoyRemove";
            }

            static void Postfix(string name, ref bool __result)
            {
                if (VRControls.mainControlsActive)
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
                if (VRControls.mainControlsActive)
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
                if (VRControls.mainControlsActive)
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
                if (VRControls.mainControlsActive)
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
                if (VRControls.mainControlsActive)
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
                // dont patch, if quickswitch is active
                if (VRPlayer.quickSwitch != null && VRPlayer.quickSwitch.activeSelf) {
                    return;
                }
                
                if (VRControls.mainControlsActive)
                {
                    __result = __result + VRControls.instance.GetJoyRightStickX();
                }
            }
        }

        [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetJoyRightStickY))]
        class ZInput_GetJoyRightStickY_Patch {

            public static bool isCrouching;
            public static bool isRunning;
            
            static void Postfix(ref float __result)
            {

                // dont patch, if quickswitch is active
                if (VRPlayer.quickSwitch != null && VRPlayer.quickSwitch.activeSelf) {
                    return;
                }
                
                if (VRControls.mainControlsActive) {
                    
                    var joystick = VRControls.instance.GetJoyRightStickY();

                    isRunning = joystick < -0.3f;
                    isCrouching = joystick > 0.7f;
                    
                    __result = __result + joystick;

                }
            }
        }

        // Patch to let us use custom hotbar selection inputs
        [HarmonyPatch(typeof(HotkeyBar), "Update")]
        class HotkeyBar_Update_Patch
        {

            public static int getElementsCount(HotkeyBar instance)
            {
                var fieldInfo = AccessTools.Field(typeof(HotkeyBar), "m_elements");
                object ElementDataList = fieldInfo.GetValue(instance);
                return (int)ElementDataList.GetType().GetProperty("Count").GetValue(ElementDataList, null);
            }

            public static void Prefix(HotkeyBar __instance, ref int ___m_selected)
            {
                Player mLocalPlayer = Player.m_localPlayer;
                if (VRControls.mainControlsActive && mLocalPlayer && !InventoryGui.IsVisible() &&
                    !Menu.IsVisible() && !GameCamera.InFreeFly() && VRControls.instance != null)
                {
                    int hotbarUpdate = VRControls.instance.getHotbarScrollUpdate();
                    if (hotbarUpdate == 1)
                    {
                        ___m_selected = Mathf.Max(0, ___m_selected - 1);
                    } else if (hotbarUpdate == -1)
                    {
                        int count = getElementsCount(__instance);
                        ___m_selected = Mathf.Min(count - 1, ___m_selected + 1);
                    }
                    if (VRControls.instance.getHotbarUseInput())
                    {
                        mLocalPlayer.UseHotbarItem(___m_selected + 1);
                    }
                }
            }
        }

        // Need to override the gamepad active check to allow the
        // VR controls to work with hotbar selection.
        [HarmonyPatch(typeof(HotkeyBar), "UpdateIcons")]
        class HotkeyBar_UpdateIcons_Patch
        {

            private static MethodInfo gamepadActive =
                AccessTools.Method(typeof(ZInput), nameof(ZInput.IsGamepadActive));

            static bool PatchedGamepadActive(bool realActive)
            {
                if (VRControls.mainControlsActive)
                {
                    // If VR Controls are active, force this to always be true.
                    return true;
                }
                return realActive;
            }

            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var original = new List<CodeInstruction>(instructions);
                var patched = new List<CodeInstruction>();
                foreach (var instruction in original)
                {
                    patched.Add(instruction);
                    if (instruction.Calls(gamepadActive))
                    {
                        patched.Add(CodeInstruction.Call(typeof(HotkeyBar_UpdateIcons_Patch), nameof(PatchedGamepadActive)));
                    }
                }
                return patched;
            }
        }

        // Patch to enable rotation of pieces using VR control actions
        [HarmonyPatch(typeof(Player), "UpdatePlacement")]
        class Player_Update_Placement_PieceRotationPatch
        {

            static void Postfix(Player __instance, bool takeInput, ref int ___m_placeRotation)
            {
                if (!VRControls.mainControlsActive || __instance != Player.m_localPlayer || !takeInput || !__instance.InPlaceMode() || Hud.IsPieceSelectionVisible())
                {
                    return;
                }
                ___m_placeRotation += VRControls.instance.getPieceRotation();
            }

        }

        // If using VR controls, disable the joystick for the purposes
        // of moving the map around since that will be done with
        // simulated mouse cursor click and drag via laser pointer.
        [HarmonyPatch(typeof(Minimap), "UpdateMap")]
        class Minimap_UpdateMap_MapTranslationPatch
        {

            private static MethodInfo getJoyLeftStickX =
                AccessTools.Method(typeof(ZInput), nameof(ZInput.GetJoyLeftStickX));
            private static MethodInfo getJoyLeftStickY =
    AccessTools.Method(typeof(ZInput), nameof(ZInput.GetJoyLeftStickY));

            private static float getJoyLeftStickXPatched()
            {
                if (VRControls.mainControlsActive)
                {
                    return 0.0f;
                } else
                {
                    return ZInput.GetJoyLeftStickX();
                }
            }

            private static float getJoyLeftStickYPatched()
            {
                if (VRControls.mainControlsActive)
                {
                    return 0.0f;
                }
                else
                {
                    return ZInput.GetJoyLeftStickY();
                }
            }

            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var original = new List<CodeInstruction>(instructions);
                var patched = new List<CodeInstruction>();
                foreach (var instruction in original)
                {
                    if (instruction.Calls(getJoyLeftStickX))
                    {
                        patched.Add(CodeInstruction.Call(typeof(Minimap_UpdateMap_MapTranslationPatch),
                            nameof(getJoyLeftStickXPatched)));
                    } else if (instruction.Calls(getJoyLeftStickY))
                    {
                        patched.Add(CodeInstruction.Call(typeof(Minimap_UpdateMap_MapTranslationPatch),
                            nameof(getJoyLeftStickYPatched)));
                    } else
                    {
                        patched.Add(instruction);
                    }
                }
                return patched;
            }
        }
        
        [HarmonyPatch(typeof(Player), "SetControls")]
        class PlayerSetControlsPatch
        {

            static void Prefix(Player __instance, ref bool attack, ref bool attackHold, ref bool block, ref bool blockHold, ref bool crouch, ref bool run)
            {
                if (!VRControls.mainControlsActive || __instance != Player.m_localPlayer) {
                    return;
                }

                run = ZInput_GetJoyRightStickY_Patch.isRunning;
                crouch = ZInput_GetJoyRightStickY_Patch.isCrouching;
                
                if (! VRPlayer.isUsingBow()) {
                    return;
                }
                
                if (BowManager.c_aborting) {
                    block = true;
                    blockHold = true;
                    BowManager.c_aborting = false;
                } else if (BowManager.c_startedPulling) {
                    attack = true;
                    BowManager.c_startedPulling = false;
                } else if (BowManager.c_isPulling) {
                    attackHold = true;
                }
            }
        }
    }
}
