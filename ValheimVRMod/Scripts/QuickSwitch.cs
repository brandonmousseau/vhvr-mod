using System;
using UnityEngine;
using Valve.VR;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts {
    public class QuickSwitch : MonoBehaviour {

        private static int SLOTS = 8;
        private static float elementDistance = 0.1f;
        
        private static Color standard = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        private static Color hovered = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        private static Color selected = new Color(0.34375f, 0.5859375f, 0.796875f, 0.5f);

        private GameObject hoveredItem;
        private int hoveredItemIndex = -1;
        private static GameObject[] equippedLayers = new GameObject[SLOTS]; 
        private static GameObject[] items = new GameObject[SLOTS]; 
        private Vector2[] positions = new Vector2[SLOTS];
    
        private void Awake() {
            initialize();
            refreshItems();
        }

        /**
         * create:
         * - standart layer (gray)
         * - disabled equipped layers (blue)
         * - items, without sprite yet
         */
        private void initialize() {
            
            var tex_standard = new Texture2D(1, 1);
            tex_standard.SetPixel(0,0, standard);
            tex_standard.Apply();
        
            var tex_hovered = new Texture2D(1, 1);
            tex_hovered.SetPixel(0,0, hovered);
            tex_hovered.Apply();
        
            var tex_selected = new Texture2D(1, 1);
            tex_selected.SetPixel(0,0, selected);
            tex_selected.Apply();

            for (int i = 0; i < SLOTS; i++) {

                double a = (i * 45 * (Math.PI)) / 180;
                double x = Math.Cos(a) * elementDistance;
                double y = Math.Sin(a) * elementDistance;
                positions[i] = new Vector2((float)x, (float)y);

                GameObject standardLayer = new GameObject();
                standardLayer.layer = LayerUtils.getWorldspaceUiLayer();
                standardLayer.transform.SetParent(transform, false);
                standardLayer.transform.localPosition = positions[i];
                standardLayer.transform.localScale *= 4;
                var standardRenderer = standardLayer.AddComponent<SpriteRenderer>();
                standardRenderer.sprite = Sprite.Create(tex_standard, new Rect(0.0f, 0.0f, tex_standard.width, tex_standard.height), new Vector2(0.5f, 0.5f));
                standardRenderer.sortingOrder = 0;
                
                GameObject equipedLayer = new GameObject();
                equipedLayer.layer = LayerUtils.getWorldspaceUiLayer();
                equipedLayer.transform.SetParent(transform, false);
                equipedLayer.transform.localPosition = positions[i];
                equipedLayer.transform.localScale *= 4;
                var equipedRenderer = equipedLayer.AddComponent<SpriteRenderer>();
                equipedRenderer.sprite = Sprite.Create(tex_selected, new Rect(0.0f, 0.0f, tex_selected.width, tex_selected.height), new Vector2(0.5f, 0.5f));
                equipedRenderer.sortingOrder = 2;
                equipedLayer.SetActive(false);
                equippedLayers[i] = equipedLayer;
                
                GameObject item = new GameObject();
                item.layer = LayerUtils.getWorldspaceUiLayer();
                item.transform.SetParent(transform, false);
                item.transform.localPosition = positions[i];
                item.transform.localScale /= 15;
                var renderer = item.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = 3;
                items[i] = item;

            }
            
            hoveredItem = new GameObject();
            hoveredItem.layer = LayerUtils.getWorldspaceUiLayer();
            hoveredItem.transform.SetParent(transform, false);
            hoveredItem.transform.localScale *= 4;
            var hoveredItemRenderer = hoveredItem.AddComponent<SpriteRenderer>();
            hoveredItemRenderer.sprite = Sprite.Create(tex_hovered, new Rect(0.0f, 0.0f, tex_hovered.width, tex_hovered.height), new Vector2(0.5f, 0.5f));
            hoveredItemRenderer.sortingOrder = 1;
            hoveredItem.SetActive(false);


        }

        /**
         * loop the inventory hotbar and set corresponding item icons + activate equipped layers
         */
        public static void refreshItems() {

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

        private void Update() {
            hoverItem();
        }

        private void hoverItem() {

            float y = SteamVR_Actions.valheim_PitchAndYaw.axis.y;
            float x = SteamVR_Actions.valheim_PitchAndYaw.axis.x;

            if (Math.Abs(x) < 0.5f && Math.Abs(y) < 0.5f) {
                hoveredItem.SetActive(false);
                hoveredItemIndex = -1;
                return;
            }

            if (!hoveredItem.activeSelf) {
                hoveredItem.SetActive(true);   
            }
            
            hoveredItemIndex = (int) Math.Round(Math.Atan2(y, x) * 180 / Math.PI / 45 + SLOTS) % SLOTS;
            hoveredItem.transform.localPosition = positions[hoveredItemIndex];

        }

        public void selectHoveredItem() {

            if (hoveredItemIndex < 0) {
                return;
            }

            var inventory = Player.m_localPlayer.GetInventory();
            ItemDrop.ItemData item = inventory.GetItemAt(hoveredItemIndex, 0);
            Player.m_localPlayer.UseItem(inventory, item, true);
            
        }
    }
}