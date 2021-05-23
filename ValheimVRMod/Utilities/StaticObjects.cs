using UnityEngine;
using ValheimVRMod.Scripts;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Utilities {
    public static class StaticObjects {
        
        private static GameObject _weaponCollider;
        public static GameObject quickSwitch;
        private static CooldownScript _leftCoolDown;
        private static CooldownScript _rightCoolDown;
        private static GameObject _shieldObj;
        
        public static GameObject weaponCollider() {
            if (_weaponCollider != null) {
                return _weaponCollider;
            }

            _weaponCollider = GameObject.CreatePrimitive(PrimitiveType.Cube);
#if ! DEBUG
            Destroy(_collisionCube.GetComponent<MeshRenderer>());
#endif
            _weaponCollider.GetComponent<BoxCollider>().isTrigger = true;
            Rigidbody rigidbody = _weaponCollider.AddComponent<Rigidbody>();
            rigidbody.useGravity = false;
            _weaponCollider.AddComponent<CollisionDetection>();
            _weaponCollider.layer = LayerUtils.WEAPON_LAYER;
            return _weaponCollider;
        }
        
        public static void addQuickSwitch(Transform hand) {
            quickSwitch = new GameObject();
            quickSwitch.transform.SetParent(hand, false);
            quickSwitch.transform.localPosition = new Vector3(0, 0.071f, 0.0123f);
            quickSwitch.transform.localRotation = Quaternion.Euler(107.34299f, -0.22f, -50.0f);
            quickSwitch.AddComponent<QuickSwitch>();
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