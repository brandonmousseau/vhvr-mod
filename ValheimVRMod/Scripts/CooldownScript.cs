using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts {
    public class CooldownScript
        : MonoBehaviour {

        public float maxCooldown;
        private float cooldownTimer;

        private GameObject grayLayer;
        private GameObject yellowLayer;
        
        private void Awake() {
            
            var tex_gray = new Texture2D(1, 1);
            tex_gray.SetPixel(0,0, Color.grey);
            tex_gray.Apply();
        
            var tex_yellow = new Texture2D(1, 1);
            tex_yellow.SetPixel(0,0, Color.yellow);
            tex_yellow.Apply();
            
            grayLayer = new GameObject();
            grayLayer.layer = LayerUtils.getWorldspaceUiLayer();
            grayLayer.transform.SetParent(transform, false);
            var grayRenderer = grayLayer.AddComponent<SpriteRenderer>();
            grayRenderer.sprite = Sprite.Create(tex_gray, new Rect(0.0f, 0.0f, tex_gray.width, tex_gray.height), new Vector2(0.5f, 0.5f));
            grayRenderer.sortingOrder = 0;
            grayLayer.SetActive(false);
            
            yellowLayer = new GameObject();
            yellowLayer.layer = LayerUtils.getWorldspaceUiLayer();
            yellowLayer.transform.SetParent(transform, false);
            var yellowRenderer = yellowLayer.AddComponent<SpriteRenderer>();
            yellowRenderer.sprite = Sprite.Create(tex_yellow, new Rect(0.0f, 0.0f, tex_yellow.width, tex_yellow.height), new Vector2(0.5f, 0.5f));
            yellowRenderer.sortingOrder = 2;
            yellowLayer.SetActive(false);

        }

        public void startCooldown() {
            cooldownTimer = maxCooldown;
            grayLayer.SetActive(true);
            yellowLayer.SetActive(true);
        }

        public bool isInCooldown() {
            return cooldownTimer > 0;
        }
        
        private void FixedUpdate() {
            
            if (! isInCooldown()) {
                return;
            }

            cooldownTimer -= Time.fixedDeltaTime;
            yellowLayer.transform.localScale = new Vector3(cooldownTimer / maxCooldown, 1, 1);

            if (! isInCooldown()) {
                grayLayer.SetActive(false);
                yellowLayer.SetActive(false);
                yellowLayer.transform.localScale = Vector3.one;
            }
        }
    }
}