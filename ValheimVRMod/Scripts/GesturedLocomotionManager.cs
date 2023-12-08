using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using ValheimVRMod.VRCore.UI;
using Valve.VR;


namespace ValheimVRMod.Scripts
{
    public class GesturedLocomotionManager
    {
        private const float STICK_OUTPUT_WEIGHT = 1f;
        private const float GROUND_SPEED_CHANGE_DAMPER = 0.25f;
        private const float WATER_SPEED_CHANGE_DAMPER = 1f;
        private const float AIR_SPEED_CHANGE_DAMPER = 4f;
        private const float RUN_ACITIVATION_SPEED = 1.25f;
        private const float RUN_DEACTIVATION_SPEED = 2f;
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
                new GesturedLocomotion[] { new GesturedSwim(vrCameraRig) };
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

            if (localPlayer.IsOnGround())
            {
                if (horizontalSpeed > RUN_ACITIVATION_SPEED)
                {
                    isRunning = true;
                }
                else if (horizontalSpeed < RUN_DEACTIVATION_SPEED)
                {
                    isRunning = false;
                }
            }
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
