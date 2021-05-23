using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts {
    public class ShieldManager : MonoBehaviour {
        
        private const float COOLDOWN = 5.0f;
        public string _name;

        private static bool _blocking;
        private static ShieldManager instance;

        private void Awake() {
            StaticObjects.leftCooldown().maxCooldown = COOLDOWN;
            instance = this;
        }

        public static void setBlocking(Vector3 hitDir) {
            _blocking = Vector3.Dot(hitDir, instance.getForward()) > 0.5f;
        }

        public static void resetBlocking() {
            _blocking = false;
        }

        public static bool isBlocking() {
            return _blocking && ! StaticObjects.leftCooldown().isInCooldown();
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

        private void FixedUpdate() {
            
            StaticObjects.shieldObj().transform.SetParent(transform, false);
            StaticObjects.shieldObj().transform.localRotation = Quaternion.identity;
            StaticObjects.shieldObj().transform.SetParent(null, true);
            
        }
    }
}