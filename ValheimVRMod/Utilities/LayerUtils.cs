using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

using static ValheimVRMod.Utilities.LogUtils;

/**
 *  Layer Value: 0   Layer Name: Default 
 *  Layer Value: 1   Layer Name: TransparentFX 
 *  Layer Value: 2   Layer Name: Ignore Raycast 
 *  Layer Value: 3   Layer Name:  
 *  Layer Value: 4   Layer Name: Water 
 *  Layer Value: 5   Layer Name: UI 
 *  Layer Value: 6   Layer Name:  
 *  Layer Value: 7   Layer Name:  
 *  Layer Value: 8   Layer Name: effect 
 *  Layer Value: 9   Layer Name: character 
 *  Layer Value: 10  Layer Name: piece 
 *  Layer Value: 11  Layer Name: terrain 
 *  Layer Value: 12  Layer Name: item 
 *  Layer Value: 13  Layer Name: ghost 
 *  Layer Value: 14  Layer Name: character_trigger 
 *  Layer Value: 15  Layer Name: static_solid 
 *  Layer Value: 16  Layer Name: piece_nonsolid 
 *  Layer Value: 17  Layer Name: character_ghost 
 *  Layer Value: 18  Layer Name: hitbox 
 *  Layer Value: 19  Layer Name: skybox 
 *  Layer Value: 20  Layer Name: Default_small 
 *  Layer Value: 21  Layer Name: WaterVolume 
 *  Layer Value: 22  Layer Name: weapon 
 *  Layer Value: 23  Layer Name: blocker 
 *  Layer Value: 24  Layer Name: pathblocker 
 *  Layer Value: 25  Layer Name: viewblock 
 *  Layer Value: 26  Layer Name: character_net 
 *  Layer Value: 27  Layer Name: character_noenv 
 *  Layer Value: 28  Layer Name: vehicle 
 *  Layer Value: 29  Layer Name:  
 *  Layer Value: 30  Layer Name:  
 *  Layer Value: 31  Layer Name: smoke 
 */
namespace ValheimVRMod.Utilities
{
    static class LayerUtils
    {
        // A layer that collides with most other layers, borrowing it for VR weapon collsion.
        public const int VHVR_WEAPON = 3;
        public const int WATER = 4;
        public const int CHARACTER = 9;
        public const int PIECE = 10;
        public const int TERRAIN = 11;
        public const int ITEM_LAYER = 12;
        public const int CHARARCTER_TRIGGER = 14;
        public const int STATIC_SOLID = 15;
        public const int PIECE_NONSOLID = 10;
        public const int WATERVOLUME_LAYER = 21;
        public const int WEAPON_LAYER = 22;
        // I need a layer with non-visible objects since
        // layers are short supply, so re-using 23. Must be
        // in sync with what is in the prefab in Unity Editor.
        private const int HANDS_LAYER = 23;
        public const int HANDS_LAYER_MASK = (1 << HANDS_LAYER);
        public const int UI_PANEL_LAYER = 29;
        public const int UI_PANEL_LAYER_MASK = (1 << UI_PANEL_LAYER);
        private const int WORLDSPACE_UI_LAYER = 30;
        public const int WORLDSPACE_UI_LAYER_MASK = (1 << WORLDSPACE_UI_LAYER);
        // TODO: Use const instead? (1 << PIECE) | (1 << PIECE_NONSOLID) | (1 << ITEM_LAYER)
        public static readonly int HARVEST_RAY_MASK = LayerMask.GetMask(new string[]
            {
                "piece",
                "piece_nonsolid",
                "item"
            });

        public static int getHandsLayer()
        {
            checkLayer(HANDS_LAYER);
            return HANDS_LAYER;
        }

        public static int getUiPanelLayer()
        {
            checkLayer(UI_PANEL_LAYER);
            return UI_PANEL_LAYER;
        }

        public static int getWorldspaceUiLayer()
        {
            checkLayer(WORLDSPACE_UI_LAYER);
            return WORLDSPACE_UI_LAYER;
        }

        private static void checkLayer(int layer)
        {
            string layerString = LayerMask.LayerToName(layer);
            if (layerString != null && layerString.Length > 0)
            {
                LogWarning("Layer " + layer + " is a named layer: " + layerString);
            }
        }

    }
}
