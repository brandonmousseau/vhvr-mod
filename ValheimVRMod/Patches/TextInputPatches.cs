using System.Text;
using GUIFramework;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
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
    
    // Steam Frame / Proton+FEX: GetKeyDownInt and GetKeyInt are InternalCall (native) methods, which Harmony patches with a
    // NativeDetour. Under FEX the detour trampoline re-enters the detour and recurses until the main-thread stack overflows.
    // Patch the managed wrappers instead (plain IL hook); the Int variants are only reached through them.
    [HarmonyPatch(typeof(Input), nameof(Input.GetKeyDown), new[] { typeof(KeyCode) })]
    class PatchInputGetKeyDown {
        public static bool Prefix(ref bool __result, KeyCode key) {
            return !VHVRConfig.UseVrControls() || InputManager.handleReturnKeyInput(ref __result, key);
        }
    }
    
    [HarmonyPatch(typeof(Input), nameof(Input.GetKey), new[] { typeof(KeyCode) })]
    class PatchInputGetKey {
        
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

        // The keyboard is opened in minimal mode, in which it keeps no text of its own and only reports the
        // keys the player presses, arrow keys included. The text, the caret and the selection are ours to
        // keep, here, which is what lets the caret be moved and a selection be typed over: OpenVR offers no
        // way to read the caret of the text a keyboard buffers itself, nor to correct that text (there is a
        // GetKeyboardText but no SetKeyboardText).
        private const uint KeyboardFlags = (uint)EKeyboardFlags.KeyboardFlag_Minimal | KeyboardFlagShowArrowKeys;

        // Missing from the EKeyboardFlags of the OpenVR bindings in use. Makes a minimal mode keyboard show
        // its arrow keys, which it reports as ANSI escape sequences. Keyboards older than the flag ignore it.
        private const uint KeyboardFlagShowArrowKeys = 1 << 2;

        private const char Escape = '\u001b';

        private static StringBuilder _liveText = new StringBuilder(256);

        // Indices into _liveText. The selection runs from the anchor to the caret, and is empty when they
        // are the same.
        private static int _caret;
        private static int _selectionAnchor;

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
                SteamVR_Events.System(EVREventType.VREvent_KeyboardDone).Listen(OnKeyboardDone);
                initialized = true;
            }

            string existingText = _inputField != null ? _inputField.text : _inputFieldTmp != null ? _inputFieldTmp.text : _inputFieldGui.text;
            _liveText.Clear();
            _liveText.Append(existingText);
            _caret = _selectionAnchor = existingText.Length;

            EVROverlayError error = SteamVR.instance.overlay.ShowKeyboard(0, 0, KeyboardFlags, "TextInput", 256, existingText, 1);
            if (error != EVROverlayError.None) {
                // No keyboard came up, so no VREvent_KeyboardClosed will ever end this session. Leaving
                // _keyboardOpen set would block every later start().
                LogUtils.LogWarning($"[Keyboard] ShowKeyboard failed: {error}");
                // The usual cause is a keyboard that is still up without us knowing about it.
                SteamVR.instance.overlay.HideKeyboard();
                if (chatInput) {
                    CancelChat();
                } else {
                    ClearSession();
                }
            }
        }

        // True while the chat is taking input, from either the SteamVR keyboard or a physical one.
        public static bool chatActive =>
            chatKeyboardActive || (Chat.instance != null && (Chat.instance.HasFocus() || Chat.instance.m_wasFocused));

        // Opens the vanilla chat window, the way Chat.Update() does on the "Chat" button. Done directly
        // rather than by emulating that button: Chat.Update() only polls it while nothing else blocks the
        // chat, so an emulated press could sit unconsumed and open the chat at some later, unrelated point.
        public static void OpenChat(bool useSteamVrKeyboard) {
            Chat chat = Chat.instance;
            chat.m_hideTimer = 0f;
            chat.m_chatWindow.gameObject.SetActive(true);
            chat.m_input.gameObject.SetActive(true);
            chat.TryShowTextCommunicationRestrictedSystemPopup();
            chat.m_input.text = "";
            chat.m_input.ActivateInputField();
            if (useSteamVrKeyboard) {
                // The keyboard types straight into the chat's own input field, so that the two cannot get
                // out of step with each other.
                start(null, null, chat.m_input, chatInput: true);
            }
        }

        // Closes the chat without sending anything, along with the SteamVR keyboard if it is typing into it.
        public static void CancelChat() {
            if (chatKeyboardActive) {
                // End the session first, so that the VREvent_KeyboardClosed that hiding the keyboard
                // raises is not taken as a submission.
                ClearSession();
                closeTime = Time.fixedTime;
                SteamVR.instance.overlay.HideKeyboard();
            }

            Chat chat = Chat.instance;
            if (chat != null) {
                chat.m_input.text = "";
                if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == chat.m_input.gameObject) {
                    EventSystem.current.SetSelectedGameObject(null);
                }
                chat.m_input.gameObject.SetActive(false);
                // Chat.Update() only refreshes these on its next run, and Menu.Update() will not open the
                // menu while the chat still counts as focused.
                chat.m_wasFocused = false;
                chat.m_focused = false;
            }
            Scripts.QuickAbstract.shouldStartChat = false;
        }

        private static void ClearSession() {
            _inputField = null;
            _inputFieldTmp = null;
            _inputFieldGui = null;
            _closedAction = null;
            _returnOnClose = false;
            _chatInput = false;
            _keyboardOpen = false;
        }

        private static void OnKeyboardCharInput(VREvent_t args) {
            if (!_keyboardOpen) {
                return;
            }

            // The event carries the keys it was raised for, which is all a keyboard in minimal mode has to
            // say. Some keyboards have been seen to leave the payload blank instead (all zero bytes,
            // confirmed via logging on the big screen keyboard while it was still asked to buffer the text
            // itself), and those need the text polled back out of SteamVR.
            string typed = DecodeKeystrokes(args.data.keyboard);
            if (typed.Length > 0) {
                LogUtils.LogInfo($"[Keyboard] KeyboardCharInput payload=\"{typed.Replace(Escape, '^')}\"");
                Type(typed);
                return;
            }

            RefreshLiveTextFromSteamVR();
        }

        // A keyboard in minimal mode is not bound to close on its Done key by itself. Submission is left to
        // the VREvent_KeyboardClosed that hiding it raises, as for every other way the keyboard closes.
        private static void OnKeyboardDone(VREvent_t args) {
            if (_keyboardOpen) {
                SteamVR.instance.overlay.HideKeyboard();
            }
        }

        // The payload is UTF-8, which VREvent_Keyboard_t.cNewInput does not decode.
        private static string DecodeKeystrokes(VREvent_Keyboard_t keyboard) {
            byte[] bytes = {
                keyboard.cNewInput0, keyboard.cNewInput1, keyboard.cNewInput2, keyboard.cNewInput3,
                keyboard.cNewInput4, keyboard.cNewInput5, keyboard.cNewInput6, keyboard.cNewInput7
            };
            int length = System.Array.IndexOf(bytes, (byte)0);
            return Encoding.UTF8.GetString(bytes, 0, length < 0 ? bytes.Length : length);
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
            if (fullText == null) {
                // The read is only what changed, so type it into our own buffer rather than replacing it.
                Type(read);
                return;
            }

            // The keyboard is buffering the text itself after all, so its text is the text of record, and
            // where its caret is cannot be known.
            _liveText.Clear();
            _liveText.Append(fullText);
            _caret = _selectionAnchor = _liveText.Length;
            ApplyLiveText();
        }

        // Applies keystrokes to the text we track, the way the field would apply them itself.
        private static void Type(string keystrokes) {
            AdoptFieldSelection();

            bool done = false;
            for (int i = 0; i < keystrokes.Length; i++) {
                char c = keystrokes[i];
                if (c == Escape) {
                    i = ApplyEscapeSequence(keystrokes, i);
                } else if (c == '\b') {
                    if (!DeleteSelection() && _caret > 0) {
                        _selectionAnchor = StepCaret(_caret, -1);
                        DeleteSelection();
                    }
                } else if (c == '\n' || c == '\r') {
                    done = true;
                } else if (!char.IsControl(c)) {
                    Insert(c);
                }
            }

            ApplyLiveText();

            if (done) {
                OnKeyboardDone(default);
            }
        }

        private static void Insert(char c) {
            DeleteSelection();
            int limit = _inputField ? _inputField.characterLimit : GetTmpField() ? GetTmpField().characterLimit : 0;
            if (limit > 0 && _liveText.Length >= limit) {
                return;
            }
            _liveText.Insert(_caret, c);
            _caret = _selectionAnchor = _caret + 1;
        }

        private static bool DeleteSelection() {
            int start = Mathf.Min(_selectionAnchor, _caret);
            int length = Mathf.Abs(_selectionAnchor - _caret);
            _liveText.Remove(start, length);
            _caret = _selectionAnchor = start;
            return length > 0;
        }

        // Applies the escape sequence starting at the given index, and returns the index it ends at.
        private static int ApplyEscapeSequence(string keystrokes, int escape) {
            int end = escape + 1;
            if (end >= keystrokes.Length || keystrokes[end] != '[') {
                return escape;
            }
            // Parameters, if any, come before the character that tells which key it is.
            do {
                end++;
            } while (end < keystrokes.Length && keystrokes[end] < '@');
            if (end >= keystrokes.Length) {
                return keystrokes.Length - 1;
            }
            if (end == escape + 2) {
                MoveCaret(keystrokes[end]);
            }
            return end;
        }

        // Moves the caret the way the given key does in a single line field, collapsing the selection.
        private static void MoveCaret(char key) {
            bool hasSelection = _selectionAnchor != _caret;
            switch (key) {
                case 'D': // Left
                    _caret = hasSelection ? Mathf.Min(_selectionAnchor, _caret) : StepCaret(_caret, -1);
                    break;
                case 'C': // Right
                    _caret = hasSelection ? Mathf.Max(_selectionAnchor, _caret) : StepCaret(_caret, 1);
                    break;
                case 'A': // Up
                case 'H': // Home
                    _caret = 0;
                    break;
                case 'B': // Down
                case 'F': // End
                    _caret = _liveText.Length;
                    break;
                default:
                    return;
            }
            _selectionAnchor = _caret;
        }

        // The position one character before or after the given one, taking the two halves of a surrogate
        // pair (an emoji, say) as the one character they are.
        private static int StepCaret(int position, int direction) {
            int next = Mathf.Clamp(position + direction, 0, _liveText.Length);
            if (next > 0 && next < _liveText.Length && char.IsLowSurrogate(_liveText[next]) && char.IsHighSurrogate(_liveText[next - 1])) {
                next += direction;
            }
            return next;
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
            if (read.IndexOf('\b') >= 0 || read.IndexOf(Escape) >= 0) {
                // A whole text never carries a raw backspace or an arrow key; a keystroke reports them so.
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

        // The field is where the player sees the caret and the selection, and can set both with the pointer;
        // the text of a dialog is also selected as a whole when it opens (TextInput#Show activates the input
        // field, which makes Unity select all of it). So the field has the say on both, whenever it is in a
        // state to: an unfocused field reports neither, and one showing another text reports them for that.
        private static void AdoptFieldSelection() {
            string fieldText;
            int anchor;
            int focus;
            if (_inputField) {
                if (!_inputField.isFocused) {
                    return;
                }
                fieldText = _inputField.text;
                anchor = _inputField.selectionAnchorPosition;
                focus = _inputField.selectionFocusPosition;
            } else {
                TMP_InputField field = GetTmpField();
                if (!field || !field.isFocused) {
                    return;
                }
                fieldText = field.text;
                anchor = field.selectionStringAnchorPosition;
                focus = field.selectionStringFocusPosition;
            }

            if (fieldText != _liveText.ToString()) {
                return;
            }

            _selectionAnchor = Mathf.Clamp(anchor, 0, _liveText.Length);
            _caret = Mathf.Clamp(focus, 0, _liveText.Length);
        }

        // GuiInputField is a TMP_InputField, so both are handled the same way.
        private static TMP_InputField GetTmpField() {
            return _inputFieldTmp ? _inputFieldTmp : _inputFieldGui;
        }

        private static void SetTextAndCaret(InputField field, string text, int caret) {
            if (field) {
                field.text = text;
                field.caretPosition = caret;
            }
        }

        private static void SetTextAndCaret(TMP_InputField field, string text, int caret) {
            if (field) {
                field.text = text;
                // Unlike its caretPosition, this is an index into the text, as our caret is.
                field.stringPosition = caret;
            }
        }

        // Shows the text we track in the field, with the caret where the next keystroke lands. A selection
        // never outlasts a keystroke, so there is only ever a caret to show.
        private static void ApplyLiveText() {
            string text = _liveText.ToString();
            LogUtils.LogInfo($"[Keyboard] liveText now: \"{text}\" caret={_caret}");
            SetTextAndCaret(_inputField, text, _caret);
            SetTextAndCaret(GetTmpField(), text, _caret);
            if (_chatInput && Chat.instance != null) {
                // Mirror into the vanilla chat window's own input field (opened alongside the
                // SteamVR keyboard, see QuickAbstract's chat quick action) so the player sees
                // what they're typing instead of typing blind into the hidden TextInput dialog.
                SetTextAndCaret(Chat.instance.m_input, text, _caret);
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

            ClearSession();

            closeTime = Time.fixedTime;
            // Every keystroke, including the last one, already arrived via OnKeyboardCharInput and left
            // _liveText holding the keyboard's text, whichever way that keyboard reports it (see
            // AsFullText()). Polling GetKeyboardText() again here would re-read the last keystroke, which
            // SteamVR doesn't seem to clear until the next one, and a keyboard that reports deltas would
            // have it merged in a second time, duplicating the final character.
            string text = _liveText.ToString();
            LogUtils.LogInfo($"[Keyboard] Closed. final text=\"{text}\"");

            SetTextAndCaret(inputField, text, _caret);
            SetTextAndCaret(inputFieldTmp ? inputFieldTmp : inputFieldGui, text, _caret);

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
