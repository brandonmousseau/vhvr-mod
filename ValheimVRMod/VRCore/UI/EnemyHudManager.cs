using System.Collections.Generic;
using TMPro;
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

        public void AddEnemyHud(Character c, bool isMount, GameObject baseHudPlayer, GameObject baseHudEnemy, GameObject baseHudMount, GameObject baseHudBoss)
        {
            if (c != null && c)
            {
                if (c.IsBoss())
                {
                    // Boss is displayed on main GUI instead of world space.
                    return;
                }
                HudData existingData;
                if (_enemyHuds.TryGetValue(c, out existingData))
                {
                    return;
                }
                HudData newData = createHudDataForCharacter(c, isMount, baseHudPlayer, baseHudEnemy, baseHudMount, baseHudBoss);
                if (newData != null)
                {
                    Debug.LogWarning($"Added entry for character {c.name}");
                    _enemyHuds.Add(c, newData);
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
            if (data == null || data.gui == null || !data.gui.activeSelf)
            {
                return;
            }
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
            if (data == null)
            {
                return;
            }
            if (data.gui != null)
            {
                Object.Destroy(data.gui);
                Object.Destroy(data.hudCanvasRoot);
            }
            _enemyHuds.Remove(c);
        }

        public void UpdateHealth(Player p, Character c, float health)
        {
            HudData data = getEnemyHud(c);
            if (data != null)
            {
                data.healthSlow.SetValue(health);
                data.healthFast.SetValue(health);

                if (!(data.healthFastFriendly is null))
                {
                    bool isEnemy = !p || BaseAI.IsEnemy(p, c);
                    data.healthFast.gameObject.SetActive(isEnemy);
                    data.healthFastFriendly.gameObject.SetActive(!isEnemy);
                    data.healthFastFriendly.SetValue(health);
                }
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

        public void UpdateMount(Character c, float maxStamina, float stamina)
        {
            HudData data = getEnemyHud(c);
            if (data != null)
            {
                data.mountStamina.SetValue(stamina / maxStamina);
                data.mountHealthText.text = Mathf.CeilToInt(c.GetHealth()).ToString();
                data.mountStaminaText.text = Mathf.CeilToInt(stamina).ToString();
            }
        }

        public void UpdateName(Character c, string name)
        {
            HudData data = getEnemyHud(c);
            if (data != null)
            {
                data.name.text = name;
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

        private HudData createHudDataForCharacter(Character c, bool isMount, GameObject baseHudPlayer,
            GameObject baseHudEnemy, GameObject baseHudMount, GameObject baseHudBoss)
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
            } else if (isMount)
            {
                baseHud = baseHudMount;
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

            // Hide the vanilla HUD since now we have duplicate it in VR.
            baseHud.transform.localScale = Vector3.zero;
            // Make sure the HUD in VR is not hidden as a side effect of hiding a vanilla HUD from earlier. 
            data.gui.transform.localScale = Vector3.one;

            updateGuiLayers(data.gui.transform);
            data.gui.SetActive(true);
            data.hudCanvasRoot = canvasRoot;
            data.healthRoot = data.gui.transform.Find("Health").gameObject; //This is no longer set in the base game
            data.healthFast = data.healthRoot.transform.Find("health_fast").GetComponent<GuiBar>();
            data.healthFastFriendly = data.healthRoot.transform.Find("health_fast_friendly")?.GetComponent<GuiBar>();
            data.healthSlow = data.healthRoot.transform.Find("health_slow").GetComponent<GuiBar>();
            data.level2 = data.gui.transform.Find("level_2") as RectTransform;
            data.level3 = data.gui.transform.Find("level_3") as RectTransform;
            data.alerted = data.gui.transform.Find("Alerted") as RectTransform;
            data.aware = data.gui.transform.Find("Aware") as RectTransform;
            data.name = data.gui.transform.Find("Name").GetComponent<TextMeshProUGUI>();
            data.name.text = Localization.instance.Localize(c.GetHoverName());
            data.isMount = isMount;

            if (isMount)
            {
                data.mountStamina = data.gui.transform.Find("Stamina/stamina_fast").GetComponent<GuiBar>();
                data.mountStaminaText = data.gui.transform.Find("Stamina/StaminaText").GetComponent<TextMeshProUGUI>();
                data.mountHealthText = data.gui.transform.Find("Health/HealthText").GetComponent<TextMeshProUGUI>();
            }

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
            public GuiBar healthFastFriendly;
            public GuiBar healthSlow;
            public TextMeshProUGUI name;
            public bool isMount;
            public GuiBar mountStamina; 
            public TextMeshProUGUI mountStaminaText;
            public TextMeshProUGUI mountHealthText;

            public HudData() { }
        }

    }
}
