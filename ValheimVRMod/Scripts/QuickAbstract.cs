using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;
using Valve.VR.InteractionSystem;
using System.Collections.Generic;

namespace ValheimVRMod.Scripts {
    public abstract class QuickAbstract : MonoBehaviour {
        
        private float elementDistance = 0.1f;
        private const int MAX_ELEMENTS = 11;
        private const int MAX_EXTRA_ELEMENTS = 4;

        private Color standard = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        private Color hovered = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        private Color selected = new Color(0.34375f, 0.5859375f, 0.796875f, 0.5f);

        protected Texture2D tex_standard;
        protected Texture2D tex_hovered;
        protected Texture2D tex_selected;

        protected GameObject hoveredItem;
        protected int hoveredIndex = -1;
        private int lastHoveredIndex = -1;
        protected QuickIcon[] elements;
        protected QuickIcon[] extraElements;
        
        protected GameObject sphere;

        public Transform parent;
        private Transform quickMenuLocker;
        protected GameObject wrist;
        protected GameObject radialMenu;
        protected Hand currentHand;
        private bool wasWrist;

        protected MethodInfo stopEmote = AccessTools.Method(typeof(Player), "StopEmote");
        protected bool hasGPower;
        private Texture2D sitTexture;
        private Texture2D mapTexture;
        private Texture2D recenterTexture;
        public static bool toggleMap;

        private void Awake() {
            sitTexture = VRAssetManager.GetAsset<Texture2D>("sit");
            mapTexture = VRAssetManager.GetAsset<Texture2D>("map");
            recenterTexture = VRAssetManager.GetAsset<Texture2D>("recenter");
            elements = new QuickIcon[MAX_ELEMENTS];
            extraElements = new QuickIcon[MAX_EXTRA_ELEMENTS];
            wrist = new GameObject();
            radialMenu = new GameObject();
            radialMenu.transform.SetParent(transform);
            initialize();
            createSphere();
            InitializeWrist();
            refreshItems();
            
        }

        protected class QuickIcon
        {
            public string Name { get; set; }
            public GameObject gameObject { get; set; }
            public Transform transform { get; set; }
            public int num { get; set; }

            public QuickIcon()
            {
                gameObject = new GameObject();
                transform = gameObject.transform;
                num = -1;
            }
        }

        protected abstract void InitializeWrist();

        private void OnEnable() {
            transform.SetParent(parent, false);
            transform.localPosition = Vector3.zero;
            
            switch (VHVRConfig.getQuickMenuType())
            {
                case "Full Hand":
                    transform.localRotation = Quaternion.Euler(VHVRConfig.getQuickMenuVerticalAngle()-120, 0, 0);
                    break;
                
                case "Full Player":
                    transform.LookAt(transform.position + Player.m_localPlayer.transform.forward, Player.m_localPlayer.transform.up);
                    transform.localRotation *= Quaternion.Euler(VHVRConfig.getQuickMenuVerticalAngle()-180, 0, 0);
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
            // Record the current relative transform of the quick menu to the vr cam rig so that we can use it later to lock it relative to the vr cam rig.
            quickMenuLocker.parent = GetVRCamRig();
            quickMenuLocker.SetPositionAndRotation(transform.position, transform.rotation);
        }
        private void Update() {
            // Lock the quick menu's positioin and rotation relative to the vr cam rig so it moves and rotates with the player.
            transform.SetPositionAndRotation(quickMenuLocker.position, quickMenuLocker.rotation);
            sphere.transform.position = parent.position;
            hoverItem();
        }

        private void OnDestroy()
        {
            Destroy(wrist);
        }
        public abstract void UpdateWristBar();
        protected abstract int getElementCount();
        protected abstract int getExtraElementCount();
        public abstract void refreshItems();
        public abstract void selectHoveredItem();

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
            
            tex_standard = new Texture2D(1, 1);
            tex_standard.SetPixel(0,0, standard);
            tex_standard.Apply();
        
            tex_hovered = new Texture2D(1, 1);
            tex_hovered.SetPixel(0,0, hovered);
            tex_hovered.Apply();
        
            tex_selected = new Texture2D(1, 1);
            tex_selected.SetPixel(0,0, selected);
            tex_selected.Apply();

            for (int i = 0; i < MAX_ELEMENTS; i++) {

                elements[i] = new QuickIcon();
                elements[i].transform.SetParent(radialMenu.transform, false);

                CreateItemLayers(elements[i].gameObject);
            }

            for (int i = 0; i < MAX_EXTRA_ELEMENTS; i++)
            {
                extraElements[i] = new QuickIcon();
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

                if (i >= getElementCount())
                {
                    elements[i].gameObject.SetActive(false);
                    continue;
                }

                double a = (360 / getElementCount() * Mathf.Deg2Rad * i) + 90 * Mathf.Deg2Rad;
                double x = Math.Cos(a) * elementDistance;
                double y = Math.Sin(a) * elementDistance;
                var position = new Vector2((float)-x, (float)y);

                elements[i].gameObject.SetActive(true);
                elements[i].transform.localPosition = position;
            }

            for (int i = 0; i < MAX_EXTRA_ELEMENTS; i++)
            {
                if (i >= getExtraElementCount())
                {
                    extraElements[i].gameObject.SetActive(false);
                    continue;
                }
                var extraOffset = (i * 0.05f) - (getExtraElementCount() / 2 * 0.05f) + (getExtraElementCount() % 2 == 0 ? 0.025f : 0);
                var position = new Vector2((float)extraOffset,0);
                extraElements[i].gameObject.SetActive(true);
                extraElements[i].transform.localPosition = position;
            }
        }
        private void hoverItem() {

            float maxDist = 0.05f;
            Vector3 hoverPos = Vector3.zero;
            Quaternion hoverRot = Quaternion.identity;
            hoveredIndex = -1;

            var projectedPos = Vector3.Project(parent.position - transform.position, transform.forward);
            transform.position += projectedPos;
            sphere.transform.position -= projectedPos;

            //extraItems
            for (int i = 0; i < getExtraElementCount(); i++)
            {
                var dist = Vector3.Distance(parent.position, extraElements[i].transform.position);

                if (dist < maxDist)
                {
                    maxDist = dist;
                    hoveredIndex = i + getElementCount();
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
            else if (hoveredIndex == -1)
            {
                radialMenu.gameObject.SetActive(true);
                var convertedPos = transform.InverseTransformPoint(parent.position);
                convertedPos = new Vector3(convertedPos.x, convertedPos.y, 0);
                //var currentangle = Vector3.SignedAngle(transform.up, parent.position - transform.position, -transform.forward);
                var currentangle = Vector3.SignedAngle(Vector3.up, convertedPos.normalized, -Vector3.forward);
                var distFromCenter = Vector3.Distance(transform.position, parent.position);
                if (distFromCenter > 0.07f)
                {
                    var elementAngle = 360 / getElementCount();
                    var wrappedAngle = currentangle + elementAngle / 2f;
                    wrappedAngle = (wrappedAngle + 360f) % 360f;
                    hoveredIndex = (int)(wrappedAngle / elementAngle);
                    if (hoveredIndex >= getElementCount()) hoveredIndex = 0;
                    hoverPos = elements[hoveredIndex].transform.position;
                    hoverRot = elements[hoveredIndex].transform.rotation;
                }
            }

            var hovering = false;
            
            if (hoveredIndex >= 0 && hoveredIndex < elements.Length+extraElements.Length - 2) {
                if (lastHoveredIndex != hoveredIndex)
                {
                    if (currentHand == VRPlayer.leftHand)
                        VRPlayer.rightHand.hapticAction.Execute(0, 0.1f, 40, 0.1f, SteamVR_Input_Sources.RightHand);
                    else
                        VRPlayer.leftHand.hapticAction.Execute(0, 0.1f, 40, 0.1f, SteamVR_Input_Sources.LeftHand);
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

        private void CreateItemLayers(GameObject currentParent)
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

        protected void ResizeIcon(GameObject element)
        {
            Vector3 fSize;
            var child = element.transform.GetChild(2);
            if (!child) return;
            var sprite = child.GetComponent<SpriteRenderer>();
            if (!sprite) return;
            fSize = child.lossyScale;
            sprite.drawMode = SpriteDrawMode.Sliced;
            sprite.size = fSize * 9;
        }

        private static Transform GetVRCamRig() {
            return CameraUtils.getCamera(CameraUtils.VR_CAMERA).transform.parent;
        }
        
        protected bool IsInArea()
        {
            var wristBasedPos = wrist.transform.InverseTransformPoint(parent.position);
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

        protected void RefreshQuickAction(Inventory inventory,QuickIcon[] extraElements,out int extraElementCount)
        {
            extraElementCount = 0;
            for (int i = 0; i < 4; i++)
            {

                ItemDrop.ItemData item = inventory?.GetItemAt(i + 4, 1);

                if (item == null)
                {
                    continue;
                }
                
                extraElements[extraElementCount].transform.GetChild(1).gameObject.SetActive(item.m_equiped || item.m_durability == 0);
                if (item.m_durability == 0)
                {
                    extraElements[extraElementCount].transform.GetChild(1).GetComponent<SpriteRenderer>().color = Color.red;
                }
                else
                {
                    extraElements[extraElementCount].transform.GetChild(1).GetComponent<SpriteRenderer>().color = Color.white;
                }


                if (item.GetIcon().name != extraElements[extraElementCount].Name)
                {
                    extraElements[extraElementCount].transform.GetChild(2).GetComponent<SpriteRenderer>().sprite = item.GetIcon();
                    ResizeIcon(extraElements[extraElementCount].gameObject);
                }
                extraElements[extraElementCount].num = i + 4;
                extraElements[extraElementCount].Name = item.GetIcon().name;
                extraElementCount++;
            }
        }
        protected void RefreshQuickSwitch(QuickIcon[] extraElements, out int extraElementCount)
        {
            StatusEffect se;
            float cooldown;
            extraElementCount = 0;
            Player.m_localPlayer.GetGuardianPowerHUD(out se, out cooldown);
            if (se)
            {
                hasGPower = true;
                if (extraElements[extraElementCount].Name != ("QuickActionPOWER_" + se.name))
                {
                    extraElements[extraElementCount].transform.GetChild(2).GetComponent<SpriteRenderer>().sprite = se.m_icon;
                    ResizeIcon(extraElements[extraElementCount].gameObject);
                    extraElements[extraElementCount].Name = "QuickActionPOWER_" + se.name;
                }
                extraElementCount++;
            }

            if (extraElements[extraElementCount].Name != "QuickActionSIT")
            {
                extraElements[extraElementCount].transform.GetChild(2).GetComponent<SpriteRenderer>().sprite = Sprite.Create(sitTexture,
                new Rect(0.0f, 0.0f, sitTexture.width, sitTexture.height),
                new Vector2(0.5f, 0.5f), 500);
                ResizeIcon(extraElements[extraElementCount].gameObject);
                extraElements[extraElementCount].Name = "QuickActionSIT";
            }
            extraElementCount++;

            if (extraElements[extraElementCount].Name != "QuickActionMAP")
            {
                extraElements[extraElementCount].transform.GetChild(2).GetComponent<SpriteRenderer>().sprite = Sprite.Create(mapTexture,
               new Rect(0.0f, 0.0f, mapTexture.width, mapTexture.height),
               new Vector2(0.5f, 0.5f), 500);
                ResizeIcon(extraElements[extraElementCount].gameObject);
                extraElements[extraElementCount].Name = "QuickActionMAP";
            }
            extraElementCount++;

            if (extraElements[extraElementCount].Name != "QuickActionRECENTER")
            {
                extraElements[extraElementCount].transform.GetChild(2).GetComponent<SpriteRenderer>().sprite = Sprite.Create(recenterTexture,
                new Rect(0.0f, 0.0f, recenterTexture.width, recenterTexture.height),
                new Vector2(0.5f, 0.5f), 500);
                ResizeIcon(extraElements[extraElementCount].gameObject);
                extraElements[extraElementCount].Name = "QuickActionRECENTER";
            }
            extraElementCount++;
        }

        protected bool SelectHoverQuickSwitch(int hoveredIndex,int elementCount,Inventory inventory)
        {
            if (hoveredIndex >= elementCount)
            {
                ItemDrop.ItemData item2 = inventory.GetItemAt(extraElements[hoveredIndex - elementCount].num, 1);
                Player.m_localPlayer.UseItem(inventory, item2, false);
                return true;
            }
            return false;
        }
        protected bool SelectHoverQuickAction(int hoveredIndex, int allElementCount)
        {
            if (hasGPower && hoveredIndex == allElementCount - 4)
            {
                Player.m_localPlayer.StartGuardianPower();
                return true;
            }

            if (hoveredIndex == allElementCount - 3)
            {
                if (Player.m_localPlayer.InEmote() && Player.m_localPlayer.IsSitting())
                    stopEmote.Invoke(Player.m_localPlayer, null);
                else
                    Player.m_localPlayer.StartEmote("sit", false);
                return true;
            }

            if (hoveredIndex == allElementCount - 2)
            {
                toggleMap = true;
                return true;
            }

            if (hoveredIndex == allElementCount - 1)
            {
                VRManager.tryRecenter();
                return true;
            }
            return false; 
        }
    }
}
