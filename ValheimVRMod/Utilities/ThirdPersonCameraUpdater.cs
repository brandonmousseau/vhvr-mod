using UnityEngine;
using ValheimVRMod.Scripts;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Utilities
{
    class ThirdPersonCameraUpdater : MonoBehaviour
    {
        private readonly int VIEW_OBSTRUCTION_LAYER_MASK =
            Physics.DefaultRaycastLayers &
            ~(1 << 3) &
            ~(1 << LayerUtils.CHARACTER) &
            ~(1 << LayerUtils.ITEM_LAYER) &
            ~(1 << LayerUtils.CHARARCTER_TRIGGER) &
            ~(1 << 31); // Smoke
        private Camera camera;
        private Camera vrCamera;
        private Vector3 velocity;
        private MeshRenderer cameraDot;
        private float cameraSpeed;
        private float targetCameraSpeed;

        private Vector3 targetCurrentPosition;
        private Vector3 targetVelocity;

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
                    viewTarget = vrCamera.transform.position + vrCamera.transform.forward * 1.5f;
                    viewPoint = targetPosition + Vector3.up * 3 - vrCamera.transform.forward * 3.5f;
                }
            }
            else
            {
                // Spectator mode
                viewPoint = transform.position;
                viewPoint.y = Mathf.Max(viewPoint.y, vrCamera.transform.position.y + 0.25f);
            }

            ClampViewPointToAvoidObstruction(targetPosition, maxDistance: 3, ref viewPoint);

            transform.position = Vector3.SmoothDamp(transform.position, viewPoint, ref velocity, cameraSpeed);
            targetCurrentPosition = Vector3.SmoothDamp(targetCurrentPosition, viewTarget, ref targetVelocity, targetCameraSpeed);
            transform.LookAt(targetCurrentPosition);

            UpdateCameraDot();
        }

        private void ClampViewPointToAvoidObstruction(Vector3 target, float maxDistance, ref Vector3 viewPoint)
        {
            var hits =
                Physics.RaycastAll(
                    target,
                    viewPoint - target,
                    maxDistance,
                    camera.cullingMask & VIEW_OBSTRUCTION_LAYER_MASK);

            var distance = maxDistance;
            foreach (var hit in hits)
            {
                if (hit.distance > distance)
                {
                    continue;
                }
                if (Player.m_localPlayer != null &&
                    hit.collider.attachedRigidbody != null &&
                    hit.collider.attachedRigidbody.gameObject == Player.m_localPlayer.gameObject)
                {
                    continue;
                }
                if (hit.collider.GetComponent<MeshRenderer>() == null && hit.collider.GetComponent<MeshRenderer>() == null)
                {
                    continue;
                }
                if (hit.collider.GetComponentInParent<Player>() == Player.m_localPlayer && Player.m_localPlayer != null)
                {
                    continue;
                }

                distance = hit.distance;
            }

            viewPoint = Vector3.MoveTowards(target, viewPoint, distance);
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
