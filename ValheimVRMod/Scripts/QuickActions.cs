using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts {
    public class QuickActions : QuickAbstract {

        private int elementCount;
        private int extraElementCount;
        private MethodInfo stopEmote = AccessTools.Method(typeof(Player), "StopEmote");
        private bool hasGPower;
        private Texture2D sitTexture; 
        private Texture2D mapTexture;

        public static bool toggleMap;
        
        QuickActions() {
            sitTexture = VRAssetManager.GetAsset<Texture2D>("sit");    
            mapTexture = VRAssetManager.GetAsset<Texture2D>("map");    
        }

        protected override int getElementCount() {
            return elementCount;
        }
        protected override int getExtraElementCount()
        {
            return extraElementCount;
        }

        public override void refreshItems() {

            if (Player.m_localPlayer == null) {
                return;
            }
            
            elementCount = 0;
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
                        ResizeIcon(elements[elementCount]);
                        elementCount++;
                        break;
                }
            }

            //Extra
            StatusEffect se;
            float cooldown;
            extraElementCount = 0;
            Player.m_localPlayer.GetGuardianPowerHUD(out se, out cooldown);
            if (se)
            {
                hasGPower = true;
                extraElements[extraElementCount].transform.GetChild(2).GetComponent<SpriteRenderer>().sprite = se.m_icon;
                ResizeIcon(extraElements[extraElementCount]);
                extraElementCount++;
            }

            extraElements[extraElementCount].transform.GetChild(2).GetComponent<SpriteRenderer>().sprite = Sprite.Create(sitTexture,
                new Rect(0.0f, 0.0f, sitTexture.width, sitTexture.height),
                new Vector2(0.5f, 0.5f), 500);
            extraElementCount++;

            extraElements[extraElementCount].transform.GetChild(2).GetComponent<SpriteRenderer>().sprite = Sprite.Create(mapTexture,
                new Rect(0.0f, 0.0f, mapTexture.width, mapTexture.height),
                new Vector2(0.5f, 0.5f), 500);
            extraElementCount++;

            //extraElements[extraElementCount].transform.GetChild(2).GetComponent<SpriteRenderer>().sprite = Sprite.Create(mapTexture,
            //    new Rect(0.0f, 0.0f, mapTexture.width, mapTexture.height),
            //    new Vector2(0.5f, 0.5f), 500);
            //extraElementCount++;

            reorderElements();
            
        }

        public override void selectHoveredItem() {

            var allElementCount = elementCount + extraElementCount;
            if (hoveredIndex < 0 || hoveredIndex >= allElementCount) {
                return;
            }

            if (hasGPower && hoveredIndex == allElementCount - 3)
            {
                Player.m_localPlayer.StartGuardianPower();
                return;
            }

            if (hoveredIndex == allElementCount - 2) {
                if (Player.m_localPlayer.InEmote() && Player.m_localPlayer.IsSitting())
                    stopEmote.Invoke(Player.m_localPlayer, null);
                else
                    Player.m_localPlayer.StartEmote("sit", false);
                return;
            }


            if (hoveredIndex == allElementCount - 1)
            {
                toggleMap = true;
                return;
            }

            var inventory = Player.m_localPlayer.GetInventory();
            ItemDrop.ItemData item = inventory.GetItemAt(Int32.Parse(elements[hoveredIndex].name), 0);
            Player.m_localPlayer.UseItem(inventory, item, false);
            
        }
    }
}