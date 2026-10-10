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
        // The time at the default FlatscreenSmoothing setting, which scales it. Larger is steadier but lags further
        // behind a deliberate head movement. Only the rotation is smoothed:
        // shake is angular, so this is where the watchability comes from, and lagging the position would let the
        // camera fall out of the character's head, which is not hidden but merely enclosing the camera, and the
        // head would then be seen from outside.
        internal const float ROTATION_SMOOTHING_TIME = 0.15f;

        // A deliberate head movement is smoothed, but a snap turn or a teleport is a jump rather than a
        // movement, and smoothing one at the full smoothing time smears the view across the whole turn. The
        // smoothing time is therefore shortened as the camera falls further behind: at this angle it is halved,
        // and it keeps shrinking with the square of the angle, so large jumps are caught up almost at once
        // without any threshold at which the camera suddenly snaps.
        internal const float CATCH_UP_ANGLE = 20f;

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

            camera.fieldOfView = VHVRConfig.FlatscreenFieldOfView();
            transform.position = targetPosition;

            float smoothingTime = ROTATION_SMOOTHING_TIME * VHVRConfig.FlatscreenSmoothingScale();
            if (!isPlaced)
            {
                transform.rotation = targetRotation;
                isPlaced = true;
                return;
            }

            transform.rotation = SmoothRotation(transform.rotation, targetRotation, smoothingTime);
        }

        internal static Quaternion SmoothRotation(Quaternion current, Quaternion target, float smoothingTime)
        {
            if (smoothingTime <= 0)
            {
                return target;
            }
            float lag = Quaternion.Angle(current, target) / CATCH_UP_ANGLE;
            float effectiveSmoothingTime = smoothingTime / (1f + lag * lag);
            // Exponential smoothing rather than a fixed Slerp fraction, so that the steadiness does not depend
            // on the frame rate.
            return Quaternion.Slerp(current, target, 1f - Mathf.Exp(-Time.deltaTime / effectiveSmoothingTime));
        }
    }
}
