using System;

namespace ValheimVRMod.Utilities {
    
    [Flags]
    public enum EquipType {
        None, 
        Fishing, Cultivator, Hammer, Hoe,
        Bow,  Spear, SpearChitin, ThrowObject,
        Shield, Tankard, Claws

        //mod
        , RuneSkyheim
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
                case "$item_tankard_anniversary":
                    return EquipType.Tankard;                
                case "$item_fistweapon_fenris":
                    return EquipType.Claws;


                //modded
                case "Rune of Frostbolt":
                case "Rune of Firebolt":
                case "Rune of Healing":
                case "Rune of Light":
                case "Rune of Force":
                case "Rune of Invigorate":
                case "Rune of Warmth":
                case "Rune of Recall":
                case "Rune of Travel":
                case "Rune of Blink":
                case "Rune of Frost Nova":
                case "Rune of Immolate":
                case "Rune of Glacial Spike":
                    return EquipType.RuneSkyheim;
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