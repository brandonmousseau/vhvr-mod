using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ValheimVRMod.Utilities;

using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.VRCore.UI
{
    class EnemyHudManager
    {
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
            ensureHudCamera();
        }

        private bool ensureHudCamera()
        {
            if (_hudCamera != null)
            {
                return true;
            }
            _hudCamera = CameraUtils.getWorldspaceUiCamera();
            return _hudCamera != null;
        }

        public void UpdateAll()
        {
            foreach(KeyValuePair<Character, HudData> entry in _enemyHuds)
            {
                UpdateHudCoordinates(entry.Key);
            }
        }

        public void AddEnemyHud(Character c, GameObject baseHudPlayer, GameObject baseHudEnemy, GameObject baseHudBoss)
        {
            if (c != null)
            {
                if (c.IsBoss())
                {
                    // Boss is displayed on main GUI instead of world space.
                    return;
                }
                HudData existingData = getEnemyHud(c);
                if (existingData == null)
                {
                    HudData newData = createHudDataForCharacter(c, baseHudPlayer, baseHudEnemy, baseHudBoss);
                    if (newData != null)
                    {
                        _enemyHuds.Add(c, newData);
                    }
                }
            }
        }

        public void SetHudActive(Character c, bool active)
        {
            HudData data = getEnemyHud(c);
            if (data != null)
            {
                data.gui.SetActive(active && VHVRConfig.ShowEnemyHuds());
            }
        }

        public void UpdateHudCoordinates(Character c)
        {
            if (!ensureHudCamera())
            {
                return;
            }
            HudData data = getEnemyHud(c);
            if (data != null)
            {
                data.hudCanvasRoot.transform.position = c.IsPlayer() ? c.GetHeadPoint() : c.GetTopPoint();
                data.hudCanvasRoot.transform.LookAt(_hudCamera.transform);
                data.hudCanvasRoot.transform.rotation *= Quaternion.Euler(0f, 180f, 0f);
                float scale = 0.06f / data.hudCanvasRoot.GetComponent<Canvas>().GetComponent<RectTransform>().rect.width;
                float distance = Vector3.Distance(_hudCamera.transform.position, data.gui.transform.position);
                data.hudCanvasRoot.GetComponent<Canvas>().GetComponent<RectTransform>().localScale =
                    Vector3.one * scale * distance * VHVRConfig.EnemyHudScale();
            }
        }

        public void RemoveEnemyHud(Character c)
        {
            HudData data = getEnemyHud(c);
            if (data != null)
            {
                Object.Destroy(data.gui);
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
                    bool isLevel2 = level == 2;
                    data.level2.gameObject.SetActive(isLevel2);
                }
                if (data.level3)
                {
                    bool isLevel3 = level == 3;
                    data.level3.gameObject.SetActive(isLevel3);
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
            HudData data;
            _enemyHuds.TryGetValue(c, out data);
            return data;
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
            data.gui.SetActive(true);
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
            return data;
        }

        private static void updateGuiLayers(Transform gui)
        {
            if (gui == null)
            {
                return;
            }
            gui.gameObject.layer = LayerUtils.getWorldspaceUiLayer();
            foreach (Transform child in gui)
            {
                updateGuiLayers(child);
            }
        }

        private GameObject createEnemyHudCanvas()
        {
            GameObject hudCanvasRoot = new GameObject(System.Guid.NewGuid().ToString());
            GameObject.DontDestroyOnLoad(hudCanvasRoot);
            hudCanvasRoot.layer = LayerUtils.getWorldspaceUiLayer();
            Canvas hudCanvas = hudCanvasRoot.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.WorldSpace;
            ensureHudCamera();
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
