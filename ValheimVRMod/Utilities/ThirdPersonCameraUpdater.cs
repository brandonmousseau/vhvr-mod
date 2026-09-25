using UnityEngine;
using ValheimVRMod.Scripts;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Utilities
{
    class ThirdPersonCameraUpdater : MonoBehaviour
    {
        // Within this of the eyes the camera is inside the head, e. g. at a follow distance of 0, where the head gear
        // would fill the view. Checked against where the camera actually is rather than the setting, since the
        // smoothing still carries it in or out of the head for a moment after the view point changes.
        private const float INSIDE_HEAD_DISTANCE = 0.3f;

        // Below this follow distance the camera is so close to the head that lagging behind it would carry the
        // camera out of the head and show its inside. The position then tracks the eyes directly, and the camera
        // looks where the player looks with the horizon kept level, smoothed like the stabilized camera, instead of
        // being pulled back for things like drawing a bow.
        private const float DIRECT_FOLLOW_DISTANCE = 0.25f;

        private const float MAX_VIEW_DISTANCE = 3f;

        private Camera camera;
        private Camera vrCamera;
        private Vector3 velocity;
        private MeshRenderer cameraDot;
        private float cameraSpeed;
        private float targetCameraSpeed;

        private Vector3 targetCurrentPosition;
        private Vector3 targetVelocity;
        private bool followsEyesDirectly;

        // Whether this camera looks at the player character from outside, as opposed to following the eyes
        // directly, in which case it should show what the headset shows.
        public bool ViewsCharacterFromOutside { get { return !followsEyesDirectly; } }

        void FixedUpdate()
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

            camera.fieldOfView = VHVRConfig.FlatscreenFieldOfView();
            // Hides the head itself when the camera is inside it, as it does for the VR camera. Copied at creation
            // too, but kept in step here in case the setting changes.
            camera.nearClipPlane = vrCamera.nearClipPlane;
            followsEyesDirectly = false;

            if (!Player.m_localPlayer)
            {
                var panel = VRCore.UI.VRGUI.getUiPanel();
                if (panel)
                {
                    transform.position = panel.transform.position - panel.transform.forward * 1.5f;
                    transform.LookAt(panel.transform.position);
                }
                else
                {
                    transform.SetPositionAndRotation(vrCamera.transform.position, vrCamera.transform.rotation);
                }
                
                velocity = Vector3.zero;
                return;
            }

            if (VHVRConfig.UseFollowCameraOnFlatscreen() &&
                VHVRConfig.FollowCameraDistance() < DIRECT_FOLLOW_DISTANCE)
            {
                // Placed in LateUpdate instead.
                followsEyesDirectly = true;
                return;
            }

            var targetPosition =
                VRPlayer.inFirstPerson ?
                vrCamera.transform.position :
                Player.m_localPlayer.transform.position + Vector3.up * 0.5f;
            if (PlayerCustomizaton.IsBarberGuiVisible())
            {
                targetPosition.y += 0.5f;
            }

            Vector3 viewPoint;
            Vector3 viewTarget = targetPosition;
            var uiPanel = VRCore.UI.VRGUI.getUiPanel();
            cameraSpeed = 0.15f;
            targetCameraSpeed = 0.2f;
            float maxViewDistance = MAX_VIEW_DISTANCE;
            if (PlayerCustomizaton.IsBarberGuiVisible())
            {
                viewPoint = vrCamera.transform.position;
                viewPoint.y = targetPosition.y;
            }
            else if (VHVRConfig.UseFollowCameraOnFlatscreen())
            {
                if (Player.m_localPlayer.IsSleeping() || Player.m_localPlayer.IsTeleporting())
                {
                    viewTarget = uiPanel.transform.position;
                    viewPoint = uiPanel.transform.position - uiPanel.transform.forward * 1.5f;
                }
                else if (VRPlayer.IsClickableGuiOpen)
                {
                    viewTarget = uiPanel.transform.position;
                    viewPoint = targetPosition - uiPanel.transform.right * 0.5f + Vector3.up * 0.3f - vrCamera.transform.forward * 0.3f;
                    cameraSpeed = 0.01f;
                    targetCameraSpeed = 0.01f;
                }
                else if (Player.m_localPlayer.IsDrawingBow() || ThrowableManager.isAiming || CrossbowManager.isAiming)
                {
                    viewTarget = vrCamera.transform.position - Vector3.up + vrCamera.transform.forward * 6;
                    viewPoint =
                        targetPosition + Vector3.up * 0.3f - vrCamera.transform.forward * 1.5f +
                        Player.m_localPlayer.transform.right * (VHVRConfig.LeftHanded() ? 0.7f : -0.7f);
                    cameraSpeed = 0.1f;
                }
                else if (BowLocalManager.instance || CrossbowMorphManager.instance)
                {
                    viewTarget = vrCamera.transform.position - Vector3.up + vrCamera.transform.forward * 3;

                    viewPoint =
                        targetPosition - Player.m_localPlayer.transform.right + Vector3.up * 0.3f - vrCamera.transform.forward * 2f;

                    cameraSpeed = 0.1f;
                }
                // When holding both grab, usually happens when trying to hit monster & two-handing
                else if (LocalWeaponWield.isCurrentlyTwoHanded() ||
                    (!Player.m_localPlayer.InPlaceMode()
                    && SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand)
                    && SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand)))
                {
                    var lateralOffset = Player.m_localPlayer.transform.right * 3;
                    switch (LocalWeaponWield.LocalPlayerTwoHandedState)
                    {
                        case WeaponWield.TwoHandedState.LeftHandBehind:
                            lateralOffset *= -1;
                            break;
                        case WeaponWield.TwoHandedState.SingleHanded:
                            if (VHVRConfig.LeftHanded())
                            {
                                lateralOffset *= -1;
                            }
                            break;
                    }

                    viewTarget = vrCamera.transform.position - Vector3.up + vrCamera.transform.forward * 3;
                    viewPoint = targetPosition + lateralOffset + Vector3.up * 2f - vrCamera.transform.forward * 3f;
                    cameraSpeed = 0.1f;
                }
                else
                {
                    float distance = VHVRConfig.FollowCameraDistance();
                    viewTarget = vrCamera.transform.position + vrCamera.transform.forward * 1.5f;
                    viewPoint = getFollowViewPoint(targetPosition, distance);
                    // The offset above is further than the clamp allows, so the clamp is what actually sets how
                    // far the camera sits, and it has to scale too for the setting to have any effect.
                    maxViewDistance *= distance;
                }
            }
            else
            {
                // Spectator mode
                viewPoint = transform.position;
                viewPoint.y = Mathf.Max(viewPoint.y, vrCamera.transform.position.y + 0.25f);
            }

            viewPoint = Vector3.MoveTowards(targetPosition, viewPoint, maxViewDistance);
            viewPoint = CameraObstructionUtils.ClampToAvoidObstruction(targetPosition, viewPoint);

            float smoothingScale = VHVRConfig.FlatscreenSmoothingScale();
            cameraSpeed *= smoothingScale;
            targetCameraSpeed *= smoothingScale;
            // Checked again after smoothing, which would otherwise leave the camera inside a wall for as long as it
            // takes to catch up with the clamped view point.
            transform.position =
                CameraObstructionUtils.ClampToAvoidObstruction(
                    targetPosition, Vector3.SmoothDamp(transform.position, viewPoint, ref velocity, cameraSpeed));
            targetCurrentPosition = Vector3.SmoothDamp(targetCurrentPosition, viewTarget, ref targetVelocity, targetCameraSpeed);
            transform.LookAt(targetCurrentPosition);

            UpdateHeadGearCulling();
            UpdateCameraDot();
        }

        // FixedUpdate places the camera only at the physics rate, which is enough for a camera smoothing toward
        // its view point but would leave one meant to stay at the eyes trailing them between physics steps.
        void LateUpdate()
        {
            if (!followsEyesDirectly || camera == null || vrCamera == null || !Player.m_localPlayer)
            {
                return;
            }
            float distance = VHVRConfig.FollowCameraDistance();
            Vector3 eyePosition = getEyePosition();
            Vector3 viewPoint =
                Vector3.MoveTowards(
                    eyePosition, getFollowViewPoint(eyePosition, distance), MAX_VIEW_DISTANCE * distance);
            transform.position = CameraObstructionUtils.ClampToAvoidObstruction(eyePosition, viewPoint);

            Quaternion targetRotation = vrCamera.transform.rotation;
            Vector3 forward = vrCamera.transform.forward;
            // Looking straight up or down leaves no horizon to level, and LookRotation has no defined roll there.
            if (Mathf.Abs(forward.y) < 0.99f)
            {
                targetRotation = Quaternion.LookRotation(forward, Vector3.up);
            }
            float smoothingTime =
                StabilizedCameraUpdater.ROTATION_SMOOTHING_TIME * VHVRConfig.FlatscreenSmoothingScale();
            if (smoothingTime <= 0 ||
                Quaternion.Angle(transform.rotation, targetRotation) > StabilizedCameraUpdater.SNAP_ANGLE)
            {
                transform.rotation = targetRotation;
            }
            else
            {
                transform.rotation =
                    Quaternion.Slerp(
                        transform.rotation, targetRotation, 1f - Mathf.Exp(-Time.deltaTime / smoothingTime));
            }

            // Leaves the smoothing in FixedUpdate at rest where this camera is, so that going back out to a larger
            // distance or a pulled back view starts from here instead of from wherever it was left.
            velocity = Vector3.zero;
            targetCurrentPosition = transform.position + transform.forward * 1.5f;
            targetVelocity = Vector3.zero;

            UpdateHeadGearCulling();
            UpdateCameraDot();
        }

        // Hides the head gear (helmet, hair and beard), which HeadEquipVisibiltiyUpdater moves to this layer to keep
        // it from the VR camera.
        private void UpdateHeadGearCulling()
        {
            if (Vector3.Distance(transform.position, getEyePosition()) < INSIDE_HEAD_DISTANCE)
            {
                camera.cullingMask &= ~(1 << LayerUtils.CHARARCTER_TRIGGER);
            }
            else
            {
                camera.cullingMask |= (1 << LayerUtils.CHARARCTER_TRIGGER);
            }
        }

        // The VR camera sits between the eyes in first person. When the VR view is zoomed out behind the character
        // the character's own eyes are used instead.
        private Vector3 getEyePosition()
        {
            return VRPlayer.inFirstPerson ? vrCamera.transform.position : Player.m_localPlayer.m_eye.position;
        }

        private Vector3 getFollowViewPoint(Vector3 targetPosition, float distance)
        {
            return targetPosition + (Vector3.up * 3 - vrCamera.transform.forward * 3.5f) * distance;
        }

        private void UpdateCameraDot()
        {
            if (cameraDot == null)
            {
                cameraDot = GameObject.CreatePrimitive(PrimitiveType.Cylinder).GetComponent<MeshRenderer>();
                cameraDot.transform.parent = transform;
                cameraDot.transform.localPosition = -0.01f * Vector3.forward;
                cameraDot.transform.localRotation = Quaternion.Euler(90, 0, 0);
                cameraDot.material = Instantiate(VRAssetManager.GetAsset<Material>("Unlit"));
                cameraDot.material.color = Color.red;
                cameraDot.gameObject.layer = LayerUtils.getWorldspaceUiLayer();
                Destroy(cameraDot.GetComponent<Collider>());
            }

            cameraDot.transform.localScale =
                new Vector3(0.0075f, 0.001f, 0.0075f) * Vector3.Distance(transform.position, vrCamera.transform.position);
        }
    }
}
