using System.Text;
using System.Threading;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
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
                    InputManager.start(instance.m_textField, instance.m_textFieldTMP, true, OnClose);
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
                InputManager.start(__instance, null);
            }
        }
    }

    [HarmonyPatch(typeof(TMP_InputField), "OnPointerClick")]
    class PatchInputFieldTmpClick {
        public static void Postfix(TMP_InputField __instance) {
            if (VHVRConfig.UseVrControls()) {
                InputManager.start(null, __instance);
            }
        }
    }

    [HarmonyPatch(typeof(InputField), "OnFocus")]
    class PatchPasswordFieldFocus
    {
        public static void Postfix(InputField __instance)
        {
            if (VHVRConfig.UseVrControls() && __instance.inputType == InputField.InputType.Password)
            {
                InputManager.start(__instance, null, true);
            }
        }
    }
    
    [HarmonyPatch(typeof(Minimap), "ShowPinNameInput")]
    class PatchMinimap {
        private static Minimap instance;
        public static bool pendingInputEnter { get; private set; }

        public static void Postfix(Minimap __instance) {
            if (VHVRConfig.UseVrControls()) {
                instance = __instance;
                InputManager.start(__instance.m_nameInput, null, true, OnClose);
            }
        }

        public static void maybeClearPendingInputEnter()
        {
            if (instance.m_namePin == null)
            {
                pendingInputEnter = false;
            }
        }

        private static void OnClose()
        {
            pendingInputEnter = true;
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
        private static UnityAction _closedAction;
        private static bool _returnOnClose;
        
        public static float closeTime;
        public static bool triggerReturn;

        public static void start(InputField inputField, TMP_InputField inputFieldTmp, bool returnOnClose = false, UnityAction closedAction = null) {

            _inputField = inputField;
            _inputFieldTmp = inputFieldTmp;
            _returnOnClose = returnOnClose;
            _closedAction = closedAction;
            
            if (_inputField != null && _inputField.text == "...") {
                _inputField.text = "";
            }

            if (_inputFieldTmp != null && _inputFieldTmp.text == "...") {
                _inputFieldTmp.text = "";
            }

            if (!initialized) {
                SteamVR_Events.System(EVREventType.VREvent_KeyboardClosed).Listen(OnKeyboardClosed);
                initialized = true;
            }
            
            SteamVR.instance.overlay.ShowKeyboard(0, 0, 0, "TextInput", 256, _inputField != null ? _inputField.text : _inputFieldTmp.text, 1);
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

            if (_inputFieldTmp )
            {
                _inputFieldTmp.caretPosition = caretPosition;
                _inputFieldTmp.text = text;
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
