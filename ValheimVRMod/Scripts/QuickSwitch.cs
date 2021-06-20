using UnityEngine;

namespace ValheimVRMod.Scripts {
    public class QuickSwitch : QuickAbstract {

        protected int SLOTS = 8;

        protected override int getSlots() {
            return SLOTS;
        }
        
        /**
         * loop the inventory hotbar and set corresponding item icons + activate equipped layers
         */
        public override void refreshItems() {

            var inventory = Player.m_localPlayer.GetInventory();
            
            for (int i = 0; i < SLOTS; i++) {
                
                ItemDrop.ItemData item = inventory.GetItemAt(i, 0);
                
                if (item == null) {
                    equippedLayers[i].SetActive(false);
                    items[i].GetComponent<SpriteRenderer>().sprite = null;
                    continue;
                }
                
                equippedLayers[i].SetActive(item.m_equiped);
                items[i].GetComponent<SpriteRenderer>().sprite = item.GetIcon();
                
            }
        }

        public override void selectHoveredItem() {

            if (hoveredItemIndex < 0) {
                return;
            }

            var inventory = Player.m_localPlayer.GetInventory();
            ItemDrop.ItemData item = inventory.GetItemAt(hoveredItemIndex, 0);
            Player.m_localPlayer.UseItem(inventory, item, true);
            
        }
    }
}