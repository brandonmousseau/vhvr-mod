using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using ValheimVRMod.Scripts;
using ValheimVRMod.Utilities;
using Object = UnityEngine.Object;

namespace ValheimVRMod.VRCore.UI {
    public class ConfigSettings {

        private const string MenuName = "VHVR";
        
        private static GameObject tabButtonPrefab;
        private static GameObject tabPrefab;
        private static GameObject togglePrefab;
        private static GameObject sliderPrefab;
        private static GameObject chooserPrefab;
        private static GameObject settingsPrefab;
        private static GameObject keyBindingPrefab;
        private static GameObject settings;
        private static Transform menuList;
        private static Transform menuParent;
        private static KeyValuePair<string, ConfigEntryBase>? saveConfigValue;
        private static int tabCounter;

        public static GameObject toolTip;
        
        public static void instantiate(Transform mList, Transform mParent, GameObject sPrefab) {
            menuList = mList;
            menuParent = mParent;
            settingsPrefab = sPrefab;
            createMenuEntry();
            generatePrefabs();
        }


        /// <summary>
        /// Create an Entry in the Menu 
        /// </summary>
        private static void createMenuEntry() {
            
            bool settingsFound = false;

            for (int i = 0; i < menuList.childCount; i++) {
                if (menuList.GetChild(i).name == "Settings") {
                    var modSettings = GameObject.Instantiate(menuList.GetChild(i), menuList);
                    modSettings.GetComponentInChildren<Text>().text = MenuName;
                    modSettings.GetComponent<Button>().onClick.SetPersistentListenerState(0, UnityEventCallState.Off);
                    modSettings.GetComponent<Button>().onClick.RemoveAllListeners();
                    modSettings.GetComponent<Button>().onClick.AddListener(createModSettings);
                    settingsFound = true;
                }
                else if (settingsFound) {
                    var rectTransform = menuList.GetChild(i).GetComponent<RectTransform>();
                    rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x,
                        rectTransform.anchoredPosition.y - 40);
                }
            }
        }

        /// <summary>
        /// Create temporary prefabs out of existing elements
        /// </summary>
        private static void generatePrefabs() {

            var tabButtons = settingsPrefab.transform.Find("panel").Find("TabButtons");
            tabButtonPrefab = tabButtons.GetChild(0).gameObject;
            var tabs = settingsPrefab.transform.Find("panel").Find("Tabs");
            tabPrefab = tabs.GetChild(0).gameObject;
            togglePrefab = tabPrefab.GetComponentInChildren<Toggle>().gameObject;
            sliderPrefab = tabPrefab.GetComponentInChildren<Slider>().transform.parent.gameObject;
            keyBindingPrefab = tabPrefab.transform.Find("Key_Binding").gameObject;
            chooserPrefab = settingsPrefab.transform.Find("panel").Find("Tabs").Find("Misc").Find("Language")
                .gameObject;

        }

        private static void createToolTip(Transform settings) {

            var padding = 4;
            var width = 400;
            toolTip = new GameObject();
            toolTip.transform.SetParent(settings, false);
            var bkgImage = toolTip.AddComponent<Image>();
            bkgImage.rectTransform.anchoredPosition = new Vector2(-600, 0);
            bkgImage.color = new Color(0,0,0,0.5f);
            var textObj = Object.Instantiate(togglePrefab.transform.GetChild(0), toolTip.transform);
            var text = textObj.GetComponent<Text>();
            text.rectTransform.sizeDelta = new Vector2(width, 1000);
            text.fontSize = 14;
            text.alignment = TextAnchor.MiddleLeft;
            text.rectTransform.anchoredPosition = new Vector2(padding, padding);
            toolTip.SetActive(false);
        }

        /// <summary>
        /// Make Copy of ingame Settings, clean up all existing tabs, then iterate bepinex config
        /// </summary>
        private static void createModSettings() {
            settings = Object.Instantiate(settingsPrefab, menuParent);
            settings.transform.Find("panel").Find("Settings_topic").GetComponent<Text>().text = MenuName;
            createToolTip(settings.transform);
            var tabButtons = settings.transform.Find("panel").Find("TabButtons");

            // destroy old tab buttons
            foreach (Transform t in tabButtons) {
                Object.Destroy(t.gameObject);
            }
            // clear tab array
            tabButtons.GetComponent<TabHandler>().m_tabs.Clear();
            
            // deactivate old tab contents
            foreach (Transform t in settings.transform.Find("panel").Find("Tabs")) {
                t.gameObject.SetActive(false);
            }

            // reorder bepinex configs by sections
            var orderedConfig = new Dictionary<string, Dictionary<string, ConfigEntryBase>>();
            foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> keyValuePair in VHVRConfig.config) {

                // skip entries with section "Immutable", these are not changeable at runtime
                if (keyValuePair.Key.Section == "Immutable") {
                    continue;
                }
                
                if (!orderedConfig.ContainsKey(keyValuePair.Key.Section)) {
                    orderedConfig.Add(keyValuePair.Key.Section, new Dictionary<string, ConfigEntryBase>());
                }

                orderedConfig[keyValuePair.Key.Section][keyValuePair.Key.Key] = keyValuePair.Value;
            }

            tabCounter = 0;
            // iterate ordered configs and create tabs out of each section
            foreach (KeyValuePair<string, Dictionary<string, ConfigEntryBase>> section in orderedConfig) {
                createTabForSection(section);
            }

            tabButtons.GetComponent<TabHandler>().SetActiveTab(0);
            Settings.instance.UpdateBindings();
        }

        /// <summary>
        /// Create a new Tab out of a config section
        /// </summary>
        private static void createTabForSection(KeyValuePair<string, Dictionary<string, ConfigEntryBase>> section) {
            var tabButtons = settings.transform.Find("panel").Find("TabButtons");

            // Create new tab button
            var newTabButton = Object.Instantiate(tabButtonPrefab, tabButtons);
            newTabButton.name = section.Key;
            var rectTransform = newTabButton.GetComponent<RectTransform>();
            rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x + tabCounter * 100,
                rectTransform.anchoredPosition.y);

            foreach (Text text in newTabButton.GetComponentsInChildren<Text>()) {
                text.text = section.Key;
            }
            
            // Create new tab content
            var tabs = settings.transform.Find("panel").Find("Tabs");
            var newTab = Object.Instantiate(tabs.GetChild(0), tabs);
            newTab.name = section.Key;

            // add listeners for ok and back buttons, remove all other elements 
            foreach (Transform child in newTab.transform) {

                switch (child.name) {
                    case "Ok":
                        child.GetComponent<Button>().onClick.AddListener(() => {VHVRConfig.config.Save();});
                        break;
                    case "Back":
                        child.GetComponent<Button>().onClick.AddListener(() => {VHVRConfig.config.Reload();});
                        break;
                    default:
                        Object.Destroy(child.gameObject);
                        break;
                }
            }

            // Register the new Tab in Tab array
            var tab = new TabHandler.Tab();
            tab.m_button = newTabButton.GetComponent<Button>();
            var activeTabIndex = tabCounter;
            tab.m_button.onClick.AddListener(() => {
                tabButtons.GetComponent<TabHandler>().SetActiveTab(activeTabIndex);
            });
            tab.m_default = true;
            tab.m_page = newTab.GetComponent<RectTransform>();
            tab.m_onClick = new UnityEvent();

            tabButtons.GetComponent<TabHandler>().m_tabs.Add(tab);

            int posX = -85;
            int posY = -20;
            
            if (section.Value.Count > 11) {
                posX = -250;
            }
            
            // iterate all config entries of current section and create elements 
            foreach (KeyValuePair<string, ConfigEntryBase> configValue in section.Value) {
                createElement(configValue, newTab, new Vector2(posX, posY));
                posY -= 30;
                if (posY < -330) {
                    posY = -20;
                    posX = 75;
                }
            }

            tabCounter++;
        }

        /// <summary>
        /// Create a Settings Element out of a Config Entry
        /// </summary>
        private static void createElement(KeyValuePair<string, ConfigEntryBase> configEntry, Transform parent, Vector2 pos) {
            
            if (configEntry.Value.SettingType == typeof(bool)) {
                createToggle(configEntry, parent, pos);
                return;
            }

            if (configEntry.Value.Description.Description.StartsWith("[Key]")) {
                createKeyBinding(configEntry, parent, pos);
                return;
            }
            
            var acceptableValues = configEntry.Value.Description.AcceptableValues;
            if (acceptableValues == null) {
                return;
            }

            var type = acceptableValues.GetType();
            if (type.GetGenericTypeDefinition() == typeof(AcceptableValueList<>)) {
                createValueList(configEntry, parent, pos, type, acceptableValues);
            } else if (type.GetGenericTypeDefinition() == typeof(AcceptableValueRange<>)) {
                createValueRange(configEntry, parent, pos, type, acceptableValues);
            }
        }

        private static void createValueRange(KeyValuePair<string, ConfigEntryBase> configValue, Transform parent, Vector2 pos, Type type,
            AcceptableValueBase acceptableValues) {

            var sliderObj = Object.Instantiate(sliderPrefab, parent);
            sliderObj.AddComponent<ToolTipTrigger>().text = configValue.Value.Description.Description;
            sliderObj.GetComponent<Text>().text = configValue.Key;
            sliderObj.GetComponent<RectTransform>().anchoredPosition = pos;
            var slider = sliderObj.GetComponentInChildren<Slider>();
            slider.minValue = float.Parse(type.GetProperty("MinValue").GetValue(acceptableValues).ToString());
            slider.maxValue =  float.Parse(type.GetProperty("MaxValue").GetValue(acceptableValues).ToString());
            slider.value = float.Parse(configValue.Value.GetSerializedValue(), CultureInfo.InvariantCulture);
            if (acceptableValues.ValueType == typeof(int)) {
                slider.wholeNumbers = true;
            }
            slider.onValueChanged.AddListener(mValue => {
                configValue.Value.SetSerializedValue(mValue.ToString(CultureInfo.InvariantCulture));
            });
        }

        private static void createValueList(KeyValuePair<string, ConfigEntryBase> configValue, Transform parent, Vector2 pos, Type type,
        AcceptableValueBase acceptableValues) {
            
            var chooserObj = Object.Instantiate(chooserPrefab, parent);
            chooserObj.AddComponent<ToolTipTrigger>().text = configValue.Value.Description.Description;
            chooserObj.GetComponent<Text>().text = configValue.Key;
            chooserObj.GetComponent<RectTransform>().anchoredPosition = pos;
            var valueList = (string[]) type.GetProperty("AcceptableValues").GetValue(acceptableValues);
            var currentIndex = Array.IndexOf(valueList, configValue.Value.GetSerializedValue());
            
            for (int i = 0; i < chooserObj.transform.childCount; i++) {
                var child = chooserObj.transform.GetChild(i);
                switch (child.name) {
                    case "bkg":
                        child.localScale *= 0.8f;
                        child.GetComponent<RectTransform>().anchoredPosition = new Vector2(180, 0);
                        child.GetComponent<RectTransform>().sizeDelta = new Vector2(150, 0);
                        child.GetComponentInChildren<Text>().fontSize = 6;
                        child.GetComponentInChildren<Text>().text = configValue.Value.GetSerializedValue();
                        break;
                    case "Left":
                        child.localScale *= 0.5f;
                        child.GetComponent<RectTransform>().anchoredPosition = new Vector2(110, 0);
                        child.GetComponent<Button>().onClick.AddListener(() => {
                            var text = valueList[mod(--currentIndex, valueList.Length)];
                            chooserObj.transform.Find("bkg").GetComponentInChildren<Text>().text = text;
                            configValue.Value.SetSerializedValue(text);
                        });
                        break;
                    case "Right":
                        child.localScale *= 0.5f;
                        child.GetComponent<RectTransform>().anchoredPosition = new Vector2(250, 0);
                        child.GetComponent<Button>().onClick.AddListener(() => {
                            var text = valueList[mod(++currentIndex, valueList.Length)];
                            chooserObj.transform.Find("bkg").GetComponentInChildren<Text>().text = text;
                            configValue.Value.SetSerializedValue(text);
                        });
                        break;
                    default:
                        child.gameObject.SetActive(false);
                        break;
                }
            }
        }
        
        private static void createToggle(KeyValuePair<string, ConfigEntryBase> configValue, Transform parent, Vector2 pos) {
            
            var toggle = Object.Instantiate(togglePrefab, parent);
            toggle.AddComponent<ToolTipTrigger>().text = configValue.Value.Description.Description;
            toggle.GetComponentInChildren<Text>().text = configValue.Key;
            toggle.GetComponent<Toggle>().isOn = configValue.Value.GetSerializedValue() == "true";
            toggle.GetComponent<Toggle>().onValueChanged.AddListener(mToggle => {
                configValue.Value.SetSerializedValue(mToggle ? "true" : "false");
            });
            pos.x -= 210;
            toggle.GetComponent<RectTransform>().anchoredPosition = pos;
        }

        private static void createKeyBinding(KeyValuePair<string, ConfigEntryBase> configValue, Transform parent, Vector2 pos) {
            
            var keyBinding = Object.Instantiate(keyBindingPrefab, parent);
            keyBinding.AddComponent<ToolTipTrigger>().text = configValue.Value.Description.Description;
            keyBinding.GetComponent<Text>().text = configValue.Key;
            Settings.m_instance.m_keys.Add(new Settings.KeySetting {m_keyName = configValue.Key, m_keyTransform = keyBinding.GetComponent<RectTransform>()});
            keyBinding.GetComponentInChildren<Button>().onClick.AddListener(() => {
                Settings.instance.OnKeySet();
                saveConfigValue = configValue;
            });
            keyBinding.GetComponent<RectTransform>().anchoredPosition = pos;
            ZInput.instance.AddButton(configValue.Key, (KeyCode) Enum.Parse(typeof(KeyCode), configValue.Value.GetSerializedValue()));
        }
        
        private static int mod(int x, int m) {
            return (x%m + m)%m;
        }

        public static void updateBindings() {
            
            if (saveConfigValue == null) {
                return;
            }

            foreach (Settings.KeySetting key in Settings.instance.m_keys) {
                if (key.m_keyName == saveConfigValue.Value.Key) {
                    var buttons = AccessTools.FieldRefAccess<ZInput, Dictionary<string, ZInput.ButtonDef>>(ZInput.instance, "m_buttons");
                    ZInput.ButtonDef buttonDef;
                    buttons.TryGetValue(key.m_keyName, out buttonDef);
                    saveConfigValue.Value.Value.SetSerializedValue(buttonDef.m_key.ToString());
                }
            }

            saveConfigValue = null;
        }
    }
}