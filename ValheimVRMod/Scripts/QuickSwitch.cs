using System;
using UnityEngine;

namespace ValheimVRMod.Scripts {
    public class QuickSwitch : QuickAbstract {

        private int elementCount;
        private int extraElementCount;

        protected override int getElementCount() {
            return elementCount;
        }
        protected override int getExtraElementCount()
        {
            return extraElementCount;
        }
        /**
         * loop the inventory hotbar and set corresponding item icons + activate equipped layers
         */
        public override void refreshItems() {

            if (Player.m_localPlayer == null) {
                return;
            }
            
            elementCount = 0;
            var inventory = Player.m_localPlayer.GetInventory();
            
            for (int i = 0; i < 8; i++) {
                
                ItemDrop.ItemData item = inventory?.GetItemAt(i, 0);

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
                        elements[elementCount].name = i.ToString() ;
                        ResizeIcon(elements[elementCount]);
                        elementCount++;
                        break;
                }
            }

            extraElementCount = 0;
            for (int i = 0; i < 4; i++)
            {

                ItemDrop.ItemData item = inventory?.GetItemAt(i+4, 1);

                if (item == null)
                {
                    continue;
                }

                switch (item.m_shared.m_itemType)
                {
                    case ItemDrop.ItemData.ItemType.Tool:
                    case ItemDrop.ItemData.ItemType.Torch:
                    case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                    case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                    default:
                        extraElements[extraElementCount].transform.GetChild(1).gameObject.SetActive(item.m_equiped || item.m_durability == 0);
                        if (item.m_durability == 0)
                        {
                            extraElements[extraElementCount].transform.GetChild(1).GetComponent<SpriteRenderer>().color = Color.red;
                        }
                        else
                        {
                            extraElements[extraElementCount].transform.GetChild(1).GetComponent<SpriteRenderer>().color = Color.white;
                        }
                        extraElements[extraElementCount].transform.GetChild(2).GetComponent<SpriteRenderer>().sprite = item.GetIcon();
                        extraElements[extraElementCount].name = (i+4).ToString();
                        ResizeIcon(extraElements[extraElementCount]);
                        extraElementCount++;
                        break;
                }
            }
            reorderElements();
        }

        public override void selectHoveredItem() {

            var allElementCount = elementCount + extraElementCount;
            if (hoveredIndex < 0 || hoveredIndex >= allElementCount) {
                return;
            }

            var inventory = Player.m_localPlayer.GetInventory();
            if (hoveredIndex >= elementCount)
            {
                ItemDrop.ItemData item2 = inventory.GetItemAt(Int32.Parse(extraElements[hoveredIndex-elementCount].name), 1);
                Player.m_localPlayer.UseItem(inventory, item2, false);
                return;
            }

            ItemDrop.ItemData item = inventory.GetItemAt(Int32.Parse(elements[hoveredIndex].name), 0);
            Player.m_localPlayer.UseItem(inventory, item, false);
            
        }
    }
}