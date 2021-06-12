using System.Collections.Generic;
using ValheimVRMod.VRCore.UI;
using HarmonyLib;
using System.Reflection;
using ValheimVRMod.Scripts;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Patches {
    // These patches are used to inject the VR inputs into the game's control system

    [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetButtonDown))]
    class ZInput_GetButtonDown_Patch {
        static bool Prefix(string name, ref bool __result) {
            // Need to bypass original function for any required ZInputs that begin
            // with "Joy" to ensure the VR Controls still work when
            // Gamepad is disabled.
            if (VRControls.mainControlsActive && !ZInput.IsGamepadEnabled() && isUsedJoyZinput(name)) {
                __result = VRControls.instance.GetButtonDown(name);
                return false;
            }

            return true;
        }

        private static bool isUsedJoyZinput(string name) {
            return name == "JoyMenu" ||
                   name == "JoyPlace" ||
                   name == "JoyPlace" ||
                   name == "JoyRemove";
        }

        static void Postfix(string name, ref bool __result) {
            if (VRControls.mainControlsActive) {
                __result = __result || VRControls.instance.GetButtonDown(name);
            }
        }
    }

    [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetButtonUp))]
    class ZInput_GetButtonUp_Patch {
        static void Postfix(string name, ref bool __result) {
            if (VRControls.mainControlsActive) {
                __result = __result || VRControls.instance.GetButtonUp(name);
            }
        }
    }

    [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetButton))]
    class ZInput_GetButton_Patch {
        static void Postfix(string name, ref bool __result) {
            if (VRControls.mainControlsActive) {
                __result = __result || VRControls.instance.GetButton(name);
            }
        }
    }

    [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetJoyLeftStickX))]
    class ZInput_GetJoyLeftStickX_Patch {
        static void Postfix(ref float __result) {
            // dont patch, if quickswitch is active
            if (StaticObjects.quickSwitch != null && StaticObjects.quickSwitch.activeSelf) {
                return;
            }
            
            if (VRControls.mainControlsActive) {
                __result = __result + VRControls.instance.GetJoyLeftStickX();
            }
        }
    }

    [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetJoyLeftStickY))]
    class ZInput_GetJoyLeftStickY_Patch {
        static void Postfix(ref float __result) {
            // dont patch, if quickswitch is active
            if (StaticObjects.quickSwitch != null && StaticObjects.quickSwitch.activeSelf) {
                return;
            }
            
            if (VRControls.mainControlsActive) {
                __result = __result + VRControls.instance.GetJoyLeftStickY();
            }
        }
    }

    [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetJoyRightStickX))]
    class ZInput_GetJoyRightStickX_Patch {
        static void Postfix(ref float __result) {
            // dont patch, if quickswitch is active
            if (StaticObjects.quickSwitch != null && StaticObjects.quickSwitch.activeSelf) {
                return;
            }

            if (VRControls.mainControlsActive) {
                __result = __result + VRControls.instance.GetJoyRightStickX();
            }
        }
    }

    [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetJoyRightStickY))]
    class ZInput_GetJoyRightStickY_Patch {
        public static bool isCrouching;
        public static bool isRunning;

        static void Postfix(ref float __result) {
            // dont patch, if quickswitch is active
            if (StaticObjects.quickSwitch != null && StaticObjects.quickSwitch.activeSelf) {
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

    // Patch to enable rotation of pieces using VR control actions
    [HarmonyPatch(typeof(Player), "UpdatePlacement")]
    class Player_Update_Placement_PieceRotationPatch {
        static void Postfix(Player __instance, bool takeInput, ref int ___m_placeRotation) {
            if (!VRControls.mainControlsActive || __instance != Player.m_localPlayer || !takeInput ||
                !__instance.InPlaceMode() || Hud.IsPieceSelectionVisible()) {
                return;
            }

            ___m_placeRotation += VRControls.instance.getPieceRotation();
        }
    }

    // If using VR controls, disable the joystick for the purposes
    // of moving the map around since that will be done with
    // simulated mouse cursor click and drag via laser pointer.
    [HarmonyPatch(typeof(Minimap), "UpdateMap")]
    class Minimap_UpdateMap_MapTranslationPatch {
        private static MethodInfo getJoyLeftStickX =
            AccessTools.Method(typeof(ZInput), nameof(ZInput.GetJoyLeftStickX));

        private static MethodInfo getJoyLeftStickY =
            AccessTools.Method(typeof(ZInput), nameof(ZInput.GetJoyLeftStickY));

        private static float getJoyLeftStickXPatched() {
            if (VRControls.mainControlsActive) {
                return 0.0f;
            }

            return ZInput.GetJoyLeftStickX();
        }

        private static float getJoyLeftStickYPatched() {
            if (VRControls.mainControlsActive) {
                return 0.0f;
            }

            return ZInput.GetJoyLeftStickY();
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            var original = new List<CodeInstruction>(instructions);
            var patched = new List<CodeInstruction>();
            foreach (var instruction in original) {
                if (instruction.Calls(getJoyLeftStickX)) {
                    patched.Add(CodeInstruction.Call(typeof(Minimap_UpdateMap_MapTranslationPatch),
                        nameof(getJoyLeftStickXPatched)));
                }
                else if (instruction.Calls(getJoyLeftStickY)) {
                    patched.Add(CodeInstruction.Call(typeof(Minimap_UpdateMap_MapTranslationPatch),
                        nameof(getJoyLeftStickYPatched)));
                }
                else {
                    patched.Add(instruction);
                }
            }

            return patched;
        }
    }

    [HarmonyPatch(typeof(Player), "SetControls")]
    class PlayerSetControlsPatch {

        static bool wasCrouching;
        
        static void Prefix(Player __instance, ref bool attack, ref bool attackHold, ref bool block, ref bool blockHold,
            ref bool secondaryAttack, ref bool crouch, ref bool run) {
            if (!VRControls.mainControlsActive || __instance != Player.m_localPlayer) {
                return;
            }

            run = ZInput_GetJoyRightStickY_Patch.isRunning;
            
            if (ZInput_GetJoyRightStickY_Patch.isCrouching) {
                if (!wasCrouching) {
                    crouch = true;
                    wasCrouching = true;
                }
            } else if (wasCrouching) {
                wasCrouching = false;
            }

            if (EquipScript.getLeft() == EquipType.Bow) {
                if (BowLocalManager.aborting) {
                    block = true;
                    blockHold = true;
                    BowLocalManager.aborting = false;
                }
                else if (BowLocalManager.startedPulling) {
                    attack = true;
                    BowLocalManager.startedPulling = false;
                }
                else {
                    attackHold = BowLocalManager.isPulling;
                }
                return;
            }

            if (EquipScript.getLeft() == EquipType.Shield) {
                blockHold = ShieldManager.isBlocking();
            }

            switch (EquipScript.getRight()) {
                case EquipType.Fishing:
                    if (FishingManager.isThrowing) {
                        attack = true;
                        attackHold = true;
                        FishingManager.isThrowing = false;
                    }
                    
                    blockHold = FishingManager.isPulling;
                    break;

                case EquipType.Spear:
                    if (SpearManager.isThrowing) {
                        secondaryAttack = true;
                        SpearManager.isThrowing = false;
                    }

                    break;
                // no one knows why all spears throw with right click, only spear-chitin throws with left click: 
                case EquipType.SpearChitin:
                    if (SpearManager.isThrowing) {
                        attack = true;
                        SpearManager.isThrowing = false;
                    }

                    break;
            }
        }
    }
}