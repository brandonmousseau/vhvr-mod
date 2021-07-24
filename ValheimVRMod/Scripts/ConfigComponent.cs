using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ValheimVRMod.VRCore.UI;

namespace ValheimVRMod.Scripts {
    public class ConfigComponent : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {

        public KeyValuePair<string, ConfigEntryBase> configValue;
        public UnityAction<string> saveAction;
        public string value;
        
        public void OnPointerEnter(PointerEventData eventData) {
            var textObj = ConfigSettings.toolTip.GetComponentInChildren<Text>();
            textObj.text = configValue.Value.Description.Description;
            ConfigSettings.toolTip.GetComponent<Image>().rectTransform.sizeDelta =  new Vector2( 758 , textObj.preferredHeight + 8);
            ConfigSettings.toolTip.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData) {
            ConfigSettings.toolTip.SetActive(false);
        }

        private void OnDestroy() {
            if (ConfigSettings.doSave) {
                saveAction(value);   
            }
        }
    }
}