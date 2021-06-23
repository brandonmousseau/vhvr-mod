using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts {
    public class ShieldManager : MonoBehaviour {
        
        public string _name;
        
        private const float cooldown = 1;
        private static bool _blocking;
        private static ShieldManager instance;
        private static MeshCooldown _meshCooldown;

        private void Awake() {
            _meshCooldown = gameObject.AddComponent<MeshCooldown>();
            instance = this;
        }

        public static void setBlocking(Vector3 hitDir) {
            _blocking = Vector3.Dot(hitDir, instance.getForward()) > 0.5f;
        }

        public static void resetBlocking() {
            _blocking = false;
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

        private void OnRenderObject() {
            StaticObjects.shieldObj().transform.rotation = transform.rotation;
        }
    }
}