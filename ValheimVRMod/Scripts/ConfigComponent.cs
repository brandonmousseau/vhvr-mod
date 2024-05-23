using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ValheimVRMod.VRCore.UI;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts {
    public class ConfigComponent : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
        private static string textStr = "";
        private static ConfigComponent currentHoveredComponent;

        public KeyValuePair<string, ConfigEntryBase> configValue;
        public UnityAction<string> saveAction;
        public string value;

        public void LateUpdate() {
            TMPro.TMP_Text textObj = ConfigSettings.toolTip.GetComponentInChildren<TMPro.TMP_Text>();
            textObj.text = textStr;
            ConfigSettings.toolTip.GetComponent<Image>().rectTransform.sizeDelta = new Vector2(908, textObj.preferredHeight + 8);
        }

        public void OnPointerEnter(PointerEventData eventData) {
            currentHoveredComponent = this;
            textStr = configValue.Value.Description.Description;
            ConfigSettings.toolTip.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData) {
            if (currentHoveredComponent == this)
            {
                ConfigSettings.toolTip.SetActive(false);
            }
        }

        private void OnDestroy() {
            if (ConfigSettings.doSave) {
                saveAction(value);   
            }
        }
    }
}
