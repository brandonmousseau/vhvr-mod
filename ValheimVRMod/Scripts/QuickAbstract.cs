using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;
using Valve.VR.InteractionSystem;
using System.Collections.Generic;

namespace ValheimVRMod.Scripts
{
    public abstract class QuickAbstract : MonoBehaviour
    {

        private float elementDistance = 0.1f;
        protected const int MAX_ELEMENTS = 11;
        protected const int MAX_EXTRA_ELEMENTS = 5;

        private Color standard = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        private Color hovered = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        private Color selected = new Color(0.34375f, 0.5859375f, 0.796875f, 0.5f);

        protected Texture2D tex_standard;
        protected Texture2D tex_hovered;
        protected Texture2D tex_selected;

        protected GameObject hoveredItem;
        private int elementCount;
        private int extraElementCount;
        protected int hoveredIndex = -1;
        private int lastHoveredIndex = -1;
        protected QuickMenuItem[] elements;
        protected QuickMenuItem[] extraElements;

        protected GameObject sphere;

        protected abstract Transform handTransform { get; }
        private Transform quickMenuLocker;
        protected GameObject wrist;
        protected GameObject radialMenu;
        private bool wasWrist;

        protected MethodInfo stopEmote = AccessTools.Method(typeof(Player), "StopEmote");
        protected bool hasGPower;
        private Texture2D sitTexture;
        private Texture2D mapTexture;
        private Texture2D recenterTexture;
        private Texture2D chatTexture;
        public static bool toggleMap;
        public static bool shouldStartChat;

        protected virtual void Awake()
        {
            sitTexture = VRAssetManager.GetAsset<Texture2D>("sit");
            mapTexture = VRAssetManager.GetAsset<Texture2D>("map");
            recenterTexture = VRAssetManager.GetAsset<Texture2D>("recenter");
            chatTexture = VRAssetManager.GetAsset<Texture2D>("black_screen");
            elements = new QuickMenuItem[MAX_ELEMENTS];
            extraElements = new QuickMenuItem[MAX_EXTRA_ELEMENTS];
            wrist = new GameObject();
            radialMenu = new GameObject();
            radialMenu.transform.SetParent(transform);
            initialize();
            createSphere();
            refreshItems();
        }

        protected class QuickMenuItem : MonoBehaviour
        {
            public string itemName { get; private set; }
            public delegate bool QuickMenuItemCallback();

            private QuickMenuItemCallback callback;
            private Sprite sprite
            {
                set
                {
                    transform.GetChild(2).GetComponent<SpriteRenderer>().sprite = value;
                    ResizeIcon();
                }
            }

            public bool execute()
            {
                return callback == null ? false : callback();
            }

            public void useAsInventoryItemAndRefreshColor(Inventory inventory, ItemDrop.ItemData item)
            {
                if (item.GetIcon().name != itemName)
                {
                    itemName = item.GetIcon().name;
                    sprite = item.GetIcon();
                    callback = delegate ()
                    {
                        Player.m_localPlayer.UseItem(inventory, item, false);
                        return true;
                    };
                }

                transform.GetChild(1).gameObject.SetActive(item.m_equipped || item.m_durability == 0);
                if (item.m_durability == 0)
                {
                    transform.GetChild(1).GetComponent<SpriteRenderer>().color = Color.red;
                }
                else
                {
                    transform.GetChild(1).GetComponent<SpriteRenderer>().color = Color.white;
                }
            }

            public void useAsQuickAction(string itemName, Sprite sprite, QuickMenuItemCallback callback)
            {
                this.itemName = itemName;
                this.sprite = sprite;
                this.callback = callback;
            }

            private void ResizeIcon()
            {
                Vector3 fSize;
                var child = gameObject.transform.GetChild(2);
                if (!child) return;
                var sprite = child.GetComponent<SpriteRenderer>();
                if (!sprite) return;
                fSize = child.lossyScale;
                sprite.drawMode = SpriteDrawMode.Sliced;
                sprite.size = fSize * 9;
            }
        }

        protected abstract void ExecuteHapticFeedbackOnHoverTo();

        private void OnEnable()
        {
            transform.SetParent(handTransform, false);
            transform.localPosition = Vector3.zero;

            switch (VHVRConfig.getQuickMenuType())
            {
                case "Full Hand":
                    transform.localRotation = Quaternion.Euler(VHVRConfig.getQuickMenuVerticalAngle() - 120, 0, 0);
                    break;

                case "Full Player":
                    transform.LookAt(transform.position + Player.m_localPlayer.transform.forward, Player.m_localPlayer.transform.up);
                    transform.localRotation *= Quaternion.Euler(VHVRConfig.getQuickMenuVerticalAngle() - 180, 0, 0);
                    break;
                case "Hand Follow Cam":
                    //Camera Version
                    Camera vrCam = CameraUtils.getCamera(CameraUtils.VR_CAMERA);
                    transform.LookAt(vrCam.transform.position);
                    break;
                case "Hand-Player":
                default:
                    transform.localRotation = Quaternion.Euler(0, 0, 0);
                    var playerDir = Player.m_localPlayer.transform.InverseTransformDirection(transform.forward);
                    playerDir = new Vector3(playerDir.x, -0.05f, playerDir.z);
                    transform.LookAt(transform.position + Player.m_localPlayer.transform.TransformDirection(playerDir), Player.m_localPlayer.transform.up);
                    transform.localRotation *= Quaternion.Euler(VHVRConfig.getQuickMenuVerticalAngle() - 180, 0, 0);
                    break;
            }

            transform.SetParent(Player.m_localPlayer.transform);
            transform.parent = null;
            wasWrist = false;
            lastHoveredIndex = -1;
            radialMenu.SetActive(false);
            resetQuickMenuLocker();
        }

        // Records the current relative transform of the quick menu to the vr cam rig so that we can use it later to lock it relative to the vr cam rig.
        private void resetQuickMenuLocker()
        {
            if (!quickMenuLocker)
            {
                quickMenuLocker = new GameObject().transform;
            }
            
            quickMenuLocker.parent = GetVRCamRig();
            quickMenuLocker.SetPositionAndRotation(transform.position, transform.rotation);
        }

        private void Update()
        {
            if (!quickMenuLocker)
            {
                resetQuickMenuLocker();
            }

            if (quickMenuLocker != null) {
                // Lock the quick menu's position and rotation relative to the vr cam rig so it moves and rotates with the player.
                transform.SetPositionAndRotation(quickMenuLocker.position, quickMenuLocker.rotation);
            }
            
            if (VHVRConfig.getQuickMenuType() == "Hand Follow Cam") {
                Transform VRCamTransform = CameraUtils.getCamera(CameraUtils.VR_CAMERA).transform;
                // Rotate the menu around its x-axis to have it facing the current height of the player.
                transform.LookAt(Vector3.ProjectOnPlane(VRCamTransform.position - transform.position, transform.right) + transform.position);
            }

            if (sphere)
            {
                sphere.transform.position = handTransform.position;
            }
            hoverItem();
        }

        private void OnDestroy()
        {
            Destroy(wrist);
            Destroy(quickMenuLocker);
            Destroy(radialMenu);
            foreach (QuickMenuItem item in elements)
            {
                Destroy(item);
            }
            foreach (QuickMenuItem item in extraElements)
            {
                Destroy(item);
            }
        }
        public abstract void UpdateWristBar();
        public abstract void refreshItems();

        private void createSphere()
        {
            sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.SetParent(transform);
            sphere.transform.localScale *= 0.02f;
            sphere.layer = LayerUtils.getWorldspaceUiLayer();
            sphere.GetComponent<MeshRenderer>().material.color = Color.red;
            Destroy(sphere.GetComponent<Collider>());
        }

        /**
         * Creates:
         * - standard layer (gray)
         * - disabled equipped layers (blue)
         * - items, without sprite yet
         */
        private void initialize()
        {

            tex_standard = new Texture2D(1, 1);
            tex_standard.SetPixel(0, 0, standard);
            tex_standard.Apply();

            tex_hovered = new Texture2D(1, 1);
            tex_hovered.SetPixel(0, 0, hovered);
            tex_hovered.Apply();

            tex_selected = new Texture2D(1, 1);
            tex_selected.SetPixel(0, 0, selected);
            tex_selected.Apply();

            for (int i = 0; i < MAX_ELEMENTS; i++)
            {

                elements[i] = new GameObject().AddComponent<QuickMenuItem>();
                elements[i].transform.SetParent(radialMenu.transform, false);

                CreateItemLayers(elements[i].gameObject);
            }

            for (int i = 0; i < MAX_EXTRA_ELEMENTS; i++)
            {
                extraElements[i] = new GameObject().AddComponent<QuickMenuItem>();
                extraElements[i].transform.SetParent(wrist.transform, false);

                CreateItemLayers(extraElements[i].gameObject);
            }

            hoveredItem = new GameObject();
            hoveredItem.layer = LayerUtils.getWorldspaceUiLayer();
            hoveredItem.transform.SetParent(transform, false);
            hoveredItem.transform.localScale *= 4;
            var hoveredItemRenderer = hoveredItem.AddComponent<SpriteRenderer>();
            hoveredItemRenderer.sprite = Sprite.Create(tex_hovered, new Rect(0.0f, 0.0f, tex_hovered.width, tex_hovered.height), new Vector2(0.5f, 0.5f));
            hoveredItemRenderer.sortingOrder = 1;
            hoveredItem.SetActive(false);
            quickMenuLocker = new GameObject().transform;
        }

        protected void reorderElements()
        {

            for (int i = 0; i < MAX_ELEMENTS; i++)
            {

                if (i >= elementCount)
                {
                    elements[i].gameObject.SetActive(false);
                    continue;
                }

                double a = (360 / elementCount * Mathf.Deg2Rad * i) + 90 * Mathf.Deg2Rad;
                double x = Math.Cos(a) * elementDistance;
                double y = Math.Sin(a) * elementDistance;
                var position = new Vector2((float)-x, (float)y);

                elements[i].gameObject.SetActive(true);
                elements[i].transform.localPosition = position;
            }

            if (!Player.m_localPlayer)
            {
                return;
            }

            for (int i = 0; i < MAX_EXTRA_ELEMENTS; i++)
            {
                if (i >= extraElementCount)
                {
                    extraElements[i].gameObject.SetActive(false);
                    continue;
                }
                var extraOffset = (i * 0.05f) - (extraElementCount / 2 * 0.05f) + (extraElementCount % 2 == 0 ? 0.025f : 0);
                var position = new Vector2((float)extraOffset, 0);

                extraElements[i].gameObject.SetActive(true);
                extraElements[i].transform.localPosition = position;
            }
        }

        private void hoverItem()
        {

            float maxDist = 0.05f;
            Vector3 hoverPos = Vector3.zero;
            Quaternion hoverRot = Quaternion.identity;
            hoveredIndex = -1;

            var projectedPos = Vector3.Project(handTransform.position - transform.position, transform.forward);
            transform.position += projectedPos;
            sphere.transform.position -= projectedPos;

            //extraItems
            for (int i = 0; i < extraElementCount; i++)
            {
                var dist = Vector3.Distance(handTransform.position, extraElements[i].transform.position);

                if (dist < maxDist)
                {
                    maxDist = dist;
                    hoveredIndex = i + elementCount;
                    hoverPos = extraElements[i].transform.position;
                    hoverRot = extraElements[i].transform.rotation;
                }
            }
            if (IsInArea())
            {
                wasWrist = true;
            }

            //radial menu mode
            if (wasWrist)
            {
                radialMenu.gameObject.SetActive(false);
            }
            else if (hoveredIndex == -1 && elementCount != 0)
            {
                radialMenu.gameObject.SetActive(true);
                var convertedPos = transform.InverseTransformPoint(handTransform.position);
                convertedPos = new Vector3(convertedPos.x, convertedPos.y, 0);
                //var currentangle = Vector3.SignedAngle(transform.up, parent.position - transform.position, -transform.forward);
                var currentangle = Vector3.SignedAngle(Vector3.up, convertedPos.normalized, -Vector3.forward);
                var distFromCenter = Vector3.Distance(transform.position, handTransform.position);
                if (distFromCenter > 0.07f)
                {
                    var elementAngle = 360 / elementCount;
                    var wrappedAngle = currentangle + elementAngle / 2f;
                    wrappedAngle = (wrappedAngle + 360f) % 360f;
                    hoveredIndex = (int)(wrappedAngle / elementAngle);
                    if (hoveredIndex >= elementCount) hoveredIndex = 0;
                    hoverPos = elements[hoveredIndex].transform.position;
                    hoverRot = elements[hoveredIndex].transform.rotation;
                }
            }

            var hovering = false;

            if (hoveredIndex >= 0 && hoveredIndex < elements.Length + extraElements.Length - 2)
            {
                if (lastHoveredIndex != hoveredIndex)
                {
                    ExecuteHapticFeedbackOnHoverTo();
                }
                hoveredItem.transform.position = hoverPos;
                hoveredItem.transform.rotation = hoverRot;
                hovering = true;
                lastHoveredIndex = hoveredIndex;
            }
            else
            {
                lastHoveredIndex = -1;
            }

            hoveredItem.SetActive(hovering);

        }

        protected void CreateItemLayers(GameObject currentParent)
        {
            GameObject standardLayer = new GameObject();
            standardLayer.layer = LayerUtils.getWorldspaceUiLayer();
            standardLayer.transform.SetParent(currentParent.transform, false);
            standardLayer.transform.localScale *= 4;
            var standardRenderer = standardLayer.AddComponent<SpriteRenderer>();
            standardRenderer.sprite = Sprite.Create(tex_standard, new Rect(0.0f, 0.0f, tex_standard.width, tex_standard.height), new Vector2(0.5f, 0.5f));
            standardRenderer.sortingOrder = 0;

            GameObject equipedLayer = new GameObject();
            equipedLayer.layer = LayerUtils.getWorldspaceUiLayer();
            equipedLayer.transform.SetParent(currentParent.transform, false);
            equipedLayer.transform.localScale *= 4;
            var equipedRenderer = equipedLayer.AddComponent<SpriteRenderer>();
            equipedRenderer.sprite = Sprite.Create(tex_selected, new Rect(0.0f, 0.0f, tex_selected.width, tex_selected.height), new Vector2(0.5f, 0.5f));
            equipedRenderer.sortingOrder = 2;
            equipedLayer.SetActive(false);

            GameObject item = new GameObject();
            item.layer = LayerUtils.getWorldspaceUiLayer();
            item.transform.SetParent(currentParent.transform, false);
            item.transform.localScale /= 15;
            var renderer = item.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 3;
        }

        private static Transform GetVRCamRig()
        {
            return CameraUtils.getCamera(CameraUtils.VR_CAMERA).transform.parent;
        }

        protected bool IsInArea()
        {
            var wristBasedPos = wrist.transform.InverseTransformPoint(handTransform.position);
            if (wristBasedPos.y > -0.07f && wristBasedPos.y < 0.07f &&
                wristBasedPos.z > -0.07f && wristBasedPos.z < 0.07f &&
                wristBasedPos.x > -0.15f && wristBasedPos.x < 0.15f)
                return true;
            return false;
        }

        protected bool isInView()
        {
            Camera vrCam = CameraUtils.getCamera(CameraUtils.VR_CAMERA);
            var deltaAngle = Vector3.Angle(vrCam.transform.forward, wrist.transform.forward);
            if (deltaAngle > 70f && !SettingCallback.configRunning)
            {
                return false;
            }

            return true;
        }

        protected void refreshRadialItems(bool isDominantHand)
        {
            if (Player.m_localPlayer == null)
            {
                return;
            }

            elementCount = 0;
            var inventory = Player.m_localPlayer?.GetInventory();
            if (inventory == null)
            {
                return;
            }
            for (int i = 0; i < 8; i++)
            {

                ItemDrop.ItemData item = inventory?.GetItemAt(i, 0);

                if (item == null)
                {
                    continue;
                }
                if (VHVRConfig.GetQuickMenuIsSeperate() && (EquipScript.IsDominantHandItem(item) ^ isDominantHand))
                {
                    continue;
                }

                elements[elementCount].useAsInventoryItemAndRefreshColor(inventory, item);
                elementCount++;
            }
        }

        protected void RefreshWristQuickAction()
        {
            if (Player.m_localPlayer == null)
            {
                return;
            }

            extraElementCount = 0;

            Inventory inventory = Player.m_localPlayer?.GetInventory();
            if (inventory == null)
            {
                return;
            }
            for (int i = 0; i < 4; i++)
            {

                ItemDrop.ItemData item = inventory?.GetItemAt(i + 4, 1);

                if (item == null)
                {
                    continue;
                }

                if (item.GetIcon().name != extraElements[extraElementCount].itemName)
                {
                    extraElements[extraElementCount].useAsInventoryItemAndRefreshColor(inventory, item);
                }
                extraElementCount++;
            }
        }

        protected void RefreshWristQuickSwitch()
        {
            if (!Player.m_localPlayer)
            {
                return;
            }
            StatusEffect se;
            float cooldown;
            extraElementCount = 0;
            Player.m_localPlayer.GetGuardianPowerHUD(out se, out cooldown);
            if (se)
            {
                hasGPower = true;
                if (extraElements[extraElementCount].itemName != ("QuickActionPOWER_" + se.name))
                {
                    extraElements[extraElementCount].useAsQuickAction(
                        "QuickActionPOWER_" + se.name,
                        se.m_icon,
                        delegate ()
                        {
                            if (!hasGPower)
                            {
                                return false;
                            }
                            Player.m_localPlayer.StartGuardianPower();
                            return true;
                        });
                }
                extraElementCount++;
            }

            if (extraElements[extraElementCount].itemName != "QuickActionSIT")
            {
                extraElements[extraElementCount].useAsQuickAction(
                    "QuickActionSIT",
                    Sprite.Create(sitTexture, new Rect(0.0f, 0.0f, sitTexture.width, sitTexture.height), new Vector2(0.5f, 0.5f), 500),
                    delegate ()
                    {
                        if (Player.m_localPlayer.InEmote() && Player.m_localPlayer.IsSitting())
                            stopEmote.Invoke(Player.m_localPlayer, null);
                        else
                            Player.m_localPlayer.StartEmote("sit", false);
                        return true;
                    });
            }
            extraElementCount++;

            if (extraElements[extraElementCount].itemName != "QuickActionMAP")
            {
                extraElements[extraElementCount].useAsQuickAction(
                    "QuickActionMAP",
                    Sprite.Create(mapTexture, new Rect(0.0f, 0.0f, mapTexture.width, mapTexture.height), new Vector2(0.5f, 0.5f), 500),
                    delegate ()
                    {
                        toggleMap = true;
                        return true;
                    });
            }
            extraElementCount++;

            if (extraElements[extraElementCount].itemName != "QuickActionRECENTER")
            {
                extraElements[extraElementCount].useAsQuickAction(
                    "QuickActionRECENTER",
                    Sprite.Create(recenterTexture, new Rect(0.0f, 0.0f, recenterTexture.width, recenterTexture.height), new Vector2(0.5f, 0.5f), 500),
                    delegate ()
                    {
                        VRManager.tryRecenter();
                        return true;
                    });
            }
            extraElementCount++;

            if (extraElements[extraElementCount].itemName != "QuickActionCHAT")
            {
                extraElements[extraElementCount].useAsQuickAction(
                    "QuickActionCHAT",
                    Sprite.Create(chatTexture, new Rect(0.0f, 0.0f, chatTexture.width, chatTexture.height), new Vector2(0.5f, 0.5f), 500),
                    delegate ()
                    {
                        shouldStartChat = true;
                        TextInput.m_instance.Show("ChatText", "", 256);
                        TextInput.m_instance.m_panel.gameObject.transform.localScale = new Vector3(0, 0, 0);
                        return true;
                    });
            }
            extraElementCount++;
        }

        public bool selectHoveredItem()
        {
            if (hoveredIndex < 0)
            {
                return false;
            }
            else if (hoveredIndex < elementCount)
            {
                return elements[hoveredIndex].execute();
            }
            else if (hoveredIndex < elementCount + extraElementCount)
            {
                return extraElements[hoveredIndex - elementCount].execute();
            }
            return false;
        }
    }
}
