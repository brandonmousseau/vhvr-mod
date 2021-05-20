using System;

namespace ValheimVRMod.Utilities {
    
    [Flags]
    public enum EquipType {
        None, Bow, Fishing, Spear
    }
    
    public static class EquipScript {
        
        public static EquipType getType() {

            if (Player.m_localPlayer.GetLeftItem()?.m_shared?.m_itemType 
                == ItemDrop.ItemData.ItemType.Bow) {
                return EquipType.Bow;
            }
            
            switch (Player.m_localPlayer.GetRightItem()?.m_shared.m_name) {
                
                case "$item_fishingrod":
                    return EquipType.Fishing;

                case "$item_spear_flint":
                case "$item_spear_bronze":
                case "$item_spear_ancientbark":
                case "$item_spear_wolffang":
                //case "$item_spear_chitin":  <== special case
                    return EquipType.Spear;
            }

            return EquipType.None;
        }
    }
}