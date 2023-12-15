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
        private bool initialized = false;

        public static bool UsingUnderwaterEffects { get; private set; }

        public void Init(PostProcessingBehaviour postProcessingBehaviour, PostProcessingProfile originalPostProcessingProfile)
        {
            this.postProcessingBehavior = postProcessingBehaviour;
            this.originalPostProcessingProfile = originalPostProcessingProfile;

            underwaterPostProcessingProfile = Instantiate(originalPostProcessingProfile);
            underwaterPostProcessingProfile.colorGrading.enabled = true;
            underwaterPostProcessingProfile.colorGrading.m_Settings.basic.postExposure = -0.25f;
            underwaterPostProcessingProfile.colorGrading.m_Settings.basic.temperature = -10f;
            underwaterPostProcessingProfile.colorGrading.m_Settings.channelMixer.red.x = -0.25f;
            underwaterPostProcessingProfile.colorGrading.m_Settings.channelMixer.green.x = -0.125f;

            underwaterLightBlocker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            underwaterLightBlocker.layer = LayerUtils.WATER;
            underwaterLightBlocker.transform.localScale = new Vector3(1024, 0.000001f, 1024);
            underwaterLightBlocker.SetActive(false);
            underwaterLightBlocker.GetComponent<MeshRenderer>().material.color = new Vector4(0.5f, 0.5f, 0.75f, 1);
            underwaterLightBlocker.GetComponent<MeshRenderer>().receiveShadows = false;
            underwaterLightBlocker.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            underwaterLightBlocker.GetComponent<MeshRenderer>().reflectionProbeUsage = ReflectionProbeUsage.Off;
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
                }
                underwaterLightBlocker.transform.position =
                    new Vector3(
                        transform.position.x,
                        Mathf.Max(transform.position.y, Player.m_localPlayer.m_waterLevel),
                        transform.position.z);
            }
            else if (!shouldUseUnderWaterEffects && UsingUnderwaterEffects)
            {
                postProcessingBehavior.profile = originalPostProcessingProfile;
                underwaterLightBlocker.SetActive(false);
                UsingUnderwaterEffects = false;
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
