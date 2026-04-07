using UnityEngine;
using UnityEngine.PostProcessing;
using UnityEngine.Rendering;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts
{
    class UnderwaterEffectsUpdater : MonoBehaviour
    {
        private static readonly Color UNDER_WATER_OVERLAY_COLOR = new Color(0.5f, 0.75f, 0.75f);
        private static readonly Vector3 UNDER_WATER_OVERLAY_OFFSET = new Vector3(0, 0, 0.125f);
        private const float UNDER_WATER_OVERLAY_SIZE = 0.25f;
        private GameObject underwaterOverlay;
        private Material underwaterOverlayMaterial;
        private GameObject underwaterLightBlocker = null;
        private Camera camera;
        private bool initialized = false;
        private bool isHidingWater;

        // A smooth blending factor for transitioning between using and not using under water effects
        public static float Underwaterness { get; private set; }

        public void Init(Camera camera, PostProcessingBehaviour postProcessingBehaviour, PostProcessingProfile originalPostProcessingProfile)
        {
            this.camera = camera;

            underwaterOverlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
            underwaterOverlay.layer = LayerUtils.WORLDSPACE_UI_LAYER;
            underwaterOverlay.transform.SetParent(camera.transform);
            underwaterOverlay.transform.localPosition = UNDER_WATER_OVERLAY_OFFSET;
            underwaterOverlay.transform.localRotation = Quaternion.identity;
            underwaterOverlay.transform.localScale = UNDER_WATER_OVERLAY_SIZE * 2 * Vector3.one;
            Destroy(underwaterOverlay.GetComponent<Collider>());
            var underwaterOverlayRenderer = underwaterOverlay.GetComponent<MeshRenderer>();
            underwaterOverlayMaterial = GameObject.Instantiate(VRAssetManager.GetAsset<Material>("VHVRMultiply"));
            underwaterOverlayMaterial.color = UNDER_WATER_OVERLAY_COLOR;
            underwaterOverlayRenderer.material = underwaterOverlayMaterial;
            underwaterOverlayRenderer.receiveShadows = false;
            underwaterOverlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
            underwaterOverlayRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            underwaterOverlay.SetActive(false);

            underwaterLightBlocker = GameObject.CreatePrimitive(PrimitiveType.Quad);
            underwaterLightBlocker.layer = LayerUtils.WATERVOLUME_LAYER;
            // TODO: consider using a one-sided plane.
            underwaterLightBlocker.transform.localScale = new Vector3(1024, 1024, 1024);
            underwaterLightBlocker.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            Destroy(underwaterLightBlocker.GetComponent<Collider>());
            var underwaterLightBlockerRenderer = underwaterLightBlocker.GetComponent<MeshRenderer>();
            underwaterLightBlockerRenderer.material = Instantiate(VRAssetManager.GetAsset<Material>("StandardClone"));
            underwaterLightBlockerRenderer.material.color = new Vector4(0.5f, 0.5f, 0.625f, 1);
            underwaterLightBlockerRenderer.receiveShadows = false;
            underwaterLightBlockerRenderer.shadowCastingMode = ShadowCastingMode.Off;
            underwaterLightBlockerRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            underwaterLightBlocker.SetActive(false);

            initialized = true;
        }

        void FixedUpdate()
        {
            if (!initialized)
            {
                return;
            }

            var localPlayer = Player.m_localPlayer;
            var elevation =
                localPlayer == null || !localPlayer.InWater() ?
                1 :
                transform.position.y - localPlayer.m_waterLevel;

            Underwaterness = GetUnderwaterness(elevation);
            if (Underwaterness > 0)
            {
                underwaterLightBlocker.transform.position =
                    new Vector3(
                        transform.position.x,
                        Player.m_localPlayer.m_waterLevel,
                        transform.position.z);
                underwaterLightBlocker.SetActive(true);
            }
            else
            {
                underwaterLightBlocker.SetActive(false);
            }
            UpdateUnderwaterOverlay(elevation);

            camera.farClipPlane =
                Underwaterness >= 1 ?
                VRPlayer.MainCameraFarClipPlane :
                Mathf.Min(VRPlayer.MainCameraFarClipPlane, 32 / Underwaterness);

            if (isHidingWater)
            {
                if (elevation >= 0)
                {
                    camera.cullingMask |= (1 << LayerUtils.WATER);
                    isHidingWater = false;
                }
            }
            else if (elevation < -0.125f)
            {
                // This hides the water from the VR camera but not from the follow camera
                camera.cullingMask &= ~(1 << LayerUtils.WATER);
                isHidingWater = true;
            }
        }

        private void UpdateUnderwaterOverlay(float elevation)
        {
            if (elevation > VHVRConfig.GetNearClipPlane())
            {
                // Above water, hide the overlay
                underwaterOverlay.SetActive(false);
                return;
            }

            var facing = camera.transform.forward;

            if (facing.y > 0.875f && elevation > 0)
            {
                // Looking up near water, hide the overlay
                underwaterOverlay.SetActive(false);
                return;
            }

            if ((facing.y < -0.875f && elevation < 0) || elevation < -VHVRConfig.GetNearClipPlane())
            {
                // Underwater or looking down near water, cover entire view with overlay
                underwaterOverlay.transform.localRotation = Quaternion.identity;
                underwaterOverlay.transform.localPosition = UNDER_WATER_OVERLAY_OFFSET;
                underwaterOverlay.SetActive(true);
                return;
            }

            // Eyes near water surface, rotate and move overlay to only cover the underwater part of view
            underwaterOverlay.transform.rotation = Quaternion.LookRotation(camera.transform.forward, Vector3.up);
            var headingSqrMagnitude = facing.x * facing.x + facing.z * facing.z;
            var verticalOffset = -elevation - UNDER_WATER_OVERLAY_SIZE * Mathf.Sqrt(headingSqrMagnitude);
            // Horizontal compensation to make sure that the overlay is slightly in front of the camera
            var horizontalCompensation =
                (UNDER_WATER_OVERLAY_OFFSET.z - facing.y * verticalOffset) / headingSqrMagnitude;
            underwaterOverlay.transform.position =
                camera.transform.position +
                new Vector3(horizontalCompensation * facing.x, verticalOffset, horizontalCompensation * facing.z);
            underwaterOverlay.SetActive(true);
        }

        private static float GetUnderwaterness(float elevation)
        {
            return Mathf.InverseLerp(0, -0.0625f, elevation);
        }
    }
}
