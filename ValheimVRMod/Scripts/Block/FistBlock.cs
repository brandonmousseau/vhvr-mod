using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts.Block {
    public class FistBlock : Block {

        private const float maxParryAngle = 45f;
        
        public static FistBlock instance;

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
            //_blocking = Vector3.Dot(hitDir, getForward()) > 0.5f;
            if(FistCollision.instance.usingFistWeapon())
            {
                var leftHandDir = VRPlayer.leftPointer.rayDirection * Vector3.forward;
                var rightHandDir = VRPlayer.rightPointer.rayDirection * Vector3.forward;
                var up = Player.m_localPlayer ? Player.m_localPlayer.transform.up : Vector3.up;
                var left = -Player.m_localPlayer.transform.right;
                var right = Player.m_localPlayer.transform.right;

                var leftHandtoUp = Vector3.Dot(up, leftHandDir);
                var leftHandtoHit = Vector3.Dot(hitDir, leftHandDir);
                var leftHandtoRight = Vector3.Dot(right, leftHandDir);
                var leftHandBlock = (leftHandtoHit > -0.6f && leftHandtoHit < 0.6f) && (leftHandtoUp > -0.1f) && (leftHandtoRight > -0.1f); ;

                var rightHandtoUp = Vector3.Dot(up, rightHandDir);
                var rightHandtoHit = Vector3.Dot(hitDir, rightHandDir);
                var rightHandtoLeft = Vector3.Dot(left, rightHandDir);
                var rightHandBlock = (rightHandtoHit > -0.6f && rightHandtoHit < 0.6f) && (rightHandtoUp > -0.1f) && (rightHandtoLeft > -0.1f);

                _blocking = leftHandBlock && rightHandBlock;
            }
            else
            {
                _blocking = false;
            }
        }

        protected override void ParryCheck(Vector3 posStart, Vector3 posEnd, Vector3 posStart2, Vector3 posEnd2) {
            if (FistCollision.instance.usingFistWeapon())
            {
                if (Vector3.Distance(posEnd, posStart) > minDist)
                {
                    blockTimer = blockTimerParry;
                }
                else if (Vector3.Distance(posEnd2, posStart2) > minDist)
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
}