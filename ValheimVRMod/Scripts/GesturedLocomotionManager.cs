using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;


namespace ValheimVRMod.Scripts
{
    public class GesturedLocomotionManager
    {
        private const float STICK_OUTPUT_WEIGHT = 2f;
        private const float GROUND_SPEED_CHANGE_DAMPER = 0.25f;
        private const float WATER_SPEED_CHANGE_DAMPER = 1f;
        private const float AIR_SPEED_CHANGE_DAMPER = 4f;
        private const float MIN_RUN_SPEED = 1.5f;
        private const float MIN_VERTICAL_JUMP_SPEED = 2.5f;
        private const float MIN_WATER_SPEED = 0.0625f;

        public float stickOutputX { get; private set; } = 0;
        public float stickOutputY { get; private set; } = 0;
        public bool isRunning { get; private set; } = false;

        private Transform vrCameraRig;
        private readonly GesturedLocomotion[] gesturedLocomotions;
        private Vector3 gesturedLocomotionVelocity = Vector3.zero;
        private Vector3 gesturedSmoothAcceleration = Vector3.zero;
        private float horizontalSpeed = 0;

        public GesturedLocomotionManager(Transform vrCameraRig)
        {
            this.vrCameraRig = vrCameraRig;
            gesturedLocomotions =
                new GesturedLocomotion[] {
                    new GesturedWalkRun(vrCameraRig),
                    new GesturedSwim(vrCameraRig),
                    new GesturedJump(vrCameraRig) };
        }

        public void UpdateMovementFromGestures(float deltaTime)
        {
            Player localPlayer = Player.m_localPlayer;
            if (VHVRConfig.NonVrPlayer() || !VHVRConfig.UseVrControls() || !VHVRConfig.GesturedLocomotion() || localPlayer == null)
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

            bool isJumping = Vector3.Dot(targetVelocity, vrCameraRig.up) > MIN_VERTICAL_JUMP_SPEED;

            float damper =
                localPlayer.IsSwimming() ?
                    WATER_SPEED_CHANGE_DAMPER :
                    localPlayer.IsOnGround() ?
                        (isJumping ? 0 : GROUND_SPEED_CHANGE_DAMPER) :
                        AIR_SPEED_CHANGE_DAMPER;

            gesturedLocomotionVelocity =
                Vector3.SmoothDamp(
                    gesturedLocomotionVelocity,
                    targetVelocity,
                    ref gesturedSmoothAcceleration,
                    damper,
                    maxSpeed: Mathf.Infinity,
                    deltaTime);

            float previousHorizontalSpeed = horizontalSpeed;
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

            isRunning = previousHorizontalSpeed > MIN_RUN_SPEED || horizontalSpeed > MIN_RUN_SPEED;
            if (isJumping)
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

        class GesturedWalkRun : GesturedLocomotion
        {
            private const float MIN_WALK_SPEED = 0.25f;
            private const float MIN_HAND_SPEED = 0.25f;

            private bool isWalkingOrRunningUsingGestures = false;

            public GesturedWalkRun(Transform vrCameraRig) : base(vrCameraRig) { }

            public override Vector3 GetTargetVelocityFromGestures(Player player)
            {
                Vector3 combinedHandVelocity = rightHandVelocity - leftHandVelocity;

                // Use both hand pointing direction and hand movement direction to decide walk direction
                Vector3 movementDirection = leftHandTransform.forward + rightHandTransform.forward;
                movementDirection += (Vector3.Dot(movementDirection, combinedHandVelocity) > 0 ? combinedHandVelocity.normalized : -combinedHandVelocity.normalized);
                movementDirection = Vector3.ProjectOnPlane(movementDirection, upDirection).normalized;

                Vector3 movementVerticalPlaneNormal = Vector3.Cross(upDirection, movementDirection).normalized;
                Vector3 wheelDiameter = Vector3.ProjectOnPlane(rightHandTransform.position - leftHandTransform.position, movementVerticalPlaneNormal).normalized;

                float speed =
                    Vector3.Dot(
                        Vector3.Cross(wheelDiameter, combinedHandVelocity),
                        movementVerticalPlaneNormal) * 0.5f;

                if (ShouldStop(player, speed, leftHandVelocity, rightHandVelocity))
                {
                    isWalkingOrRunningUsingGestures = false;
                }
                else if (ShouldStart())
                {
                    isWalkingOrRunningUsingGestures = true;
                }

                return player.IsOnGround() && isWalkingOrRunningUsingGestures && Mathf.Abs(speed) > MIN_WALK_SPEED ? movementDirection * speed : Vector3.zero;
            }

            private bool ShouldStart()
            {
                if (!SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand) || !SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand))
                {
                    return false;
                }

                return SteamVR_Actions.valheim_UseLeft.GetState(SteamVR_Input_Sources.LeftHand) || SteamVR_Actions.valheim_Use.GetState(SteamVR_Input_Sources.RightHand);
            }

            private bool ShouldStop(Player player, float currentSpeed, Vector3 leftHandVelocity, Vector3 rightHandVelocity)
            {
                if (Player.m_localPlayer.m_attached)
                {
                    return true;
                }

                if (player.IsSwimming() && !player.IsOnGround())
                {
                    return true;
                }

                if (SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand) || SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand))
                {
                    return false;
                }

                return Mathf.Abs(currentSpeed) < MIN_WALK_SPEED && leftHandVelocity.magnitude < MIN_HAND_SPEED && rightHandVelocity.magnitude < MIN_HAND_SPEED;
            }
        }

        class GesturedJump : GesturedLocomotion
        {
            private const float HORIZONTAL_SPEED_DEADZONE = 0.75f;
            private const float FORWARD_CORRECTION_FACTOR = 0.25f;

            private bool isPreparingJump = false;
            private Vector3 jumpVelocity = Vector3.zero;

            public GesturedJump(Transform vrCameraRig) : base(vrCameraRig) { }

            public override Vector3 GetTargetVelocityFromGestures(Player player)
            {
                bool wasPreparingJump = isPreparingJump;
                isPreparingJump =
                    SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand) &&
                    SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand) &&
                    SteamVR_Actions.valheim_UseLeft.GetState(SteamVR_Input_Sources.LeftHand) &&
                    SteamVR_Actions.valheim_Use.GetState(SteamVR_Input_Sources.RightHand);

                if (IsInAir(player))
                {
                    return Vector3.ProjectOnPlane(jumpVelocity, upDirection);
                }

                bool attemptingJump = wasPreparingJump && !isPreparingJump && player.IsOnGround();

                if (attemptingJump)
                {
                    Vector3 velocity = -Vector3.Lerp(leftHandVelocity, rightHandVelocity, 0.5f);
                    Vector3 verticalVelocty = Vector3.Project(velocity, upDirection);
                    Vector3 horizontalVelocity = Vector3.ProjectOnPlane(velocity, upDirection);
                    horizontalVelocity += Vector3.ProjectOnPlane(leftHandTransform.forward + rightHandTransform.forward, upDirection).normalized * verticalVelocty.magnitude * FORWARD_CORRECTION_FACTOR;
                    float horizontalSpeed = horizontalVelocity.magnitude;
                    if (horizontalSpeed > HORIZONTAL_SPEED_DEADZONE)
                    {
                        horizontalVelocity *= (horizontalSpeed - HORIZONTAL_SPEED_DEADZONE) / horizontalSpeed;
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

        class GesturedSwim : GesturedLocomotion
        {
            private float HAND_PROPULSION_DEADZONE = 0.5f;
            private float MIN_SWIM_SPEED = 0.375f;

            public GesturedSwim(Transform vrCameraRig) : base(vrCameraRig) { }

            public override Vector3 GetTargetVelocityFromGestures(Player player)
            {
                if (!player.IsSwimming())
                {
                    return Vector3.zero;
                }
                float liquidLevel = Player.m_localPlayer.GetLiquidLevel();
                Vector3 velocity = Vector3.zero;
                if (!SteamVR_Actions.valheim_UseLeft.GetState(SteamVR_Input_Sources.LeftHand) &&
                    !SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand) &&
                    leftHandTransform.position.y < liquidLevel)
                {
                    Vector3 leftHandPalmar = leftHandTransform.right;
                    float leftHandPropulsion = Vector3.Dot(leftHandVelocity, leftHandPalmar);
                    leftHandPropulsion = Mathf.Max(0, leftHandPropulsion - HAND_PROPULSION_DEADZONE);
                    velocity += -leftHandVelocity.normalized * leftHandPropulsion;
                }
                if (!SteamVR_Actions.valheim_Use.GetState(SteamVR_Input_Sources.RightHand) &&
                    !SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand) &&
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
    }
}
