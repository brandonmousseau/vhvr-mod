using System.Text;
using System.Threading;
using Fishlabs;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using ValheimVRMod.Patches;
using ValheimVRMod.Utilities;
using Valve.VR;
using TMPro;

namespace ValheimVRMod.Patches {

    [HarmonyPatch(typeof(TextInput), "Show")]
    class PatchTextInputAwake {
        
        private static TextInput instance;

        public static void Postfix(TextInput __instance) {
            if (VHVRConfig.UseVrControls()) {
                instance = __instance;
                if (VHVRConfig.AutoOpenKeyboardOnInteract() || instance.m_topic.text == "ChatText")
                {
                    InputManager.start(null, null, instance.m_inputField, true, OnClose);
                }
            }
        }

        private static void OnClose() {
            instance.OnEnter();
        }
    }
    
    [HarmonyPatch(typeof(InputField), "OnPointerClick")]
    class PatchInputFieldClick
    {
        public static void Postfix(InputField __instance)
        {
            if (VHVRConfig.UseVrControls())
            {
                InputManager.start(__instance, null, null);
            }
        }
    }

    [HarmonyPatch(typeof(TMP_InputField), "OnPointerClick")]
    class PatchInputFieldTmpClick {
        public static void Postfix(TMP_InputField __instance) {
            if (VHVRConfig.UseVrControls()) {
                InputManager.start(null, __instance, null);
            }
        }
    }


    [HarmonyPatch(typeof(TMP_InputField), "OnFocus")]
    class PatchPasswordFieldFocus
    {
        static private TMP_InputField passwordInputField;
        public static void Postfix(TMP_InputField __instance)
        {
            if (VHVRConfig.UseVrControls() && __instance.inputType == TMP_InputField.InputType.Password)
            {
                InputManager.start(null, __instance, null, false, OnClose);
                passwordInputField = __instance;
            }
        }

        private static void OnClose()
        {
            passwordInputField.OnSubmit(null);
        }

    }

    [HarmonyPatch(typeof(Minimap), "ShowPinNameInput")]
    class PatchMinimap {
        public static void Postfix(Minimap __instance) {
            if (VHVRConfig.UseVrControls()) {
                InputManager.start(null, null, __instance.m_nameInput, returnOnClose: false, OnClose);
            }
        }

        private static void OnClose()
        {
            Minimap.m_instance.m_nameInput.OnSubmit(null);
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
        private static TMP_InputField _inputFieldTmp;
        private static GuiInputField _inputFieldGui; 
        private static UnityAction _closedAction;
        private static bool _returnOnClose;
        
        public static float closeTime;
        public static bool triggerReturn;

        public static void start(InputField inputField, TMP_InputField inputFieldTmp, GuiInputField inputFieldGui, bool returnOnClose = false, UnityAction closedAction = null) {
            // TODO: consider enforcing the check that one and only one among inputField, inputFieldGui, and inputFieldTmp is non-null.
            _inputField = inputField;
            _inputFieldGui = inputFieldGui;
            _inputFieldTmp = inputFieldTmp;
            _returnOnClose = returnOnClose;
            _closedAction = closedAction;
            
            if (_inputField != null && _inputField.text == "...") {
                _inputField.text = "";
            }

            if (_inputFieldTmp != null && _inputFieldTmp.text == "...")
            {
                _inputFieldTmp.text = "";
            }

            if (_inputFieldGui != null && _inputFieldGui.text == "...") {
                _inputFieldGui.text = "";
            }

            if (!initialized) {
                SteamVR_Events.System(EVREventType.VREvent_KeyboardClosed).Listen(OnKeyboardClosed);
                initialized = true;
            }
            
            SteamVR.instance.overlay.ShowKeyboard(0, 0, 0, "TextInput", 256, _inputField != null ? _inputField.text : _inputFieldTmp != null ? _inputFieldTmp.text : _inputFieldGui.text, 1);
        }

        private static void OnKeyboardClosed(VREvent_t args) {
            closeTime = Time.fixedTime;
            StringBuilder textBuilder = new StringBuilder(256);
            int caretPosition = (int)SteamVR.instance.overlay.GetKeyboardText(textBuilder, 256);
            string text = textBuilder.ToString();

            if (_inputField)
            {
                _inputField.caretPosition = caretPosition;
                _inputField.text = text;
            }

            if (_inputFieldTmp)
            {
                _inputFieldTmp.caretPosition = caretPosition;
                _inputFieldTmp.text = text;
            }

            if (_inputFieldGui)
            {
                _inputFieldGui.caretPosition = caretPosition;
                _inputFieldGui.text = text;
            }

            if (Scripts.QuickAbstract.shouldStartChat)
            {
                if (text != "")
                {
                    if (text.StartsWith("/cmd")) //SEND CONSOLE INPUT
                    {
                        if (text.StartsWith("/cmd "))
                            text = text.Remove(0, 5);
                        else
                            text = text.Remove(0, 4);

                        Console.instance.TryRunCommand(text);
                    }
                    else //SEND CHAT INPUT
                    {
                        Chat.instance.m_input.text = text;
                        Chat.instance.InputText();
                        Chat.instance.m_input.text = "";
                    }
                }
            }
            Scripts.QuickAbstract.shouldStartChat = false;

            triggerReturn = _returnOnClose;

            // If return is to be triggered, we will wait until then to fire close action.
            if (!_returnOnClose)
            {
                _closedAction?.Invoke();
            }
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
