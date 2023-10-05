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

        private static Dictionary<string, CrossbowAnatomy> anatomies = new Dictionary<string, CrossbowAnatomy>
        {
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
            }
        };

        public static CrossbowAnatomy getAnatomy(string name)
        {
            return anatomies[name];
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
            float boltCenterToTailDistance)
        {
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
