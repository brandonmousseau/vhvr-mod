using System;
using System.Text;
using Valve.VR;

namespace ValheimVRMod.VRCore.Backends
{
    public sealed class SteamVRKeyboardBackend : IVRKeyboardBackend
    {
        private const uint KeyboardFlags = (uint)EKeyboardFlags.KeyboardFlag_Minimal | KeyboardFlagShowArrowKeys;
        // Older runtimes ignore this flag, as in the original keyboard implementation.
        private const uint KeyboardFlagShowArrowKeys = 1 << 2;
        public void ListenClosed(Action listener) => SteamVR_Events.System(EVREventType.VREvent_KeyboardClosed).Listen(e => listener());
        public void ListenDone(Action listener) => SteamVR_Events.System(EVREventType.VREvent_KeyboardDone).Listen(e => listener());
        public void ListenInput(Action<VRKeyboardInput> listener)
        {
            SteamVR_Events.System(EVREventType.VREvent_KeyboardCharInput).Listen(e => {
                var k = e.data.keyboard;
                listener(new VRKeyboardInput {
                    cNewInput0 = k.cNewInput0, cNewInput1 = k.cNewInput1,
                    cNewInput2 = k.cNewInput2, cNewInput3 = k.cNewInput3,
                    cNewInput4 = k.cNewInput4, cNewInput5 = k.cNewInput5,
                    cNewInput6 = k.cNewInput6, cNewInput7 = k.cNewInput7 });
            });
        }
        public string Show(string existingText)
        {
            var error = SteamVR.instance.overlay.ShowKeyboard(0, 0, KeyboardFlags, "TextInput", 256, existingText, 1);
            return error == EVROverlayError.None ? null : error.ToString();
        }
        public void Hide() => SteamVR.instance.overlay.HideKeyboard();
        public void ReadText(StringBuilder text, uint capacity) => SteamVR.instance.overlay.GetKeyboardText(text, capacity);
    }
}
