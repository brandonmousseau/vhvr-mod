using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ValheimVRMod.Utilities;

using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.VRCore.UI
{
    class EnemyHudManager
    {
        public static readonly int HUD_LAYER = 27;
        public static readonly int HUD_LAYER_MASK = (1 << HUD_LAYER);

        private static EnemyHudManager _instance;
        private Dictionary<Character, HudData> _enemyHuds;
        private Camera _hudCamera;

        public static EnemyHudManager instance {  get
            {
                if (_instance == null)
                {
                    _instance = new EnemyHudManager();
                }
                return _instance;
            } }

        private EnemyHudManager()
        {
            _enemyHuds = new Dictionary<Character, HudData>();
            _hudCamera = createHudCamera();
        }

        private static Camera createHudCamera()
        {
            GameObject hudCameraParent = new GameObject(CameraUtils.HUD_CAMERA);
            GameObject.DontDestroyOnLoad(hudCameraParent);
            Camera hudCam = hudCameraParent.AddComponent<Camera>();
            hudCam.depth = 2;
            hudCam.clearFlags = CameraClearFlags.Depth;
            hudCam.cullingMask = HUD_LAYER_MASK;
            hudCam.transform.SetParent(CameraUtils.getCamera(CameraUtils.VR_CAMERA).transform, false);
            hudCam.orthographic = false;
            hudCam.enabled = true;
            return hudCam;
        }

        public void UpdateAll()
        {
            LogDebug("UpdateAll");
            foreach(KeyValuePair<Character, HudData> entry in _enemyHuds)
            {
                UpdateHudCoordinates(entry.Key);
            }
        }

        public void AddEnemyHud(Character c, GameObject baseHudPlayer, GameObject baseHudEnemy, GameObject baseHudBoss)
        {
            if (c != null)
            {
                LogDebug("AddEnemyHud Character: " + c.name);
                HudData existingData = getEnemyHud(c);
                if (existingData == null)
                {
                    LogDebug("Existing data is null. Adding new HudData");
                    HudData newData = createHudDataForCharacter(c, baseHudPlayer, baseHudEnemy, baseHudBoss);
                    if (newData != null)
                    {
                        _enemyHuds.Add(c, newData);
                    }
                }
            }
        }

        public void UpdateHudCoordinates(Character c)
        {
            HudData data = getEnemyHud(c);
            if (data != null)
            {
                data.hudCanvasRoot.transform.position = c.IsPlayer() ? c.GetHeadPoint() : c.GetTopPoint();
                data.hudCanvasRoot.transform.LookAt(VRPlayer.instance.transform);
                data.hudCanvasRoot.transform.rotation *= Quaternion.Euler(0f, 180f, 0f);
                float scale = 0.06f / data.hudCanvasRoot.GetComponent<Canvas>().GetComponent<RectTransform>().rect.width;
                float distance = Vector3.Distance(VRPlayer.instance.transform.position, data.hudCanvasRoot.transform.position);
                data.hudCanvasRoot.GetComponent<Canvas>().GetComponent<RectTransform>().localScale = Vector3.one * scale * distance;
            }
        }

        public void RemoveEnemyHud(Character c)
        {
            HudData data = getEnemyHud(c);
            if (data != null)
            {
                LogDebug("RemoveEnemyHud Character " + c.m_name);
                Object.Destroy(data.hudCanvasRoot);
                _enemyHuds.Remove(c);
            }
        }

        public void UpdateHealth(Character c, float health)
        {
            HudData data = getEnemyHud(c);
            if (data != null)
            {
                data.healthSlow.SetValue(health);
                data.healthFast.SetValue(health);
            }
        }

        public void UpdateLevel(Character c, int level)
        {
            HudData data = getEnemyHud(c);
            if (data != null)
            {
                if (data.level2)
                {
                    data.level2.gameObject.SetActive(level == 2);
                }
                if (data.level3)
                {
                    data.level3.gameObject.SetActive(level == 3);
                }
            }
        }

        public void UpdateAlerted(Character c, bool alerted)
        {
            HudData data = getEnemyHud(c);
            if (data != null)
            {
                data.alerted.gameObject.SetActive(alerted);
            }
        }

        public void UpdateAware(Character c, bool aware)
        {
            HudData data = getEnemyHud(c);
            if (data != null)
            {
                data.aware.gameObject.SetActive(aware);
            }
        }

        private HudData getEnemyHud(Character c)
        {
            LogDebug("getEnemyHud");
            if (c == null)
            {
                LogDebug("Null c");
                return null;
            }
            HudData data;
            if(_enemyHuds.TryGetValue(c, out data))
            {
                LogDebug("Got Data for C: " + c.name);
                return data;
            }
            LogDebug("Got no data.");
            return null;
        }

        private HudData createHudDataForCharacter(Character c, GameObject baseHudPlayer,
            GameObject baseHudEnemy, GameObject baseHudBoss)
        {
            if (baseHudPlayer == null)
            {
                LogError("baseHudPlayer is null.");
                return null;
            }
            if (baseHudEnemy == null)
            {
                LogError("baseHudEnemy is null.");
                return null;
            }
            if (baseHudBoss == null)
            {
                LogError("baseHudBoss is null.");
                return null;
            }
            GameObject baseHud;
            if (c.IsPlayer())
            {
                baseHud = baseHudPlayer;
            } else if (c.IsBoss())
            {
                baseHud = baseHudBoss;
            } else
            {
                baseHud = baseHudEnemy;
            }
            GameObject canvasRoot = createEnemyHudCanvas();
            Canvas canvas = canvasRoot.GetComponent<Canvas>();
            HudData data = new HudData()
            {
                character = c,
                gui = Object.Instantiate(baseHud, canvas.transform)
            };
            updateGuiLayers(data.gui.transform);
            data.hudCanvasRoot = canvasRoot;
            data.healthRoot = data.gui.transform.Find("Health").gameObject;
            data.healthFast = data.healthRoot.transform.Find("health_fast").GetComponent<GuiBar>();
            data.healthSlow = data.healthRoot.transform.Find("health_slow").GetComponent<GuiBar>();
            data.level2 = data.gui.transform.Find("level_2") as RectTransform;
            data.level3 = data.gui.transform.Find("level_3") as RectTransform;
            data.alerted = data.gui.transform.Find("Alerted") as RectTransform;
            data.aware = data.gui.transform.Find("Aware") as RectTransform;
            data.name = data.gui.transform.Find("Name").GetComponent<Text>();
            data.name.text = Localization.instance.Localize(c.GetHoverName());
            data.gui.transform.localPosition = data.hudCanvasRoot.GetComponent<Canvas>().GetComponent<RectTransform>().rect.center;
            data.gui.SetActive(true);
            return data;
        }

        private static void updateGuiLayers(Transform gui)
        {
            if (gui == null)
            {
                return;
            }
            gui.gameObject.layer = HUD_LAYER;
            foreach (Transform child in gui)
            {
                updateGuiLayers(child);
            }
        }

        private GameObject createEnemyHudCanvas()
        {
            GameObject hudCanvasRoot = new GameObject(System.Guid.NewGuid().ToString());
            hudCanvasRoot.layer = HUD_LAYER;
            GameObject.DontDestroyOnLoad(hudCanvasRoot);
            Canvas hudCanvas = hudCanvasRoot.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.WorldSpace;
            hudCanvas.worldCamera = _hudCamera;
            hudCanvas.GetComponent<RectTransform>().SetParent(hudCanvasRoot.transform, false);
            return hudCanvasRoot;
        }

        public class HudData
        {
            public GameObject hudCanvasRoot;
            public Character character;
            public GameObject gui;
            public GameObject healthRoot;
            public RectTransform level2;
            public RectTransform level3;
            public RectTransform alerted;
            public RectTransform aware;
            public GuiBar healthFast;
            public GuiBar healthSlow;
            public Text name;
            public HudData() { }
        }

    }
}
