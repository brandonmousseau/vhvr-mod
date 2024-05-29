using UnityEngine;
using UnityEngine.PostProcessing;
using UnityEngine.Rendering;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts
{
    class UnderwaterEffectsUpdater : MonoBehaviour
    {
        private GameObject underwaterLightBlocker = null;
        private PostProcessingBehaviour postProcessingBehavior = null;
        private PostProcessingProfile originalPostProcessingProfile = null;
        private PostProcessingProfile underwaterPostProcessingProfile = null;
        private Camera camera;
        private bool initialized = false;

        public static bool UsingUnderwaterEffects { get; private set; }

        public void Init(Camera camera, PostProcessingBehaviour postProcessingBehaviour, PostProcessingProfile originalPostProcessingProfile)
        {
            this.camera = camera;
            this.postProcessingBehavior = postProcessingBehaviour;
            this.originalPostProcessingProfile = originalPostProcessingProfile;

            underwaterPostProcessingProfile = Instantiate(originalPostProcessingProfile);
            underwaterPostProcessingProfile.colorGrading.enabled = true;
            underwaterPostProcessingProfile.colorGrading.m_Settings.basic.postExposure = -0.25f;
            underwaterPostProcessingProfile.colorGrading.m_Settings.basic.temperature = -10f;
            underwaterPostProcessingProfile.colorGrading.m_Settings.channelMixer.red.x = -0.25f;
            underwaterPostProcessingProfile.colorGrading.m_Settings.channelMixer.green.x = -0.125f;

            underwaterLightBlocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            underwaterLightBlocker.layer = LayerUtils.WATERVOLUME_LAYER;
            // TODO: consider using a one-sided plane.
            underwaterLightBlocker.transform.localScale = new Vector3(1024, 1024, 1024);
            underwaterLightBlocker.SetActive(false);
            var renderer = underwaterLightBlocker.GetComponent<MeshRenderer>();
            renderer.material = Instantiate(VRAssetManager.GetAsset<Material>("StandardClone"));
            renderer.material.color = new Vector4(0.5f, 0.5f, 0.625f, 1);
            renderer.receiveShadows = false;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            Destroy(underwaterLightBlocker.GetComponent<Collider>());

            initialized = true;
        }

        void FixedUpdate()
        {
            if (!initialized)
            {
                return;
            }

            bool shouldUseUnderWaterEffects = ShouldUseUnderWaterEffects();

            if (shouldUseUnderWaterEffects)
            {
                if (!UsingUnderwaterEffects)
                {
                    postProcessingBehavior.profile = underwaterPostProcessingProfile;
                    underwaterLightBlocker.SetActive(true);
                    UsingUnderwaterEffects = true;
                    // This hides the water from the VR camera but not from the follow camera
                    camera.cullingMask &= ~(1 << LayerUtils.WATER);
                }
                underwaterLightBlocker.transform.position =
                    new Vector3(
                        transform.position.x,
                        Player.m_localPlayer.m_waterLevel + 512,
                        transform.position.z);
            }
            else if (!shouldUseUnderWaterEffects && UsingUnderwaterEffects)
            {
                postProcessingBehavior.profile = originalPostProcessingProfile;
                underwaterLightBlocker.SetActive(false);
                UsingUnderwaterEffects = false;
                camera.cullingMask |= (1 << LayerUtils.WATER);
            }
        }

        private bool ShouldUseUnderWaterEffects()
        {
            if (!Player.m_localPlayer || !Player.m_localPlayer.InWater())
            {
                return false;
            }

            return transform.position.y + transform.forward.y * VHVRConfig.GetNearClipPlane() < Player.m_localPlayer.m_waterLevel;
        }
    }
}
