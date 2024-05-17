using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts
{
    public class GesturedLocomotionManager
    {
        private const float STICK_OUTPUT_WEIGHT = 1f;
        private const float GROUND_SPEED_CHANGE_DAMPER = 0.25f;
        private const float WATER_SPEED_CHANGE_DAMPER = 1f;
        private const float RUN_ACITIVATION_SPEED = 1.375f;
        private const float RUN_DEACTIVATION_SPEED = 1.125f;
        private const float MIN_WATER_SPEED = 0.0625f;

        public float stickOutputX { get; private set; } = 0;
        public float stickOutputY { get; private set; } = 0;
        public bool isRunning { get; private set; } = false;

        private Transform vrCameraRig;
        private readonly GesturedLocomotion[] gesturedLocomotions;
        private Vector3 gesturedLocomotionVelocity = Vector3.zero;
        private float horizontalSpeed = 0;
        public static bool isInUse
        {
            get
            {
                return SteamVR_Actions.valheim_StopGesturedLocomotion.activeBinding &&
              !SteamVR_Actions.valheim_StopGesturedLocomotion.GetState(SteamVR_Input_Sources.Any);
            }
        }

        public GesturedLocomotionManager(Transform vrCameraRig)
        {
            this.vrCameraRig = vrCameraRig;
            gesturedLocomotions =
                new GesturedLocomotion[] {
                    new GesturedSwim(vrCameraRig),
                    new GesturedJump(vrCameraRig),
                    new MaximizingGesturedLocomotion(
                        vrCameraRig,
                        new LeftHandGesturedWalkRun(vrCameraRig),
                        new RightHandGesturedWalkRun(vrCameraRig))};
        }

        public void UpdateMovementFromGestures(float deltaTime)
        {
            Player localPlayer = Player.m_localPlayer;
            if (VHVRConfig.NonVrPlayer() || !VHVRConfig.UseVrControls() || localPlayer == null)
            {
                stickOutputX = stickOutputY = 0;
                isRunning = false;
                return;
            }

            Vector3 targetVelocity = Vector3.zero;
            foreach (GesturedLocomotion locomotion in gesturedLocomotions)
            {
                targetVelocity += locomotion.GetTargetVelocityFromGestures(localPlayer);
            }

            bool isJumping = Vector3.Dot(targetVelocity, vrCameraRig.up) > VHVRConfig.GesturedJumpMinSpeed();

            float damper = localPlayer.IsSwimming() ? WATER_SPEED_CHANGE_DAMPER : GROUND_SPEED_CHANGE_DAMPER;

            gesturedLocomotionVelocity =
                Vector3.Lerp(gesturedLocomotionVelocity, targetVelocity, deltaTime / damper);

            horizontalSpeed = Vector3.ProjectOnPlane(gesturedLocomotionVelocity, vrCameraRig.up).magnitude;

            if (localPlayer.IsSwimming() && horizontalSpeed < MIN_WATER_SPEED)
            {
                stickOutputX = stickOutputY = 0;
            }
            else
            {
                Vector3 stickYDirection = Vector3.ProjectOnPlane(-localPlayer.transform.forward, vrCameraRig.up).normalized;
                Vector3 stickXDirection = Vector3.Cross(stickYDirection, vrCameraRig.up);
                stickOutputX = Vector3.Dot(gesturedLocomotionVelocity, stickXDirection) * STICK_OUTPUT_WEIGHT;
                stickOutputY = Vector3.Dot(gesturedLocomotionVelocity, stickYDirection) * STICK_OUTPUT_WEIGHT;
            }

            if (horizontalSpeed > RUN_ACITIVATION_SPEED)
            {
                isRunning = true;
            }
            else if (horizontalSpeed < RUN_DEACTIVATION_SPEED)
            {
                isRunning = false;
            }

            if (isJumping && localPlayer.IsOnGround())
            {
                localPlayer.Jump();
            }
        }

        abstract class GesturedLocomotion
        {
            protected Vector3 upDirection { get { return vrCameraRig.up; } }
            protected Transform leftHandTransform { get { return VRPlayer.leftHand.transform; } }
            protected Transform rightHandTransform { get { return VRPlayer.rightHand.transform; } }
            protected Vector3 leftHandVelocity { get { return VRPlayer.leftHandPhysicsEstimator.GetVelocity(); } }
            protected Vector3 rightHandVelocity { get { return VRPlayer.rightHandPhysicsEstimator.GetVelocity(); } }

            private Transform vrCameraRig;

            public GesturedLocomotion(Transform vrCameraRig)
            {
                this.vrCameraRig = vrCameraRig;
            }

            public abstract Vector3 GetTargetVelocityFromGestures(Player player);
        }

        class GesturedSwim : GesturedLocomotion
        {
            private float HAND_PROPULSION_DEADZONE = 0.5f;
            private float MIN_SWIM_SPEED = 0.375f;

            public GesturedSwim(Transform vrCameraRig) : base(vrCameraRig) { }

            public override Vector3 GetTargetVelocityFromGestures(Player player)
            {
                if (!VHVRConfig.IsGesturedSwimEnabled() || !player.IsSwimming())
                {
                    return Vector3.zero;
                }
                float liquidLevel = Player.m_localPlayer.GetLiquidLevel();
                Vector3 velocity = Vector3.zero;
                if (!SteamVR_Actions.valheim_StopGesturedLocomotion.GetState(SteamVR_Input_Sources.LeftHand) &&
                    leftHandTransform.position.y < liquidLevel)
                {
                    Vector3 leftHandPalmar = leftHandTransform.right;
                    float leftHandPropulsion = Vector3.Dot(leftHandVelocity, leftHandPalmar);
                    leftHandPropulsion = Mathf.Max(0, leftHandPropulsion - HAND_PROPULSION_DEADZONE);
                    velocity += -leftHandVelocity.normalized * leftHandPropulsion;
                }
                if (!SteamVR_Actions.valheim_StopGesturedLocomotion.GetState(SteamVR_Input_Sources.RightHand) &&
                    rightHandTransform.position.y < liquidLevel)
                {
                    Vector3 rightHandPalmar = -rightHandTransform.right;
                    float rightHandPropulsion = Vector3.Dot(rightHandVelocity, rightHandPalmar);
                    rightHandPropulsion = Mathf.Max(0, rightHandPropulsion - HAND_PROPULSION_DEADZONE);
                    velocity += -rightHandVelocity.normalized * rightHandPropulsion;
                }
                velocity = Vector3.ProjectOnPlane(velocity, upDirection);
                return velocity.magnitude >= MIN_SWIM_SPEED ? velocity : Vector3.zero;
            }
        }

        class GesturedJump : GesturedLocomotion
        {
            private const float HORIZONTAL_SPEED_DEADZONE_FACTOR = 0.125f;

            private bool isPreparingJump = false;
            private Vector3 jumpVelocity = Vector3.zero;

            public GesturedJump(Transform vrCameraRig) : base(vrCameraRig) { }

            public override Vector3 GetTargetVelocityFromGestures(Player player)
            {
                if (!VHVRConfig.IsGesturedJumpEnabled() || player.IsAttached())
                {
                    return Vector3.zero;
                }

                var height = Valve.VR.InteractionSystem.Player.instance.eyeHeight;

                bool wasPreparingJump = isPreparingJump;

                isPreparingJump = height < VRPlayer.referencePlayerHeight * VHVRConfig.GesturedJumpPreparationHeight();

                if (IsInAir(player))
                {
                    return Vector3.ProjectOnPlane(jumpVelocity, upDirection);
                }

                bool attemptingJump =
                    wasPreparingJump && !isPreparingJump && player.IsOnGround() &&
                    (!SteamVR_Actions.valheim_StopGesturedLocomotion.GetState(SteamVR_Input_Sources.LeftHand) ||
                        !SteamVR_Actions.valheim_StopGesturedLocomotion.GetState(SteamVR_Input_Sources.RightHand));

                if (attemptingJump)
                {
                    Vector3 velocity = VRPlayer.headPhysicsEstimator.GetVelocity();
                    Vector3 verticalVelocty = Vector3.Project(velocity, upDirection);
                    float verticalSpeed = verticalVelocty.magnitude;
                    Vector3 horizontalVelocity = Vector3.ProjectOnPlane(velocity, upDirection);
                    float horizontalSpeed = horizontalVelocity.magnitude;
                    float horizontalSpeedDeadzone = verticalSpeed * HORIZONTAL_SPEED_DEADZONE_FACTOR;
                    if (horizontalSpeed > horizontalSpeedDeadzone)
                    {
                        horizontalVelocity *= (horizontalSpeed - horizontalSpeedDeadzone) / horizontalSpeed;
                    }
                    else
                    {
                        horizontalVelocity = Vector3.zero;
                    }
                    jumpVelocity = horizontalVelocity + verticalVelocty;
                }
                else
                {
                    jumpVelocity = Vector3.zero;
                }

                return jumpVelocity;
            }

            private static bool IsInAir(Player player)
            {
                return !player.IsAttached() && !player.IsSwimming() && !player.IsOnGround();
            }
        }

        class MaximizingGesturedLocomotion : GesturedLocomotion
        {

            private GesturedLocomotion a;
            private GesturedLocomotion b;

            public MaximizingGesturedLocomotion(Transform vrCameraRig, GesturedLocomotion a, GesturedLocomotion b) : base(vrCameraRig)
            {
                this.a = a;
                this.b = b;
            }

            public override Vector3 GetTargetVelocityFromGestures(Player player)
            {
                Vector3 vA = a.GetTargetVelocityFromGestures(player);
                Vector3 vB = b.GetTargetVelocityFromGestures(player);
                return (vA + vB).normalized * Mathf.Max(vA.magnitude, vB.magnitude);
            }
        }

        class LeftHandGesturedWalkRun : GesturedWalkRun {

            public LeftHandGesturedWalkRun(Transform vrCameraRig) : base(vrCameraRig)
            {
                new GameObject("LeftHandWalkingWheel").AddComponent<WalkRunIndicator>().Init(true, this);
            }

            protected override SteamVR_Input_Sources inputSource { get { return SteamVR_Input_Sources.LeftHand; } }
            protected override Vector3 handVelocity { get { return VRPlayer.leftHandPhysicsEstimator.GetVelocity(); } }
            protected override Transform handTransform { get { return VRPlayer.leftHand.transform; } }
            protected override Transform otherHandTransform { get { return VRPlayer.rightHand.transform; } }
        }

        class RightHandGesturedWalkRun : GesturedWalkRun {

            public RightHandGesturedWalkRun(Transform vrCameraRig) : base(vrCameraRig) 
            {
                new GameObject("RightHandWalkingWheel").AddComponent<WalkRunIndicator>().Init(false, this);
            }

            protected override SteamVR_Input_Sources inputSource { get { return SteamVR_Input_Sources.RightHand; } }
            protected override Vector3 handVelocity { get { return VRPlayer.rightHandPhysicsEstimator.GetVelocity(); } }
            protected override Transform handTransform { get { return VRPlayer.rightHand.transform; } }
            protected override Transform otherHandTransform { get { return VRPlayer.leftHand.transform; } }
        }

        abstract class GesturedWalkRun : GesturedLocomotion
        {
            private const float MIN_HAND_SPEED = 0.125f;
            private const float HEAD_TILT_STRAFE_DEADZONE = 0.125f;
            private const float HEAD_TILT_STRAFE_WEIGHT = 2f;
            private const float MIN_ACTIVATION_HAND_DISTANCE = 0.375f;

            private Camera vrCam;
            private bool isWalkingOrRunningUsingGestures = false;

            protected abstract SteamVR_Input_Sources inputSource { get; }
            protected abstract Vector3 handVelocity { get; }
            protected abstract Transform handTransform { get; }

            protected abstract Transform otherHandTransform { get; }

            public GesturedWalkRun(Transform vrCameraRig) : base(vrCameraRig) { }

            public Vector3 movementVerticalPlaneNormal { get; private set; }

            public override Vector3 GetTargetVelocityFromGestures(Player player)
            {
                if (!VHVRConfig.IsGesturedWalkRunEnabled())
                {
                    return Vector3.zero;
                }

                Vector3 handVelocity = this.handVelocity;
                float handSpeed = handVelocity.magnitude;
                Vector3 clampedHandVelocity = handSpeed > 1 ? handVelocity / handSpeed : handVelocity;

                // Use both hand pointing direction and hand movement direction to decide walk direction
                Vector3 walkDirection = handTransform.forward * 2;
                walkDirection += (Vector3.Dot(walkDirection, clampedHandVelocity) > 0 ? clampedHandVelocity : -clampedHandVelocity);
                walkDirection = Vector3.ProjectOnPlane(walkDirection, upDirection).normalized;

                movementVerticalPlaneNormal = Vector3.Cross(upDirection, walkDirection).normalized;
                Vector3 wheelDiameter = Vector3.ProjectOnPlane(handTransform.position - otherHandTransform.position, movementVerticalPlaneNormal);

                float walkSpeed =
                    Vector3.Dot(Vector3.Cross(wheelDiameter.normalized, handVelocity), movementVerticalPlaneNormal);

                if (ShouldStop(player, handSpeed))
                {
                    isWalkingOrRunningUsingGestures = false;
                }
                else if (ShouldStart(wheelDiameter, walkDirection, handSpeed))
                {
                    isWalkingOrRunningUsingGestures = true;
                }

                return isWalkingOrRunningUsingGestures ? ApplyHeadTiltStrafe(walkDirection) * walkSpeed : Vector3.zero;
            }

            private Vector3 ApplyHeadTiltStrafe(Vector3 walkDirection)
            {
                if (vrCam == null)
                {
                    vrCam = CameraUtils.getCamera(CameraUtils.VR_CAMERA);
                    if (vrCam == null)
                    {
                        return walkDirection;
                    }
                }
                var heading = Vector3.ProjectOnPlane(vrCam.transform.forward, upDirection);
                var strafe = Vector3.ProjectOnPlane(Vector3.ProjectOnPlane(vrCam.transform.up, heading) - upDirection, upDirection);
                var strafeAmount = strafe.magnitude;
                if (strafeAmount < HEAD_TILT_STRAFE_DEADZONE)
                {
                    return walkDirection;
                }

                strafe -= strafe * HEAD_TILT_STRAFE_DEADZONE / strafeAmount;
                return (walkDirection + strafe * HEAD_TILT_STRAFE_WEIGHT).normalized;
            }

            private bool isStoppingWalkRunByButton()
            {
                if (!SteamVR_Actions.valheim_StopGesturedLocomotion.activeBinding)
                {
                    // If the steam action has not been set, disable gestured walk because there will be
                    // otherwise no way to stop it.
                    return true;
                }
                return SteamVR_Actions.valheim_StopGesturedLocomotion.GetState(inputSource);
            }

            private bool ShouldStart(Vector3 wheelDiameter, Vector3 walkDirection, float handSpeed)
            {
                return !isStoppingWalkRunByButton() && handSpeed > MIN_HAND_SPEED &&
                    wheelDiameter.y < -MIN_ACTIVATION_HAND_DISTANCE;
            }

            private bool ShouldStop(Player player, double handSpeed)
            {
                if (isStoppingWalkRunByButton() || Player.m_localPlayer.m_attached)
                {
                    return true;
                }

                if (player.IsSwimming() && !player.IsOnGround())
                {
                    return true;
                }

                // Stop gestured walking if hand movements are slow.
                return isWalkingOrRunningUsingGestures && handSpeed < MIN_HAND_SPEED / 2;
            }

            // "Hand wheels" displayed to indicate the activity and direction of gestured walking.
            protected class WalkRunIndicator : HexagonWheel
            {
                private const bool ENABLE_DEBUG_WALK_RUN_INDICATOR = false;
                private bool isInLeftHand;
                private Transform hand { get { return isInLeftHand ? VRPlayer.leftHand.transform : VRPlayer.rightHand.transform; } }
                private Transform otherHand { get { return isInLeftHand ? VRPlayer.rightHand.transform : VRPlayer.leftHand.transform; } }
                private GesturedWalkRun gesturedWalkRun;
 
                public void Init(bool isInLeftHand, GesturedWalkRun gesturedWalkRun)
                {
                    this.isInLeftHand = isInLeftHand;
                    this.gesturedWalkRun = gesturedWalkRun;
                }

                void Update()
                {
                    if (!ENABLE_DEBUG_WALK_RUN_INDICATOR)
                    {
                        return;
                    }

                    if (gesturedWalkRun == null)
                    {
                        Destroy(this.gameObject);
                    }

                    if (VRPlayer.leftHand == null || VRPlayer.rightHand == null || !gesturedWalkRun.isWalkingOrRunningUsingGestures)
                    {
                        lineRenderer.enabled = false;
                        return;
                    }

                    lineRenderer.enabled = true;
                    transform.position = hand.position;
                    Vector3 diameter = Vector3.ProjectOnPlane(otherHand.position - transform.position, gesturedWalkRun.movementVerticalPlaneNormal);
                    transform.LookAt(transform.position + diameter, gesturedWalkRun.movementVerticalPlaneNormal);
                    transform.localScale = Vector3.one * diameter.magnitude;
                }
            }

            protected class HexagonWheel : MonoBehaviour
            {
                private static readonly Vector3[] HEXAGON_VERTICES =
                    new Vector3[]
                    {
                        Vector3.zero,
                        new Vector3(Mathf.Sqrt(3) / 4, 0, 0.25f),
                        new Vector3(Mathf.Sqrt(3) / 4, 0, 0.75f),
                        Vector3.forward,
                        new Vector3(-Mathf.Sqrt(3) / 4, 0, 0.75f),
                        new Vector3(-Mathf.Sqrt(3) / 4, 0, 0.25f),
                        Vector3.zero
                    };
                protected LineRenderer lineRenderer { get; private set; }

                void Awake()
                {
                    CreateHexagonWheel();
                }


                private void CreateHexagonWheel()
                {
                    lineRenderer = gameObject.AddComponent<LineRenderer>();
                    lineRenderer.useWorldSpace = false;
                    lineRenderer.widthMultiplier = 0.006f;
                    lineRenderer.positionCount = HEXAGON_VERTICES.Length;
                    lineRenderer.material.color = Color.red;
                    for (int i = 0; i < HEXAGON_VERTICES.Length; i++)
                    {
                        lineRenderer.SetPosition(i, HEXAGON_VERTICES[i]);
                    }
                }
            }
        }
    }
}
