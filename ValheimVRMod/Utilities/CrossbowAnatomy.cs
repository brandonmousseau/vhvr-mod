using System.Collections.Generic;
using UnityEngine;

namespace ValheimVRMod.Utilities {
    public class CrossbowAnatomy
    {
        public readonly Vector3 hardLimbLeft;
        public readonly Vector3 hardLimbRight;
        public readonly Vector3 restingStringLeft;
        public readonly Vector3 restingStringRight;
        public readonly Vector3 restingNockingPoint;
        public readonly Vector3 anchorPoint;
        public readonly float maxBendAngleRadians;
        public readonly float softLimbHeight;
        public readonly float stringRadius;
        public readonly float boltCenterToTailDistance;
        // The local axis the left limb rotates around (the right limb rotates the opposite way) as the string is
        // drawn, i.e. bowRight x bowForward. Crossbows point along local +Y, so this is +Z for them.
        public readonly Vector3 limbBendAxis;

        private static CrossbowAnatomy GoldCrossbowAnatomy = new CrossbowAnatomy(
            /* hardLimbLeft= */ new Vector3(-0.17f, 1.58f, 0),
            /* hardLimbRight= */ new Vector3(0.17f, 1.58f, 0),
            /* restingStringLeft= */ new Vector3(-0.7f, 1.255f, -0.053f),
            /* restingStringRight= */ new Vector3(0.7f, 1.255f, -0.053f),
            /* restingNockingPoint= */ new Vector3(0, 1.25f, -0.053f),
            /* anchorPoint= */  new Vector3(0, 0.739f, -0.06f),
            /* maxBendAngleRadians= */ 0.31f,
            /* softLimbHeight= */ 0.01f,
            /* stringRadius= */ 0.0075f,
            /* boltCenterToTailDistance= */ 0.51f);

        private static Dictionary<string, CrossbowAnatomy> anatomies = new Dictionary<string, CrossbowAnatomy>
        {
            // Note the suffix order differs from the gold bows: crossbows put "gold" last.
            { "$item_crossbow_gold", GoldCrossbowAnatomy },                 // Nord Crossbow
            { "$item_crossbow_bloodlightning_gold", GoldCrossbowAnatomy },  // Thunderblood Crossbow
            { "$item_crossbow_frostfire_gold", GoldCrossbowAnatomy },       // Frostfire Crossbow
            {
                "$item_crossbow_arbalest", // Arbalest position: (0.0, 0.0, -1.0) rotation: (0.7, 0.0, 0.0, 0.7) bound center: (0.0, 0.8, 0.0) bound extends: (0.6, 0.8, 0.1)            
                new CrossbowAnatomy(
                    /* hardLimbLeft= */ new Vector3(-0.1425f, 1.475f, 0),
                    /* hardLimbRight= */ new Vector3(0.1425f, 1.475f, 0),
                    /* restingStringLeft= */ new Vector3(-0.625f, 1.255f, -0.05f),
                    /* restingStringRight= */ new Vector3(0.625f, 1.255f, -0.05f),
                    /* restingNockingPoint= */ new Vector3(0, 1.255f, -0.05f),
                    /* anchorPoint= */  new Vector3(0, 0.69f, -0.05f),
                    /* maxBendAngleRadians= */ 0.28f,
                    /* softLimbHeight= */ 0.01f,
                    /* stringRadius= */ 0.005f,
                    /* boltCenterToTailDistance= */ 0.51f)
            },
            {
                "$item_crossbow_ripper",     
                new CrossbowAnatomy(
                    /* hardLimbLeft= */ new Vector3(-0.25f, 1.475f, 0),
                    /* hardLimbRight= */ new Vector3(0.25f, 1.475f, 0),
                    /* restingStringLeft= */ new Vector3(-0.625f, 1.19f, -0.0525f),
                    /* restingStringRight= */ new Vector3(0.625f, 1.19f, -0.0525f),
                    /* restingNockingPoint= */ new Vector3(0, 1.19f, -0.0525f),
                    /* anchorPoint= */  new Vector3(0, 0.69f, -0.05f),
                    /* maxBendAngleRadians= */ 0.21f,
                    /* softLimbHeight= */ 0.025f,
                    /* stringRadius= */ 0.0075f,
                    /* boltCenterToTailDistance= */ 0.51f)
            },
            {
                "$item_crossbow_ripper_blood",
                new CrossbowAnatomy(
                    /* hardLimbLeft= */ new Vector3(-0.2f, 1.475f, 0),
                    /* hardLimbRight= */ new Vector3(0.2f, 1.475f, 0),
                    /* restingStringLeft= */ new Vector3(-0.625f, 1.19f, -0.0525f),
                    /* restingStringRight= */ new Vector3(0.625f, 1.19f, -0.0525f),
                    /* restingNockingPoint= */ new Vector3(0, 1.19f, -0.0525f),
                    /* anchorPoint= */  new Vector3(0, 0.69f, -0.05f),
                    /* maxBendAngleRadians= */ 0.4f,
                    /* softLimbHeight= */ 0.01f,
                    /* stringRadius= */ 0.0075f,
                    /* boltCenterToTailDistance= */ 0.51f)
            },
            {
                "$item_crossbow_ripper_lightning",
                new CrossbowAnatomy(
                    /* hardLimbLeft= */ new Vector3(-0.2f, 1.475f, 0),
                    /* hardLimbRight= */ new Vector3(0.2f, 1.475f, 0),
                    /* restingStringLeft= */ new Vector3(-0.625f, 1.19f, -0.0525f),
                    /* restingStringRight= */ new Vector3(0.625f, 1.19f, -0.0525f),
                    /* restingNockingPoint= */ new Vector3(0, 1.19f, -0.0525f),
                    /* anchorPoint= */  new Vector3(0, 0.69f, -0.05f),
                    /* maxBendAngleRadians= */ 0.31f,
                    /* softLimbHeight= */ 0.01f,
                    /* stringRadius= */ 0.0075f,
                    /* boltCenterToTailDistance= */ 0.51f)
            },
            {
                "$item_crossbow_ripper_nature",
                new CrossbowAnatomy(
                    /* hardLimbLeft= */ new Vector3(-0.2f, 1.475f, 0),
                    /* hardLimbRight= */ new Vector3(0.2f, 1.475f, 0),
                    /* restingStringLeft= */ new Vector3(-0.625f, 1.19f, -0.0525f),
                    /* restingStringRight= */ new Vector3(0.625f, 1.19f, -0.0525f),
                    /* restingNockingPoint= */ new Vector3(0, 1.19f, -0.0525f),
                    /* anchorPoint= */  new Vector3(0, 0.69f, -0.05f),
                    /* maxBendAngleRadians= */ 0.31f,
                    /* softLimbHeight= */ 0.01f,
                    /* stringRadius= */ 0.0075f,
                    /* boltCenterToTailDistance= */ 0.51f)
            },
            {
                // Unlike the crossbows, the grappling hook points along local +Z (limbs still along X, "up" is +Y).
                // Unloaded mesh bound center: (0.00, -0.26, 0.57) bound extends: (0.64, 0.38, 0.85).
                // TODO: educated guess mapping the ripper layout onto that frame; tune against the actual model.
                "$item_graplinghook",
                new CrossbowAnatomy(
                    /* hardLimbLeft= */ new Vector3(-0.25f, 0, 1f),
                    /* hardLimbRight= */ new Vector3(0.25f, 0, 1f),
                    /* restingStringLeft= */ new Vector3(-0.625f, 0.115f, 0.74f),
                    /* restingStringRight= */ new Vector3(0.625f, 0.10f, 0.74f),
                    /* restingNockingPoint= */ new Vector3(0, 0.1075f, 0.74f),
                    /* anchorPoint= */  new Vector3(0, 0.105f, 0.25f),
                    /* maxBendAngleRadians= */ 0.5f,
                    /* softLimbHeight= */ 0.01f,
                    /* stringRadius= */ 0.0075f,
                    // Unused: the grappling hook fires its own projectile and never shows a bolt.
                    /* boltCenterToTailDistance= */ 0.51f,
                    /* limbBendAxis= */ Vector3.down)
            }
        };

        public static CrossbowAnatomy getAnatomy(string name)
        {
            anatomies.TryGetValue(name, out var anatomy);
            return anatomy;
        }

        protected CrossbowAnatomy(
            Vector3 hardLimbLeft,
            Vector3 hardLimbRight,
            Vector3 restingStringLeft,
            Vector3 restingStringRight,
            Vector3 restingNockingPoint,
            Vector3 anchorPoint,
            float maxBendAngleRadians,
            float softLimbHeight,
            float stringRadius,
            float boltCenterToTailDistance,
            Vector3? limbBendAxis = null)
        {
            this.limbBendAxis = limbBendAxis ?? Vector3.forward;
            this.hardLimbLeft = hardLimbLeft;
            this.hardLimbRight = hardLimbRight;
            this.restingStringLeft = restingStringLeft;
            this.restingStringRight = restingStringRight;
            this.restingNockingPoint = restingNockingPoint;
            this.anchorPoint = anchorPoint;
            this.maxBendAngleRadians = maxBendAngleRadians;
            this.softLimbHeight = softLimbHeight;
            this.stringRadius = stringRadius;
            this.boltCenterToTailDistance = boltCenterToTailDistance;
        }
    }
}
