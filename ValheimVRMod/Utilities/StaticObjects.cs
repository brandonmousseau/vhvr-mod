using UnityEngine;
using ValheimVRMod.Scripts;
using ValheimVRMod.VRCore;
using Object = UnityEngine.Object;

namespace ValheimVRMod.Utilities {
    public static class StaticObjects {
        
        private static WeaponCollision _leftWeaponCollider;
        private static WeaponCollision _rightWeaponCollider;
        private static FistCollision _leftFist;
        private static FistCollision _rightFist;
        public static GameObject quickActions;
        public static GameObject quickSwitch;
        private static GameObject _shieldObj;
        
        public static Vector3 lastHitPoint;
        public static Vector3 lastHitDir;
        public static Collider lastHitCollider;
        
        public static WeaponCollision leftWeaponCollider() {
            return getCollisionScript(ref _leftWeaponCollider);
        }
        
        public static WeaponCollision rightWeaponCollider() {
            return getCollisionScript(ref _rightWeaponCollider);
        }
        
        public static FistCollision leftFist() {
            return getCollisionScript(ref _leftFist);
        }
        
        public static FistCollision rightFist() {
            return getCollisionScript(ref _rightFist);
        }
        
        private static T getCollisionScript<T>(ref T collisionScript) where T : Component{
            
            if (collisionScript != null) {
                return collisionScript;
            }
            
            var collisionObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(collisionObj.GetComponent<MeshRenderer>());
            collisionObj.GetComponent<BoxCollider>().isTrigger = true;
            Rigidbody rigidbody = collisionObj.AddComponent<Rigidbody>();
            rigidbody.useGravity = false;
            return collisionScript = collisionObj.AddComponent<T>();
        } 
        
        public static void addQuickActions(Transform hand) {
            quickActions = new GameObject();
            quickActions.AddComponent<QuickActions>().parent = hand;
            quickActions.SetActive(false);
        }
        
        public static void addQuickSwitch(Transform hand) {
            quickSwitch = new GameObject();
            quickSwitch.AddComponent<QuickSwitch>().parent = hand;
            quickSwitch.SetActive(false);
        }

        public static GameObject shieldObj() {
            
            if (_shieldObj != null) {
                return _shieldObj;
            }
            
            _shieldObj = new GameObject();
            return _shieldObj;
        }
    }
}