using static ValheimVRMod.Utilities.LogUtils;
using HarmonyLib;


namespace ValheimVRMod.Patches {

    
    /*[HarmonyPatch(typeof(EffectArea), "OnTriggerStay")]
    class DebugEffectArea {
        
        static void Postfix()
        {
            LogDebug("TRIGGERED: EffectArea/OnTriggerStay");
        }
    } 
    
    [HarmonyPatch(typeof(Aoe), "OnTriggerEnter")]
    class DebugAoePatches {
        
        static void Postfix()
        {
            LogDebug("TRIGGERED: Aoe/OnTriggerEnter");
        }
    }
    
    [HarmonyPatch(typeof(TriggerTracker), "OnTriggerEnter")]
    class DebugTriggerTrackerPatches {
        
        static void Postfix()
        {
            LogDebug("TRIGGERED: TriggerTracker/OnTriggerEnter");
        }
    }
    
    [HarmonyPatch(typeof(TreeLog), "Damage")]
    class DebugTreeLogDamage {
        
        static void Postfix()
        {
            LogDebug("TRIGGERED: TreeLog/Damage");
        }
    }
    
    [HarmonyPatch(typeof(TreeLog), "RPC_Damage")]
    class DebugTreeLogRpcDamage {
        
        static void Postfix()
        {
            LogDebug("TRIGGERED: TreeLog/RPC_Damage");
        }
    }
    
    [HarmonyPatch(typeof(TreeBase), "Damage")]
    class DebugTreeBaseDamage {
        
        static void Postfix()
        {
            LogDebug("TRIGGERED: TreeBase/Damage");
        }
    }

    [HarmonyPatch(typeof(TreeBase), "RPC_Damage")]
    class DebugTreeBaseRpcDamage
    {

        static void Postfix()
        {
            LogDebug("TRIGGERED: TreeBase/RPC_Damage");
        }
    }

    [HarmonyPatch(typeof(TreeBase), "Awake")]
    class DebugTreeBaseAwake {
        
        static void Postfix()
        {
            LogDebug("TRIGGERED: TreeBase/RPC_Damage");
        }
    }*/
    
    
    [HarmonyPatch(typeof(Humanoid), "EquipItem")]
    class HumanoidEquipItem {
        
        static void Postfix(ref Humanoid __instance, ref bool __result, ItemDrop.ItemData item, bool triggerEquipEffects = true)
        {
            
            LogDebug("EQUIP_DEBUG: NAME: " + item.m_shared.m_name);
    
            if (!__result) {
                LogDebug("EQUIP_DEBUG: EQUIP FAILED");
                return;
            }

            //
            //
            // //GameObject component = UnityEngine.Object.Instantiate<GameObject>(item.m_dropPrefab);
            //
            //
            //
            //
            // if (component == null)
            // {
            //     LogDebug("FUCK 1 !!!!!!!!!!!!!");
            // }
            // else if(VRPlayer.rightHand == null)
            // {
            //     LogDebug("FUCK 2 !!!!!!!!!!!!!");
            // } else
            // {
            //     Object.Destroy(component.GetComponent<Rigidbody>());
            //     component.transform.SetParent(VRPlayer.rightHand.transform);
            //     component.transform.position = Vector3.zero;
            // }

            if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Tool)
            {
                
                LogDebug("EQUIP_DEBUG: ITS A TOOL");
                
             


                //this.UnequipItem(this.m_rightItem, triggerEquipEffects);
                //this.UnequipItem(this.m_leftItem, triggerEquipEffects);
                //this.m_rightItem = item;
                //this.m_hiddenRightItem = (ItemDrop.ItemData) null;
                //this.m_hiddenLeftItem = (ItemDrop.ItemData) null;
            }
            else if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch)
            {
                LogDebug("EQUIP_DEBUG: ITS A TORCH");
                // if (this.m_rightItem != null && this.m_leftItem == null && this.m_rightItem.m_shared.m_itemType ==
                //     ItemDrop.ItemData.ItemType.OneHandedWeapon)
                // {
                //     this.m_leftItem = item;
                // }
                // else
                // {
                //     this.UnequipItem(this.m_rightItem, triggerEquipEffects);
                //     if (this.m_leftItem != null &&
                //         this.m_leftItem.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Shield)
                //         this.UnequipItem(this.m_leftItem, triggerEquipEffects);
                //     this.m_rightItem = item;
                // }
                //
                // this.m_hiddenRightItem = (ItemDrop.ItemData) null;
                // this.m_hiddenLeftItem = (ItemDrop.ItemData) null;
            }
            else if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon)
            {
                
                LogDebug("EQUIP_DEBUG: ONE HANDED");

                // if (this.m_rightItem != null &&
                //     this.m_rightItem.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch && this.m_leftItem == null)
                // {
                //     ItemDrop.ItemData rightItem = this.m_rightItem;
                //     this.UnequipItem(this.m_rightItem, triggerEquipEffects);
                //     this.m_leftItem = rightItem;
                //     this.m_leftItem.m_equiped = true;
                // }
                //
                // this.UnequipItem(this.m_rightItem, triggerEquipEffects);
                // if (this.m_leftItem != null &&
                //     this.m_leftItem.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Shield &&
                //     this.m_leftItem.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Torch)
                //     this.UnequipItem(this.m_leftItem, triggerEquipEffects);
                // this.m_rightItem = item;
                // this.m_hiddenRightItem = (ItemDrop.ItemData) null;
                // this.m_hiddenLeftItem = (ItemDrop.ItemData) null;
            }
            else if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield)
            {
                
                LogDebug("EQUIP_DEBUG: SHIELD");
                
                // this.UnequipItem(this.m_leftItem, triggerEquipEffects);
                // if (this.m_rightItem != null &&
                //     this.m_rightItem.m_shared.m_itemType != ItemDrop.ItemData.ItemType.OneHandedWeapon &&
                //     this.m_rightItem.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Torch)
                //     this.UnequipItem(this.m_rightItem, triggerEquipEffects);
                // this.m_leftItem = item;
                // this.m_hiddenRightItem = (ItemDrop.ItemData) null;
                // this.m_hiddenLeftItem = (ItemDrop.ItemData) null;
            }
            else if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow)
            {
                LogDebug("EQUIP_DEBUG: BOW");
                
                // this.UnequipItem(this.m_leftItem, triggerEquipEffects);
                // this.UnequipItem(this.m_rightItem, triggerEquipEffects);
                // this.m_leftItem = item;
                // this.m_hiddenRightItem = (ItemDrop.ItemData) null;
                // this.m_hiddenLeftItem = (ItemDrop.ItemData) null;
            }
            else if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon)
            {
                
                LogDebug("EQUIP_DEBUG: TWOHANDED");
                
                // this.UnequipItem(this.m_leftItem, triggerEquipEffects);
                // this.UnequipItem(this.m_rightItem, triggerEquipEffects);
                // this.m_rightItem = item;
                // this.m_hiddenRightItem = (ItemDrop.ItemData) null;
                // this.m_hiddenLeftItem = (ItemDrop.ItemData) null;
            }
            else if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Chest)
            {
                LogDebug("EQUIP_DEBUG: CHEST");
                // this.UnequipItem(this.m_chestItem, triggerEquipEffects);
                // this.m_chestItem = item;
            }
            else if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Legs)
            {
                LogDebug("EQUIP_DEBUG: LEGS");
                // this.UnequipItem(this.m_legItem, triggerEquipEffects);
                // this.m_legItem = item;
            }
            else if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo)
            {
                LogDebug("EQUIP_DEBUG: AMMO");
                // this.UnequipItem(this.m_ammoItem, triggerEquipEffects);
                // this.m_ammoItem = item;
            }
            else if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Helmet)
            {
                LogDebug("EQUIP_DEBUG: HELMET");
                // this.UnequipItem(this.m_helmetItem, triggerEquipEffects);
                // this.m_helmetItem = item;
            }
            else if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shoulder)
            {
                LogDebug("EQUIP_DEBUG: SHOULDER");
                // this.UnequipItem(this.m_shoulderItem, triggerEquipEffects);
                // this.m_shoulderItem = item;
            }
            else if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Utility)
            {
                LogDebug("EQUIP_DEBUG: UTILITY");
                // this.UnequipItem(this.m_utilityItem, triggerEquipEffects);
                // this.m_utilityItem = item;
            }
            
            // if (this.IsItemEquiped(item))
            //     item.m_equiped = true;
            // this.SetupEquipment();
            // if (triggerEquipEffects)
            //     this.TriggerEquipEffect(item);
            // return true;
        }
    }
}
