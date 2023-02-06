using System.Text;
using System.Threading;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using ValheimVRMod.Utilities;
using Valve.VR;

namespace ValheimVRMod.Patches {

    [HarmonyPatch(typeof(TextInput), "Show")]
    class PatchTextInputAwake {
        
        private static TextInput instance;

        public static void Postfix(TextInput __instance) {
            if (VHVRConfig.UseVrControls()) {
                instance = __instance;
                if (VHVRConfig.AutoOpenKeyboardOnInteract() || instance.m_topic.text == "ChatText")
                {
                    InputManager.start(instance.m_textField, true, OnClose);
                }
            }
        }

        private static void OnClose() {
            instance.Hide();
        }
    }
    
    [HarmonyPatch(typeof(InputField), "OnPointerClick")]
    class PatchInputFieldClick {
        public static void Postfix(InputField __instance) {
            if (VHVRConfig.UseVrControls()) {
                InputManager.start(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(InputField), "OnFocus")]
    class PatchPasswordFieldFocus {
        public static void Postfix(InputField __instance) {
            if(VHVRConfig.UseVrControls() && __instance.inputType == InputField.InputType.Password) {
                InputManager.start(__instance, true);
            }
        }
    }
    
    [HarmonyPatch(typeof(Minimap), "ShowPinNameInput")]
    class PatchMinimap {
        public static void Postfix(InputField ___m_nameInput) {
            if (VHVRConfig.UseVrControls()) {
                InputManager.start(___m_nameInput, true);
            }
        }
    }
    
    [HarmonyPatch(typeof(Player), "Interact")]
    class PatchPlayerInteract {
        public static bool Prefix() {
            return !VHVRConfig.UseVrControls() || Time.fixedTime - InputManager.closeTime > 0.2f;
        }
    }
    
    [HarmonyPatch(typeof(Input), "GetKeyDownInt")]
    class PatchInputGetKeyDownInt {
        public static bool Prefix(ref bool __result, KeyCode key) {
            return !VHVRConfig.UseVrControls() || InputManager.handleReturnKeyInput(ref __result, key);
        }
    }
    
    [HarmonyPatch(typeof(Input), "GetKeyInt")]
    class PatchInputGetKeyInt {
        
        public static bool Prefix(ref bool __result, KeyCode key) {
            return !VHVRConfig.UseVrControls() || InputManager.handleReturnKeyInput(ref __result, key);
        }
    }

    public static class InputManager {

        private static bool initialized;
        private static InputField _inputField;
        private static UnityAction _closedAction;
        private static bool _returnOnClose;
        
        public static float closeTime;
        public static bool triggerReturn;

        public static void start(InputField inputField, bool returnOnClose = false, UnityAction closedAction = null) {

            _inputField = inputField;
            _returnOnClose = returnOnClose;
            _closedAction = closedAction;
            
            if (_inputField.text == "...") {
                _inputField.text = "";
            }
            
            if (!initialized) {
                SteamVR_Events.System(EVREventType.VREvent_KeyboardClosed).Listen(OnKeyboardClosed);
                initialized = true;
            }
            
            SteamVR.instance.overlay.ShowKeyboard(0, 0, 0, "TextInput", 256, _inputField.text, 1);
        }

        private static void OnKeyboardClosed(VREvent_t args) {
            closeTime = Time.fixedTime;
            StringBuilder text = new StringBuilder(256);
            _inputField.caretPosition = (int) SteamVR.instance.overlay.GetKeyboardText(text, 256);
            _inputField.text = text.ToString();

            if (Scripts.QuickAbstract.shouldStartChat)
            {
                if (_inputField.text != "")
                {
                    if (_inputField.text.StartsWith("/cmd")) //SEND CONSOLE INPUT
                    {
                        if (_inputField.text.StartsWith("/cmd "))
                            _inputField.text = _inputField.text.Remove(0, 5);
                        else
                            _inputField.text = _inputField.text.Remove(0, 4);

                        Console.instance.TryRunCommand(_inputField.text);
                    }
                    else //SEND CHAT INPUT
                    {
                        Chat.instance.m_input.text = _inputField.text;
                        Chat.instance.InputText();
                        Chat.instance.m_input.text = "";
                    }
                }
            }
            Scripts.QuickAbstract.shouldStartChat = false;

            triggerReturn = _returnOnClose;
        }

        public static bool handleReturnKeyInput(ref bool result, KeyCode key) {
            
            if (triggerReturn && key == KeyCode.Return) {
                result = true;
                triggerReturn = false;
                new Thread(()=>_closedAction?.Invoke()).Start();
                return false;
            }

            return true;
        }
    }
}
