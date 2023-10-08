using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using ValheimVRMod.Scripts;
using ValheimVRMod.Utilities;
using Object = UnityEngine.Object;
using TMPro;

namespace ValheimVRMod.VRCore.UI {
    public class ConfigSettings {

        private const string MenuName = "VHVR";
        private const int TabButtonWidth = 100;
        
        private static GameObject tabButtonPrefab;
        private static GameObject controlSettingsPrefab;
        private static GameObject togglePrefab;
        private static GameObject sliderPrefab;
        private static GameObject chooserPrefab;
        private static GameObject settingsPrefab;
        private static GameObject keyBindingPrefab;
        private static GameObject buttonPrefab;
        private static GameObject settings;
        private static Transform menuList;
        private static Transform menuParent;
        private static ConfigComponent tmpComfigComponent;
        private static int tabCounter;
        public static bool doSave;
        public static GameObject toolTip;
        
        public static void instantiate(Transform mList, Transform mParent, GameObject sPrefab) {
            menuList = mList.transform.Find("MenuEntries").transform;
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
                    modSettings.GetComponentInChildren<TextMeshProUGUI>().text = MenuName;
                    modSettings.GetComponent<Button>().onClick.SetPersistentListenerState(0, UnityEventCallState.Off);
                    modSettings.GetComponent<Button>().onClick.RemoveAllListeners();
                    modSettings.GetComponent<Button>().onClick.AddListener(createModSettings);

                    Transform screenshotButton = GameObject.Instantiate(menuList.GetChild(i), menuList);
                    screenshotButton.GetComponentInChildren<TextMeshProUGUI>().text = "Screenshot";
                    screenshotButton.GetComponent<Button>().onClick.SetPersistentListenerState(0, UnityEventCallState.Off);
                    screenshotButton.GetComponent<Button>().onClick.RemoveAllListeners();
                    screenshotButton.GetComponent<Button>().onClick.AddListener(CaptureScreenshot);
                    Vector2 currentPosition = screenshotButton.GetComponent<RectTransform>().anchoredPosition;
                    screenshotButton.GetComponent<RectTransform>().anchoredPosition = currentPosition + Vector2.up * 40;

                    settingsFound = true;
                }
                else if (settingsFound) {
                    var rectTransform = menuList.GetChild(i).GetComponent<RectTransform>();
                    rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x,
                        rectTransform.anchoredPosition.y - 80);
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
            controlSettingsPrefab = tabs.GetChild(0).gameObject;
            togglePrefab = controlSettingsPrefab.GetComponentInChildren<Toggle>().gameObject;
            sliderPrefab = controlSettingsPrefab.GetComponentInChildren<Slider>().transform.parent.gameObject;
            keyBindingPrefab = controlSettingsPrefab.transform.Find("Use").gameObject;
            chooserPrefab = settingsPrefab.transform.Find("panel").Find("Tabs").Find("Misc").Find("Language")
                .gameObject;
            buttonPrefab = settingsPrefab.transform.Find("panel").Find("Back").gameObject;

        }

        private static void createToolTip(Transform settings) {

            var padding = 4;
            var width = 750;
            toolTip = new GameObject();
            toolTip.transform.SetParent(settings, false);
            var bkgImage = toolTip.AddComponent<Image>();
            bkgImage.rectTransform.anchoredPosition = new Vector2(0, -350);
            bkgImage.color = new Color(0,0,0,0.5f);
            var textObj = Object.Instantiate(togglePrefab.transform.GetChild(0), toolTip.transform);
            TMP_Text text = textObj.GetComponent<TMP_Text>();
            text.rectTransform.sizeDelta = new Vector2(width, 300);
            // text.resizeTextForBestFit = false;
            text.fontSize = 20;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.rectTransform.anchoredPosition = new Vector2(padding, padding);
            toolTip.SetActive(false);
        }

        /// <summary>
        /// Make Copy of ingame Settings, clean up all existing tabs, then iterate bepinex config
        /// </summary>
        private static void createModSettings() {
            settings = Object.Instantiate(settingsPrefab, menuParent);
            settings.transform.Find("panel").Find("Settings_topic").GetComponent<TMP_Text>().text = MenuName;
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
            int sectionCount = 0;
            foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> keyValuePair in VHVRConfig.config) {

                // skip entries with section "Immutable", these are not changeable at runtime
                if (keyValuePair.Key.Section == "Immutable") {
                    continue;
                }
                if (!orderedConfig.ContainsKey(keyValuePair.Key.Section)) {
                    orderedConfig.Add(keyValuePair.Key.Section, new Dictionary<string, ConfigEntryBase>());
                    sectionCount++;
                }

                orderedConfig[keyValuePair.Key.Section][keyValuePair.Key.Key] = keyValuePair.Value;
            }

            tabCounter = 0;
            // iterate ordered configs and create tabs out of each section
            foreach (KeyValuePair<string, Dictionary<string, ConfigEntryBase>> section in orderedConfig) {
                createTabForSection(section, sectionCount);
            }

            setupOkAndBack(settings.transform.Find("panel"));

            tabButtons.GetComponent<TabHandler>().SetActiveTab(0);
            Settings.instance.UpdateBindings();
        }

        // Adds listeners for ok and back buttons
        private static void setupOkAndBack(Transform panel) {
            Button okButton = panel.Find("Ok")?.GetComponent<Button>();
            if (okButton == null)
            {
                LogUtils.LogWarning("Failed to find Ok button for VHVR");
            }
            else
            {
                okButton.onClick.AddListener(() => { doSave = true; });
                Object.Destroy(okButton.GetComponent<UIGamePad>());
            }

            Button backButton = panel.Find("Back")?.GetComponent<Button>();
            if (backButton == null)
            {
                LogUtils.LogWarning("Failed to find Back button for VHVR");
            }
            else
            {
                backButton.onClick.AddListener(() => { doSave = false; });
                Object.Destroy(backButton.GetComponent<UIGamePad>());
            }
        }

        /// <summary>
        /// Create a new Tab out of a config section
        /// </summary>
        private static void createTabForSection(KeyValuePair<string, Dictionary<string, ConfigEntryBase>> section, int sectionCount) {
            var tabButtons = settings.transform.Find("panel").Find("TabButtons");

            // Create new tab button
            var newTabButton = Object.Instantiate(tabButtonPrefab, tabButtons);
            newTabButton.name = section.Key;
            var rectTransform = newTabButton.GetComponent<RectTransform>();
            var tabButtonXPosition = TabButtonWidth * (tabCounter - (sectionCount - 1) * 0.5f);
            rectTransform.anchoredPosition = new Vector2(tabButtonXPosition, rectTransform.anchoredPosition.y);

            foreach (TMP_Text text in newTabButton.GetComponentsInChildren<TMP_Text>()) {
                text.text = section.Key;
            }
            
            // Create new tab content
            var tabs = settings.transform.Find("panel").Find("Tabs");
            var newTab = Object.Instantiate(tabs.GetChild(0), tabs);
            newTab.name = section.Key;

            foreach (Transform child in newTab.transform)
            {
                Object.Destroy(child.gameObject);
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
            int posY = -30;
            
            if (section.Value.Count > 14) {
                posX = -250;
            }
            
            // iterate all config entries of current section and create elements 
            foreach (KeyValuePair<string, ConfigEntryBase> configValue in section.Value) {
                
                if (! createElement(configValue, newTab, new Vector2(posX, posY), section.Key)) {
                    continue;
                }
                
                posY -= 30;
                if (posY < -420) {
                    posY = -30;
                    posX = 75;
                }
            }

            tabCounter++;
        }

        /// <summary>
        /// Create a Settings Element out of a Config Entry
        /// </summary>
        private static bool createElement(KeyValuePair<string, ConfigEntryBase> configEntry, Transform parent, Vector2 pos, string sectionName) {
            
            if (configEntry.Value.SettingType == typeof(bool)) {
                createToggle(configEntry, parent, pos);
                return true;
            }

            if (configEntry.Value.SettingType == typeof(KeyCode)) {
                createKeyBinding(configEntry, parent, pos);
                return true;
            }
            
            if (configEntry.Value.SettingType == typeof(Vector3)) {
                createTransformButton(configEntry, parent, pos, sectionName);
                return true;
            }
            
            if (configEntry.Value.SettingType == typeof(Quaternion)) {
                // ignore
                return false;
            }
            
            var acceptableValues = configEntry.Value.Description.AcceptableValues;
            if (acceptableValues == null) {
                return false;
            }

            var type = acceptableValues.GetType();
            if (type.GetGenericTypeDefinition() == typeof(AcceptableValueList<>)) {
                createValueList(configEntry, parent, pos, type, acceptableValues);
                return true;
            }
            
            if (type.GetGenericTypeDefinition() == typeof(AcceptableValueRange<>)) {
                createValueRange(configEntry, parent, pos, type, acceptableValues);
                return true;
            }

            return false;
        }

        private static void createValueRange(KeyValuePair<string, ConfigEntryBase> configValue, Transform parent, Vector2 pos, Type type,
            AcceptableValueBase acceptableValues) {

            var sliderObj = Object.Instantiate(sliderPrefab, parent);
            var configComponent = sliderObj.AddComponent<ConfigComponent>();
            configComponent.configValue = configValue;
            sliderObj.GetComponent<TMP_Text>().text = configValue.Key;
            sliderObj.GetComponent<RectTransform>().anchoredPosition = pos;
            var slider = sliderObj.GetComponentInChildren<Slider>();
            slider.minValue = float.Parse(type.GetProperty("MinValue").GetValue(acceptableValues).ToString());
            slider.maxValue =  float.Parse(type.GetProperty("MaxValue").GetValue(acceptableValues).ToString());
            slider.value = float.Parse(configValue.Value.GetSerializedValue(), CultureInfo.InvariantCulture);
            if (acceptableValues.ValueType == typeof(int)) {
                slider.wholeNumbers = true;
            }
            
            configComponent.saveAction = param => {
                configValue.Value.SetSerializedValue(slider.value.ToString(CultureInfo.InvariantCulture));
            };
        }

        private static void createValueList(KeyValuePair<string, ConfigEntryBase> configValue, Transform parent, Vector2 pos, Type type,
        AcceptableValueBase acceptableValues) {
            
            var chooserObj = Object.Instantiate(chooserPrefab, parent);
            var configComponent = chooserObj.AddComponent<ConfigComponent>();
            configComponent.configValue = configValue;
            chooserObj.GetComponent<TMP_Text>().text = configValue.Key;
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
                        child.GetComponentInChildren<TMP_Text>().fontSize = 6;
                        child.GetComponentInChildren<TMP_Text>().text = configValue.Value.GetSerializedValue();
                        break;
                    case "Left":
                        child.localScale *= 0.5f;
                        child.GetComponent<RectTransform>().anchoredPosition = new Vector2(110, 0);
                        child.GetComponent<Button>().onClick.AddListener(() => {
                            var text = valueList[mod(--currentIndex, valueList.Length)];
                            chooserObj.transform.Find("bkg").GetComponentInChildren<TMP_Text>().text = text;
                        });
                        break;
                    case "Right":
                        child.localScale *= 0.5f;
                        child.GetComponent<RectTransform>().anchoredPosition = new Vector2(250, 0);
                        child.GetComponent<Button>().onClick.AddListener(() => {
                            var text = valueList[mod(++currentIndex, valueList.Length)];
                            chooserObj.transform.Find("bkg").GetComponentInChildren<TMP_Text>().text = text;
                        });
                        break;
                    default:
                        child.gameObject.SetActive(false);
                        break;
                }
            }
            
            configComponent.saveAction = param => {
                configValue.Value.SetSerializedValue(chooserObj.transform.Find("bkg").GetComponentInChildren<TMP_Text>().text);
            };
        }
        
        private static void createToggle(KeyValuePair<string, ConfigEntryBase> configValue, Transform parent, Vector2 pos) {
            
            var toggle = Object.Instantiate(togglePrefab, parent);
            var configComponent = toggle.AddComponent<ConfigComponent>();
            configComponent.configValue = configValue;
            configComponent.saveAction = param => {
                configValue.Value.SetSerializedValue(toggle.GetComponent<Toggle>().isOn ? "true" : "false");
            };
            toggle.GetComponentInChildren<TMP_Text>().text = configValue.Key;
            toggle.GetComponent<Toggle>().isOn = configValue.Value.GetSerializedValue() == "true";
            
            pos.x -= 210;
            toggle.GetComponent<RectTransform>().anchoredPosition = pos;
        }

        private static void createTransformButton(KeyValuePair<string, ConfigEntryBase> configValue, Transform parent, Vector2 pos, string sectionName) {
            
            ConfigEntry<Quaternion> confRot;
            if (!VHVRConfig.config.TryGetEntry(sectionName, configValue.Key + "Rot", out confRot)) {
                Debug.LogError(configValue.Key + "Rot not found. Please make sure a Quaternion with this name exists in section " + sectionName);
                return;
            }
            var labledButton = new GameObject();
            labledButton.transform.SetParent(parent, false);
            var configComponent = labledButton.AddComponent<ConfigComponent>();
            configComponent.configValue = configValue;
            configComponent.saveAction = param => {};
            
            // Label
            var label = Object.Instantiate(sliderPrefab, labledButton.transform);
            foreach (Transform child in label.transform) {
                GameObject.Destroy(child.gameObject);
            }
            var text = label.GetComponent<TMP_Text>();
            text.text = configValue.Key;
            
            pos.y += 225;
            text.GetComponent<RectTransform>().anchoredPosition = pos;
            
            // Button
            var button = Object.Instantiate(buttonPrefab, labledButton.transform);
            pos.x += 150;
            button.GetComponent<RectTransform>().anchoredPosition = pos;
            button.GetComponent<RectTransform>().localScale *= 0.5f;
            button.GetComponentInChildren<TMP_Text>().text = "Set";

            if (menuList.name != "MenuEntries") {
                button.GetComponent<Button>().enabled = false;
            }
            
            button.GetComponent<Button>().onClick.SetPersistentListenerState(0, UnityEventCallState.Off);
            button.GetComponent<Button>().onClick.RemoveAllListeners();
            button.GetComponent<Button>().onClick.AddListener(() => {
                if (! (bool) typeof(SettingCallback).GetMethod(configValue.Key).Invoke(null,
                    new UnityAction<Vector3, Quaternion>[] {
                        (mPos, mRot) => {
                            configValue.Value.SetSerializedValue(String.Format(CultureInfo.InvariantCulture,
                                "{{\"x\":{0}, \"y\":{1}, \"z\":{2}}}", mPos.x, mPos.y, mPos.z));
                            confRot.SetSerializedValue(String.Format(CultureInfo.InvariantCulture,
                                "{{\"x\":{0}, \"y\":{1}, \"z\":{2}, \"w\":{3}}}", mRot.x, mRot.y, mRot.z, mRot.w));
                        }
                    })) {
                    return;
                }
                
                doSave = false;
                Settings.instance.OnBack();
                Menu.instance.OnClose();
            });

            // This button will just reset the position and rotation back to the default values.
            var defaultButton = Object.Instantiate(buttonPrefab, labledButton.transform);
            pos.x += button.GetComponent<RectTransform>().rect.width/2;
            defaultButton.GetComponent<RectTransform>().anchoredPosition = pos;
            defaultButton.GetComponent<RectTransform>().localScale *= 0.5f;
            defaultButton.GetComponentInChildren<TMP_Text>().text = "Reset";

            if (menuList.name != "MenuEntries")
            {
                defaultButton.GetComponent<Button>().enabled = false;
                return;
            }

            defaultButton.GetComponent<Button>().onClick.SetPersistentListenerState(0, UnityEventCallState.Off);
            defaultButton.GetComponent<Button>().onClick.RemoveAllListeners();
            defaultButton.GetComponent<Button>().onClick.AddListener(() => {
                if (!(bool)typeof(SettingCallback).GetMethod(configValue.Key +"Default").Invoke(null,
                    new UnityAction<Vector3, Quaternion>[] {
                        (mPos, mRot) => {
                            configValue.Value.SetSerializedValue(String.Format(CultureInfo.InvariantCulture,
                                "{{\"x\":{0}, \"y\":{1}, \"z\":{2}}}", mPos.x, mPos.y, mPos.z));
                            confRot.SetSerializedValue(String.Format(CultureInfo.InvariantCulture,
                                "{{\"x\":{0}, \"y\":{1}, \"z\":{2}, \"w\":{3}}}", mRot.x, mRot.y, mRot.z, mRot.w));
                        }
                    }))
                {
                    return;
                }
            });

        }

        private static void createKeyBinding(KeyValuePair<string, ConfigEntryBase> configValue, Transform parent, Vector2 pos) {
            
            var keyBinding = Object.Instantiate(keyBindingPrefab, parent);
            var configComponent = keyBinding.AddComponent<ConfigComponent>();
            configComponent.configValue = configValue;
            configComponent.saveAction = param => {
                configValue.Value.SetSerializedValue(param);
            };
            keyBinding.GetComponent<TMP_Text>().text = configValue.Key;
            Settings.m_instance.m_keys.Add(new Settings.KeySetting {m_keyName = configValue.Key, m_keyTransform = keyBinding.GetComponent<RectTransform>()});
            keyBinding.GetComponentInChildren<Button>().onClick.AddListener(() => {
                Settings.instance.OnKeySet();
                tmpComfigComponent = configComponent;
            });
            keyBinding.GetComponent<RectTransform>().anchoredPosition = pos;
            ZInput.instance.AddButton(configValue.Key, (KeyCode) Enum.Parse(typeof(KeyCode), configValue.Value.GetSerializedValue()));
        }

        private static void CaptureScreenshot()
        {
            string dir = new Regex("[\\/]valheim_Data$", RegexOptions.IgnoreCase).Replace(Application.dataPath, "") + "/VHVRScreenshots";
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            string path = dir + "/vhvr_screenshot_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
            LogUtils.LogDebug("Saving screenshot to " + path);
            ScreenCapture.CaptureScreenshot(path);
        }

        private static int mod(int x, int m) {
            return (x%m + m)%m;
        }

        public static void updateBindings() {
            
            if (tmpComfigComponent == null) {
                return;
            }
            
            foreach (Settings.KeySetting key in Settings.instance.m_keys) {
                if (key.m_keyName == tmpComfigComponent.configValue.Key) {
                    var buttons = AccessTools.FieldRefAccess<ZInput, Dictionary<string, ZInput.ButtonDef>>(ZInput.instance, "m_buttons");
                    ZInput.ButtonDef buttonDef;
                    buttons.TryGetValue(key.m_keyName, out buttonDef);
                    tmpComfigComponent.value = buttonDef.m_key.ToString();
                }
            }
            
            tmpComfigComponent = null;
        }
    }
}
