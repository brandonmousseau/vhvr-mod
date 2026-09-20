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

                // TextInput.m_instance.m_panel is a singleton GameObject reused for chat, sign,
                // portal, and map pin naming dialogs alike. Chat input hides it (the vanilla chat
                // window plus the SteamVR overlay keyboard already give the player feedback), but
                // every other use must explicitly restore the scale on every Show() call, or it
                // stays hidden from a previous chat session (this was the "TODO: find out why
                // portal tag and sign text input dialog can no longer be visible after chat input
                // is used once" bug).
                __instance.m_panel.gameObject.transform.localScale = isChatInput ? Vector3.zero : Vector3.one;

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
            Minimap.s_instance.m_nameInput.OnInputSubmit.Invoke(Minimap.s_instance.m_nameInput.text);
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

        // True while a chat text keyboard session (opened via the SteamVR virtual keyboard path)
        // is in flight. VRControls' grip-based confirm/cancel gesture handling is meant for the
        // physical-keyboard chat flow only and must not also react while this is true, or it will
        // race with the keyboard-driven submit/cancel below.
        public static bool chatKeyboardActive => _keyboardOpen && _chatInput;

        // True while any SteamVR virtual keyboard session is in flight.
        public static bool keyboardActive => _keyboardOpen;

        // Some SteamVR versions no longer render their own temp text row, and VREvent_KeyboardCharInput's
        // cNewInput payload comes back all zero bytes (confirmed via logging) even though the event still
        // fires once per keystroke. So we treat that event purely as a "poll now" signal and re-fetch the
        // text via GetKeyboardText() on every keystroke, mirroring it into the live UI field ourselves as a
        // stand-in for the temp text row.
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
            // The event carries the keystroke it was raised for. A keyboard that fills this in - the legacy
            // one does - tells us exactly what was typed, so its own text buffer never has to be read or
            // second-guessed at all: what the player typed is merged into ours and that is the text of
            // record. Only the keyboards that leave the payload blank (all zero bytes, confirmed via
            // logging on the big screen keyboard) need the text polled back out of SteamVR below.
            string typed = args.data.keyboard.cNewInput;
            if (typed.Length > 0) {
                LogUtils.LogInfo($"[Keyboard] KeyboardCharInput payload=\"{typed}\"");
                MergeIntoLiveText(typed);
                _liveCaretPosition = _liveText.Length;
                LogUtils.LogInfo($"[Keyboard] liveText now: \"{_liveText}\"");
                ApplyLiveText(_liveText.ToString());
                return;
            }

            RefreshLiveTextFromSteamVR();
        }

        private static string ReadKeyboardText() {
            StringBuilder textBuilder = new StringBuilder(256);
            SteamVR.instance.overlay.GetKeyboardText(textBuilder, 256);
            return textBuilder.ToString().Replace("\0", string.Empty);
        }

        private static void RefreshLiveTextFromSteamVR() {
            string read = ReadKeyboardText();
            string current = _liveText.ToString();
            LogUtils.LogInfo($"[Keyboard] GetKeyboardText read=\"{read}\" current=\"{current}\"");

            if (read.Length == 0) {
                // Nothing was reported, so there is nothing to merge. Clearing the buffer on this would
                // wipe text the keyboard still holds.
                return;
            }

            string fullText = AsFullText(read, current);
            if (fullText != null) {
                _liveText.Clear();
                _liveText.Append(fullText);
            } else {
                // The read is only what changed, so merge it into our own buffer rather than replacing it.
                MergeIntoLiveText(read);
            }
            _liveCaretPosition = _liveText.Length;

            LogUtils.LogInfo($"[Keyboard] liveText now: \"{_liveText}\"");
            ApplyLiveText(_liveText.ToString());
        }

        // Applies keystrokes to the text we track, the way the field would apply them itself.
        private static void MergeIntoLiveText(string keystrokes) {
            foreach (char c in keystrokes) {
                if (c == '\b') {
                    if (TryGetSelection(out int selectionStart, out int selectionLength)) {
                        // Delete what is selected rather than the last character, the way a physical keyboard
                        // would. Note that this is only possible while merging keystrokes, where _liveText is
                        // the text of record: when the keyboard reports its whole text instead, that buffer is,
                        // and OpenVR offers no way to correct it (there is a GetKeyboardText but no
                        // SetKeyboardText).
                        _liveText.Remove(selectionStart, selectionLength);
                    } else if (_liveText.Length > 0) {
                        _liveText.Remove(_liveText.Length - 1, 1);
                    }
                } else if (c == '\n' || c == '\r') {
                    // Submission is handled by VREvent_KeyboardClosed.
                } else {
                    _liveText.Append(c);
                }
            }
        }

        // A keyboard that reports no keystroke payload has to have its text polled instead, and SteamVR
        // versions disagree on what that poll returns: the whole text the keyboard holds, or only the
        // newest keystroke. Returns the whole text when the read can only be that, or null when it is a
        // keystroke to be merged in instead.
        //
        // Every read is judged on its own against the text we already have, rather than pinning the runtime
        // down once and remembering the verdict: a single wrong guess used to corrupt every keystroke that
        // followed it, which is how a whole text ended up appended to the text we track instead of
        // replacing it, doubling what the player had typed.
        private static string AsFullText(string read, string current) {
            if (read.IndexOf('\b') >= 0) {
                // A whole text never carries a raw backspace; a keystroke reports a deletion with one.
                return null;
            }
            if (read == current) {
                // The text did not change, so the keystroke edited nothing (a modifier), or the same read
                // arrived twice. Below two characters that is indistinguishable from the player repeating
                // the only character typed so far ("ll"), and taking that as a keystroke is the lesser evil:
                // it costs one character in a rare case, whereas taking a repeated whole text for a
                // keystroke doubles everything the player has typed.
                return current.Length > 1 ? current : null;
            }
            if (read.Length > current.Length && read.StartsWith(current)) {
                // What we already have, plus the keystroke that just arrived.
                return read;
            }
            if (read.Length >= 2 && read.Length == current.Length - 1 && current.StartsWith(read)) {
                // What we already have, with its last character deleted. Single character reads are left
                // out: they are far more often the one character that was just typed, and reading "lo" + "l"
                // as a deletion would cost the player the "l" of "lol" on every keyboard that reports
                // keystrokes this way.
                return read;
            }
            return null;
        }

        // The text of a dialog is selected as a whole when it opens (TextInput#Show activates the input field, which
        // makes Unity select all of it), and the player can also select text themselves with the pointer. Reports
        // what is selected, so that a backspace can delete it instead of the last character.
        private static bool TryGetSelection(out int start, out int length) {
            start = length = 0;

            string fieldText;
            int anchor;
            int focus;
            if (_inputField) {
                fieldText = _inputField.text;
                anchor = _inputField.selectionAnchorPosition;
                focus = _inputField.selectionFocusPosition;
            } else {
                TMP_InputField field = GetTmpField();
                if (!field) {
                    return false;
                }
                fieldText = field.text;
                anchor = field.selectionAnchorPosition;
                focus = field.selectionFocusPosition;
            }

            if (fieldText != _liveText.ToString()) {
                // The field is not showing the text we are about to edit, so its selection indices mean nothing here.
                return false;
            }

            start = Mathf.Clamp(Mathf.Min(anchor, focus), 0, _liveText.Length);
            length = Mathf.Clamp(Mathf.Max(anchor, focus), 0, _liveText.Length) - start;
            return length > 0;
        }

        // GuiInputField is a TMP_InputField, so both are handled the same way.
        private static TMP_InputField GetTmpField() {
            return _inputFieldTmp ? _inputFieldTmp : _inputFieldGui;
        }

        // Keeps a selection that has just been deleted from being deleted again on the next backspace, and puts the
        // caret where the next keystroke lands, since this keyboard always appends at the end.
        private static void CollapseSelection(string text) {
            if (_inputField) {
                _inputField.caretPosition =
                    _inputField.selectionAnchorPosition = _inputField.selectionFocusPosition = text.Length;
            }
            TMP_InputField field = GetTmpField();
            if (field) {
                field.caretPosition = field.selectionAnchorPosition = field.selectionFocusPosition = text.Length;
            }
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
            CollapseSelection(text);
            if (_chatInput && Chat.instance != null) {
                // Mirror into the vanilla chat window's own input field (opened alongside the
                // SteamVR keyboard, see QuickAbstract's chat quick action) so the player sees
                // what they're typing instead of typing blind into the hidden TextInput dialog.
                Chat.instance.m_input.text = text;
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
            // Every keystroke, including the last one, already arrived via OnKeyboardCharInput and left
            // _liveText holding the keyboard's text, whichever way that keyboard reports it (see
            // AsFullText()). Polling GetKeyboardText() again here would re-read the last keystroke, which
            // SteamVR doesn't seem to clear until the next one, and a keyboard that reports deltas would
            // have it merged in a second time, duplicating the final character.
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
                if (text != "" && text.StartsWith("/cmd")) //SEND CONSOLE INPUT
                {
                    string command = text.StartsWith("/cmd ") ? text.Remove(0, 5) : text.Remove(0, 4);
                    Console.instance.TryRunCommand(command);
                    Chat.instance.m_input.text = "";
                }
                else
                {
                    // Leave submission itself to Chat.instance.SendInput() rather than calling
                    // Chat.instance.InputText() directly: per Terminal.SendInput()/Chat.SendInput(),
                    // that's also what closes the window - it deactivates m_input's GameObject
                    // afterward (unconditionally, even if text is empty), which is what actually
                    // clears Unity's EventSystem focus and stops the caret from blinking. Emulating
                    // an Escape keypress instead (the previous approach) raced against whatever else
                    // polls ZInput.GetKeyDown(Escape) that frame and could lose it, leaving the
                    // window focused with no SteamVR keyboard left open to close it.
                    Chat.instance.m_input.text = text;
                }
                Chat.instance.SendInput();
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
