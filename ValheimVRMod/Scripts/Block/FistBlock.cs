using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts.Block {
    public class FistBlock : Block {
        private const float MIN_PARRY_SPEED = 1f;

        private Collider leftHandBlockBox;
        private Collider rightHandBlockBox;
        private EquipType? currentEquipType = null;
        private MeshRenderer leftHandBlockBoxRenderer;
        private MeshRenderer rightHandBlockBoxRenderer;

        public static FistBlock instance;

        protected void OnDisable()
        {
            instance = null;
        }

        protected override void Awake() {
            base.Awake();
            _meshCooldown = gameObject.AddComponent<MeshCooldown>();
            instance = this;
            hand = VHVRConfig.LeftHanded() ? VRPlayer.leftHand.transform : VRPlayer.rightHand.transform;
            offhand = VHVRConfig.LeftHanded() ? VRPlayer.rightHand.transform : VRPlayer.leftHand.transform;
            CreateBlockBoxes();
        }

        protected override void FixedUpdate() {
            base.FixedUpdate();
            // TODO: maybe move this to VRPlayer.FixedUpdate()
            fadeHitIndicator(Time.fixedDeltaTime);
        }

        public override void setBlocking(HitData hitData) {
            if (VHVRConfig.UseRealisticBlock())
            {
                bool blockedWithLeftHand =
                    StaticObjects.leftFist().blockingWithFist() && hitIntersectsBlockBox(hitData, leftHandBlockBox);
                bool blockedWithRightHand =
                    StaticObjects.rightFist().blockingWithFist() && hitIntersectsBlockBox(hitData, rightHandBlockBox);

                if (blockedWithLeftHand || blockedWithRightHand)
                {
                    _blocking = true;
                }

                CheckParryMotion(hitData.m_dir, blockedWithLeftHand, blockedWithRightHand);
            }
            else if (FistCollision.hasDualWieldingWeaponEquipped() && EquipScript.getRight() != EquipType.Claws)
            {
                var leftAngle = Vector3.Angle(hitData.m_dir, VRPlayer.leftHandBone.right);
                var rightAngle = Vector3.Angle(hitData.m_dir, VRPlayer.rightHandBone.right);
                _blocking = leftAngle > 60 && leftAngle < 120 && rightAngle > 60 && rightAngle < 120;
                CheckParryMotion(hitData.m_dir, true, true);
            }
            else if (StaticObjects.leftFist().blockingWithFist() && StaticObjects.rightFist().blockingWithFist())
            {
                var leftHandDir = VRPlayer.leftPointer.rayDirection * Vector3.forward;
                var rightHandDir = VRPlayer.rightPointer.rayDirection * Vector3.forward;
                var up = Player.m_localPlayer ? Player.m_localPlayer.transform.up : Vector3.up;
                var left = -Player.m_localPlayer.transform.right;
                var right = Player.m_localPlayer.transform.right;

                var leftHandtoUp = Vector3.Dot(up, leftHandDir);
                var leftHandtoHit = Vector3.Dot(hitData.m_dir, leftHandDir);
                var leftHandtoRight = Vector3.Dot(right, leftHandDir);
                var leftHandLateralOffset = Vector3.Dot(VRPlayer.leftHand.transform.position - Player.m_localPlayer.transform.position, right);
                var leftHandBlock = leftHandtoHit > -0.6f && leftHandtoHit < 0.6f && leftHandtoUp > -0.1f && leftHandtoRight > -0.5f && leftHandLateralOffset > -0.2f;

                var rightHandtoUp = Vector3.Dot(up, rightHandDir);
                var rightHandtoHit = Vector3.Dot(hitData.m_dir, rightHandDir);
                var rightHandtoLeft = Vector3.Dot(left, rightHandDir);
                var rightHandLateralOffset = Vector3.Dot(VRPlayer.rightHand.transform.position - Player.m_localPlayer.transform.position, right);
                var rightHandBlock = rightHandtoHit > -0.6f && rightHandtoHit < 0.6f && rightHandtoUp > -0.1f && rightHandtoLeft > -0.5f && rightHandLateralOffset < 0.2f;

                _blocking = leftHandBlock && rightHandBlock;
                CheckParryMotion(hitData.m_dir, true, true);
            }
        }

        public void updateBlockBoxShape()
        {
            if (EquipScript.getRight() == currentEquipType)
            {
                return;
            }

            currentEquipType = EquipScript.getRight();

            var colliderData = WeaponUtils.GetDualWieldLeftHandBlockingColliderData(Player.m_localPlayer?.GetRightItem());

            leftHandBlockBox.transform.localPosition = colliderData.pos;
            rightHandBlockBox.transform.localPosition = Vector3.Reflect(colliderData.pos, Vector3.right);
            leftHandBlockBox.transform.localRotation = rightHandBlockBox.transform.localRotation =
                Quaternion.Euler(colliderData.euler);
            leftHandBlockBox.transform.localScale = rightHandBlockBox.transform.localScale =
                colliderData.scale;

            RefreshDebugRenderers();
        }

        private void CheckParryMotion(Vector3 hitDir, bool blockedWithLeftHand, bool blockedWithRightHand)
        {
            // Only consider the component of the velocity perpendicular to the hit direction as parrying speed.
            float leftHandParrySpeed = Vector3.ProjectOnPlane(VRPlayer.leftHandPhysicsEstimator.GetVelocity(), hitDir).magnitude;
            float rightHandParrySpeed = Vector3.ProjectOnPlane(VRPlayer.rightHandPhysicsEstimator.GetVelocity(), hitDir).magnitude;

            if (blockedWithLeftHand && leftHandParrySpeed > MIN_PARRY_SPEED && StaticObjects.leftFist().blockingWithFist())
            {
                blockTimer = blockTimerParry;
            }
            else if (blockedWithRightHand && rightHandParrySpeed > MIN_PARRY_SPEED && StaticObjects.rightFist().blockingWithFist())
            {
                blockTimer = blockTimerParry;
            }
            else
            {
                blockTimer = blockTimerNonParry;
            }
        }
        
        private void CreateBlockBoxes()
        {
            leftHandBlockBox = GameObject.CreatePrimitive(PrimitiveType.Cube).GetComponent<Collider>();
            leftHandBlockBox.transform.parent = VRPlayer.leftHandBone;
            leftHandBlockBox.isTrigger = true;
            leftHandBlockBox.gameObject.layer = LayerUtils.CHARACTER;
            rightHandBlockBox = GameObject.CreatePrimitive(PrimitiveType.Cube).GetComponent<Collider>();
            rightHandBlockBox.transform.parent = VRPlayer.rightHandBone;
            rightHandBlockBox.isTrigger = true;
            rightHandBlockBox.gameObject.layer = LayerUtils.CHARACTER;

            leftHandBlockBoxRenderer = leftHandBlockBox.GetComponent<MeshRenderer>();
            rightHandBlockBoxRenderer = rightHandBlockBox.GetComponent<MeshRenderer>();
            leftHandBlockBoxRenderer.material = Object.Instantiate(VRAssetManager.GetAsset<Material>("Unlit"));
            leftHandBlockBoxRenderer.material.color = new Vector4(0.5f, 0.25f, 0, 0.5f);
            rightHandBlockBoxRenderer.material = Object.Instantiate(VRAssetManager.GetAsset<Material>("Unlit"));
            rightHandBlockBoxRenderer.material.color = new Vector4(0.5f, 0.25f, 0, 0.5f);

            RefreshDebugRenderers();
        }

        private void RefreshDebugRenderers()
        {
            var leftHandBlockBoxRenderer = leftHandBlockBox.GetComponent<MeshRenderer>();
            var rightHandBlockBoxRenderer = rightHandBlockBox.GetComponent<MeshRenderer>();
            if (VHVRConfig.ShowDebugColliders())
            {
                leftHandBlockBoxRenderer.enabled = true;
                rightHandBlockBoxRenderer.enabled = true;
            }
            else
            {
                leftHandBlockBoxRenderer.enabled = false;
                rightHandBlockBoxRenderer.enabled = false;
            }
        }
    }
}
