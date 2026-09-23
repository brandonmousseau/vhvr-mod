using UnityEngine;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Utilities
{
    // Places the flat screen camera of the stabilized mirror mode between the eyes and steadies it, so that
    // what viewers see keeps the player's point of view without the constant small motion of a real head.
    //
    // Unlike ThirdPersonCameraUpdater this runs in LateUpdate rather than FixedUpdate, so the camera moves once
    // per rendered frame instead of at the 50 Hz physics rate, which would otherwise stutter visibly on a view
    // that tracks the head directly.
    class StabilizedCameraUpdater : MonoBehaviour
    {
        // Larger is steadier but lags further behind a deliberate head movement. Only the rotation is smoothed:
        // shake is angular, so this is where the watchability comes from, and lagging the position would let the
        // camera fall out of the character's head, which is not hidden but merely enclosing the camera, and the
        // head would then be seen from outside.
        private const float ROTATION_SMOOTHING_TIME = 0.15f;

        // A deliberate head movement is smoothed, but a snap turn or a teleport is a jump rather than a
        // movement, and smoothing one smears the view across the whole turn. Past this the camera is aimed
        // directly instead.
        private const float SNAP_ANGLE = 20f;

        // Vanilla exposes no field of view setting, so this is simply chosen. It is vertical, which at 16:9 is
        // about 107 degrees horizontally, close to a wide headset and matching the third person spectator
        // camera. Much wider stretches the edges of the frame and puts more in the frustum to draw.
        public const float FIELD_OF_VIEW = 75f;

        private Camera camera;
        private Camera vrCamera;
        private bool isPlaced;

        void LateUpdate()
        {
            if (camera == null)
            {
                camera = GetComponent<Camera>();
            }
            if (vrCamera == null)
            {
                vrCamera = CameraUtils.getCamera(CameraUtils.VR_CAMERA);
            }
            if (camera == null || vrCamera == null)
            {
                return;
            }

            // The VR camera transform is the midpoint between the two eye positions, which is exactly where a
            // view meant to represent what the player sees belongs.
            Vector3 targetPosition = vrCamera.transform.position;
            Quaternion targetRotation = vrCamera.transform.rotation;

            transform.position = targetPosition;

            if (!isPlaced || Quaternion.Angle(transform.rotation, targetRotation) > SNAP_ANGLE)
            {
                transform.rotation = targetRotation;
                isPlaced = true;
                return;
            }

            // Exponential smoothing rather than a fixed Slerp fraction, so that the steadiness does not depend
            // on the frame rate.
            transform.rotation =
                Quaternion.Slerp(
                    transform.rotation, targetRotation, 1f - Mathf.Exp(-Time.deltaTime / ROTATION_SMOOTHING_TIME));
        }
    }
}
