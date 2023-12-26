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
                new GesturedLocomotion[] { new GesturedSwim(vrCameraRig), new GesturedJump(vrCameraRig), new GesturedWalkRun(vrCameraRig) };
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
                if (!VHVRConfig.IsGesturedSwimEnabled() || !player.IsSwimming())
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

        class GesturedJump : GesturedLocomotion
        {
            private const float HORIZONTAL_SPEED_DEADZONE_FACTOR = 0.4f;
            private const float FORWARD_CORRECTION_FACTOR = 0.4f;

            private bool isPreparingJump = false;
            private Vector3 jumpVelocity = Vector3.zero;

            public GesturedJump(Transform vrCameraRig) : base(vrCameraRig) { }

            public override Vector3 GetTargetVelocityFromGestures(Player player)
            {
                if (!VHVRConfig.IsGesturedJumpEnabled())
                {
                    return Vector3.zero;
                }

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
                    float verticalSpeed = verticalVelocty.magnitude;
                    Vector3 horizontalVelocity = Vector3.ProjectOnPlane(velocity, upDirection);
                    Vector3 handForwardDirection = Vector3.ProjectOnPlane(leftHandTransform.forward + rightHandTransform.forward, upDirection).normalized;
                    horizontalVelocity += handForwardDirection * FORWARD_CORRECTION_FACTOR * verticalSpeed;
                    float horizontalSpeed = horizontalVelocity.magnitude;
                    float horizontalSpeedDeadzone = HORIZONTAL_SPEED_DEADZONE_FACTOR * verticalSpeed;
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

        class GesturedWalkRun : GesturedLocomotion
        {
            private const float MIN_WALK_SPEED = 0.25f;
            private const float MIN_HAND_SPEED = 0.25f;

            private bool isWalkingOrRunningUsingGestures = false;
            private GrabStartWheel grabStartWheel;
            private EquipType leftEquipType;
            private EquipType rightEquipType;

            public GesturedWalkRun(Transform vrCameraRig) : base(vrCameraRig)
            {
                new GameObject("LeftHandWalkingWheel").AddComponent<WalkRunIndicator>().Init(
                    true, this);
                new GameObject("RightHandWalkingWheel").AddComponent<WalkRunIndicator>().Init(
                    false, this);
                (grabStartWheel = new GameObject("WalkRunGrabStartWheel").AddComponent<GrabStartWheel>()).Init(this);
            }
            
            public Vector3 movementVerticalPlaneNormal { get; private set; }

            public override Vector3 GetTargetVelocityFromGestures(Player player)
            {
                if (!VHVRConfig.IsGesturedWalkRunEnabled())
                {
                    return Vector3.zero;
                }

                Vector3 combinedHandVelocity = rightHandVelocity - leftHandVelocity;
                float combinedHandSpeed = combinedHandVelocity.magnitude;
                Vector3 clampedHandVelocity = combinedHandSpeed > 1 ? combinedHandVelocity / combinedHandSpeed : combinedHandVelocity;

                // Use both hand pointing direction and hand movement direction to decide walk direction
                Vector3 walkDirection = leftHandTransform.forward + rightHandTransform.forward;
                walkDirection += (Vector3.Dot(walkDirection, clampedHandVelocity) > 0 ? clampedHandVelocity : -clampedHandVelocity);
                walkDirection = Vector3.ProjectOnPlane(walkDirection, upDirection).normalized;
                movementVerticalPlaneNormal = Vector3.Cross(upDirection, walkDirection).normalized;
                Vector3 wheelDiameter = Vector3.ProjectOnPlane(rightHandTransform.position - leftHandTransform.position, movementVerticalPlaneNormal).normalized;

                float walkSpeed =
                    Vector3.Dot(
                        Vector3.Cross(wheelDiameter, combinedHandVelocity),
                        movementVerticalPlaneNormal) * 0.5f;

                if (ShouldStop(player, walkSpeed, leftHandVelocity, rightHandVelocity))
                {
                    isWalkingOrRunningUsingGestures = false;
                }
                else if (ShouldStart())
                {
                    isWalkingOrRunningUsingGestures = true;
                }

                return isWalkingOrRunningUsingGestures && Mathf.Abs(walkSpeed) > MIN_WALK_SPEED ? walkDirection * walkSpeed : Vector3.zero;
            }

            private bool ShouldStart()
            {
                return SteamVR_Actions.valheim_Grab.GetState(VRPlayer.dominantHandInputSource) && grabStartWheel.isNearDominantHand;
            }

            private bool ShouldStop(Player player, float currentSpeed, Vector3 leftHandVelocity, Vector3 rightHandVelocity)
            {
                EquipType previousLeftEquipType = leftEquipType;
                EquipType previousRightEquipType = rightEquipType;
                leftEquipType = EquipScript.getLeft();
                rightEquipType = EquipScript.getRight();

                if (leftEquipType != EquipType.None && leftEquipType != previousLeftEquipType)
                {
                    // Stop gestured walking when equipping something.
                    return true;
                }

                if (rightEquipType != EquipType.None && rightEquipType != previousRightEquipType)
                {
                    // Stop gestured walking when equipping something.
                    return true;
                }

                if (leftEquipType == EquipType.Bow && BowLocalManager.isPulling)
                {
                    // Stop gestured walking when pulling a bow.
                    return true;
                }

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
                    // If at least one grip is being pressed, resume gestured walking.
                    return false;
                }

                if (VRControls.instance)
                {
                    float x = VRControls.instance.GetJoyLeftStickX();
                    float y = VRControls.instance.GetJoyLeftStickY();
                    if (x < -0.75 || x > 0.75 || y < -0.75 || y > 0.75)
                    {
                        // Stop gestured walking to let controller stick input take over locomtion.
                        return true;
                    }
                }
                
                // Stop gestured walking if hand movements are slow.
                return Mathf.Abs(currentSpeed) < MIN_WALK_SPEED && leftHandVelocity.magnitude < MIN_HAND_SPEED && rightHandVelocity.magnitude < MIN_HAND_SPEED;
            }

            // A small wheel displayed near the non-dominant hand that can be grabbed with the other hand to start gestured walking.
            class GrabStartWheel : HexagonWheel
            {
                private const float GRAB_DISTANCE = 0.125f;
                private GesturedWalkRun gesturedWalkRun;
                public bool isNearDominantHand {
                    get {
                        return lineRenderer.enabled && Vector3.Distance(transform.position, VRPlayer.dominantHand.transform.position) < GRAB_DISTANCE;
                    }
                }

                public void Init(GesturedWalkRun gesturedWalkRun)
                {
                    this.gesturedWalkRun = gesturedWalkRun;
                }

                void Update()
                {
                    if (gesturedWalkRun == null)
                    {
                        Destroy(this.gameObject);
                    }

                    if (VRPlayer.leftHand != null && VRPlayer.rightHand != null)
                    {
                        Transform handTransform = VRPlayer.dominantHand.otherHand.transform;
                        if (transform.parent != handTransform)
                        {
                            transform.parent = handTransform;
                            transform.localPosition = Vector3.zero;
                            transform.localRotation = Quaternion.Euler(0, 0, 90);
                            transform.localScale = Vector3.one / 16f;
                        }
                    }

                    if (!VHVRConfig.IsGesturedWalkRunEnabled())
                    {
                        lineRenderer.enabled = false;
                        return;
                    }

                    if (gesturedWalkRun.isWalkingOrRunningUsingGestures)
                    {
                        // If gestured walk-run is already active, there is no need to display the grab-start wheel
                        lineRenderer.enabled = false;
                        return;
                    }

                    // Only show the grab-start wheel when the non-dominant hand is holding both the trigger and the grip.
                    if (!SteamVR_Actions.valheim_Grab.GetState(VRPlayer.nonDominantHandInputSource)
                        || (!SteamVR_Actions.valheim_UseLeft.GetState(VRPlayer.nonDominantHandInputSource) &&
                            !SteamVR_Actions.valheim_Use.GetState(VRPlayer.nonDominantHandInputSource)))
                    {
                        lineRenderer.enabled = false;
                        return;
                    }

                    lineRenderer.enabled = true;
                    lineRenderer.material.color = isNearDominantHand ? Color.red : Color.blue;
                }
            }

            // "Hand wheels" displayed to indicate the activity and direction of gestured walking.
            class WalkRunIndicator : HexagonWheel
            {
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

            class HexagonWheel : MonoBehaviour
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
