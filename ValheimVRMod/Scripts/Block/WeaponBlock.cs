using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts.Block {
    public class WeaponBlock : Block {
        
        public WeaponWield weaponWield;
        public static WeaponBlock instance;

        private void OnDisable() {
            instance = null;
        }
        
        private void Awake() {
            _meshCooldown = gameObject.AddComponent<MeshCooldown>();
            instance = this;
            hand = VHVRConfig.LeftHanded() ? VRPlayer.leftHand.transform : VRPlayer.rightHand.transform;
        }

        public override void setBlocking(Vector3 hitDir) {
            var angle = Vector3.Dot(hitDir, weaponWield.weaponForward);
            _blocking = weaponWield.allowBlocking() && angle > -0.5f && angle < 0.5f ;
        }
        
        protected override void ParryCheck(Vector3 posStart, Vector3 posEnd) {
            if (Vector3.Distance(posEnd, posStart) > minDist) {
                blockTimer = blockTimerParry;
            } else {
                blockTimer = blockTimerNonParry;
            }
        }
    }
}
