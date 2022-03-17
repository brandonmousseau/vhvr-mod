using System;

namespace ValheimVRMod.Utilities {
    
    [Flags]
    public enum EquipType {
        None, 
        Fishing, Cultivator, Hammer, Hoe,
        Bow,  Spear, SpearChitin, ThrowObject,
        Shield, Tankard, Claws
    }
    
    public static class EquipScript {
        
        public static EquipType getRight() {
            
            switch (Player.m_localPlayer.GetRightItem()?.m_shared.m_name) {
                
                //tool
                case "$item_fishingrod":
                    return EquipType.Fishing;
                case "$item_cultivator":
                    return EquipType.Cultivator;
                case "$item_hammer":
                    return EquipType.Hammer;
                case "$item_hoe":
                    return EquipType.Hoe;

                case "$item_spear_flint":
                case "$item_spear_bronze":
                case "$item_spear_ancientbark":
                case "$item_spear_wolffang":
                    return EquipType.Spear;
                case "$item_spear_chitin":
                    return EquipType.SpearChitin;
                case "$item_oozebomb":
                    return EquipType.ThrowObject;
                case "$item_tankard":
                    return EquipType.Tankard;                
                case "$item_fistweapon_fenris":
                    return EquipType.Claws;
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