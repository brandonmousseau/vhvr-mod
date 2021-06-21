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
        // I need a layer with non-visible objects since
        // layers are short supply, so re-using 23. Must be
        // in sync with what is in the prefab in Unity Editor.
        public static readonly int WATER = 4;
        public static readonly int ITEM_LAYER = 12;
        public static readonly int WATERVOLUME_LAYER = 21;
        public static readonly int WEAPON_LAYER = 22;
        private static readonly int HANDS_LAYER = 23;
        public static readonly int HANDS_LAYER_MASK = (1 << HANDS_LAYER);
        private static readonly int UI_PANEL_LAYER = 29;
        public static readonly int UI_PANEL_LAYER_MASK = (1 << UI_PANEL_LAYER);
        private static readonly int WORLDSPACE_UI_LAYER = 30;
        public static readonly int WORLDSPACE_UI_LAYER_MASK = (1 << WORLDSPACE_UI_LAYER);

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
