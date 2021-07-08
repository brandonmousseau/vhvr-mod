using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts {
    public class ShieldManager : MonoBehaviour {
        
        public string _name;
        
        private const float cooldown = 1;
        private const float blockTimerParry = 0.1f;
        public const float blockTimerTolerance = blockTimerParry + 0.2f;
        private const float blockTimerNonParry = 9999f;
        private const float minDist = 0.4f;
        private const float maxParryAngle = 25f;
        private static bool _blocking;
        public static float blockTimer;
        private static ShieldManager instance;
        private static MeshCooldown _meshCooldown;

        private const int MAX_SNAPSHOTS = 7;
        private int tickCounter;
        private List<Vector3> snapshots = new List<Vector3>();
        private static float scaling = 1f;
        private static Vector3 posRef;
        private static Vector3 scaleRef;

        private void Awake() {
            _meshCooldown = gameObject.AddComponent<MeshCooldown>();
            instance = this;
            posRef = transform.localPosition;
            scaleRef = transform.localScale;
        }

        public static void setBlocking(Vector3 hitDir) {
            _blocking = Vector3.Dot(hitDir, instance.getForward()) > 0.5f;
        }

        public static void resetBlocking() {
            _blocking = false;
            blockTimer = blockTimerNonParry;
        }

        public static bool isBlocking() {
            return _blocking && ! _meshCooldown.inCoolDown();
        }
        
        public static void block() {
            _meshCooldown.tryTrigger(cooldown);
        }
        
        private Vector3 getForward() {
            
            switch (_name) {
                case "ShieldWood":
                case "ShieldBanded":
                    return StaticObjects.shieldObj().transform.forward;
                case "ShieldKnight":
                    return -StaticObjects.shieldObj().transform.right;
                case "ShieldBronzeBuckler":
                    return -StaticObjects.shieldObj().transform.up;
                default:
                    return -StaticObjects.shieldObj().transform.forward;
            }
        }
        private void ParryCheck() {
            var dist = 0.0f;
            Vector3 posEnd = Player.m_localPlayer.transform.InverseTransformPoint(VRPlayer.leftHand.transform.position);
            Vector3 posStart = Player.m_localPlayer.transform.InverseTransformPoint(VRPlayer.leftHand.transform.position);

            foreach (Vector3 snapshot in snapshots) {
                var curDist = Vector3.Distance(snapshot, posEnd);
                if (curDist > dist) {
                    dist = curDist;
                    posStart = snapshot;
                }
            }

            Vector3 shieldPos = (snapshots[snapshots.Count - 1] + (Player.m_localPlayer.transform.InverseTransformDirection(-VRPlayer.leftHand.transform.right) / 2) );

            if (Vector3.Distance(posEnd, posStart) > minDist) {
                if (Vector3.Angle(shieldPos - snapshots[0] , snapshots[snapshots.Count - 1] - snapshots[0]) < maxParryAngle) {
                    blockTimer = blockTimerParry;
                }
                
            } else {
                blockTimer = blockTimerNonParry;
            }
        }
        private void FixedUpdate() {
            tickCounter++;
            if (tickCounter < 5) {
                return;
            }
            snapshots.Add(Player.m_localPlayer.transform.InverseTransformPoint(VRPlayer.leftHand.transform.position));

            if (snapshots.Count > MAX_SNAPSHOTS) {
                snapshots.RemoveAt(0);
            }

            tickCounter = 0;

            ParryCheck();
        }

        private void OnRenderObject() {
            if(scaling!=1f) {
                transform.localScale = scaleRef * scaling;
                transform.localPosition = CalculatePos();
            }else if(transform.localPosition != posRef||transform.localScale !=scaleRef) {
                transform.localScale = scaleRef;
                transform.localPosition = posRef;
            }
            StaticObjects.shieldObj().transform.rotation = transform.rotation;
        }
        public static void ScaleShieldSize(float scale)
        {
            scaling = scale;
        }
        private Vector3 CalculatePos()
        {
            return VRPlayer.leftHand.transform.InverseTransformDirection(VRPlayer.leftHand.transform.TransformDirection(posRef) *(scaleRef * scaling).x);
        }
    }
}