using System.Collections.Generic;
using TMPro;
using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts
{
    class VRDamageTexts : MonoBehaviour
    {
        private const int MAX_COUNT = 16;
        private static readonly Queue<VRDamageTexts> pool = new Queue<VRDamageTexts>();

        private Canvas canvasText ;
        private TextMeshProUGUI currText;
        private float timer = 0f;
        private float textDuration = 1.5f;
        private bool selfText;

        // The scale per meter of distance from the VR camera, when a text about something other than the player
        // appears, so that it appears equally legible at any distance. Not below MIN_SCALE_DISTANCE, for a text right in
        // front of the camera to not become too small to read.
        private const float SCALE_PER_DISTANCE = 0.00025f;
        private const float MIN_SCALE_DISTANCE = 2f;

        private static Camera vrCam;

        public static VRDamageTexts Pool()
        {
            VRDamageTexts member = pool.Count < MAX_COUNT ? new GameObject().AddComponent<VRDamageTexts>() : pool.Dequeue();
            if (member == null || member.gameObject == null)
            {
                member = new GameObject().AddComponent<VRDamageTexts>();
            }
            member.gameObject.SetActive(true);
            member.enabled = true;
            pool.Enqueue(member);
            return member;
        }

        private void Awake()
        {
            canvasText = gameObject.GetOrAddComponent<Canvas>();
            canvasText.renderMode = RenderMode.WorldSpace;
            currText = gameObject.GetOrAddComponent<TextMeshProUGUI>();
            currText.enableAutoSizing = false;
            currText.fontSize = 80;
            currText.textWrappingMode = TextWrappingModes.NoWrap;
            currText.overflowMode = TextOverflowModes.Overflow;
            currText.alignment = TextAlignmentOptions.Center;
            currText.raycastTarget = false;
            currText.enabled = true;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            timer += dt;
            if (timer > textDuration)
            {
                gameObject.SetActive(false);
                return;
            }

            if (selfText)
            {
                transform.localPosition += new Vector3(0, dt / 30, dt / 200);
            }
            else
            {
                if (vrCam == null)
                {
                    gameObject.SetActive(false);
                    return;
                }
                // Rises in the world like the vanilla text does.
                transform.position += Vector3.up * dt;
                faceCamera();
            }

            var colorA = currText.color;
            colorA.a = 1f - Mathf.Pow(Mathf.Clamp01(timer / textDuration), 3f);
            currText.color = colorA;
        }

        private void faceCamera()
        {
            Vector3 direction = transform.position - vrCam.transform.position;
            if (direction.sqrMagnitude > 1e-6f)
            {
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
        }

        // Uses the font and material of the vanilla text so that the VR copy looks the same.
        public void CreateText(TMP_Text source, Vector3 worldPos, bool myself, float textDur)
        {
            vrCam = CameraUtils.getCamera(CameraUtils.VR_CAMERA);
            // Draws the world space UI layer; it is otherwise only created along with VR HUD elements that use it.
            if (source.font == null || vrCam == null || CameraUtils.getWorldspaceUiCamera() == null)
            {
                gameObject.SetActive(false);
                return;
            }

            string text = source.text;
            timer = 0;

            if (myself)
            {
                transform.SetParent(vrCam.transform);
                transform.localScale = Vector3.one * 0.0004f;
                if (Hud.instance.m_healthText)
                {
                    Vector3 randomPos = new Vector3(Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f), 0) / 100;
                    transform.position = Hud.instance.m_healthText.transform.position;
                    transform.localPosition += Vector3.right * 0.05f + randomPos;
                    transform.rotation = Hud.instance.m_healthText.transform.rotation;
                }
                else
                {
                    Vector3 randomPos = new Vector3(Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f), 0) / 100;
                    float hudDistance = 1f;
                    float hudVerticalOffset = -0.5f;
                    transform.localPosition = new Vector3(VHVRConfig.CameraLockedPos().x, hudVerticalOffset + VHVRConfig.CameraLockedPos().y, hudDistance) + Vector3.right * 0.05f + Vector3.up * -0.1f + randomPos;
                    transform.LookAt(vrCam.transform, Vector3.up);
                    transform.Rotate(0, 180, 0);
                }
            }
            else
            {
                // A pooled text may have been shown on the player's own HUD before.
                transform.SetParent(null);
                transform.position = worldPos;
                float distance = Mathf.Max(Vector3.Distance(worldPos, vrCam.transform.position), MIN_SCALE_DISTANCE);
                transform.localScale = Vector3.one * SCALE_PER_DISTANCE * distance;
                if (text.Length > 4)
                {
                    transform.localScale /= 1 + text.Length / 10;
                }
                faceCamera();
            }

            if (currText.font != source.font)
            {
                currText.font = source.font;
            }
            if (currText.fontSharedMaterial != source.fontSharedMaterial)
            {
                currText.fontSharedMaterial = source.fontSharedMaterial;
            }
            currText.text = text;
            currText.color = source.color;

            textDuration = textDur;
            selfText = myself;
            currText.gameObject.layer = LayerUtils.getWorldspaceUiLayer();
        }
    }
}
