using System;

namespace ValheimVRMod.Utilities {
    
    [Flags]
    public enum EquipType {
        None, Bow, Fishing, Spear, SpearChitin, Shield
    }
    
    public static class EquipScript {
        
        public static EquipType getRight() {
            
            switch (Player.m_localPlayer.GetRightItem()?.m_shared.m_name) {
                
                case "$item_fishingrod":
                    return EquipType.Fishing;

                case "$item_spear_flint":
                case "$item_spear_bronze":
                case "$item_spear_ancientbark":
                case "$item_spear_wolffang":
                    return EquipType.Spear;
                case "$item_spear_chitin":
                    return EquipType.SpearChitin;
            }

            return EquipType.None;
        }

        public static EquipType getLeft() {

            switch (Player.m_localPlayer.GetLeftItem()?.m_shared.m_itemType) {
                case ItemDrop.ItemData.ItemType.Bow:
                    return EquipType.Bow;

                case ItemDrop.ItemData.ItemType.Shield:
                    return EquipType.Shield;
            }
            
            return EquipType.None;
        }
    }
}