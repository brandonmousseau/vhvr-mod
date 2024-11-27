using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts.Block {
    public class ShieldBlock : Block {

        public string itemName;
        private const float MIN_PARRY_ENTRY_SPEED = 1.5f;
        private const float MAX_PARRY_ANGLE = 150f;
        private const float PARRY_EXIT_SPEED = 0.2f;
        private const int PARRY_CHECK_INTERVAL = 3;
        private static float PARRY_WINDOW_EASING_FACTOR { get { return VHVRConfig.UseRealisticBlock() ? 0.5f : VHVRConfig.UseGestureBlock() ? 0.75f : 1; } }

        private float scaling = 1f;
        private Vector3 posRef;
        private Vector3 scaleRef;
        private bool attemptingParry;
        private int parryCheckFixedUpateTicker = 0;
        private Vector3 shieldFacing { get { return VHVRConfig.LeftHanded() ? VRPlayer.rightHand.transform.right : -VRPlayer.leftHand.transform.right; } }

        public static ShieldBlock instance;

        private void OnDisable() {
            instance = null;
        }
        
        protected override void Awake() {
            base.Awake();
            _meshCooldown = gameObject.AddComponent<MeshCooldown>();
            instance = this;
            InitShield();
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            // Parry time restriction is made more lenient for realistic blocking to balance difficulty.
            blockTimer += Time.fixedDeltaTime * PARRY_WINDOW_EASING_FACTOR;

            if (++parryCheckFixedUpateTicker >= PARRY_CHECK_INTERVAL)
            {
                CheckParryMotion();
                parryCheckFixedUpateTicker = 0;
            }
        }

        private void InitShield()
        {
            posRef = _meshCooldown.transform.localPosition;
            scaleRef = _meshCooldown.transform.localScale;
            hand = VHVRConfig.LeftHanded() ? VRPlayer.rightHand.transform : VRPlayer.leftHand.transform;
            offhand = VHVRConfig.LeftHanded() ? VRPlayer.leftHand.transform : VRPlayer.rightHand.transform;
        }

        public override void setBlocking(HitData hitData) {
            if (VHVRConfig.UseGrabButtonBlock())
            {
                _blocking = SteamVR_Actions.valheim_Grab.GetState(VRPlayer.nonDominantHandInputSource);
            }
            else if (VHVRConfig.UseRealisticBlock())
            {
                _blocking = Vector3.Dot(hitData.m_dir, shieldFacing) < -0.25f && hitIntersectsBlockBox(hitData);
                CheckParryMotion();
            }
            else {
                _blocking = Vector3.Dot(hitData.m_dir, shieldFacing) < -0.5f;
                CheckParryMotion();
            }
        }

        private void CheckParryMotion() {
            PhysicsEstimator handPhysicsEstimator = VHVRConfig.LeftHanded() ? VRPlayer.rightHandPhysicsEstimator : VRPlayer.leftHandPhysicsEstimator;
            float l = handPhysicsEstimator.GetLongestLocomotion(/* deltaT= */ 0.4f).magnitude;
            if (physicsEstimator.GetVelocity().magnitude > MIN_PARRY_ENTRY_SPEED && Vector3.Angle(physicsEstimator.GetVelocity(), shieldFacing) < MAX_PARRY_ANGLE) {
                if (!attemptingParry)
                {
                    blockTimer = 0;
                    attemptingParry = true;
                }
            }
            else if (attemptingParry && physicsEstimator.GetAverageVelocityInSnapshots().magnitude < PARRY_EXIT_SPEED)
            {
                blockTimer = blockTimerNonParry;
                attemptingParry = false;
            }
        }

        protected override void OnRenderObject() {
            base.OnRenderObject();
            if (scaling != 1f)
            {
                transform.localScale = scaleRef * scaling;
                transform.localPosition = CalculatePos();
            }
            else if (transform.localPosition != posRef || transform.localScale != scaleRef)
            {
                transform.localScale = scaleRef;
                transform.localPosition = posRef;
            }
            StaticObjects.shieldObj().transform.position = transform.position;
            StaticObjects.shieldObj().transform.rotation = transform.rotation;

            Vector3 v = physicsEstimator.GetVelocity();
        }

        public void ScaleShieldSize(float scale)
        {
            scaling = scale;
        }

        private Vector3 CalculatePos()
        {
            return VRPlayer.leftHand.transform.InverseTransformDirection(hand.TransformDirection(posRef) *(scaleRef * scaling).x);
        }
    }
}
