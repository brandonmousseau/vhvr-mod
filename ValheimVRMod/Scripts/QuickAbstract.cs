using System;
using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts {
    public abstract class QuickAbstract : MonoBehaviour {
        
        private float elementDistance = 0.1f;
        private const int MAX_ELEMENTS = 10;

        private Color standard = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        private Color hovered = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        private Color selected = new Color(0.34375f, 0.5859375f, 0.796875f, 0.5f);

        protected GameObject hoveredItem;
        protected int hoveredIndex = -1;
        protected GameObject[] elements;
        
        private GameObject sphere;

        public Transform parent;
        
        
        private void Awake() {
            
            elements = new GameObject[MAX_ELEMENTS];
            initialize();
            refreshItems();
            createSphere();
        }
        
        private void OnEnable() {
            transform.SetParent(parent, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.Euler(107, 0, -5);
            transform.SetParent(Player.m_localPlayer.transform);
        }
        private void Update() {
            sphere.transform.position = parent.position;
            hoverItem();
        }

        protected abstract int getElementCount();
        public abstract void refreshItems();
        public abstract void selectHoveredItem();

        private void createSphere() {
            sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.SetParent(transform);
            sphere.transform.localScale *= 0.02f;
            sphere.layer = LayerUtils.getWorldspaceUiLayer();
            sphere.GetComponent<MeshRenderer>().material.color = Color.red;
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

            for (int i = 0; i < MAX_ELEMENTS; i++) {

                elements[i] = new GameObject();
                elements[i].transform.SetParent(transform, false);
                
                GameObject standardLayer = new GameObject();
                standardLayer.layer = LayerUtils.getWorldspaceUiLayer();
                standardLayer.transform.SetParent(elements[i].transform, false);
                standardLayer.transform.localScale *= 4;
                var standardRenderer = standardLayer.AddComponent<SpriteRenderer>();
                standardRenderer.sprite = Sprite.Create(tex_standard, new Rect(0.0f, 0.0f, tex_standard.width, tex_standard.height), new Vector2(0.5f, 0.5f));
                standardRenderer.sortingOrder = 0;

                GameObject equipedLayer = new GameObject();
                equipedLayer.layer = LayerUtils.getWorldspaceUiLayer();
                equipedLayer.transform.SetParent(elements[i].transform, false);
                equipedLayer.transform.localScale *= 4;
                var equipedRenderer = equipedLayer.AddComponent<SpriteRenderer>();
                equipedRenderer.sprite = Sprite.Create(tex_selected, new Rect(0.0f, 0.0f, tex_selected.width, tex_selected.height), new Vector2(0.5f, 0.5f));
                equipedRenderer.sortingOrder = 2;
                equipedLayer.SetActive(false);

                GameObject item = new GameObject();
                item.layer = LayerUtils.getWorldspaceUiLayer();
                item.transform.SetParent(elements[i].transform, false);
                item.transform.localScale /= 15;
                var renderer = item.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = 3;

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

        protected void reorderElements() {
            
            for (int i = 0; i < MAX_ELEMENTS; i++) {

                if (i >= getElementCount()) {
                    elements[i].SetActive(false);
                    continue;
                }
                
                double a = i * 2 * Math.PI / getElementCount();
                double x = Math.Cos(a) * elementDistance;
                double y = Math.Sin(a) * elementDistance;
                var position = new Vector2((float)x, (float)y);
                
                elements[i].SetActive(true);
                elements[i].transform.localPosition = position;
            }
        }
        
        private void hoverItem() {

            float maxDist = 0.05f;
            hoveredIndex = -1;
            
            for (int i = 0; i < getElementCount(); i++) {
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
    }
}