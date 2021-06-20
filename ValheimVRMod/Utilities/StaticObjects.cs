using UnityEngine;
using ValheimVRMod.Scripts;
using ValheimVRMod.VRCore;
using Object = UnityEngine.Object;

namespace ValheimVRMod.Utilities {
    public static class StaticObjects {
        
        private static WeaponCollision _weaponCollider;
        private static FistCollision _leftFist;
        private static FistCollision _rightFist;
        public static GameObject quickActions;
        public static GameObject quickSwitch;
        private static CooldownScript _leftCoolDown;
        private static CooldownScript _rightCoolDown;
        private static GameObject _shieldObj;
        
        public static Vector3 lastHitPoint;
        public static Collider lastHitCollider;
        
        public static WeaponCollision weaponCollider() {
            return getCollisionScript(ref _weaponCollider);
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
            return collisionScript = collisionObj.AddComponent<T>();;
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
        
        public static CooldownScript leftCooldown() {

            if (_leftCoolDown != null) {
                return _leftCoolDown;
            }
            
            GameObject go = new GameObject();
            go.transform.SetParent(VRPlayer.leftHand.transform, false);
            go.transform.localPosition = new Vector3(0.02f, 0.05f, -0.1f);
            go.transform.localRotation = Quaternion.Euler(90,0, 0);
            go.transform.localScale = new Vector2(4, 1);
            _leftCoolDown = go.AddComponent<CooldownScript>();
            return _leftCoolDown;
        }
        
        public static CooldownScript rightCooldown() {
            
            if (_rightCoolDown != null) {
                return _rightCoolDown;
            }
            
            GameObject go = new GameObject();
            go.transform.SetParent(VRPlayer.rightHand.transform, false);
            go.transform.localPosition = new Vector3(-0.02f, 0.05f, -0.1f);
            go.transform.localRotation = Quaternion.Euler(-90,0, 0);
            go.transform.localScale = new Vector2(4, 1);
            _rightCoolDown = go.AddComponent<CooldownScript>();
            return _rightCoolDown;
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