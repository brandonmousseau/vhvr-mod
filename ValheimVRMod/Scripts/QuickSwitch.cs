using System;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts {
    public class QuickSwitch : QuickAbstract {

        private int elementCount;
        private int extraElementCount;

        public static QuickSwitch instance;

        protected override void InitializeWrist()
        {
            currentHand = VRPlayer.leftHand;
            instance = this;
        }
        protected override int getElementCount() {
            return elementCount;
        }
        protected override int getExtraElementCount()
        {
            return extraElementCount;
        }
        public override void UpdateWristBar()
        {
            if (wrist.transform.parent != VRPlayer.leftHand.transform)
            {
                wrist.transform.SetParent(VRPlayer.leftHand.transform);
            }
            wrist.transform.localPosition = VHVRConfig.LeftWristQuickSwitchPos();
            wrist.transform.localRotation = VHVRConfig.LeftWristQuickSwitchRot();
            wrist.SetActive(isInView() || IsInArea());
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

                elements[elementCount].transform.GetChild(1).gameObject.SetActive(item.m_equiped || item.m_durability == 0);
                if (item.m_durability == 0)
                {
                    elements[elementCount].transform.GetChild(1).GetComponent<SpriteRenderer>().color = Color.red;
                }
                else
                {
                    elements[elementCount].transform.GetChild(1).GetComponent<SpriteRenderer>().color = Color.white;
                }

                if (item.GetIcon().name != elements[elementCount].Name)
                {
                    elements[elementCount].transform.GetChild(2).GetComponent<SpriteRenderer>().sprite = item.GetIcon();
                    ResizeIcon(elements[elementCount].gameObject);
                }

                elements[elementCount].num = i;
                elements[elementCount].Name = item.GetIcon().name;
                elementCount++;
            }


            //Extra
            if (VHVRConfig.QuickActionOnLeftHand())
            {
                RefreshQuickSwitch(extraElements, out extraElementCount);
            }
            else
            {
                RefreshQuickAction(inventory, extraElements, out extraElementCount);
            }


            reorderElements();
        }

        public override void selectHoveredItem() {

            var allElementCount = elementCount + extraElementCount;
            if (hoveredIndex < 0 || hoveredIndex >= allElementCount) {
                return;
            }
            var inventory = Player.m_localPlayer.GetInventory();

            if (VHVRConfig.QuickActionOnLeftHand())
            {
                if (SelectHoverQuickAction(hoveredIndex, allElementCount))
                {
                    return;
                }
            }
            else
            {
                if (SelectHoverQuickSwitch(hoveredIndex, elementCount, inventory))
                {
                    return;
                }
            }
            ItemDrop.ItemData item = inventory.GetItemAt(elements[hoveredIndex].num, 0);
            Player.m_localPlayer.UseItem(inventory, item, false);
        }
    }
}