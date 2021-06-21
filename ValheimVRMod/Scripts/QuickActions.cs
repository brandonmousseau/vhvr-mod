using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts {
    public class QuickActions : QuickAbstract {

        private int elementCount;
        private MethodInfo stopEmote = AccessTools.Method(typeof(Player), "StopEmote");
        private bool hasGPower;
        private Texture2D sitTexture; 
        
        QuickActions() {
            sitTexture = VRAssetManager.GetAsset<Texture2D>("sit");    
        }

        protected override int getElementCount() {
            return elementCount;
        }
        
        public override void refreshItems() {

            elementCount = 0;
            
            List<ItemDrop.ItemData> items = new List<ItemDrop.ItemData>();
            var inventory = Player.m_localPlayer.GetInventory();

            for (int i = 0; i < 8; i++) {
                
                ItemDrop.ItemData item = inventory.GetItemAt(i, 0);

                if (item == null) {
                    continue;
                }
                
                switch (item.m_shared.m_itemType) {
                    case ItemDrop.ItemData.ItemType.Tool:
                    case ItemDrop.ItemData.ItemType.Torch:
                    case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                    case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                        break;
                    default:
                        
                        elements[elementCount].transform.GetChild(1).gameObject.SetActive(item.m_equiped || item.m_durability == 0);
                        if (item.m_durability == 0) {
                            elements[elementCount].transform.GetChild(1).GetComponent<SpriteRenderer>().color = Color.red;
                        }
                        else {
                            elements[elementCount].transform.GetChild(1).GetComponent<SpriteRenderer>().color = Color.white;
                        }
                        elements[elementCount].transform.GetChild(2).GetComponent<SpriteRenderer>().sprite = item.GetIcon();
                        elements[elementCount].name = i.ToString();
                        elementCount++;
                        break;
                }
            }

            StatusEffect se;
            float cooldown;
            Player.m_localPlayer.GetGuardianPowerHUD(out se, out cooldown);
            if (se) {
                hasGPower = true;
                elements[elementCount].transform.GetChild(2).GetComponent<SpriteRenderer>().sprite = se.m_icon;
                elementCount++;
            }
            
            elements[elementCount].transform.GetChild(2).GetComponent<SpriteRenderer>().sprite =  Sprite.Create(sitTexture,
                new Rect(0.0f, 0.0f, sitTexture.width, sitTexture.height),
                new Vector2(0.5f, 0.5f), 500);
            elementCount++;
            
            reorderElements();
            
        }

        public override void selectHoveredItem() {
        
            if (hoveredIndex < 0) {
                return;
            }

            if (hoveredIndex == elementCount - 1) {
                if (Player.m_localPlayer.InEmote() && Player.m_localPlayer.IsSitting())
                    stopEmote.Invoke(Player.m_localPlayer, null);
                else
                    Player.m_localPlayer.StartEmote("sit", false);
                return;
            }

            if (hasGPower && hoveredIndex == elementCount - 2) {
                Player.m_localPlayer.StartGuardianPower();
                return;
            }
          
            var inventory = Player.m_localPlayer.GetInventory();
            ItemDrop.ItemData item = inventory.GetItemAt(Int32.Parse(elements[hoveredIndex].name), 0);
            Player.m_localPlayer.UseItem(inventory, item, true);
            
        }
    }
}