using UnityEngine;
using Valve.VR;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts
{
    // Scales the distance between the two eyes of a stereo camera by VHVRConfig.EyeSeparationScale(): 1 leaves the
    // headset's own eye positions alone, 0 puts both eyes at the head centre so that they see the same viewpoint (mono)
    // while head tracking stays as it is. Only the position of each eye is changed. Its rotation and its projection
    // matrix are left to the headset, since on headsets with canted displays those differ between the eyes.
    // Only the cameras that render the game world get this. The hands and VR GUI cameras keep the headset's own eye
    // views, so that the hands and the menus stay where the user feels and expects them.
    [RequireComponent(typeof(Camera))]
    class EyeSeparationScaler : MonoBehaviour
    {
        private Camera stereoCamera;
        private bool overridden;

        void Awake()
        {
            stereoCamera = GetComponent<Camera>();
        }

        void OnEnable()
        {
            Application.onBeforeRender += UpdateEyeViews;
        }

        // Runs before any camera renders and after the tracked pose driver has moved the head for this frame.
        // OnPreCull() would be too late: by then the first eye to render has already taken its view matrix, so it
        // would render with the previous frame's.
        private void UpdateEyeViews()
        {
            float scale = VHVRConfig.EyeSeparationScale();
            var system = OpenVR.System;
            if (scale >= 1 || !stereoCamera.stereoEnabled || system == null)
            {
                StopOverriding();
                return;
            }

            // The eye views are built from the camera transform and the headset's eye offsets. Reading them back
            // with GetStereoViewMatrix() is not an option: once overridden, resetting them does not bring up to
            // date headset matrices back within the frame.
            // Camera.worldToCameraMatrix is not used for the same reason: on a stereo camera it can be a matrix left
            // behind by XR or by Camera.CopyFrom() that no longer follows the transform.
            Matrix4x4 headView =
                Matrix4x4.Scale(new Vector3(1, 1, -1)) *
                Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one).inverse;
            // Offsets are in metres of the play area, which the camera rig may scale.
            float offsetScale = scale * transform.lossyScale.x;
            stereoCamera.SetStereoViewMatrix(
                Camera.StereoscopicEye.Left, GetHeadToEye(system, EVREye.Eye_Left, offsetScale) * headView);
            stereoCamera.SetStereoViewMatrix(
                Camera.StereoscopicEye.Right, GetHeadToEye(system, EVREye.Eye_Right, offsetScale) * headView);
            overridden = true;
        }

        void OnDisable()
        {
            Application.onBeforeRender -= UpdateEyeViews;
            StopOverriding();
        }

        // OpenVR's head space has the same axes as Unity's camera space (x right, y up, z backward), so its eye to
        // head transform applies to a view matrix as is.
        private static Matrix4x4 GetHeadToEye(CVRSystem system, EVREye eye, float offsetScale)
        {
            HmdMatrix34_t m = system.GetEyeToHeadTransform(eye);
            Matrix4x4 eyeToHead = Matrix4x4.identity;
            eyeToHead.SetRow(0, new Vector4(m.m0, m.m1, m.m2, m.m3 * offsetScale));
            eyeToHead.SetRow(1, new Vector4(m.m4, m.m5, m.m6, m.m7 * offsetScale));
            eyeToHead.SetRow(2, new Vector4(m.m8, m.m9, m.m10, m.m11 * offsetScale));
            return eyeToHead.inverse;
        }

        private void StopOverriding()
        {
            if (!overridden)
            {
                return;
            }
            stereoCamera.ResetStereoViewMatrices();
            overridden = false;
        }
    }
}
