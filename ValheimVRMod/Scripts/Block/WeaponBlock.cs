using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts.Block {
    public class WeaponBlock : Block {
        
        public WeaponWield weaponWield;
        public static WeaponBlock instance;
        private readonly Vector3 handUp = new Vector3(0, -0.15f, -0.85f);

        private void OnDisable() {
            instance = null;
        }
        
        private void Awake() {
            _meshCooldown = gameObject.AddComponent<MeshCooldown>();
            instance = this;
            hand = VHVRConfig.LeftHanded() ? VRPlayer.leftHand.transform : VRPlayer.rightHand.transform;
            offhand = VHVRConfig.LeftHanded() ? VRPlayer.rightHand.transform : VRPlayer.leftHand.transform;
        }

        public override void setBlocking(Vector3 hitDir) {
            var angle = Vector3.Dot(hitDir, weaponWield.weaponForward);
            if (weaponWield.isLeftHandWeapon())
            {
                var leftAngle = Vector3.Dot(hitDir, offhand.TransformDirection(handUp));
                var rightAngle = Vector3.Dot(hitDir, hand.TransformDirection(handUp));
                var leftHandBlock = (leftAngle > -0.5f && leftAngle < 0.5f) ;
                var rightHandBlock = (rightAngle > -0.5f && rightAngle < 0.5f);
                _blocking = leftHandBlock && rightHandBlock;
            }
            else
            {
                if (VHVRConfig.BlockingType() == "GrabButton")
                {
                    bool isShieldorWeaponBlock = (EquipScript.getLeft() != EquipType.Shield) || (EquipScript.getLeft() == EquipType.Shield && weaponWield.allowBlocking());
                    _blocking = isShieldorWeaponBlock && angle > -0.5f && angle < 0.5f;
                }
                else
                {
                    _blocking = weaponWield.allowBlocking() && angle > -0.5f && angle < 0.5f;
                }
            }
            
        }
        
        protected override void ParryCheck(Vector3 posStart, Vector3 posEnd, Vector3 posStart2, Vector3 posEnd2) {
            if (Vector3.Distance(posEnd, posStart) > minDist) 
            {
                blockTimer = blockTimerParry;
            }
            else if (weaponWield.isLeftHandWeapon() && Vector3.Distance(posEnd2, posStart2) > minDist)
            {
                blockTimer = blockTimerParry;
            }
            else 
            {
                blockTimer = blockTimerNonParry;
            }
        }
    }
}
