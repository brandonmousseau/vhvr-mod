using System;
using UnityEngine;
using Valve.VR;

namespace ValheimVRMod.VRCore.Backends
{
    public sealed class SteamVROverlayBackend : IVROverlayBackend
    {
        readonly CVROverlay overlay;
        public SteamVROverlayBackend(CVROverlay overlay) { this.overlay = overlay; }
        static VROverlayError Error(EVROverlayError error)
        {
            switch (error)
            {
                case EVROverlayError.None: return VROverlayError.None;
                case EVROverlayError.InvalidHandle: return VROverlayError.InvalidHandle;
                case EVROverlayError.UnknownOverlay: return VROverlayError.UnknownOverlay;
                default: return VROverlayError.Other;
            }
        }
        public VROverlayError CreateOverlay(string key, string name, ref ulong handle) => Error(overlay.CreateOverlay(key, name, ref handle));
        public void DestroyOverlay(ulong handle) => overlay.DestroyOverlay(handle);
        public VROverlayError FindOverlay(string key, ref ulong handle) => Error(overlay.FindOverlay(key, ref handle));
        public VROverlayError SetOverlayCurvature(ulong handle, float curvature) => Error(overlay.SetOverlayCurvature(handle, curvature));
        public VROverlayError ShowOverlay(ulong handle) => Error(overlay.ShowOverlay(handle));
        public void SetTexture(ulong handle, IntPtr texture)
        {
            var tex = new Texture_t { handle = texture, eType = SteamVR.instance.textureType, eColorSpace = EColorSpace.Auto };
            overlay.SetOverlayTexture(handle, ref tex);
        }
        public void SetOverlayAlpha(ulong handle, float alpha) => overlay.SetOverlayAlpha(handle, alpha);
        public void SetWidthAndTransform(ulong handle, float width, Vector3 position, Quaternion rotation)
        {
            var offset = new SteamVR_Utils.RigidTransform(position, rotation);
            overlay.SetOverlayWidthInMeters(handle, width);
            var t = offset.ToHmdMatrix34();
            overlay.SetOverlayTransformAbsolute(handle, SteamVR.settings.trackingSpace, ref t);
        }
        public Vector3 GetDirection(ulong handle)
        {
            var currentTransform = new HmdMatrix34_t();
            var trackingOrigin = SteamVR.settings.trackingSpace;
            var error = overlay.GetOverlayTransformAbsolute(handle, ref trackingOrigin, ref currentTransform);
            if (error != EVROverlayError.None) return Vector3.forward;
            return new SteamVR_Utils.RigidTransform(currentTransform).rot * Vector3.forward;
        }
    }
}
