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
using Valheim.SettingsGui;
using ValheimVRMod.Scripts;
using ValheimVRMod.Utilities;
using Object = UnityEngine.Object;
using TMPro;

namespace ValheimVRMod.VRCore.UI {
    public class ConfigSettings {

        // TODO: Refactor and fix VHVR settings dialog layout.
        private const bool ENABLE_VHVR_SETTINGS_DIALOG = true;
        
        private const float MENU_ENTRY_HEIGHT = 40;
        private const string MenuName = "VHVR";
        private const int TabButtonWidth = 100;
        
        private static GameObject tabButtonPrefab;
        private static GameObject controlSettingsPrefab;
        private static GameObject togglePrefab;
        private static GameObject sliderPrefab;
        private static GameObject chooserPrefab;
        private static GameObject settingsPrefab;
        private static GameObject keyBindingPrefab;
        private static GameObject transformButtonPrefab;
        private static GameObject settings;
        private static Transform menuList;
        private static Transform menuParent;
        private static ConfigComponent tmpComfigComponent;
        private static bool enableTransformButtons;
        private static int tabCounter;
        public static bool doSave;
        public static GameObject toolTip;

        public static KeyboardMouseSettings keyboardMouseSettings;

        public static void instantiate(Transform mList, Transform mParent, GameObject sPrefab, bool enableTransformButtons) {
            menuList = mList.transform.Find("MenuEntries").transform;
            menuParent = mParent;
            settingsPrefab = sPrefab;
            createMenuEntry();
            generatePrefabs();
            ConfigSettings.enableTransformButtons = enableTransformButtons;
        }

        public static bool isVHVRClone(KeyboardMouseSettings settings)
        {
            return settings.GetComponentInParent<SettingsCloneMarker>(includeInactive: true) != null;
        }
 
        /// <summary>
        /// Create an Entry in the Menu 
        /// </summary>
        private static void createMenuEntry() {
            int addedMenuEntryCount = 0;
            for (int i = 0; i < menuList.childCount; i++) {
                Transform menuEntry = menuList.GetChild(i);
                if (menuEntry.name == "Settings") {
                    if (ENABLE_VHVR_SETTINGS_DIALOG)
                    {
                        AddMenuEntry(MenuName, menuEntry, Vector2.zero, createModSettings);
                        addedMenuEntryCount++;
                    }

                    AddMenuEntry("Screenshot", menuEntry, Vector2.up * MENU_ENTRY_HEIGHT * addedMenuEntryCount, CaptureScreenshot);
                    addedMenuEntryCount++;
                }
                else if (addedMenuEntryCount > 0) {
                    var rectTransform = menuList.GetChild(i).GetComponent<RectTransform>();
                    rectTransform.anchoredPosition = rectTransform.anchoredPosition + Vector2.up * MENU_ENTRY_HEIGHT * addedMenuEntryCount;
                }
            }
        }

        private static void AddMenuEntry(string text, Transform original, Vector2 offsetFromOriginal, UnityAction onClick)
        {
            Transform menuEntry = GameObject.Instantiate(original, parent: menuList);
            menuEntry.GetComponentInChildren<TextMeshProUGUI>().text = text;
            menuEntry.GetComponent<Button>().onClick.SetPersistentListenerState(0, UnityEventCallState.Off);
            menuEntry.GetComponent<Button>().onClick.RemoveAllListeners();
            menuEntry.GetComponent<Button>().onClick.AddListener(onClick);
            RectTransform rectTransform = menuEntry.GetComponent<RectTransform>();
            rectTransform.anchoredPosition = rectTransform.anchoredPosition + offsetFromOriginal;
        }

        /// <summary>
        /// Create temporary prefabs out of existing elements
        /// </summary>
        private static void generatePrefabs() {

            var tabButtons = settingsPrefab.transform.Find("Panel").Find("TabButtons");
            if (tabButtonPrefab == null)
            {
                tabButtonPrefab = createTabButtonPrefab(tabButtons.GetChild(0).gameObject);
            }
            var tabs = settingsPrefab.transform.Find("Panel").Find("TabContent");
            controlSettingsPrefab = tabs.Find("KeyboardMouse").gameObject;
            togglePrefab = controlSettingsPrefab.GetComponentInChildren<Toggle>().gameObject;
            if (sliderPrefab == null)
            {
                sliderPrefab = createSliderPrefab(controlSettingsPrefab.GetComponentInChildren<Slider>().gameObject);
            }
            if (keyBindingPrefab == null)
            {
                keyBindingPrefab = createKeyBindingPrefab(
                    controlSettingsPrefab.transform.Find("List").Find("Bindings").Find("Viewport").Find("Grid").Find("Use").gameObject);
            }
            if (chooserPrefab == null)
            {
                chooserPrefab = createChooserPrefab(
                    settingsPrefab.transform.Find("Panel").Find("TabContent").Find("Gamepad").Find("List").Find("InputLayout").gameObject);
            }
            if (transformButtonPrefab == null)
            {
                transformButtonPrefab = createTransformButtonPrefab(
                    sliderPrefab.transform.Find("Label").gameObject,
                    settingsPrefab.transform.Find("Panel").Find("Back").gameObject);
            }
        }

        private static void createToolTip(Transform settings) {
            toolTip = new GameObject();
            toolTip.transform.SetParent(settings, false);

            var bkgImage = toolTip.AddComponent<Image>();
            bkgImage.rectTransform.pivot = new Vector2(0.5f, 0);
            bkgImage.rectTransform.anchoredPosition = new Vector2(0, -400);
            bkgImage.color = new Color(0,0,0,0.5f);
            bkgImage.raycastTarget = false;

            var textObj = Object.Instantiate(togglePrefab.GetComponentInChildren<TMP_Text>().gameObject, toolTip.transform);
            TMP_Text text = textObj.GetComponent<TMP_Text>();
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            text.rectTransform.sizeDelta = new Vector2(900, 125);
            text.rectTransform.anchoredPosition = new Vector2(454, 0);
            // text.resizeTextForBestFit = false;
            text.fontSize = 20;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;

            toolTip.SetActive(false);
        }

        /// <summary>
        /// Make Copy of ingame Settings, clean up all existing tabs, then iterate bepinex config
        /// </summary>
        private static void createModSettings() {
            settings = Object.Instantiate(settingsPrefab, menuParent);
            settings.AddComponent<SettingsCloneMarker>();
            settings.transform.Find("Panel").Find("Title").GetComponent<TMP_Text>().text = MenuName;
            createToolTip(settings.transform);
            var tabButtons = settings.transform.Find("Panel").Find("TabButtons");

            // destroy old tab buttons
            foreach (Transform t in tabButtons) {
                Object.Destroy(t.gameObject);
            }
            // clear tab array
            tabButtons.GetComponent<TabHandler>().m_tabs.Clear();

            // deactivate old tab contents
            foreach (Transform t in settings.transform.Find("Panel").Find("TabContent")) {
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

            setupOkAndBack(settings.transform.Find("Panel"));

            tabButtons.GetComponent<TabHandler>().SetActiveTab(0);
            keyboardMouseSettings.UpdateBindings();
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
                okButton.onClick.RemoveAllListeners();
                okButton.onClick.m_PersistentCalls.Clear();
                okButton.onClick.AddListener(() => {
                    doSave = true;
                    GameObject.Destroy(settings);
                });
                Object.Destroy(okButton.GetComponent<UIGamePad>());
                var hint = okButton.transform.Find("KeyHint");
                if (hint) Object.Destroy(hint.gameObject);
            }

            Button backButton = panel.Find("Back")?.GetComponent<Button>();
            if (backButton == null)
            {
                LogUtils.LogWarning("Failed to find Back button for VHVR");
            }
            else
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.m_PersistentCalls.Clear();
                backButton.onClick.AddListener(() => {
                    doSave = false;
                    GameObject.Destroy(settings);
                });
                Object.Destroy(backButton.GetComponent<UIGamePad>());
                var hint = backButton.transform.Find("KeyHint");
                if (hint) Object.Destroy(hint.gameObject);
            }
        }

        /// <summary>
        /// Create a new Tab out of a config section
        /// </summary>
        private static void createTabForSection(KeyValuePair<string, Dictionary<string, ConfigEntryBase>> section, int sectionCount) {
            var tabButtons = settings.transform.Find("Panel").Find("TabButtons");

            // Create new tab button
            var newTabButton = Object.Instantiate(tabButtonPrefab, tabButtons);

            newTabButton.name = section.Key;
            var rectTransform = newTabButton.GetComponent<RectTransform>();
            var tabButtonXPosition = TabButtonWidth * (tabCounter - (sectionCount - 1) * 0.5f);
            rectTransform.anchoredPosition = new Vector2(tabButtonXPosition, rectTransform.anchoredPosition.y);

            var labels = newTabButton.GetComponentsInChildren<TMP_Text>(includeInactive: true);
            foreach (var label in labels)
            {
                label.text = section.Key;
            }

            // Create new tab content
            var tabs = settings.transform.Find("Panel").Find("TabContent");
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

            int posX = 0;
            int posY = 250;
            
            if (section.Value.Count > 18) {
                posX = -200;
            }
            
            // iterate all config entries of current section and create elements 
            foreach (KeyValuePair<string, ConfigEntryBase> configValue in section.Value) {
                
                if (! createElement(configValue, newTab, new Vector2(posX, posY), section.Key)) {
                    continue;
                }
                
                posY -= 30;
                if (posY < -270) {
                    posY = 250;
                    posX = 250;
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

        private static GameObject createTabButtonPrefab(GameObject vanillaObject)
        {
            var prefab = GameObject.Instantiate(vanillaObject);
            var hint = prefab.transform.Find("KeyHint");
            if (hint) GameObject.Destroy(hint.gameObject);
            var button = prefab.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.m_PersistentCalls.Clear();
            return prefab;

        }

        private static GameObject createSliderPrefab(GameObject vanillaPrefab)
        {

            var sliderObj = Object.Instantiate(vanillaPrefab);
            var rectTransform = sliderObj.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(150, 30);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            sliderObj.transform.Find("Label").GetComponent<RectTransform>().sizeDelta = new Vector3(200, 0);
            return sliderObj;
        }

        private static void createValueRange(KeyValuePair<string, ConfigEntryBase> configValue, Transform parent, Vector2 pos, Type type,
            AcceptableValueBase acceptableValues) {

            var sliderObj = Object.Instantiate(sliderPrefab, parent);
            var configComponent = sliderObj.AddComponent<ConfigComponent>();
            configComponent.configValue = configValue;  
            sliderObj.transform.Find("Label").GetComponent<TMP_Text>().text = configValue.Key;
            sliderObj.GetComponent<RectTransform>().anchoredPosition = pos + Vector2.right * 60;
            var slider = sliderObj.GetComponentInChildren<Slider>();
            slider.minValue = float.Parse(type.GetProperty("MinValue").GetValue(acceptableValues).ToString());
            slider.maxValue =  float.Parse(type.GetProperty("MaxValue").GetValue(acceptableValues).ToString());
            slider.value = float.Parse(configValue.Value.GetSerializedValue(), CultureInfo.InvariantCulture);
            var text = slider.transform.Find("Value").GetComponent<TMP_Text>();
            text.text = "" + slider.value;

            slider.onValueChanged.AddListener(
                (value) =>
                {
                    text.text = "" + slider.value;
                });

            if (acceptableValues.ValueType == typeof(int)) {
                slider.wholeNumbers = true;
            }
            
            configComponent.saveAction = param => {
                configValue.Value.SetSerializedValue(slider.value.ToString(CultureInfo.InvariantCulture));
            };


        }

        private static GameObject createChooserPrefab(GameObject vanillaPrefab)
        {
            var chooserPrefab = Object.Instantiate(vanillaPrefab);
            chooserPrefab.transform.Find("LabelLeft").gameObject.SetActive(true);
            var rectTransform = chooserPrefab.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(150, 30);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            Transform stepper = chooserPrefab.transform.Find("GUIStepper");
            for (int i = 0; i < stepper.childCount; i++)
            {
                var child = stepper.transform.GetChild(i);
                switch (child.name)
                {
                    case "Value":
                        child.gameObject.SetActive(true);
                        child.GetComponent<RectTransform>().sizeDelta = new Vector2(-60, -5);
                        child.GetComponentInChildren<TMP_Text>().fontSize = 5;
                        break;
                    case "Left":
                        child.gameObject.SetActive(true);
                        child.GetComponent<RectTransform>().sizeDelta = new Vector2(30, 30);
                        child.GetComponent<Button>().onClick.m_PersistentCalls.Clear();
                        child.GetComponent<Button>().onClick.RemoveAllListeners();
                        break;
                    case "Right":
                        child.gameObject.SetActive(true);
                        child.GetComponent<RectTransform>().sizeDelta = new Vector2(30, 30);
                        child.GetComponent<Button>().onClick.m_PersistentCalls.Clear();
                        child.GetComponent<Button>().onClick.RemoveAllListeners();
                        break;
                    default:
                        child.gameObject.SetActive(false);
                        break;
                }
            }
            return chooserPrefab;
        }

        private static void createValueList(
            KeyValuePair<string, ConfigEntryBase> configValue, Transform parent, Vector2 pos, Type type, AcceptableValueBase acceptableValues) {

            var chooserObj = Object.Instantiate(chooserPrefab, parent);
            chooserObj.GetComponent<RectTransform>().anchoredPosition = pos + Vector2.left * 10;
            chooserObj.SetActive(true);

            var configComponent = chooserObj.AddComponent<ConfigComponent>();
            configComponent.configValue = configValue;
            var configKeyText = chooserObj.transform.Find("LabelLeft").GetComponent<TMP_Text>();
            configKeyText.text = configValue.Key;
            if (configValue.Key == VHVRConfig.GesturedLocomotionLabel())
            {
                var distance = GesturedLocomotionManager.distanceTraveled;
                if (distance > 1000)
                {
                    configKeyText.text += "(s=" + (distance / 1000).ToString("F1") + "k)";
                } else if (distance > 10)
                {
                    configKeyText.text += "(s=" + distance.ToString("F0") + ")";
                }
            }
            Transform stepper = chooserObj.transform.Find("GUIStepper");
            var valueList = (string[])type.GetProperty("AcceptableValues").GetValue(acceptableValues);
            var currentIndex = Array.IndexOf(valueList, configValue.Value.GetSerializedValue());
            var valueText = stepper.Find("Value").GetComponentInChildren<TMP_Text>();
            valueText.text = configValue.Value.GetSerializedValue();
            stepper.Find("Left").GetComponent<Button>().onClick.AddListener(() =>{
                var text = valueList[mod(--currentIndex, valueList.Length)];
                valueText.text = text;
            });
            stepper.Find("Right").GetComponent<Button>().onClick.AddListener(() => {
                var text = valueList[mod(++currentIndex, valueList.Length)];
                valueText.text = text;
            });

            configComponent.saveAction = param => {
                configValue.Value.SetSerializedValue(stepper.Find("Value").GetComponentInChildren<TMP_Text>().text);
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
            toggle.GetComponent<RectTransform>().anchoredPosition = pos + Vector2.right * 100;
        }

        private static GameObject createTransformButtonPrefab(GameObject vanillaLabel, GameObject vanillaButton)
        {
            var transformButtonPrefab = new GameObject();
            transformButtonPrefab.AddComponent<RectTransform>().sizeDelta = new Vector2(180, 30);

            // Label
            var label = Object.Instantiate(vanillaLabel, transformButtonPrefab.transform);
            label.name = "Label";
            foreach (Transform child in label.transform)
            {
                GameObject.Destroy(child.gameObject);
            }
            label.GetComponent<RectTransform>().anchorMin = Vector2.one * 0.5f;
            label.GetComponent<RectTransform>().anchorMax = Vector2.one * 0.5f;

            // Button
            var setButton = Object.Instantiate(vanillaButton, transformButtonPrefab.transform);
            setButton.name = "SetButton";
            setButton.GetComponent<RectTransform>().anchoredPosition = Vector2.right * 50;
            setButton.GetComponent<RectTransform>().anchorMin = Vector2.one * 0.5f;
            setButton.GetComponent<RectTransform>().anchorMax = Vector2.one * 0.5f;
            setButton.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 30);
            setButton.GetComponentInChildren<TMP_Text>().text = "Set";

            setButton.GetComponent<Button>().onClick.m_PersistentCalls.Clear();
            setButton.GetComponent<Button>().onClick.RemoveAllListeners();

            // This button will just reset the position and rotation back to the default values.
            var resetButton = Object.Instantiate(vanillaButton, transformButtonPrefab.transform);
            resetButton.name = "ResetButton";
            resetButton.GetComponent<RectTransform>().anchoredPosition = Vector2.right * 150;
            resetButton.GetComponent<RectTransform>().anchorMin = Vector2.one * 0.5f;
            resetButton.GetComponent<RectTransform>().anchorMax = Vector2.one * 0.5f;
            resetButton.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 30);
            resetButton.GetComponentInChildren<TMP_Text>().text = "Reset";

            resetButton.GetComponent<Button>().onClick.m_PersistentCalls.Clear();
            resetButton.GetComponent<Button>().onClick.RemoveAllListeners();

            return transformButtonPrefab;
        }

        private static void createTransformButton(KeyValuePair<string, ConfigEntryBase> configValue, Transform parent, Vector2 pos, string sectionName) {

            var is3Axis = false;
            ConfigEntry<Quaternion> confRot;
            if (!VHVRConfig.config.TryGetEntry(sectionName, configValue.Key + "Rot", out confRot)) {
                Debug.LogError(configValue.Key + "Rot not found (in " + sectionName + " section), will only read Vector3 Axis");
                is3Axis = true;
            }
            var transformButton = GameObject.Instantiate(transformButtonPrefab, parent);
            transformButton.GetComponent<RectTransform>().anchoredPosition = pos + Vector2.left * 10;

            var configComponent = transformButton.AddComponent<ConfigComponent>();
            configComponent.configValue = configValue;
            configComponent.saveAction = param => {};

            var label = transformButton.transform.Find("Label").GetComponent<TMP_Text>();
            label.text = configValue.Key;

            var setButton = transformButton.transform.Find("SetButton").GetComponent<Button>();
            if (!enableTransformButtons) {
                setButton.enabled = false;
            }
            setButton.onClick.AddListener(() => {
                // TODO: use something else (e. g. a dictionary from key to method) instead of Reflection to get the methods.
                // This can be broken easily without noticing: if the target method's name is changed,
                // This call does not show up in call hierarchy in IDE and does not throw any error at build time.
                var method = typeof(SettingCallback).GetMethod(configValue.Key);
                if (method == null)
                {
                    LogUtils.LogError("Cannot find method SettingCallback." + configValue.Key);
                    return;
                }

                if (is3Axis)
                {
                    if (!(bool)method.Invoke(
                    null,
                    new UnityAction<Vector3>[] {
                        (mPos) => {
                            configValue.Value.SetSerializedValue(String.Format(CultureInfo.InvariantCulture,
                                "{{\"x\":{0}, \"y\":{1}, \"z\":{2}}}", mPos.x, mPos.y, mPos.z));
                        }
                    }))
                    {
                        return;
                    }
                }
                else
                {
                    if (!(bool)method.Invoke(
                    null,
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
                }
                doSave = false;
                GameObject.Destroy(settings);
                Menu.instance.OnClose();
            });

            // This button will just reset the position and rotation back to the default values.
            var resetButton = transformButton.transform.Find("ResetButton").GetComponent<Button>();
       
            if (!enableTransformButtons)
            {
                resetButton.enabled = false;
                return;
            }

            resetButton.onClick.AddListener(() => {
                // TODO: use something else (e. g. a dictionary from key to method) instead of Reflection to get the methods.
                // This can be broken easily without noticing: if the target method's name is changed,
                // This call does not show up in call hierarchy in IDE and does not throw any error at build time.                
                var method = typeof(SettingCallback).GetMethod(configValue.Key + "Default");
                if (method == null)
                {
                    LogUtils.LogError("Cannot find method SettingCallback." + configValue.Key + "Default");
                    return;
                }
                if (is3Axis)
                {
                    if (!(bool)method.Invoke(null,
                    new UnityAction<Vector3>[] {
                        (mPos) => {
                            configValue.Value.SetSerializedValue(String.Format(CultureInfo.InvariantCulture,
                                "{{\"x\":{0}, \"y\":{1}, \"z\":{2}}}", mPos.x, mPos.y, mPos.z));
                        }
                    }))
                    {
                        return;
                    }
                }
                else
                {
                    if (!(bool)method.Invoke(null,
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
                }
            });
        }

        private static GameObject createKeyBindingPrefab(GameObject vanillaPrefab)
        {            
            var keyBindingPrefab = Object.Instantiate(vanillaPrefab);
            keyBindingPrefab.GetComponentInChildren<RectTransform>().anchorMin = new Vector2(0.5f, 0.5f);
            keyBindingPrefab.GetComponentInChildren<RectTransform>().anchorMax = new Vector2(0.5f, 0.5f);
            keyBindingPrefab.GetComponentInChildren<RectTransform>().sizeDelta = new Vector2(100, 25);
            keyBindingPrefab.GetComponentInChildren<Button>().gameObject.SetActive(true);
            return keyBindingPrefab;
        }

        private static void createKeyBinding(KeyValuePair<string, ConfigEntryBase> configValue, Transform parent, Vector2 pos) {
            
            var keyBinding = Object.Instantiate(keyBindingPrefab, parent);


            keyBinding.GetComponent<RectTransform>().anchoredPosition = pos + Vector2.right * 125;

            var configComponent = keyBinding.AddComponent<ConfigComponent>();
            configComponent.configValue = configValue;
            configComponent.saveAction = param => {
                configValue.Value.SetSerializedValue(param);
            };

            keyBinding.transform.Find("Label").GetComponent<TMP_Text>().text = configValue.Key;
            keyboardMouseSettings.m_keys.Add(new KeySetting {m_keyName = configValue.Key, m_keyTransform = keyBinding.GetComponent<RectTransform>()});
            keyBinding.GetComponentInChildren<Button>().onClick.AddListener(() => {
                keyboardMouseSettings.OnOk();
                tmpComfigComponent = configComponent;
            });
            // TODO: create a proper key binding UI prefab instead of adjusting the position here.
            if (ZInput.instance.m_buttons.ContainsKey(configValue.Key))
            {
                ZInput.instance.m_buttons.Remove(configValue.Key);
            }
            ZInput.instance.AddButton(configValue.Key, ZInput.KeyCodeToPath((KeyCode)Enum.Parse(typeof(KeyCode), configValue.Value.GetSerializedValue())));
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
            
            foreach (KeySetting key in keyboardMouseSettings.m_keys) {
                if (key.m_keyName == tmpComfigComponent.configValue.Key) {
                    var buttons = AccessTools.FieldRefAccess<ZInput, Dictionary<string, ZInput.ButtonDef>>(ZInput.instance, "m_buttons");
                    ZInput.ButtonDef buttonDef;
                    buttons.TryGetValue(key.m_keyName, out buttonDef);
                    tmpComfigComponent.value = buttonDef.ButtonAction.bindings[0].path;
                }
            }
            
            tmpComfigComponent = null;
        }

        private class SettingsCloneMarker : MonoBehaviour { }
    }
}
