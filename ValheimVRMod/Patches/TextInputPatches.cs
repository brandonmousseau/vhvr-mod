using System.Text;
using GUIFramework;
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
        public static void Postfix(TextInput __instance) {
            if (VHVRConfig.UseVrControls()) {
                bool isChatInput = __instance.m_topic.text == "ChatText";
                if (VHVRConfig.AutoOpenKeyboardOnInteract() || isChatInput)
                {
                    InputManager.start(
                        null,
                        null,
                        __instance.m_inputField,
                        returnOnClose: false,
                        closedAction: delegate { __instance.OnEnter(); },
                        chatInput: isChatInput);
                }
            }
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
        static private GuiInputField passwordInputField;
        public static void Postfix(TMP_InputField __instance)
        {
            if (!VHVRConfig.UseVrControls() || __instance.inputType != TMP_InputField.InputType.Password)
            {
                return;
            }

            passwordInputField = __instance.GetComponent<GuiInputField>();

            if (passwordInputField != null) {
                InputManager.start(null, null, passwordInputField, returnOnClose: false, OnClose);
            }
        }

        private static void OnClose()
        {
            passwordInputField.OnInputSubmit.Invoke(passwordInputField.text);
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
            Minimap.m_instance.m_nameInput.OnInputSubmit.Invoke(Minimap.m_instance.m_nameInput.text);
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

        public static void start(
            InputField inputField,
            TMP_InputField inputFieldTmp,
            GuiInputField inputFieldGui,
            bool returnOnClose = false,
            UnityAction closedAction = null,
            bool chatInput = false) {
            if (_keyboardOpen)
            {
                return;
            }

            // TODO: consider enforcing the check that one and only one among inputField, inputFieldGui, and inputFieldTmp is non-null.
            _inputField = inputField;
            _inputFieldGui = inputFieldGui;
            _inputFieldTmp = inputFieldTmp;
            _returnOnClose = returnOnClose;
            _closedAction = closedAction;
            _chatInput = chatInput;
            _keyboardOpen = true;
            triggerReturn = false;
            
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

        private static bool _keyboardOpen;
        private static bool _chatInput;

        private static void OnKeyboardClosed(VREvent_t args) {
            if (!_keyboardOpen)
            {
                return;
            }

            InputField inputField = _inputField;
            TMP_InputField inputFieldTmp = _inputFieldTmp;
            GuiInputField inputFieldGui = _inputFieldGui;
            UnityAction closedAction = _closedAction;
            bool returnOnClose = _returnOnClose;
            bool chatInput = _chatInput;
            _inputField = null;
            _inputFieldTmp = null;
            _inputFieldGui = null;
            _closedAction = null;
            _returnOnClose = false;
            _chatInput = false;
            _keyboardOpen = false;

            closeTime = Time.fixedTime;
            StringBuilder textBuilder = new StringBuilder(256);
            int caretPosition = (int)SteamVR.instance.overlay.GetKeyboardText(textBuilder, 256);
            string text = textBuilder.ToString();

            if (inputField)
            {
                inputField.caretPosition = caretPosition;
                inputField.text = text;
            }

            if (inputFieldTmp)
            {
                inputFieldTmp.caretPosition = caretPosition;
                inputFieldTmp.text = text;
            }

            if (inputFieldGui)
            {
                inputFieldGui.caretPosition = caretPosition;
                inputFieldGui.text = text;
            }

            if (chatInput)
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

            triggerReturn = returnOnClose;

            // If return is to be triggered, we will wait until then to fire close action.
            if (!returnOnClose)
            {
                closedAction?.Invoke();
            }
            else
            {
                _closedAction = closedAction;
            }
        }

        public static bool handleReturnKeyInput(ref bool result, KeyCode key) {
            
            if (triggerReturn && key == KeyCode.Return) {
                result = true;
                triggerReturn = false;
                UnityAction closedAction = _closedAction;
                _closedAction = null;
                closedAction?.Invoke();
                return false;
            }

            return true;
        }
    }
}
