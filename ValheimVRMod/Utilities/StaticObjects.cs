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
        private static FootCollision _leftFootCollision;
        private static FootCollision _rightFootCollision;
        public static GameObject leftHandQuickMenu;
        public static GameObject rightHandQuickMenu;
        private static GameObject _shieldObj;
        private static GameObject _mouthCollider;
        
        public static Vector3 lastHitPoint;
        public static Vector3 lastHitDir;
        public static Collider lastHitCollider;
        
        public static WeaponCollision leftWeaponCollider() {
            return getCollisionScriptCube(ref _leftWeaponCollider);
        }
        
        public static WeaponCollision rightWeaponCollider() {
            return getCollisionScriptCube(ref _rightWeaponCollider);
        }
        
        public static FistCollision leftFist() {
            return getCollisionScriptSphere(ref _leftFist);
        }
        
        public static FistCollision rightFist() {
            return getCollisionScriptSphere(ref _rightFist);
        }

        public static FootCollision leftFootCollision()
        {
            return getCollisionScriptCube(ref _leftFootCollision);
        }

        public static FootCollision rightFootCollision()
        {
            return getCollisionScriptCube(ref _rightFootCollision);
        }

        private static T getCollisionScriptCube<T>(ref T collisionScript) where T : Component{
            
            if (collisionScript != null) {
                return collisionScript;
            }
            
            var collisionObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(collisionObj.GetComponent<MeshRenderer>());
            collisionObj.GetComponent<BoxCollider>().isTrigger = true;
            // Use this layer to make sure the weapon collides with all targets including soft building pieces and plants.
            collisionObj.layer = LayerUtils.VHVR_WEAPON;
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
            // Use this layer to make sure the weapon collides with all targets including soft building pieces and plants.
            collisionObj.layer = LayerUtils.VHVR_WEAPON;
            Rigidbody rigidbody = collisionObj.AddComponent<Rigidbody>();
            rigidbody.useGravity = false;
            return collisionScript = collisionObj.AddComponent<T>();
        } 
        
        public static void destroyQuickMenus()
        {
            if (leftHandQuickMenu != null)
            {
                GameObject.Destroy(leftHandQuickMenu);
                leftHandQuickMenu = null;
            }

            if (rightHandQuickMenu != null)
            {
                GameObject.Destroy(rightHandQuickMenu);
                rightHandQuickMenu = null;
            }
        }

        public static void addQuickMenus() {
            destroyQuickMenus();
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

        public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();

        }
    }
}
