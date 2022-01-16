using System.Reflection;
using HarmonyLib;
using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts {
    public class QuickSwitch : QuickAbstract {
        
        private MethodInfo stopEmote = AccessTools.Method(typeof(Player), "StopEmote");
        private Texture2D sitTexture;

        QuickSwitch() {
            sitTexture = VRAssetManager.GetAsset<Texture2D>("sit");   
        }
        
        public override int refreshHandSpecific() {
            elements[elementCount].transform.GetChild(2).GetComponent<SpriteRenderer>().sprite =  Sprite.Create(sitTexture,
                new Rect(0.0f, 0.0f, sitTexture.width, sitTexture.height),
                new Vector2(0.5f, 0.5f), 500);
            elementCount++;
            
            return 1;
        }

        public override bool selectHandSpecific() {

            if (hoveredIndex < 0) {
                return true;
            }
            
            if (hoveredIndex == 0) {
                if (Player.m_localPlayer.InEmote() && Player.m_localPlayer.IsSitting()) {
                    stopEmote.Invoke(Player.m_localPlayer, null);
                }
                else {
                    Player.m_localPlayer.StartEmote("sit", false);    
                }

                return true;
            }

            return false;
        }
    }
}