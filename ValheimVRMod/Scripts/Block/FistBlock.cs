using UnityEngine;
using UnityEngine.Rendering;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts.Block {
    public class FistBlock : Block {
        private const float MIN_PARRY_SPEED = 1f;
        private readonly Vector3 handUp = new Vector3(0, -0.15f, -0.85f);

        private Collider leftHandBlockBox;
        private Collider rightHandBlockBox;
        private MeshRenderer hitIndicator; // Renderer of disk indicating the position, direction, and block tolerance of an attack.
        private EquipType? currentEquipType = null;

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

            CreateHitIndicator();
        }

        protected override void FixedUpdate() {
            base.FixedUpdate();
            if (!hitIndicator.gameObject.activeSelf)
            {
                return;
            }

            Color color = hitIndicator.material.color;
            if (color.a <= 0.05f)
            {
                hitIndicator.gameObject.SetActive(false);
                return;
            }

            // Fade the hit indicator gradually.
            hitIndicator.material.color = new Color(color.r, color.g, color.b, color.a * (1 - Time.fixedDeltaTime * 3));
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (hitIndicator != null)
            {
                Destroy(hitIndicator.gameObject);
            }
        }

        public override void setBlocking(HitData hitData) {
            if (VHVRConfig.BlockingType() == "Realistic")
            {
                float blockTolerance = GetBlockTolerance(hitData.m_damage, hitData.m_pushForce);
                hitIndicator.gameObject.SetActive(true);
                hitIndicator.transform.SetPositionAndRotation(
                    hitData.m_point, Quaternion.LookRotation(hitData.m_dir) * Quaternion.Euler(90, 0, 0));
                hitIndicator.transform.localScale = new Vector3(blockTolerance * 2, 0.001f, blockTolerance * 2);
                hitIndicator.material.color = new Color(1, 0, 0, 0.75f);

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
                var leftAngle = Vector3.Dot(hitData.m_dir, offhand.TransformDirection(handUp));
                var rightAngle = Vector3.Dot(hitData.m_dir, hand.TransformDirection(handUp));
                var leftHandBlock = (leftAngle > -0.5f && leftAngle < 0.5f);
                var rightHandBlock = (rightAngle > -0.5f && rightAngle < 0.5f);
                _blocking = leftHandBlock && rightHandBlock;
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
            rightHandBlockBox = GameObject.CreatePrimitive(PrimitiveType.Cube).GetComponent<Collider>();
            rightHandBlockBox.transform.parent = VRPlayer.rightHandBone;
            rightHandBlockBox.isTrigger = true;

            var leftHandBlockBoxRenderer = leftHandBlockBox.GetComponent<MeshRenderer>();
            var rightHandBlockBoxRenderer = rightHandBlockBox.GetComponent<MeshRenderer>();
            if (VHVRConfig.ShowDebugColliders())
            {
                leftHandBlockBoxRenderer.material = Object.Instantiate(VRAssetManager.GetAsset<Material>("Unlit"));
                leftHandBlockBoxRenderer.material.color = new Vector4(0.5f, 0.25f, 0, 0.5f);
                rightHandBlockBoxRenderer.material = Object.Instantiate(VRAssetManager.GetAsset<Material>("Unlit"));
                rightHandBlockBoxRenderer.material.color = new Vector4(0.5f, 0.25f, 0, 0.5f);
            }
            else
            {
                Destroy(leftHandBlockBoxRenderer);
                Destroy(rightHandBlockBoxRenderer);
            }
        }

        private void CreateHitIndicator()
        {
            hitIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder).GetComponent<MeshRenderer>();
            GameObject.Destroy(hitIndicator.gameObject.GetComponent<Collider>());
            hitIndicator.material = Instantiate(VRAssetManager.GetAsset<Material>("Unlit"));
            hitIndicator.gameObject.layer = LayerUtils.getWorldspaceUiLayer();
            hitIndicator.receiveShadows = false;
            hitIndicator.shadowCastingMode = ShadowCastingMode.Off;
            hitIndicator.lightProbeUsage = LightProbeUsage.Off;
            hitIndicator.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }
}
