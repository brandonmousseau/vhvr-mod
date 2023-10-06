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

        private static HashSet<ConfigComponent> hovered = new HashSet<ConfigComponent>();
        private static string textStr = "";

        public KeyValuePair<string, ConfigEntryBase> configValue;
        public UnityAction<string> saveAction;
        public string value;

        public void LateUpdate() {
            TMPro.TMP_Text textObj = ConfigSettings.toolTip.GetComponentInChildren<TMPro.TMP_Text>();
            textObj.text = textStr;
            ConfigSettings.toolTip.GetComponent<Image>().rectTransform.sizeDelta = new Vector2(758, textObj.preferredHeight + 8);
            ConfigSettings.toolTip.SetActive(hovered.Count > 0);
        }

        public void OnPointerEnter(PointerEventData eventData) {
            hovered.Add(this);
            textStr = configValue.Value.Description.Description;
        }

        public void OnPointerExit(PointerEventData eventData) {
            hovered.Remove(this);
        }

        private void OnDestroy() {
            if (ConfigSettings.doSave) {
                saveAction(value);   
            }
        }
    }
}
