using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Valve.VR;

namespace ValheimVRMod.Patches {

    [HarmonyPatch(typeof(TextInput), "Show")]
    class PatchTextInputAwake {
        
        private static TextInput instance;

        public static void Postfix(TextInput __instance) {
            instance = __instance;
            InputManager.start(__instance.m_textField.text, OnUpdate, OnClose);
        }

        private static void OnUpdate(string text) {
            instance.m_textField.text = text;
        }
        
        private static void OnClose() {
            try {
                AccessTools.Method(typeof(TextInput), "OnEnter", new[] {typeof(string)})
                    .Invoke(instance, new object[] {instance.m_textField.text});
            }
            catch {}
            instance.Hide();
        }
    }
    
    
    [HarmonyPatch(typeof(InputField), "OnPointerClick")]
    class PatchInputFieldClick {
        
        private static InputField instance;
        
        public static void Postfix(InputField __instance) {
            instance = __instance;
            InputManager.start(__instance.text, OnUpdate, OnClose);
        }
        
        private static void OnUpdate(string text) {
            instance.text = text;
        }
        
        private static void OnClose() {}

    }
    
    [HarmonyPatch(typeof(Player), "Interact")]
    class PatchPlayerInteract {
        
        public static bool Prefix() {
            return Time.fixedTime - InputManager.closeTime > 0.2f;
        }
    }
    
    
    public static class InputManager {

        private static bool initialized;

        private static string _text;
        private static UnityAction<string> _updateAction;
        private static UnityAction _closedAction;

        public static float closeTime;

        public static void start(string text, UnityAction<string> updateAction,  UnityAction closedAction) {

            _text = text;
            if (_text == "...") {
                _text = "";
            }
            _updateAction = updateAction;
            _closedAction = closedAction;
            
            if (!initialized) {
                SteamVR_Events.System(EVREventType.VREvent_KeyboardCharInput).Listen(OnKeyboardCharInput);
                SteamVR_Events.System(EVREventType.VREvent_KeyboardClosed).Listen(OnKeyboardClosed);
                initialized = true;
            }
            
            SteamVR.instance.overlay.ShowKeyboard(0, 0, 1, "TextInput", 256, text, 1);
            
        }
        
        private static void OnKeyboardCharInput(VREvent_t args) {
            Debug.Log("Giving INPUT");
            VREvent_Keyboard_t keyboard = args.data.keyboard;
            byte[] inputBytes = {
                keyboard.cNewInput0, keyboard.cNewInput1, keyboard.cNewInput2, keyboard.cNewInput3, keyboard.cNewInput4,
                keyboard.cNewInput5, keyboard.cNewInput6, keyboard.cNewInput7
            };
            int len = 0;
            for (; inputBytes[len] != 0 && len < 7; len++) ;
            string input = System.Text.Encoding.UTF8.GetString(inputBytes, 0, len);

            if (input == "\b") {
                if (_text.Length > 0) {
                    _text = _text.Substring(0, _text.Length - 1);
                }
            }
            else {
                _text += input;
            }
            _updateAction.Invoke(_text);
        }

        private static void OnKeyboardClosed(VREvent_t args) {
            closeTime = Time.fixedTime;
            _closedAction.Invoke();
        }
    }
}