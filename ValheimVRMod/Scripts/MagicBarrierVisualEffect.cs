using UnityEngine.Rendering;
using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts
{
    class MagicBarrierVisualEffect : MonoBehaviour
    {
        private static Material SimpleColorOverlayMaterial;

        private SE_Shield shield;
        private MeshRenderer originalRenderer;
        private GameObject overlay;
        private Material overlayMaterial;
        private Color overlayColor = new Color(0.5f, 0.25f, 0.5f, 0.5f);

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
            overlay.layer = originalRenderer.gameObject.layer;
            var renderer = overlay.GetComponent<MeshRenderer>();
            if (VHVRConfig.EnableFullTextureMagicBarrierOverlay())
            {
                overlayMaterial = originalRenderer.material;
            }
            else
            {
                if (SimpleColorOverlayMaterial == null)
                {
                    SimpleColorOverlayMaterial = Instantiate(VRAssetManager.GetAsset<Material>("Unlit"));
                }
                overlayMaterial = SimpleColorOverlayMaterial;
                overlayMaterial.color = overlayColor;
            }
            renderer.material = overlayMaterial;
            renderer.receiveShadows = false;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.lightProbeUsage = LightProbeUsage.Off;
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
                return;
            }

            var t = shield.GetRemaningTime();
            var frequency = t > 16 ? 2 : (6 - t / 4);
            overlayColor.a = (Mathf.Abs(Mathf.Sin(Time.time * frequency)) + 1) * 0.0625f;
            overlayMaterial.color = overlayColor;
        }

        private void EnsureOverlay()
        {
            if (overlay == null)
            {
                overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(overlay.GetComponent<Collider>());
            }

            overlay.transform.SetParent(transform);
            overlay.transform.localPosition = new Vector3(0, 0, 1);
            overlay.transform.localRotation = Quaternion.identity;
            overlay.transform.localScale = new Vector3(4, 4, 1);
        }

        private void Hide()
        {
            if (overlay != null)
            {
                overlay.SetActive(false);
            }
            this.enabled = false;
        }
    }
}
