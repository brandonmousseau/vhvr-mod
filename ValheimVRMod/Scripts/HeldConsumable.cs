using UnityEngine;
using ValheimVRMod.Patches;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts
{
    // Holds a food or mead in a hand instead of consuming it right away, and consumes it when it is brought to the
    // mouth. The food is shown with the same hand item visual that vanilla shows for a moment after eating.
    //
    // It goes into the off hand if only that one is free, and into the main hand otherwise, holstering what is in it.
    public class HeldConsumable : MonoBehaviour
    {
        // Where the mouth is relative to the headset.
        private static readonly Vector3 MOUTH_OFFSET = new Vector3(0, -0.08f, 0.04f);
        // How close to the mouth the controller holding the food has to get. Generous, since the headset and the
        // controller keep the hand from actually reaching the mouth.
        private const float MOUTH_RANGE = 0.16f;

        private static Inventory inventory;
        private static ItemDrop.ItemData item;
        private static bool inOffHand;
        // Whether the main hand item was holstered to make room for the food.
        private static bool hidMainHandItem;
        // The food has to be away from the mouth once before it can be eaten, so that it is not eaten right away when
        // it is picked with the hand near the face.
        private static bool armed;

        // Whether the consumption in progress is one that is to happen right away rather than hold the food.
        public static bool isConsuming { get; private set; }

        // Consumes an item the vanilla way, without holding it first.
        public static void ConsumeRightAway(Inventory foodInventory, ItemDrop.ItemData food)
        {
            isConsuming = true;
            try
            {
                Player.m_localPlayer.UseItem(foodInventory, food, fromInventoryGui: true);
            }
            finally
            {
                isConsuming = false;
            }
        }

        // Equipping anything into the hand of the food replaces the food, even before Update() notices.
        public static bool isHolding { get { return item != null && Player.m_localPlayer != null && GetEquippedItem(Player.m_localPlayer) == null; } }
        public static bool isHoldingInMainHand { get { return isHolding && !inOffHand; } }
        public static bool isHoldingInOffHand { get { return isHolding && inOffHand; } }
        public static bool isInRightHand { get { return inOffHand != VRPlayer.isRightHandMainWeaponHand; } }

        public static bool IsHolding(ItemDrop.ItemData other)
        {
            return isHolding && item == other;
        }

        public static void Hold(Inventory foodInventory, ItemDrop.ItemData food)
        {
            var player = Player.m_localPlayer;
            bool useOffHand;
            bool hadMainHandItem = false;
            if (isHolding)
            {
                // Replaces the food in the same hand, and the item holstered for that one is still the one to bring
                // back.
                useOffHand = inOffHand;
                hadMainHandItem = hidMainHandItem;
                SetVisual(player, 0, 0);
                Clear();
            }
            else
            {
                Clear();
                useOffHand = player.m_rightItem != null && player.m_leftItem == null;
                if (!useOffHand && player.m_rightItem != null)
                {
                    hadMainHandItem = true;
                    PatchHideHandItems.HideLocalPlayerHandItem(isMainHandItem: true);
                }
            }

            inventory = foodInventory;
            item = food;
            inOffHand = useOffHand;
            hidMainHandItem = hadMainHandItem;
            armed = false;
            SetVisual(player, food.m_dropPrefab.name.GetStableHashCode(), food.m_quality);
            LogUtils.LogDebug("Holding food: " + food.m_shared.m_name + (inOffHand ? " (off hand)" : " (main hand)"));
        }

        public static void PutAway(bool restoreWeapon)
        {
            if (item == null)
            {
                return;
            }

            bool shouldRestoreWeapon = restoreWeapon && hidMainHandItem;
            var player = Player.m_localPlayer;
            LogUtils.LogDebug("Putting away held food: " + item.m_shared.m_name);
            if (player != null && GetEquippedItem(player) == null)
            {
                // Otherwise an item has taken the place of the food already.
                SetVisual(player, 0, 0);
            }
            Clear();
            if (player != null && shouldRestoreWeapon && player.m_rightItem == null)
            {
                PatchShowHandItems.ShowLocalPlayerHandItem(isMainHandItem: true);
            }
        }

        // The item that is equipped in the hand of the food, which there is none of as long as the food is held.
        private static ItemDrop.ItemData GetEquippedItem(Player player)
        {
            return inOffHand ? player.m_leftItem : player.m_rightItem;
        }

        // Shows an item in the hand of the food without equipping it, like VisEquipment.SetRightItemVisual(), which
        // has no counterpart for the left item.
        private static void SetVisual(Player player, int itemHash, int quality)
        {
            if (!inOffHand)
            {
                player.m_visEquipment.SetRightItemVisual(itemHash, quality);
                return;
            }

            ZNetView nview = player.m_nview;
            if (nview != null && nview.GetZDO() != null && nview.IsOwner())
            {
                nview.GetZDO().Set(ZDOVars.s_leftItem, itemHash);
                nview.GetZDO().Set(ZDOVars.s_leftItemVariant, 0);
                nview.GetZDO().Set(ZDOVars.s_leftItemQuality, quality);
            }
        }

        private static void Clear()
        {
            inventory = null;
            item = null;
            inOffHand = false;
            hidMainHandItem = false;
            armed = false;
        }

        private void Update()
        {
            if (item == null)
            {
                return;
            }

            var player = Player.m_localPlayer;
            if (player == null || GetEquippedItem(player) != null)
            {
                Clear();
                return;
            }

            if (player.IsDead() || !VHVRConfig.UseVrControls())
            {
                PutAway(restoreWeapon: false);
                return;
            }

            if (inventory == null || !inventory.ContainsItem(item))
            {
                PutAway(restoreWeapon: true);
                return;
            }

            if (VRPlayer.vrCam == null)
            {
                return;
            }

            var hand = isInRightHand ? VRPlayer.rightHand : VRPlayer.leftHand;
            if (!IsInMouthRange(hand.transform.position, VRPlayer.vrCam.transform))
            {
                armed = true;
                return;
            }

            if (armed)
            {
                Consume(player);
            }
        }

        private static bool IsInMouthRange(Vector3 position, Transform head)
        {
            return Vector3.Distance(position, head.TransformPoint(MOUTH_OFFSET)) < MOUTH_RANGE;
        }

        private static void Consume(Player player)
        {
            bool consumed;
            isConsuming = true;
            try
            {
                consumed = player.ConsumeItem(inventory, item, checkWorldLevel: true);
            }
            finally
            {
                isConsuming = false;
            }

            LogUtils.LogDebug("Held food brought to mouth, consumed: " + consumed);
            if (!consumed)
            {
                // E. g. when full, which the player has been told by now. The food stays in the hand and has to be
                // taken away from the mouth before trying again.
                armed = false;
                return;
            }

            // What Humanoid.UseItem() does after consuming an item.
            player.m_consumeItemEffects.Create(player.transform.position, Quaternion.identity, null, 1f, -1, player.GetZDOID());
            player.m_zanim.SetTrigger("eat");

            PutAway(restoreWeapon: true);
        }
    }
}
