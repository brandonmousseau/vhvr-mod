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
                    // Capture __instance in the closure directly instead of via a shared static field:
                    // if chat's keyboard-close event is still pending when a portal/sign dialog opens
                    // (or vice versa), a static field would get overwritten before the earlier close
                    // event is handled, causing it to fire against the wrong TextInput.
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
        private static bool _chatInput;

        // Guards against a second start() clobbering these static fields (and _liveText below) while a
        // keyboard session is still in flight - e.g. if a chat close event is still pending delivery when
        // a portal/sign dialog opens. Without this, the stale close event ends up firing against whatever
        // session's fields happen to be sitting in the statics by the time it's finally handled.
        private static bool _keyboardOpen;

        // Some SteamVR versions no longer render their own temp text row, and VREvent_KeyboardCharInput's
        // cNewInput payload comes back all zero bytes (confirmed via logging) even though the event still
        // fires once per keystroke. So we treat that event purely as a "poll now" signal and re-fetch the
        // full current text via GetKeyboardText() on every keystroke, mirroring it into the live UI field
        // ourselves as a stand-in for the temp text row.
        private static StringBuilder _liveText = new StringBuilder(256);
        private static int _liveCaretPosition;

        public static float closeTime;
        public static bool triggerReturn;

        public static void start(InputField inputField, TMP_InputField inputFieldTmp, GuiInputField inputFieldGui, bool returnOnClose = false, UnityAction closedAction = null, bool chatInput = false) {
            if (_keyboardOpen) {
                return;
            }
            _keyboardOpen = true;

            // TODO: consider enforcing the check that one and only one among inputField, inputFieldGui, and inputFieldTmp is non-null.
            _inputField = inputField;
            _inputFieldGui = inputFieldGui;
            _inputFieldTmp = inputFieldTmp;
            _returnOnClose = returnOnClose;
            _closedAction = closedAction;
            _chatInput = chatInput;
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
                SteamVR_Events.System(EVREventType.VREvent_KeyboardCharInput).Listen(OnKeyboardCharInput);
                initialized = true;
            }

            string existingText = _inputField != null ? _inputField.text : _inputFieldTmp != null ? _inputFieldTmp.text : _inputFieldGui.text;
            _liveText.Clear();
            _liveText.Append(existingText);
            _liveCaretPosition = existingText.Length;

            SteamVR.instance.overlay.ShowKeyboard(0, 0, 0, "TextInput", 256, existingText, 1);
        }

        private static void OnKeyboardCharInput(VREvent_t args) {
            RefreshLiveTextFromSteamVR();
        }

        private static void RefreshLiveTextFromSteamVR() {
            StringBuilder textBuilder = new StringBuilder(256);
            int caretPosition = (int)SteamVR.instance.overlay.GetKeyboardText(textBuilder, 256);
            string delta = textBuilder.ToString();
            LogUtils.LogInfo($"[Keyboard] GetKeyboardText delta=\"{delta}\" caret={caretPosition}");

            // GetKeyboardText() doesn't return the full accumulated text on this SteamVR version - it
            // returns only what changed since the last read (typically the single newest character), so
            // we merge it into our own persistent buffer rather than replacing it wholesale.
            foreach (char c in delta) {
                if (c == '\0') {
                    continue;
                }
                if (c == '\b') {
                    if (_liveText.Length > 0) {
                        _liveText.Remove(_liveText.Length - 1, 1);
                    }
                } else if (c == '\n' || c == '\r') {
                    // Submission is handled by VREvent_KeyboardClosed.
                } else {
                    _liveText.Append(c);
                }
            }
            _liveCaretPosition = _liveText.Length;

            LogUtils.LogInfo($"[Keyboard] liveText now: \"{_liveText}\"");
            ApplyLiveText(_liveText.ToString());
        }

        private static void ApplyLiveText(string text) {
            if (_inputField) {
                _inputField.text = text;
            }
            if (_inputFieldTmp) {
                _inputFieldTmp.text = text;
            }
            if (_inputFieldGui) {
                _inputFieldGui.text = text;
            }
        }

        private static void OnKeyboardClosed(VREvent_t args) {
            if (!_keyboardOpen) {
                return;
            }

            // Snapshot this session's state into locals and clear the statics before doing any
            // processing or invoking callbacks. If closedAction synchronously opens another dialog
            // (e.g. a chained TextInput.Show()), that new session's start() call must see _keyboardOpen
            // already false and must not stomp on the values this call is still using.
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
            // Every keystroke, including the last one, already arrived via OnKeyboardCharInput and was
            // merged into _liveText. Polling GetKeyboardText() again here re-reads and re-merges that
            // same last delta a second time (SteamVR doesn't seem to clear it until the next keystroke),
            // which duplicates the final character - so just use what we already have.
            string text = _liveText.ToString();
            int caretPosition = Mathf.Clamp(_liveCaretPosition, 0, text.Length);
            LogUtils.LogInfo($"[Keyboard] Closed. final text=\"{text}\"");

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
                // Invoke synchronously on the calling (main) thread instead of via a background Thread:
                // closedAction touches Unity APIs (e.g. TextInput.OnEnter(), Chat.InputText()), which
                // aren't safe to call off the main thread, and this Prefix already runs on it.
                UnityAction closedAction = _closedAction;
                _closedAction = null;
                closedAction?.Invoke();
                return false;
            }

            return true;
        }
    }
}
