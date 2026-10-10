using System;
using System.Text;
using UnityEngine;

namespace ValheimVRMod.VRCore.Backends
{
    public enum VRMirrorViewMode { None, Left, Right, OpenVR }
    public struct VRTrackerInfo
    {
        public int Index;
        public string Title;
    }
    public struct VRKeyboardInput
    {
        public byte cNewInput0, cNewInput1, cNewInput2, cNewInput3;
        public byte cNewInput4, cNewInput5, cNewInput6, cNewInput7;
    }
    public interface IVRKeyboardBackend
    {
        void ListenClosed(Action listener);
        void ListenInput(Action<VRKeyboardInput> listener);
        void ListenDone(Action listener);
        // Null means success; otherwise preserve the runtime's diagnostic name.
        string Show(string existingText);
        void Hide();
        void ReadText(StringBuilder text, uint capacity);
    }
    public enum VROverlayError { None, InvalidHandle, UnknownOverlay, Other }
    public interface IVROverlayBackend
    {
        VROverlayError CreateOverlay(string key, string name, ref ulong handle);
        void DestroyOverlay(ulong handle);
        VROverlayError FindOverlay(string key, ref ulong handle);
        VROverlayError SetOverlayCurvature(ulong handle, float curvature);
        VROverlayError ShowOverlay(ulong handle);
        void SetTexture(ulong handle, IntPtr texture);
        void SetOverlayAlpha(ulong handle, float alpha);
        void SetWidthAndTransform(ulong handle, float width, Vector3 position, Quaternion rotation);
        Vector3 GetDirection(ulong handle);
    }
    public static class VROverlay
    {
        public const ulong InvalidHandle = 0;
    }
}
