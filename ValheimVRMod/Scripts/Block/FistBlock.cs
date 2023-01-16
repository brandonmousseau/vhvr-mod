using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts.Block {
    public class FistBlock : Block {

        private const float maxParryAngle = 45f;
        private readonly Vector3 handUp = new Vector3(0, -0.15f, -0.85f);

        private GameObject leftHandBlockBox;
        private GameObject rightHandBlockBox;
        private LineRenderer debugHitRenderer; // A line indicating the position of direction of attack, used for debugging only.

        public static FistBlock instance;

        private void OnDisable() {
            instance = null;
        }
        
        protected override void Awake() {
            base.Awake();
            _meshCooldown = gameObject.AddComponent<MeshCooldown>();
            instance = this;
            hand = VHVRConfig.LeftHanded() ? VRPlayer.leftHand.transform : VRPlayer.rightHand.transform;
            offhand = VHVRConfig.LeftHanded() ? VRPlayer.rightHand.transform : VRPlayer.leftHand.transform;
            CreateBlockBoxes();

            debugHitRenderer = new GameObject().AddComponent<LineRenderer>();
            debugHitRenderer.useWorldSpace = true;
            debugHitRenderer.widthMultiplier = 0.006f;
            debugHitRenderer.positionCount = 2;
            debugHitRenderer.enabled = false;
        }

        void OnDestroy()
        {
            Destroy(debugHitRenderer.gameObject);
        }

        public override void setBlocking(Vector3 hitPoint, Vector3 hitDir) {
            debugHitRenderer.SetPosition(0, hitPoint);
            debugHitRenderer.SetPosition(1, hitPoint + hitDir.normalized);

            if (VHVRConfig.BlockingType() == "Realistic")
            {
                if (!FistCollision.instance.usingDualKnives() && !FistCollision.instance.usingFistWeapon())
                {
                    _blocking = false;
                }
                else if (WeaponUtils.LineIntersectsWithBounds(leftHandBlockBox.GetComponent<MeshFilter>().sharedMesh.bounds, leftHandBlockBox.transform.InverseTransformPoint(hitPoint), leftHandBlockBox.transform.InverseTransformDirection(hitDir)))
                {
                    _blocking = true;
                }
                else if (WeaponUtils.LineIntersectsWithBounds(rightHandBlockBox.GetComponent<MeshFilter>().sharedMesh.bounds, rightHandBlockBox.transform.InverseTransformPoint(hitPoint), rightHandBlockBox.transform.InverseTransformPoint(hitDir)))
                {
                    _blocking = true;
                }
            } else if (FistCollision.instance.usingDualKnives())
            {
                var leftAngle = Vector3.Dot(hitDir, offhand.TransformDirection(handUp));
                var rightAngle = Vector3.Dot(hitDir, hand.TransformDirection(handUp));
                var leftHandBlock = (leftAngle > -0.5f && leftAngle < 0.5f);
                var rightHandBlock = (rightAngle > -0.5f && rightAngle < 0.5f);
                _blocking = leftHandBlock && rightHandBlock;
            }
            else if (FistCollision.instance.usingFistWeapon())
            {
                var leftHandDir = VRPlayer.leftPointer.rayDirection * Vector3.forward;
                var rightHandDir = VRPlayer.rightPointer.rayDirection * Vector3.forward;
                var up = Player.m_localPlayer ? Player.m_localPlayer.transform.up : Vector3.up;
                var left = -Player.m_localPlayer.transform.right;
                var right = Player.m_localPlayer.transform.right;

                var leftHandtoUp = Vector3.Dot(up, leftHandDir);
                var leftHandtoHit = Vector3.Dot(hitDir, leftHandDir);
                var leftHandtoRight = Vector3.Dot(right, leftHandDir);
                var leftHandLateralOffset = Vector3.Dot(VRPlayer.leftHand.transform.position - Player.m_localPlayer.transform.position, right);
                var leftHandBlock = leftHandtoHit > -0.6f && leftHandtoHit < 0.6f && leftHandtoUp > -0.1f && leftHandtoRight > -0.5f && leftHandLateralOffset > -0.2f;

                var rightHandtoUp = Vector3.Dot(up, rightHandDir);
                var rightHandtoHit = Vector3.Dot(hitDir, rightHandDir);
                var rightHandtoLeft = Vector3.Dot(left, rightHandDir);
                var rightHandLateralOffset = Vector3.Dot(VRPlayer.rightHand.transform.position - Player.m_localPlayer.transform.position, right);
                var rightHandBlock = rightHandtoHit > -0.6f && rightHandtoHit < 0.6f && rightHandtoUp > -0.1f && rightHandtoLeft > -0.5f && rightHandLateralOffset < 0.2f;

                _blocking = leftHandBlock && rightHandBlock;
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
        
        private void CreateBlockBoxes()
        {
            leftHandBlockBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftHandBlockBox.transform.parent = VRPlayer.leftHand.transform;
            leftHandBlockBox.transform.localRotation = Quaternion.Euler(45, 0, 0);
            leftHandBlockBox.transform.localPosition = new Vector3(0, 0.15f, -0.2f);
            leftHandBlockBox.transform.localScale = new Vector3(0.25f, 0.25f, 0.8f);
            leftHandBlockBox.GetComponent<MeshRenderer>().enabled = false;
            Destroy(leftHandBlockBox.GetComponent<Collider>());

            rightHandBlockBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightHandBlockBox.transform.parent = VRPlayer.rightHand.transform;
            rightHandBlockBox.transform.localRotation = Quaternion.Euler(45, 0, 0);
            rightHandBlockBox.transform.localPosition = new Vector3(0, 0.15f, -0.2f);
            rightHandBlockBox.transform.localScale = new Vector3(0.25f, 0.25f, 0.8f);
            rightHandBlockBox.GetComponent<MeshRenderer>().enabled = false;
            Destroy(rightHandBlockBox.GetComponent<Collider>());
        }        
    }
}
