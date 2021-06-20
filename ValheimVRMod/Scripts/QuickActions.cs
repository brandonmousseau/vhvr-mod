using System.Reflection;
using HarmonyLib;
using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts {
    public class QuickActions : QuickAbstract {

        protected int SLOTS = 2;

        private MethodInfo stopEmote = AccessTools.Method(typeof(Player), "StopEmote");


        protected override int getSlots() {
            return SLOTS;
        }
        
        public override void refreshItems() {
        
            StatusEffect se;
            float cooldown;
            Player.m_localPlayer.GetGuardianPowerHUD(out se, out cooldown);
            if (se) {
                items[0].GetComponent<SpriteRenderer>().sprite = se.m_icon;
            }
            
            Texture2D sitTexture = VRAssetManager.GetAsset<Texture2D>("sit");
            items[1].GetComponent<SpriteRenderer>().sprite =  Sprite.Create(sitTexture,
                new Rect(0.0f, 0.0f, sitTexture.width, sitTexture.height),
                new Vector2(0.5f, 0.5f), 500);
        }

        public override void selectHoveredItem() {
        
            if (hoveredItemIndex < 0) {
                return;
            }

            switch (hoveredItemIndex) {
                
                case 0:
                    Player.m_localPlayer.StartGuardianPower();
                    break;
                case 1:
                    if (Player.m_localPlayer.InEmote() && Player.m_localPlayer.IsSitting())
                        stopEmote.Invoke(Player.m_localPlayer, null);
                    else
                        Player.m_localPlayer.StartEmote("sit", false);
                    break;
            }
        }
    }
}