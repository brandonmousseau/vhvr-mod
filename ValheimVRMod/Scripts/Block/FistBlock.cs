using UnityEngine;
using UnityEngine.Rendering;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts.Block {
    public class FistBlock : Block {
        private const float MIN_PARRY_SPEED = 1f;
        private readonly Vector3 handUp = new Vector3(0, -0.15f, -0.85f);

        private GameObject leftHandBlockBox;
        private GameObject rightHandBlockBox;
        private MeshRenderer hitIndicator; // Renderer of disk indicating the position, direction, and block tolerance of an attack.

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

        void OnDestroy()
        {
            if (hitIndicator?.gameObject)
            {
                Destroy(hitIndicator.gameObject);
            }
        }

        public override void setBlocking(HitData hitData) {
            if (VHVRConfig.BlockingType() == "Realistic")
            {
                float blockTolerance = GetBlockTolerance(hitData.m_damage, hitData.m_pushForce);

                hitIndicator.gameObject.SetActive(true);
                hitIndicator.transform.SetPositionAndRotation(hitData.m_point, Quaternion.LookRotation(hitData.m_dir));
                hitIndicator.transform.localScale = new Vector3(blockTolerance * 2, blockTolerance * 2, 0.001f);
                hitIndicator.material.color = new Color(1, 0, 0, 0.75f);

                Bounds leftBlockBounds = leftHandBlockBox.GetComponent<MeshFilter>().sharedMesh.bounds;
                (leftBlockBounds = new Bounds(leftBlockBounds.center, leftBlockBounds.size)).Expand(blockTolerance);
                Bounds rightBlockBounds = rightHandBlockBox.GetComponent<MeshFilter>().sharedMesh.bounds;
                (rightBlockBounds = new Bounds(rightBlockBounds.center, rightBlockBounds.size)).Expand(blockTolerance);
                bool blockedWithLeftHand = WeaponUtils.LineIntersectsWithBounds(leftBlockBounds, leftHandBlockBox.transform.InverseTransformPoint(hitData.m_point), leftHandBlockBox.transform.InverseTransformDirection(hitData.m_dir));
                bool blockedWithRightHand = WeaponUtils.LineIntersectsWithBounds(rightBlockBounds, rightHandBlockBox.transform.InverseTransformPoint(hitData.m_point), rightHandBlockBox.transform.InverseTransformDirection(hitData.m_dir));

                if (!FistCollision.instance.usingDualKnives() && !FistCollision.instance.usingFistWeapon())
                {
                    _blocking = false;
                }
                else if (blockedWithLeftHand || blockedWithRightHand)
                {
                    _blocking = true;
                }

                CheckParryMotion(hitData.m_dir, blockedWithLeftHand, blockedWithRightHand);
            }
            else if (FistCollision.instance.usingDualKnives())
            {
                var leftAngle = Vector3.Dot(hitData.m_dir, offhand.TransformDirection(handUp));
                var rightAngle = Vector3.Dot(hitData.m_dir, hand.TransformDirection(handUp));
                var leftHandBlock = (leftAngle > -0.5f && leftAngle < 0.5f);
                var rightHandBlock = (rightAngle > -0.5f && rightAngle < 0.5f);
                _blocking = leftHandBlock && rightHandBlock;
                CheckParryMotion(hitData.m_dir, true, true);
            }
            else if (FistCollision.instance.usingFistWeapon())
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

        private void CheckParryMotion(Vector3 hitDir, bool blockedWithLeftHand, bool blockedWithRightHand)
        {
            if (!FistCollision.instance.usingFistWeapon()) {
                return;
            }

            // Only consider the component of the velocity perpendicular to the hit direction as parrying speed.
            float leftHandParrySpeed = Vector3.ProjectOnPlane(VRPlayer.leftHandPhysicsEstimator.GetVelocity(), hitDir).magnitude;
            float rightHandParrySpeed = Vector3.ProjectOnPlane(VRPlayer.rightHandPhysicsEstimator.GetVelocity(), hitDir).magnitude;

            if (blockedWithLeftHand && leftHandParrySpeed > MIN_PARRY_SPEED)
            {
                blockTimer = blockTimerParry;
            }
            else if (blockedWithRightHand && rightHandParrySpeed > MIN_PARRY_SPEED)
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
            leftHandBlockBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftHandBlockBox.transform.parent = VRPlayer.leftHand.transform;
            leftHandBlockBox.transform.localRotation = Quaternion.Euler(45, 0, 0);
            leftHandBlockBox.transform.localPosition = new Vector3(0, 0.15f, -0.2f);
            leftHandBlockBox.transform.localScale = new Vector3(0.3f, 0.3f, 0.85f);
            leftHandBlockBox.GetComponent<MeshRenderer>().enabled = false;
            Destroy(leftHandBlockBox.GetComponent<Collider>());

            rightHandBlockBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightHandBlockBox.transform.parent = VRPlayer.rightHand.transform;
            rightHandBlockBox.transform.localRotation = Quaternion.Euler(45, 0, 0);
            rightHandBlockBox.transform.localPosition = new Vector3(0, 0.15f, -0.2f);
            rightHandBlockBox.transform.localScale = new Vector3(0.3f, 0.3f, 0.85f);
            rightHandBlockBox.GetComponent<MeshRenderer>().enabled = false;
            Destroy(rightHandBlockBox.GetComponent<Collider>());
        }

        private void CreateHitIndicator()
        {
            hitIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere).GetComponent<MeshRenderer>();
            GameObject.Destroy(hitIndicator.gameObject.GetComponent<Collider>());
            Material material = hitIndicator.material;
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.SetFloat("_Glossiness", 0);
            material.SetFloat("_Metallic", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int) RenderQueue.Overlay;
            hitIndicator.material = material;
            hitIndicator.gameObject.layer = LayerUtils.getWorldspaceUiLayer();
            hitIndicator.receiveShadows = false;
            hitIndicator.shadowCastingMode = ShadowCastingMode.Off;
            hitIndicator.lightProbeUsage = LightProbeUsage.Off;
            hitIndicator.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }
}
