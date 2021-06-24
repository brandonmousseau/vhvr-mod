using System;
using UnityEngine;

namespace ValheimVRMod.Scripts {
    public class QuickSwitch : QuickAbstract {

        private int elementCount;

        protected override int getElementCount() {
            return elementCount;
        }
        
        /**
         * loop the inventory hotbar and set corresponding item icons + activate equipped layers
         */
        public override void refreshItems() {

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
                        elements[elementCount].transform.GetChild(1).gameObject.SetActive(item.m_equiped || item.m_durability == 0);
                        if (item.m_durability == 0) {
                            elements[elementCount].transform.GetChild(1).GetComponent<SpriteRenderer>().color = Color.red;
                        }
                        else {
                            elements[elementCount].transform.GetChild(1).GetComponent<SpriteRenderer>().color = Color.white;
                        }
                        elements[elementCount].transform.GetChild(2).GetComponent<SpriteRenderer>().sprite = item.GetIcon();
                        elements[elementCount].name = i.ToString() ;
                        elementCount++;
                        break;
                }
            }

            reorderElements();
        }

        public override void selectHoveredItem() {

            if (hoveredIndex < 0) {
                return;
            }

            var inventory = Player.m_localPlayer.GetInventory();
            ItemDrop.ItemData item = inventory.GetItemAt(Int32.Parse(elements[hoveredIndex].name), 0);
            Player.m_localPlayer.UseItem(inventory, item, false);
            
        }
    }
}