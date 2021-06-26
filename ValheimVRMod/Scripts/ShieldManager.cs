using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;


namespace ValheimVRMod.Scripts {
    public class ShieldManager : MonoBehaviour {
        
        public string _name;
        
        private const float cooldown = 1;
        private static bool _blocking;
        public static float blockTimer;
        private static ShieldManager instance;
        private static MeshCooldown _meshCooldown;

        private const int MAX_SNAPSHOTS = 7;
        private int tickCounter;
        private List<Vector3> snapshots = new List<Vector3>();

        private void Awake() {
            _meshCooldown = gameObject.AddComponent<MeshCooldown>();
            instance = this;
        }

        public static void setBlocking(Vector3 hitDir) {
            _blocking = Vector3.Dot(hitDir, instance.getForward()) > 0.5f;
        }

        public static void resetBlocking() {
            _blocking = false;
            blockTimer = 9999f;
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
            Vector3 posEnd = VRPlayer.leftHand.transform.position;
            Vector3 posStart = VRPlayer.leftHand.transform.position;

            foreach (Vector3 snapshot in snapshots) {
                var curDist = Vector3.Distance(snapshot, posEnd);
                if (curDist > dist) {
                    dist = curDist;
                    posStart = snapshot;
                }
            }

            Transform lHand = VRPlayer.leftHand.transform;
            Vector3 shieldPos = (snapshots[snapshots.Count - 1] + (-lHand.right / 2) );

            if (Vector3.Distance(posEnd, posStart) > 0.4f) {
                if (Vector3.Angle(shieldPos - snapshots[0] , snapshots[snapshots.Count - 1] - snapshots[0]) < 25) {
                    blockTimer = 0.1f;
                }
                
            } else {
                blockTimer = 9999f;
            }
        }
        private void FixedUpdate() {
            tickCounter++;
            if (tickCounter < 5) {
                return;
            }
            snapshots.Add(VRPlayer.leftHand.transform.position);

            if (snapshots.Count > MAX_SNAPSHOTS) {
                snapshots.RemoveAt(0);
            }

            tickCounter = 0;

            ParryCheck();
        }

        private void OnRenderObject() {
            StaticObjects.shieldObj().transform.rotation = transform.rotation;
        }
    }
}