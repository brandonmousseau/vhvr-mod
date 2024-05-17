using System;
using System.Collections.Generic;
using UnityEngine;

/**
 * Manages hardcoded bow anatomy data.
 */
namespace ValheimVRMod.Utilities
{
    public class BowAnatomy
    {
        public enum BowBendingImplType
        {
            Shader, // Use custom shader for bow bending animation
            Skinned, // Attempt to use skinned mesh renderer for bow bending animation
            Auto // Use skinned mesh renderer for bow bending animation if the mesh can be accessed or use custom shader otherwise
        }

        public readonly float handleHeight;
        public readonly float softLimbHeight;
        public readonly float stringRadius;
        public readonly BowBendingImplType bowBendingImpl;

        // The following data stand when they cannot be calculated from the mesh data.
        public readonly float fallbackHandleWidth;
        public readonly Vector3 fallbackStringTop;
        public readonly Vector3 fallbackStringBottom;
        public readonly Vector3 fallbackHandleTop;
        public readonly Vector3 fallbackHandleBottom;


        private static BowAnatomy DefaultBowAnatomy = new BowAnatomy(
            /* handleHeight= */ 0.624f,
            /* softLimbHeight= */ 0.125f,
            /* stringRadius= */ 0.008f,
            /* bowBendingImpl= */ BowBendingImplType.Skinned,
            /* fallbackHandleWidth= */ 0.05f,
            /* fallbackStringTop= */ new Vector3(0, 0.75f, -0.325f),
            /* fallbackStringBottom= */ new Vector3(0, -0.75f, -0.325f),
            /* fallbackHandleTop= */ new Vector3(0, 0.312f, 0),
            /* fallbackHandleBottom= */ new Vector3(0, -0.312f, 0));

        private static Dictionary<string, BowAnatomy> BowAnatomies = new Dictionary<string, BowAnatomy>
        {
            {
                "$item_bow_snipesnap", // Note: item name is snipesnap not spinesnap
                new BowAnatomy(
                    /* handleHeight= */ 0.52f,
                    /* softLimbHeight= */ 0.15f,
                    /* stringRadius= */ 0.008f,
                    /* bowBendingImpl= */ BowBendingImplType.Auto,
                    /* fallbackHandleWidth= */ 0.05f,
                    /* fallbackStringTop= */ new Vector3(0, 0.75f, -0.325f),
                    /* fallbackStringBottom= */ new Vector3(0, -0.75f, -0.325f),
                    /* fallbackHandleTop= */ new Vector3(0, 0.26f, 0),
                    /* fallbackHandleBottom= */ new Vector3(0, -0.26f, 0))
            },
            {
                "$item_bow_ashlands",
                new BowAnatomy(
                    /* handleHeight= */ 0.75f,
                    /* softLimbHeight= */ 0.15f,
                    /* stringRadius= */ 0.01f,
                    /* bowBendingImpl= */ BowBendingImplType.Auto,
                    /* fallbackHandleWidth= */ 0.0625f,
                    /* fallbackStringTop= */ new Vector3(0.01f, 0.75f, -0.25f),
                    /* fallbackStringBottom= */ new Vector3(0.01f, -0.75f, -0.25f),
                    /* fallbackHandleTop= */ new Vector3(0, 0.375f, 0),
                    /* fallbackHandleBottom= */ new Vector3(0, -0.375f, 0))
            },
            {
                "$item_bow_ashlandsblood",
                new BowAnatomy(
                    /* handleHeight= */ 0.7f,
                    /* softLimbHeight= */ 0.1f,
                    /* stringRadius= */ 0.01f,
                    /* bowBendingImpl= */ BowBendingImplType.Auto,
                    /* fallbackHandleWidth= */ 0.0625f,
                    /* fallbackStringTop= */ new Vector3(0, 0.75f, -0.245f),
                    /* fallbackStringBottom= */ new Vector3(0, -0.75f, -0.245f),
                    /* fallbackHandleTop= */ new Vector3(0, 0.35f, 0),
                    /* fallbackHandleBottom= */ new Vector3(0, -0.35f, 0))
            },
            {
                "$item_bow_ashlandsstorm",
                new BowAnatomy(
                    /* handleHeight= */ 0.75f,
                    /* softLimbHeight= */ 0.1f,
                    /* stringRadius= */ 0.01f,
                    /* bowBendingImpl= */ BowBendingImplType.Auto,
                    /* fallbackHandleWidth= */ 0.0625f,
                    /* fallbackStringTop= */ new Vector3(0, 0.8f, -0.24f),
                    /* fallbackStringBottom= */ new Vector3(0, -0.8f, -0.24f),
                    /* fallbackHandleTop= */ new Vector3(0, 0.375f, 0),
                    /* fallbackHandleBottom= */ new Vector3(0, -0.375f, 0))
            },
            {
                "$item_bow_ashlandsroot",
                new BowAnatomy(
                    /* handleHeight= */ 0.8f,
                    /* softLimbHeight= */ 0.1f,
                    /* stringRadius= */ 0.01f,
                    /* bowBendingImpl= */ BowBendingImplType.Auto,
                    /* fallbackHandleWidth= */ 0.0625f,
                    /* fallbackStringTop= */ new Vector3(0.005f, 0.75f, -0.25f),
                    /* fallbackStringBottom= */ new Vector3(0.005f, -0.75f, -0.25f),
                    /* fallbackHandleTop= */ new Vector3(0, 0.4f, 0),
                    /* fallbackHandleBottom= */ new Vector3(0, -0.4f, 0))
            }
        };

        public static BowAnatomy getBowAnatomy(string bowName)
        {
            if (BowAnatomies.ContainsKey(bowName))
            {
                return BowAnatomies[bowName];
            }
            else
            {
                return DefaultBowAnatomy;
            }
        }

        protected BowAnatomy(
            float handleHeight,
            float softLimbHeight,
            float stringRadius,
            BowBendingImplType bowBendingImpl,
            float fallbackHandleWidth,
            Vector3 fallbackStringTop,
            Vector3 fallbackStringBottom,
            Vector3 fallbackHandleTop,
            Vector3 fallbackHandleBottom)
        {
            this.handleHeight = handleHeight;
            this.softLimbHeight = softLimbHeight;
            this.stringRadius = stringRadius;
            this.bowBendingImpl = bowBendingImpl;
            this.fallbackHandleWidth = fallbackHandleWidth;
            this.fallbackStringTop = fallbackStringTop;
            this.fallbackStringBottom = fallbackStringBottom;
            this.fallbackHandleTop = fallbackHandleTop;
            this.fallbackHandleBottom = fallbackHandleBottom;
        }
    }
}
