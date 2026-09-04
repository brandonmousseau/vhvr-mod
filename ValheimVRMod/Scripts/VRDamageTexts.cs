using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts
{
    class VRDamageTexts : MonoBehaviour
    {
        private const int MAX_COUNT = 16;
        private static readonly Queue<VRDamageTexts> pool = new Queue<VRDamageTexts>();

        private Canvas canvasText ;
        private Text currText;
        private Font ArialFont;
        private float timer = 0f;
        private float textDuration = 1.5f;
        private bool selfText;

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
            ArialFont = (Font)Resources.GetBuiltinResource(typeof(Font), "Arial.ttf");
            canvasText = gameObject.GetOrAddComponent<Canvas>();
            canvasText.renderMode = RenderMode.WorldSpace;
            currText = gameObject.GetOrAddComponent<Text>();
            currText.fontSize = 80;
            currText.font = ArialFont;
            currText.horizontalOverflow = HorizontalWrapMode.Overflow;
            currText.verticalOverflow = VerticalWrapMode.Overflow;
            currText.alignment = TextAnchor.MiddleCenter;
            currText.enabled = true;
        }

        private void OnRenderObject()
        {
            var dt = Time.unscaledDeltaTime;
            timer += dt / 10; // TODO: Maybe move timer update to Update() or FixedUpdate();
            //LogUtils.LogDebug("timer : " + timer + " / " + textDuration);
            
            if (selfText)
            {
                gameObject.transform.localPosition += new Vector3(0, dt / 300, dt / 2000);
            }
            else
            {
                if (vrCam == null)
                {
                    return;
                }

                var camerapos = vrCam.transform.position;
                var range = Mathf.Min(Vector3.Distance(gameObject.transform.position, camerapos)/20, 0.25f)*4;
                gameObject.transform.localPosition += new Vector3(0, dt * range / 30, 0);
                gameObject.transform.LookAt(vrCam.transform, Vector3.up);
                gameObject.transform.Rotate(0, 180, 0);
            }

            if (timer > textDuration)
            {
                base.gameObject.SetActive(false);
                return;
            }

            var colorA = currText.color;
            colorA.a = 1f - Mathf.Pow(Mathf.Clamp01(timer / textDuration), 3f);
            currText.color = colorA;
        }

        public void CreateText(string text, Vector3 pos, Color color, bool myself,float textDur)
        {
            timer = 0;

            vrCam = CameraUtils.getCamera(CameraUtils.VR_CAMERA);
            if (myself)
            {
                transform.localScale = Vector3.one * 0.0004f;
                transform.SetParent(vrCam.transform);
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
                var camerapos = vrCam.transform.position;
                var range = Mathf.Max(Vector3.Distance(pos, camerapos) / 6.5f, 0.2f) ;
                transform.localScale = Vector3.one * 0.0015f * range;
                if (text.Length > 4)
                {
                    gameObject.transform.localScale /= 1 + text.Length/10;
                }
                transform.position = pos ;
                transform.LookAt(vrCam.transform, Vector3.up);
                transform.Rotate(0, 180, 0);
            }
            
            currText.text = text;
            currText.color = color;

            textDuration = textDur;
            selfText = myself;
            currText.gameObject.layer = LayerUtils.getWorldspaceUiLayer();
        }
    }
}
