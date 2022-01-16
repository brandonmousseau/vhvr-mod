using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts {
    public class QuickSwitchLeft : QuickAbstract {
        
        private bool hasGPower;
        private Texture2D mapTexture;

        public static bool toggleMap;
        
        QuickSwitchLeft() : base()
        {
            mapTexture = VRAssetManager.GetAsset<Texture2D>("map");
        }

        public override int refreshHandSpecific() {

            StatusEffect se;
            int extraElements = 0;

            elements[elementCount].transform.GetChild(2).GetComponent<SpriteRenderer>().sprite =  Sprite.Create(mapTexture,
                new Rect(0.0f, 0.0f, mapTexture.width, mapTexture.height),
                new Vector2(0.5f, 0.5f), 500);
            elementCount++;
            extraElements++;
            
            float cooldown;
            Player.m_localPlayer.GetGuardianPowerHUD(out se, out cooldown);
            if (se) {
                hasGPower = true;
                elements[elementCount].transform.GetChild(2).GetComponent<SpriteRenderer>().sprite = se.m_icon;
                elementCount++;
                extraElements++;
            }

            return extraElements;
        }

        public override bool selectHandSpecific() {

            if (hoveredIndex == 0) {
                toggleMap = true;
                return true;
            }
            
            if (hasGPower && hoveredIndex == 1) {
                Player.m_localPlayer.StartGuardianPower();
                return true;
            }

            return false;
        }
    }
}