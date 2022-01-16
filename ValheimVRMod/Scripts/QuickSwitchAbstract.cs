using System;
using BepInEx.Configuration;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts {
    public abstract class QuickAbstract : MonoBehaviour {
        
        private const float DIST_CIRCLE_1 = 0.08f;
        private const float DIST_CIRCLE_2 = 0.13f;
        private const float DIST_CIRCLE_3 = 0.18f;
        private const int MAX_ELEMENTS = 34;
        private const int INVENTORY_COUNT_X = 8;
        private const int INVENTORY_COUNT_Y = 4;

        private static readonly Color COLOR_STANDARD = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        private static readonly Color COLOR_HOVERED = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        private static readonly Color COLOR_EQUIPED = new Color(0.34375f, 0.5859375f, 0.796875f, 0.5f);

        protected int elementCount;
        protected GameObject hoveredItem;
        protected int hoveredIndex = -1;
        protected GameObject[] elements;
        protected int layer;
        
        private GameObject sphere;
        private bool handsSwitched;

        public Transform parent;
        private Vector3 offset;
        private Texture2D circleTexture;

        public QuickAbstract()
        {
            circleTexture = VRAssetManager.GetAsset<Texture2D>("circle");  
        }
        
        private void Awake() {
            
            elements = new GameObject[MAX_ELEMENTS];
            initialize();
            refreshItems();
            createSphere();
        }

        private void OnEnable()
        {
            
            transform.SetParent(parent, false);
            transform.localPosition = Vector3.zero;
            
            if (VHVRConfig.getQuickMenuFollowCam()) {
                //Camera Version
                Camera vrCam = CameraUtils.getCamera(CameraUtils.VR_CAMERA);
                transform.LookAt(vrCam.transform.position);
            }
            else {
                //Hand Version
                transform.localRotation = Quaternion.Euler(VHVRConfig.getQuickMenuAngle(), 0, -5);
            }
            
            transform.SetParent(Player.m_localPlayer.transform);
            transform.parent = null;
            offset = transform.position - Player.m_localPlayer.transform.position ;
        }
        private void Update() {
            transform.position = offset + Player.m_localPlayer.transform.position;
            sphere.transform.position = parent.position;
            hoverItem();
            checkCircle();
        }

        private void checkCircle()
        {
            var hand = GetType() == typeof(QuickSwitch) ? VRPlayer.rightHand : VRPlayer.leftHand;

            if (Vector3.Distance(hand.transform.position, transform.position) < DIST_CIRCLE_1 + 0.025f) {
                if (layer != 0) {
                    layer = 0;
                    refreshItems();   
                }
                return;
            }
            
            if (Vector3.Distance(hand.transform.position, transform.position) < DIST_CIRCLE_2 + 0.025f) {
                if (layer != 1) {
                    layer = 1;
                    refreshItems();   
                }
                return;
            }
            
            if (layer != 2) {
                layer = 2;
                refreshItems();   
            }
        }
        
        public abstract int refreshHandSpecific();
        public abstract bool selectHandSpecific();

        private void createSphere() {
            sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.SetParent(transform);
            sphere.transform.localScale *= 0.02f;
            sphere.layer = LayerUtils.getWorldspaceUiLayer();
            sphere.GetComponent<MeshRenderer>().material.color = Color.red;
            Destroy(sphere.GetComponent<Collider>());
        }
        
        /**
         * create:
         * - standart layer (gray)
         * - disabled equipped layers (blue)
         * - items, without sprite yet
         */
        private void initialize() {

            for (int i = 0; i < MAX_ELEMENTS; i++) {

                elements[i] = new GameObject();
                elements[i].transform.SetParent(transform, false);
                
                GameObject standardLayer = new GameObject();
                standardLayer.layer = LayerUtils.getWorldspaceUiLayer();
                standardLayer.transform.SetParent(elements[i].transform, false);
                standardLayer.transform.localScale /= 8;
                var standardRenderer = standardLayer.AddComponent<SpriteRenderer>();
                standardRenderer.sprite =  Sprite.Create(circleTexture,
                    new Rect(0.0f, 0.0f, circleTexture.width, circleTexture.height),
                    new Vector2(0.5f, 0.5f), 500);
                standardRenderer.color = COLOR_STANDARD;
                standardRenderer.sortingOrder = 0;

                GameObject equipedLayer = new GameObject();
                equipedLayer.layer = LayerUtils.getWorldspaceUiLayer();
                equipedLayer.transform.SetParent(elements[i].transform, false);
                equipedLayer.transform.localScale /= 8;
                var equipedRenderer = equipedLayer.AddComponent<SpriteRenderer>();
                equipedRenderer.sprite =  Sprite.Create(circleTexture,
                    new Rect(0.0f, 0.0f, circleTexture.width, circleTexture.height),
                    new Vector2(0.5f, 0.5f), 500);
                equipedRenderer.color = COLOR_EQUIPED;
                equipedRenderer.sortingOrder = 2;
                equipedLayer.SetActive(false);

                GameObject item = new GameObject();
                item.layer = LayerUtils.getWorldspaceUiLayer();
                item.transform.SetParent(elements[i].transform, false);
                item.transform.localScale /= 20;
                var renderer = item.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = 3;

            }
            
            hoveredItem = new GameObject();
            hoveredItem.layer = LayerUtils.getWorldspaceUiLayer();
            hoveredItem.transform.SetParent(transform, false);
            hoveredItem.transform.localScale /= 8;
            var hoveredItemRenderer = hoveredItem.AddComponent<SpriteRenderer>();
            hoveredItemRenderer.sprite =  Sprite.Create(circleTexture,
                new Rect(0.0f, 0.0f, circleTexture.width, circleTexture.height),
                new Vector2(0.5f, 0.5f), 500);
            hoveredItemRenderer.color = COLOR_HOVERED;
            hoveredItemRenderer.sortingOrder = 1;
            hoveredItem.SetActive(false);
            
        }
        
        /**
         * loop the inventory and set corresponding item icons + activate equipped layers
         */
        public void refreshItems() {
            
            if (Player.m_localPlayer == null) {
                return;
            }
            
            elementCount = 0;
            
            var extraElements = refreshHandSpecific();
            var inventory = Player.m_localPlayer.GetInventory();
            
            for (int y = 0; y < INVENTORY_COUNT_Y; y++) {
                for (int x = 0; x < INVENTORY_COUNT_X; x++) {

                    ItemDrop.ItemData item = inventory?.GetItemAt(x, y);

                    if (item == null) {
                        continue;
                    }
                    
                    elements[elementCount].transform.GetChild(0).gameObject
                        .SetActive(item.m_equiped && isCurrentHandHoldingItem(item) || item.m_durability == 0);

                    elements[elementCount].transform.GetChild(0).GetComponent<SpriteRenderer>().color =
                        item.m_durability == 0 ? Color.red : COLOR_EQUIPED;
                    elements[elementCount].transform.GetChild(2).GetComponent<SpriteRenderer>().sprite =
                    item.GetIcon();
                    elements[elementCount].name = (x + y * INVENTORY_COUNT_X).ToString();
                    elementCount++;
                }
            }

            reorderElements(extraElements);
        }

        private bool isCurrentHandHoldingItem(ItemDrop.ItemData item)
        {
            switch (item.m_shared.m_itemType) {
                case ItemDrop.ItemData.ItemType.Tool:
                case ItemDrop.ItemData.ItemType.Torch:
                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeapon:

                    return VHVRConfig.LeftHanded() && GetType() == typeof(QuickSwitchLeft)
                        || !VHVRConfig.LeftHanded() && GetType() == typeof(QuickSwitch);
                
                case ItemDrop.ItemData.ItemType.Shield:
                case ItemDrop.ItemData.ItemType.Bow:
                    
                    return VHVRConfig.LeftHanded() && GetType() == typeof(QuickSwitch)
                        || !VHVRConfig.LeftHanded() && GetType() == typeof(QuickSwitchLeft);
                default:
                    return false;
            }
        }

        private void reorderElements(int extraElements) {
            var curDist = DIST_CIRCLE_1;
            var curElementCount = Math.Min(elementCount, 8 + extraElements);
            
            for (int i = 0, j = 0; i < MAX_ELEMENTS; i++, j++) {

                if (i == 8 + extraElements) {
                    curDist = DIST_CIRCLE_2;
                    curElementCount = Math.Min(elementCount - j, 10);
                    j = 0;
                }
                
                if (i == 18 + extraElements) {
                    curDist = DIST_CIRCLE_3;
                    curElementCount = elementCount - 18 - extraElements;
                    j = 0;
                }
                
                double a = j * 2 * Math.PI / curElementCount;
                double x = Math.Cos(a) * curDist;
                double y = Math.Sin(a) * curDist;

                elements[i].transform.localPosition = new Vector2((float)x, (float)y);;
                elements[i].SetActive(i < elementCount &&
                    (layer != 0 || i < 8 + extraElements) &&
                    (layer != 1 || i < 18 + extraElements)
                );
            }
        }
        
        private void hoverItem() {

            float maxDist = 0.05f;
            hoveredIndex = -1;
            
            for (int i = 0; i < elementCount; i++) {
                var dist = Vector3.Distance(parent.position, elements[i].transform.position);

                if (dist < maxDist) {
                    maxDist = dist;
                    hoveredIndex = i;
                }
            }

            var hovering = hoveredIndex >= 0;
            if (hovering) {
                hoveredItem.transform.position = elements[hoveredIndex].transform.position;    
            }
            hoveredItem.SetActive(hovering);
        }
        
        public void selectHoveredItem() {

            if (hoveredIndex < 0) {
                return;
            }
            
            if (selectHandSpecific()) {
                return;
            }

            var inventory = Player.m_localPlayer.GetInventory();
            var index = Int32.Parse(elements[hoveredIndex].name);
            ItemDrop.ItemData item = inventory.GetItemAt(index % 8, index / 8);
            var itemType = item.m_shared.m_itemType;
            ItemDrop.ItemData otherItem = null;
            
            // if (item.m_equiped && !isCurrentHandHoldingItem(item)) {
            //     
            //     
            //     
            //     //Player.m_localPlayer.UnequipItem(item, false);
            // }

            handsSwitched = false;
            
            switch (itemType) {
                case ItemDrop.ItemData.ItemType.Tool:
                case ItemDrop.ItemData.ItemType.Torch:
                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                    if (VHVRConfig.LeftHanded() && GetType() == typeof(QuickSwitch)) {
                        switchHand(false);
                    }
                    if (! VHVRConfig.LeftHanded() && GetType() == typeof(QuickSwitchLeft)) {
                        switchHand(true);
                    }
                    break;
                case ItemDrop.ItemData.ItemType.Shield:
                case ItemDrop.ItemData.ItemType.Bow:
                    if (VHVRConfig.LeftHanded() && GetType() == typeof(QuickSwitchLeft)) {
                        switchHand(false);
                    }
                    if (! VHVRConfig.LeftHanded() && GetType() == typeof(QuickSwitch)) {
                        switchHand(true);
                    }
                    break;
                case ItemDrop.ItemData.ItemType.Consumable:
                    Debug.Log("CONSUMABLE !");

                    Transform hand;
                    
                    if (GetType() == typeof(QuickSwitchLeft)) {
                        // if (VHVRConfig.LeftHanded() && Player.m_localPlayer.GetLeftItem() != null 
                        // && ! VHVRConfig.LeftHanded() && Player.m_localPlayer.GetLeftItem() != null) {
                        //     Debug.Log("unequipping left item");
                        //     Player.m_localPlayer.UnequipItem(Player.m_localPlayer.GetLeftItem());
                        // }
                        hand = VRPlayer.leftHand.transform;
                    } else {
                        hand = VRPlayer.rightHand.transform;
                    }
                    
                    var itemObj = Instantiate(item.m_dropPrefab, hand);
                    Destroy(itemObj.GetComponentInChildren<Rigidbody>());
                    itemObj.transform.localPosition = Vector3.zero;
                    itemObj.transform.localRotation = Quaternion.identity;

                    return;
            }

            if (!handsSwitched) {
                Player.m_localPlayer.UseItem(inventory, item, false);
                return;
            }

            if (Player.m_localPlayer.IsItemEquiped(item)) {
                switchVisually(item);

                //Player.m_localPlayer.m_visEquipment.m_rightHand;
            } else {
                Player.m_localPlayer.UseItem(inventory, item, false);   
            }  
            // } else {
            //     Player.m_localPlayer.UseItem(inventory, item, false);
            // }
            

        }
    
        private void switchVisually(ItemDrop.ItemData item) {
            if (Player.m_localPlayer.m_visEquipment. == item) {
                
            }
            
            
            __result.transform.localScale = new Vector3(__result.transform.localScale.x,
            __result.transform.localScale.y* -1, __result.transform.localScale.z);
        }

        private void switchHand(bool leftHanded) {
            ConfigEntry<bool> configEntry;
            VHVRConfig.config.TryGetEntry("Controls", "Left Handed", out configEntry);
            configEntry.Value = leftHanded;
            handsSwitched = true;
            
            // if (bla) {
            //     otherItem = item == Player.m_localPlayer.m_rightItem
            //         ? Player.m_localPlayer.m_leftItem
            //         : Player.m_localPlayer.m_rightItem;                
            //     Player.m_localPlayer.UnequipItem(otherItem, false);
            // }
        }
    }
}