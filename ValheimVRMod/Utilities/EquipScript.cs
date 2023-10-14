using System;
using System.Collections.Generic;
using ValheimVRMod.Scripts;

namespace ValheimVRMod.Utilities
{

    [Flags]
    public enum EquipType
    {
        None,
        Fishing, Cultivator, Hammer, Hoe,
        Bow, Spear, SpearChitin, ThrowObject,
        Shield, Tankard, Claws, Magic, Crossbow
        ,
        //Melee Weapon
        Sword, Axe, Knife, Pickaxe, Club, Polearms, DualKnives
        ,
        //utility
        Lantern
        ,
        //Modded
        RuneSkyheim
    }

    public static class EquipScript
    {

        public readonly static HashSet<ItemDrop.ItemData.ItemType> DominantHandItemTypes =
            new HashSet<ItemDrop.ItemData.ItemType>(
                new ItemDrop.ItemData.ItemType[]{
                    ItemDrop.ItemData.ItemType.Tool,
                    ItemDrop.ItemData.ItemType.Torch,
                    ItemDrop.ItemData.ItemType.OneHandedWeapon,
                    ItemDrop.ItemData.ItemType.TwoHandedWeapon});

        public static bool IsDominantHandItem(ItemDrop.ItemData item)
        {
            return DominantHandItemTypes.Contains(item.m_shared.m_itemType);
        }

        public static EquipType getRight()
        {
            if (Player.m_localPlayer.GetRightItem() != null)
            {
                return getRightEquipType(Player.m_localPlayer.GetRightItem());
            }
            return EquipType.None;
        }

        public static EquipType getLeft()
        {
            if (Player.m_localPlayer.GetLeftItem() != null)
            {
                return getLeftEquipType(Player.m_localPlayer.GetLeftItem());
            }
            return EquipType.None;
        }

        public static EquipType getEquippedItem(ItemDrop.ItemData item)
        {
            var equip = getRightEquipType(item);
            if (equip == EquipType.None)
                equip = getLeftEquipType(item);
            return equip;
        }

        public static EquipType getRightEquipType(ItemDrop.ItemData item)
        {
            //Right Equipment List
            switch (item?.m_shared.m_name)
            {

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
                case "$item_spear_carapace":
                    return EquipType.Spear;
                case "$item_spear_chitin":
                    return EquipType.SpearChitin;
                case "$item_oozebomb":
                case "$item_bilebomb":
                    return EquipType.ThrowObject;
                case "$item_tankard":
                case "$item_tankard_dvergr":
                case "$item_tankard_anniversary":
                case "$item_tankard_odin":
                    return EquipType.Tankard;
                case "$item_fistweapon_fenris":
                    return EquipType.Claws;
                case "$item_lantern":
                    return EquipType.Lantern;
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
                case "Rune of Immolation":
                case "Rune of Chain Lightning":
                case "Rune of Glacial Spike":
                    return EquipType.RuneSkyheim;
            }
            //compatibility setting 
            var skillType = item?.m_shared.m_skillType;
            switch (skillType)
            {
                //Right Equipment
                case Skills.SkillType.Unarmed:
                    return EquipType.Claws;
                case Skills.SkillType.Spears:
                    return EquipType.Spear;
                case Skills.SkillType.BloodMagic:
                case Skills.SkillType.ElementalMagic:
                    return EquipType.Magic;
                case Skills.SkillType.Swords:
                    return EquipType.Sword;
                case Skills.SkillType.Axes:
                    return EquipType.Axe;
                case Skills.SkillType.Pickaxes:
                    return EquipType.Pickaxe;
                case Skills.SkillType.Clubs:
                    return EquipType.Club;
                case Skills.SkillType.Polearms:
                    return EquipType.Polearms;
            }

            var attackAnim = item?.m_shared.m_attack.m_attackAnimation;
            switch (attackAnim)
            {
                case "unarmed_attack":
                    return EquipType.Claws;
                case "throw_bomb":
                    return EquipType.ThrowObject;
                case "dual_knives":
                    return EquipType.DualKnives;
                case "knife_stab":
                    return EquipType.Knife;
                case "swing_hammer":
                    return EquipType.Hammer;
                case "emote_drink":
                    return EquipType.Tankard;
            }

            return EquipType.None;
        }
        public static EquipType getLeftEquipType(ItemDrop.ItemData item)
        {


            //LeftEquipment List 
            switch (item?.m_shared.m_itemType)
            {
                case ItemDrop.ItemData.ItemType.Bow:

                    if (item?.m_shared.m_ammoType == "$ammo_bolts")
                        return EquipType.Crossbow;

                    return EquipType.Bow;
                case ItemDrop.ItemData.ItemType.Shield:
                    return EquipType.Shield;
            }

            //compatibility setting 
            var skillType = item?.m_shared.m_skillType;
            //var itemType = item?.m_shared.m_itemType;
            switch (skillType)
            {
                case Skills.SkillType.BloodMagic:
                case Skills.SkillType.ElementalMagic:
                    return EquipType.Magic;

                //Left Equipments
                case Skills.SkillType.Bows:
                    return EquipType.Bow;
                case Skills.SkillType.Crossbows:
                    return EquipType.Crossbow;
            }

            switch (item?.m_shared.m_name)
            {
                case "$item_lantern":
                    return EquipType.Lantern;
            }

            return EquipType.None;
        }

        public static ItemDrop.ItemData equipAmmo()
        {
            if (getLeft() != EquipType.Bow && getLeft() != EquipType.Crossbow)
            {
                LogUtils.LogWarning("Attempting to equip ammo without bow or crossbow equipped");
                return null;
            }
                
            ItemDrop.ItemData weapon = Player.m_localPlayer.GetLeftItem();
            if (Attack.HaveAmmo(Player.m_localPlayer, weapon))
            {
                Attack.EquipAmmoItem(Player.m_localPlayer, weapon);
                return Player.m_localPlayer.GetAmmoItem();
            }

            return null;
        }

        public static bool isThrowable(ItemDrop.ItemData item)
        {
            if (item != null)
            {
                string getValue = "";
                if (item.m_customData.TryGetValue("randyknapp.mods.epicloot#EpicLoot.MagicItemComponent", out getValue))
                {
                    return getValue.Contains("Throwable");
                }
                return item.m_crafterName.Contains("\"EffectType\":\"Throwable\"");
            }
            return false;
        }

        public static bool isSpearEquipped()
        {
            return getRight() == EquipType.Spear || getRight() == EquipType.SpearChitin;
        }

        // Whether there is a spear equipped pointing at the ulnar direction of hold hand.
        public static bool isSpearEquippedUlnarForward()
        {
            return isSpearEquipped() && !VHVRConfig.SpearInverseWield();
        }

        // Whether there is a spear equipped pointing at the radial direction of hold hand.
        public static bool isSpearEquippedRadialForward()
        {
            return isSpearEquipped() && VHVRConfig.SpearInverseWield();
        }

        public static bool isTwoHandedAxeEquiped()
        {
            return getRight() == EquipType.Axe && Player.m_localPlayer.GetRightItem().m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon;
        }

        public static bool isTwoHandedClubEquiped()
        {
            return getRight() == EquipType.Club && Player.m_localPlayer.GetRightItem().m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon;
        }

        public static bool shouldSkipAttackAnimation()
        {
            if (getLeft() == EquipType.Magic || getRight() == EquipType.Magic) 
            {
                return MagicWeaponManager.ShouldSkipAttackAnimation();
            }
            return getLeft() != EquipType.Crossbow;
        }
    }
}
