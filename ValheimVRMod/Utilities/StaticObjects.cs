using UnityEngine;
using ValheimVRMod.Scripts;
using ValheimVRMod.VRCore;
using Object = UnityEngine.Object;

namespace ValheimVRMod.Utilities {
    public static class StaticObjects {
        
        private static MainWeaponCollision _leftWeaponCollider;
        private static MainWeaponCollision _rightWeaponCollider;
        private static FistCollision _leftFist;
        private static FistCollision _rightFist;
        public static GameObject leftHandQuickMenu;
        public static GameObject rightHandQuickMenu;
        private static GameObject _shieldObj;
        private static GameObject _mouthCollider;
        
        public static Vector3 lastHitPoint;
        public static Vector3 lastHitDir;
        public static Collider lastHitCollider;
        
        public static MainWeaponCollision leftWeaponCollider() {
            return getCollisionScriptCube(ref _leftWeaponCollider);
        }
        
        public static MainWeaponCollision rightWeaponCollider() {
            return getCollisionScriptCube(ref _rightWeaponCollider);
        }
        
        public static FistCollision leftFist() {
            return getCollisionScriptSphere(ref _leftFist);
        }
        
        public static FistCollision rightFist() {
            return getCollisionScriptSphere(ref _rightFist);
        }
        
        private static T getCollisionScriptCube<T>(ref T collisionScript) where T : Component{
            
            if (collisionScript != null) {
                return collisionScript;
            }
            
            var collisionObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(collisionObj.GetComponent<MeshRenderer>());
            collisionObj.GetComponent<BoxCollider>().isTrigger = true;
            collisionObj.layer = LayerUtils.CHARACTER;
            Rigidbody rigidbody = collisionObj.AddComponent<Rigidbody>();
            rigidbody.useGravity = false;
            return collisionScript = collisionObj.AddComponent<T>();
        } 
        
        private static T getCollisionScriptSphere<T>(ref T collisionScript) where T : Component{
            
            if (collisionScript != null) {
                return collisionScript;
            }
            
            var collisionObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(collisionObj.GetComponent<MeshRenderer>());
            collisionObj.GetComponent<SphereCollider>().isTrigger = true;
            collisionObj.layer = LayerUtils.CHARACTER;
            Rigidbody rigidbody = collisionObj.AddComponent<Rigidbody>();
            rigidbody.useGravity = false;
            return collisionScript = collisionObj.AddComponent<T>();
        } 
        
        public static void addQuickMenus() {
            leftHandQuickMenu = new GameObject();
            leftHandQuickMenu.AddComponent<LeftHandQuickMenu>();
            leftHandQuickMenu.SetActive(false);

            rightHandQuickMenu = new GameObject();
            rightHandQuickMenu.AddComponent<RightHandQuickMenu>();
            rightHandQuickMenu.SetActive(false);
        }

        public static GameObject shieldObj() {
            
            if (_shieldObj != null) {
                return _shieldObj;
            }
            
            _shieldObj = new GameObject();
            return _shieldObj;
        }
        
        public static void mouthCollider(Transform head) {
            
            if (_mouthCollider != null) {
                return;
            }
            
            _mouthCollider = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(_mouthCollider.GetComponent<MeshRenderer>());
            _mouthCollider.GetComponent<BoxCollider>().isTrigger = true;
            _mouthCollider.layer = LayerUtils.CHARACTER;
            _mouthCollider.name = "MouthCollider";
            _mouthCollider.transform.parent = head;
            _mouthCollider.transform.localPosition = new Vector3(0,-0.06f,0.04f);
            _mouthCollider.transform.localRotation = Quaternion.identity;
            _mouthCollider.transform.localScale = new Vector3(0.06f, 0.03f, 0.06f);
        }
    }
}