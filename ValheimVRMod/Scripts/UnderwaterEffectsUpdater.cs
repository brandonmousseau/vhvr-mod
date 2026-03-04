using UnityEngine;
using UnityEngine.PostProcessing;
using UnityEngine.Rendering;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts
{
    class UnderwaterEffectsUpdater : MonoBehaviour
    {
        private static readonly Color UNDER_WATER_OVERLAY_COLOR = new Color(0.5f, 0.75f, 0.75f);
        private GameObject underwaterOverlay;
        private Material underwaterOverlayMaterial;
        private GameObject underwaterLightBlocker = null;
        private Camera camera;
        private bool initialized = false;
        private bool isHidingWater;

        public static bool UsingUnderwaterEffects { get; private set; }
        // A smooth blending factor for transitioning between using and not using under water effects
        public static float Underwaterness { get; private set; }

        public void Init(Camera camera, PostProcessingBehaviour postProcessingBehaviour, PostProcessingProfile originalPostProcessingProfile)
        {
            this.camera = camera;

            underwaterOverlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
            underwaterOverlay.layer = LayerUtils.WORLDSPACE_UI_LAYER;
            underwaterOverlay.transform.SetParent(transform);
            underwaterOverlay.transform.localPosition = new Vector3(0, 0, 0.125f); // close to face
            underwaterOverlay.transform.localRotation = Quaternion.identity;
            underwaterOverlay.transform.localScale = new Vector3(1, 0.5f, 1); // cover FOV
            Destroy(underwaterOverlay.GetComponent<Collider>());
            var underwaterOverlayRenderer = underwaterOverlay.GetComponent<MeshRenderer>();
            underwaterOverlayMaterial = GameObject.Instantiate(VRAssetManager.GetAsset<Material>("VHVRMultiply"));
            underwaterOverlayRenderer.material = underwaterOverlayMaterial;
            underwaterOverlayRenderer.receiveShadows = false;
            underwaterOverlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
            underwaterOverlayRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            underwaterOverlay.SetActive(false);

            underwaterLightBlocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            underwaterLightBlocker.layer = LayerUtils.WATERVOLUME_LAYER;
            // TODO: consider using a one-sided plane.
            underwaterLightBlocker.transform.localScale = new Vector3(1024, 1024, 1024);
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

            Underwaterness = GetUnderwaterness();
            if (Underwaterness > 0)
            {
                if (!UsingUnderwaterEffects)
                {
                    UsingUnderwaterEffects = true;
                    underwaterOverlay.SetActive(true);
                    underwaterLightBlocker.SetActive(true);
                }
                underwaterOverlayMaterial.color = Color.Lerp(Color.white, UNDER_WATER_OVERLAY_COLOR, Underwaterness);
                underwaterLightBlocker.transform.position =
                    new Vector3(
                        transform.position.x,
                        Player.m_localPlayer.m_waterLevel + 512,
                        transform.position.z);
            }
            else if (UsingUnderwaterEffects)
            {
                UsingUnderwaterEffects = false;
                underwaterOverlay.SetActive(false);
                underwaterLightBlocker.SetActive(false);
            }

            if (Underwaterness > 0.5f)
            {
                if (!isHidingWater)
                {
                    isHidingWater = true;
                    // This hides the water from the VR camera but not from the follow camera
                    camera.cullingMask &= ~(1 << LayerUtils.WATER);
                }
            }
            else if (isHidingWater)
            {
                isHidingWater = false;
                camera.cullingMask |= (1 << LayerUtils.WATER);
            }
        }

        private float GetUnderwaterness()
        {
            var player = Player.m_localPlayer;
            if (player == null || !player.InWater())
            {
                return 0;
            }

            return Mathf.InverseLerp(
                0.0625f,
                -0.0625f,
                transform.position.y + transform.forward.y * VHVRConfig.GetNearClipPlane() - player.m_waterLevel);
        }
    }
}
