using UnityEngine;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Utilities
{
    class ThirdPersonCameraUpdater : MonoBehaviour
    {
        private readonly int VIEW_OBSTRUCTION_LAYER_MASK =
            Physics.DefaultRaycastLayers &
            ~(1 << LayerUtils.CHARACTER) &
            ~(1 << LayerUtils.ITEM_LAYER) &
            ~(1 << LayerUtils.CHARARCTER_TRIGGER) &
            ~(1 << 31); // Smoke
        private Camera camera;
        private Camera vrCamera;
        private Vector3 velocity;
        private MeshRenderer cameraDot;

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
                transform.SetPositionAndRotation(vrCamera.transform.position, vrCamera.transform.rotation);
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
            if (PlayerCustomizaton.IsBarberGuiVisible())
            {
                viewPoint = vrCamera.transform.position;
                viewPoint.y = targetPosition.y;
            }
            else if (VHVRConfig.UseFollowCameraOnFlatscreen())
            {
                viewPoint = targetPosition + Vector3.up * 2 - vrCamera.transform.forward * 3.5f;
            }
            else
            {
                // Spectator mode
                viewPoint = transform.position;
                viewPoint.y = Mathf.Max(viewPoint.y, vrCamera.transform.position.y + 0.25f);
            }

            ClampViewPointToAvoidObstruction(targetPosition, maxDistance: 3, ref viewPoint);

            transform.position = Vector3.SmoothDamp(transform.position, viewPoint, ref velocity, 0.25f);
            transform.LookAt(targetPosition);

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
                cameraDot.transform.localPosition = Vector3.zero;
                cameraDot.transform.localRotation = Quaternion.Euler(90, 0, 0);
                cameraDot.material = Instantiate(VRAssetManager.GetAsset<Material>("Unlit"));
                cameraDot.material.color = Color.red;
                cameraDot.gameObject.layer = LayerUtils.getUiPanelLayer();
            }

            cameraDot.transform.localScale =
                new Vector3(0.0075f, 0.001f, 0.0075f) * Vector3.Distance(transform.position, vrCamera.transform.position);
        }
    }
}
