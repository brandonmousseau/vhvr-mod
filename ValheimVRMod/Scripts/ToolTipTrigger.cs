using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore.UI;

namespace ValheimVRMod.Scripts {
    public class ToolTipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {

        public string text;
        
        public void OnPointerEnter(PointerEventData eventData) {
            var textObj = ConfigSettings.toolTip.GetComponentInChildren<Text>();
            textObj.text = text;
            ConfigSettings.toolTip.GetComponent<Image>().rectTransform.sizeDelta =  new Vector2( 408 , textObj.preferredHeight + 8);
            ConfigSettings.toolTip.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData) {
            ConfigSettings.toolTip.SetActive(false);
        }
    }
}