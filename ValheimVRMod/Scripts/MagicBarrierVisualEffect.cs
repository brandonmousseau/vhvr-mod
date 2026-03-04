using UnityEngine.Rendering;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts
{
    class MagicBarrierVisualEffect : MonoBehaviour
    {
        private static Material SimpleColorMaterial;

        private SE_Shield shield;
        private MeshRenderer originalRenderer;
        private GameObject simpleColorOverlay;
        private GameObject fullTextureOverlay;
        private Color overlayColor = new Color(0.25f, 0.125f, 0.5f, 0.375f);

        public void Show(SE_Shield shield, Character character)
        {
            var vfx = character.transform.Find("vfx_StaffShield(Clone)");
            if (vfx == null)
            {
                LogUtils.LogError("Magic barrier not found");
                return;
            }
            var originalRenderer = vfx.gameObject.GetComponentInChildren<MeshRenderer>();
            if (originalRenderer == null)
            {
                LogUtils.LogError("Magic barrier renderer not found");
                return;
            }

            this.enabled = true;
            this.shield = shield;
            this.originalRenderer = originalRenderer;

            EnsureOverlay();
            fullTextureOverlay.layer = originalRenderer.gameObject.layer;
            simpleColorOverlay.layer = LayerUtils.WORLDSPACE_UI_LAYER;
            var renderer = simpleColorOverlay.GetComponent<MeshRenderer>();
            fullTextureOverlay.GetComponent<MeshRenderer>().material = originalRenderer.material;
            if (SimpleColorMaterial == null)
            {
                SimpleColorMaterial = Instantiate(VRAssetManager.GetAsset<Material>("Unlit"));
            }
            simpleColorOverlay.GetComponent<MeshRenderer>().material = SimpleColorMaterial;
            SimpleColorMaterial.color = overlayColor;
        }

        void FixedUpdate()
        {
            if (shield == null || shield.IsDone())
            {
                Hide();
                return;
            }

            var p = originalRenderer.transform.position;
            // Without this the magic bubble would be centered around feet
            originalRenderer.transform.position = new Vector3(p.x, transform.position.y, p.z);

            if (VHVRConfig.EnableFullTextureMagicBarrierOverlay())
            {
                fullTextureOverlay.SetActive(true);
                simpleColorOverlay.SetActive(false);
                return;
            }

            var t = shield.GetRemaningTime();
            var frequency = t > 16 ? 2 : (6 - t / 4);
            overlayColor.b = (Mathf.Abs(Mathf.Sin(Time.time * frequency)) + 1) * 0.25f;
            overlayColor.a = 0.125f;
            SimpleColorMaterial.color = overlayColor;

            fullTextureOverlay.SetActive(false);
            simpleColorOverlay.SetActive(VRPlayer.inFirstPerson);
        }

        private void EnsureOverlay()
        {
            if (simpleColorOverlay == null)
            {
                simpleColorOverlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(simpleColorOverlay.GetComponent<Collider>());
                var renderer = simpleColorOverlay.GetComponent<Renderer>();
                renderer.receiveShadows = false;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                simpleColorOverlay.SetActive(false);
            }

            simpleColorOverlay.transform.SetParent(transform);
            simpleColorOverlay.transform.localPosition = new Vector3(0, 0, 0.125f);
            simpleColorOverlay.transform.localRotation = Quaternion.identity;
            simpleColorOverlay.transform.localScale = new Vector3(0.5f, 0.5f, 1);

            if (fullTextureOverlay == null)
            {
                fullTextureOverlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(fullTextureOverlay.GetComponent<Collider>());
                var renderer = fullTextureOverlay.GetComponent<Renderer>();
                renderer.receiveShadows = false;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                fullTextureOverlay.SetActive(false);
            }

            fullTextureOverlay.transform.SetParent(transform);
            fullTextureOverlay.transform.localPosition = new Vector3(0, 0, 1);
            fullTextureOverlay.transform.localRotation = Quaternion.identity;
            fullTextureOverlay.transform.localScale = new Vector3(4, 4, 1);
        }

        private void Hide()
        {
            if (simpleColorOverlay != null)
            {
                simpleColorOverlay.SetActive(false);
            }
            this.enabled = false;
        }
    }
}
