using UnityEngine;
using UnityEngine.UI;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts
{
    class VRDamageTexts : MonoBehaviour
    {
        private GameObject damageTextObject;
        private Canvas canvasText ;
        private Text currText;
        private Font ArialFont = (Font)Resources.GetBuiltinResource(typeof(Font), "Arial.ttf");
        private float timer = 0f;
        private float textDuration = 1.5f;
        private bool selfText;

        private static Camera vrCam;
        private void OnRenderObject()
        {
            var dt = Time.unscaledDeltaTime;
            timer += dt/10;
            //LogUtils.LogDebug("timer : " + timer + " / " + textDuration);
            
            if (selfText)
            {
                damageTextObject.transform.localPosition += new Vector3(0, dt / 300, dt / 2000);
            }
            else
            {
                var camerapos = vrCam.transform.position;
                var range = Mathf.Min(Vector3.Distance(damageTextObject.transform.position, camerapos)/20, 0.25f)*4;
                damageTextObject.transform.localPosition += new Vector3(0, dt * range / 30, 0);
                damageTextObject.transform.LookAt(vrCam.transform, Vector3.up);
                damageTextObject.transform.Rotate(0, 180, 0);
            }
            var colorA = currText.color;
            colorA.a = 1f - Mathf.Pow(Mathf.Clamp01(timer / textDuration), 3f);
            currText.color = colorA;
        }
        public void CreateText(string text, Vector3 pos, Color color, bool myself,float textDur)
        {
            damageTextObject = transform.gameObject;
            canvasText = damageTextObject.AddComponent<Canvas>();
            canvasText.renderMode = RenderMode.WorldSpace;
            currText = damageTextObject.AddComponent<Text>();
            currText.fontSize = 80;
            currText.font = ArialFont;
            currText.horizontalOverflow = HorizontalWrapMode.Overflow;
            currText.verticalOverflow = VerticalWrapMode.Overflow;
            currText.alignment = TextAnchor.MiddleCenter;
            currText.enabled = true;

            vrCam = CameraUtils.getCamera(CameraUtils.VR_CAMERA);
            if (myself)
            {
                damageTextObject.transform.localScale *= 0.0004f;
                damageTextObject.transform.SetParent(vrCam.transform);
                if (Hud.instance.m_healthText)
                {
                    Vector3 randomPos = new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f), 0) / 100;
                    damageTextObject.transform.position = Hud.instance.m_healthText.transform.position;
                    damageTextObject.transform.localPosition += Vector3.right * 0.05f + randomPos;
                    damageTextObject.transform.rotation = Hud.instance.m_healthText.transform.rotation;
                }
                else
                {
                    Vector3 randomPos = new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f), 0) / 100;
                    float hudDistance = 1f;
                    float hudVerticalOffset = -0.5f;
                    damageTextObject.transform.localPosition = new Vector3(VHVRConfig.CameraHudX() * 1000, hudVerticalOffset + VHVRConfig.CameraHudY() * 1000, hudDistance) + Vector3.right * 0.05f + Vector3.up * -0.1f + randomPos;
                    damageTextObject.transform.LookAt(vrCam.transform, Vector3.up);
                    damageTextObject.transform.Rotate(0, 180, 0);
                }
            }
            else
            {
                var camerapos = vrCam.transform.position;
                var range = Mathf.Max(Vector3.Distance(pos, camerapos)/6.5f, 0.2f) ;
                damageTextObject.transform.localScale *= 0.0015f * range;
                if (text.Length > 4)
                {
                    damageTextObject.transform.localScale /= 1 + text.Length/10;
                }
                damageTextObject.transform.position = pos ;
                damageTextObject.transform.LookAt(vrCam.transform, Vector3.up);
                damageTextObject.transform.Rotate(0, 180, 0);
            }
            
            currText.text = text;
            currText.color = color;

            textDuration = textDur;
            selfText = myself;
            currText.gameObject.layer = LayerUtils.getWorldspaceUiLayer();
            GameObject.Destroy(damageTextObject, textDur);
        }
    }
}
