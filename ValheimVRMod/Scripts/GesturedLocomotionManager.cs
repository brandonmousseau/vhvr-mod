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
        private const float AIR_SPEED_CHANGE_DAMPER = 0.25f;
        private const float RUN_ACITIVATION_SPEED = 1.75f;
        private const float GROUND_RUN_DEACTIVATION_SPEED = 1.125f;
        private const float AIR_RUN_DEACTIVATION_SPEED = 0.125f;
        private const float MIN_WATER_SPEED = 0.0625f;

        public static float distanceTraveled { get; private set; } = 0;
        public float stickOutputX { get; private set; } = 0;
        public float stickOutputY { get; private set; } = 0;
        public bool isRunning { get; private set; } = false;
        public static bool isUsingFootTracking {  get { return WalkInPlace.pace != WalkInPlace.Pace.STOP || SlideInPlace.IsSlidingInPlace; } }
        public Vector3? dodgeDirection { get; private set; } = null;

        private static Vector3? upDirection { get { return VRPlayer.instance ? VRPlayer.instance.transform.up : (Vector3?) null; } }
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

        public GesturedLocomotionManager()
        {
            gesturedLocomotions =
                new GesturedLocomotion[] {
                    new GesturedSwim(),
                    new GesturedJump(),
                    new MaximizingGesturedLocomotion(
                        new LeftHandGesturedWalkRun(), new RightHandGesturedWalkRun()),
                    new WalkInPlace(),
                    new SlideInPlace(),
                    new HandGesturedJump(isRightHand: true),
                    new HandGesturedJump(isRightHand: false),
                    new GesturedGlide(),
                    new GesturedDodgeRoll()};
        }

        public void UpdateMovementFromGestures(float deltaTime)
        {
            Player localPlayer = Player.m_localPlayer;
            Vector3? upDirection = GesturedLocomotionManager.upDirection;
            if (VHVRConfig.NonVrPlayer() || !VHVRConfig.UseVrControls() || localPlayer == null || upDirection == null)
            {
                stickOutputX = stickOutputY = 0;
                isRunning = false;
                dodgeDirection = null;
                return;
            }

            Vector3 targetVelocity = Vector3.zero;
            foreach (GesturedLocomotion locomotion in gesturedLocomotions)
            {
                targetVelocity += locomotion.GetTargetVelocityFromGestures(localPlayer, deltaTime);
            }

            if (SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand) &&
                SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand))
            {
                gesturedLocomotionVelocity = targetVelocity;
            }
            else
            {
                float damper =
                    localPlayer.IsSwimming() ? WATER_SPEED_CHANGE_DAMPER : IsInAir(localPlayer) ? AIR_SPEED_CHANGE_DAMPER : GROUND_SPEED_CHANGE_DAMPER;

                gesturedLocomotionVelocity =
                    Vector3.Lerp(gesturedLocomotionVelocity, targetVelocity, deltaTime / damper);
            }

            var horizontalVelocity = Vector3.ProjectOnPlane(gesturedLocomotionVelocity, upDirection.Value);
            horizontalSpeed = horizontalVelocity.magnitude;

            if (localPlayer.IsSwimming() && horizontalSpeed < MIN_WATER_SPEED)
            {
                stickOutputX = stickOutputY = 0;
            }
            else
            {
                Vector3 stickYDirection = -Vector3.ProjectOnPlane(localPlayer.transform.forward, upDirection.Value).normalized;
                Vector3 stickXDirection = Vector3.Cross(stickYDirection, upDirection.Value);
                stickOutputX = Vector3.Dot(gesturedLocomotionVelocity, stickXDirection) * STICK_OUTPUT_WEIGHT;
                stickOutputY = Vector3.Dot(gesturedLocomotionVelocity, stickYDirection) * STICK_OUTPUT_WEIGHT;
                distanceTraveled += targetVelocity.magnitude * deltaTime;
            }

            if (isRunning)
            {
                if (!localPlayer.HaveStamina() ||
                    horizontalSpeed < (IsInAir(localPlayer) ? AIR_RUN_DEACTIVATION_SPEED : GROUND_RUN_DEACTIVATION_SPEED))
                {
                    isRunning = false;
                }
                else if (Vector3.Dot(-VRPlayer.leftHand.transform.right, upDirection.Value) > 0.8f &&
                    Vector3.Dot(VRPlayer.rightHand.transform.right, upDirection.Value) > 0.8f) {
                    // Both hands palms are facing down, stop running.
                    isRunning = false;
                }
            }
            else if (horizontalSpeed > RUN_ACITIVATION_SPEED &&
                Vector3.Dot(-VRPlayer.leftHand.transform.right, (Vector3)upDirection) < 0.5f &&
                Vector3.Dot(VRPlayer.rightHand.transform.right, (Vector3)upDirection) < 0.5f &&
                !SteamVR_Actions.valheim_StopGesturedLocomotion.GetState(SteamVR_Input_Sources.Any))
            {
                isRunning = true;
            }

            var verticalSpeed = Vector3.Dot(targetVelocity, upDirection.Value);
            dodgeDirection = verticalSpeed < -0.5f || (isRunning && VRPlayer.isRoomscaleSneaking) ? horizontalVelocity : (Vector3?) null;
            if (verticalSpeed > VHVRConfig.GesturedJumpMinSpeed())
            {
                if (localPlayer.IsSitting())
                {
                    localPlayer.StopEmote();
                }
                else
                {
                    localPlayer.Jump();
                }
            }
        }

        private static bool IsInAir(Player player)
        {
            return !player.IsAttached() && !player.IsSwimming() && !player.IsOnGround();
        }

        private static Vector3 ApplyHeadTiltStrafe(Camera head, Vector3 walkDirection, float walkSpeed)
        {
            const float HEAD_TILT_STRAFE_DEADZONE = 0.125f;
            const float HEAD_TILT_STRAFE_WEIGHT = 2f;

            if (head == null)
            {
                return walkDirection * walkSpeed;
            }
            var heading = Vector3.ProjectOnPlane(head.transform.forward, upDirection.Value);
            var strafe =
                Vector3.ProjectOnPlane(
                    Vector3.ProjectOnPlane(head.transform.up, heading) - upDirection.Value, upDirection.Value);
            var strafeAmount = strafe.magnitude;
            if (strafeAmount < HEAD_TILT_STRAFE_DEADZONE)
            {
                return walkDirection * walkSpeed;
            }

            strafe -= strafe * HEAD_TILT_STRAFE_DEADZONE / strafeAmount;
            if (walkSpeed < 0)
            {
                strafe = -strafe;
            }
            return (walkDirection + strafe * HEAD_TILT_STRAFE_WEIGHT).normalized * walkSpeed;
        }


        abstract class GesturedLocomotion
        {
            protected Transform leftHandTransform { get { return VRPlayer.leftHand.transform; } }
            protected Transform rightHandTransform { get { return VRPlayer.rightHand.transform; } }
            protected Vector3 leftHandVelocity { get { return VRPlayer.leftHandPhysicsEstimator.GetVelocity(); } }
            protected Vector3 rightHandVelocity { get { return VRPlayer.rightHandPhysicsEstimator.GetVelocity(); } }

            public abstract Vector3 GetTargetVelocityFromGestures(Player player, float deltaTime);
        }

        class GesturedSwim : GesturedLocomotion
        {
            private float HAND_PROPULSION_DEADZONE = 0.5f;
            private float MIN_SWIM_SPEED = 0.375f;

            public override Vector3 GetTargetVelocityFromGestures(Player player, float deltaTime)
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
                velocity = Vector3.ProjectOnPlane(velocity, upDirection.Value);
                return velocity.magnitude >= MIN_SWIM_SPEED ? velocity * 2 : Vector3.zero;
            }
        }

        class GesturedJump : GesturedLocomotion
        {
            public override Vector3 GetTargetVelocityFromGestures(Player player, float deltaTime)
            {
                if (!VHVRConfig.IsGesturedJumpEnabled() || IsInAir(player))
                {
                    return Vector3.zero;
                }

                if (StaticObjects.leftFist().isGrabbingJumpingAid || StaticObjects.rightFist().isGrabbingJumpingAid)
                {
                    return Vector3.zero;
                }

                var height = Valve.VR.InteractionSystem.Player.instance.eyeHeight;
                if (height < VRPlayer.referencePlayerHeight * VHVRConfig.GesturedJumpPreparationHeight())
                {
                    return Vector3.zero;
                }

                var verticalSpeed = Vector3.Dot(VRPlayer.headPhysicsEstimator.GetVelocity(), upDirection.Value);
                if (verticalSpeed < VHVRConfig.GesturedJumpMinSpeed())
                {
                    return Vector3.zero;
                }

                var verticalAcceleration = Vector3.Dot(VRPlayer.headPhysicsEstimator.GetAcceleration(), upDirection.Value);
                if (verticalAcceleration < VHVRConfig.GesturedJumpMinSpeed() * 8) // TODO: consider adding an option for min acceleration.
                {
                    return Vector3.zero;
                }

                LogUtils.LogInfo("Gestured jump at speed " + verticalSpeed + " and acceleration " + verticalAcceleration);
                return upDirection.Value * verticalSpeed;
            }
        }

        class HandGesturedJump : GesturedLocomotion
        {
            private bool isRightHand;
            private Vector3 horizontalVelocity = Vector3.zero;

            public HandGesturedJump(bool isRightHand)
            {
                this.isRightHand = isRightHand;
            }

            public override Vector3 GetTargetVelocityFromGestures(Player player, float deltaTime)
            {
                var fistCollision = isRightHand ? StaticObjects.rightFist() :StaticObjects.leftFist();
                var physicsEstimator = isRightHand ? VRPlayer.rightHandPhysicsEstimator : VRPlayer.leftHandPhysicsEstimator;
                var isJumping = fistCollision.isGrabbingJumpingAid && Vector3.Dot(physicsEstimator.GetVelocity(), upDirection.Value) < -3f;

                if (isJumping)
                {
                    horizontalVelocity =
                        Vector3.ProjectOnPlane(-physicsEstimator.GetVelocity(), upDirection.Value).normalized *
                        (GesturedLocomotionManager.RUN_ACITIVATION_SPEED + 0.1f);
                    return horizontalVelocity + upDirection.Value * (VHVRConfig.GesturedJumpMinSpeed() + 0.1f);
                }
                
                if (!IsInAir(player) || !fistCollision.isGrabbingJumpingAid)
                {
                    horizontalVelocity = Vector3.zero;
                }

                return horizontalVelocity;
            }
        }

        class GesturedGlide : GesturedLocomotion
        {
            public static bool isGlideActive = false;

            public override Vector3 GetTargetVelocityFromGestures(Player player, float deltaTime)
            {
                if (!SteamVR_Actions.valheim_StopGesturedLocomotion.activeBinding ||
                    !VHVRConfig.IsGesturedJumpEnabled() ||
                    (player.IsSwimming() && !player.IsOnGround()))
                {
                    isGlideActive = false;
                    return Vector3.zero;
                }

                if (SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand) &&
                    SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand))
                {
                    isGlideActive = false;
                    return Vector3.zero;
                }

                var handSpan = VRPlayer.rightHand.transform.position - VRPlayer.leftHand.transform.position;
                handSpan.y = 0;
                var handHorizontalDistance = handSpan.magnitude;

                if (handHorizontalDistance < 0.125f)
                {
                    isGlideActive = false;
                    return Vector3.zero;
                }

                var leftHandVelocity = VRPlayer.leftHandPhysicsEstimator.GetVelocity();
                var rightHandVelocity = VRPlayer.rightHandPhysicsEstimator.GetVelocity();
                var leftHandSpeed = leftHandVelocity.magnitude;
                var rightHandSpeed = rightHandVelocity.magnitude;
                var leftHandPalmar = VRPlayer.leftHand.transform.right;
                var rightHandPalmar = -VRPlayer.rightHand.transform.right;
                var leftHandPropulsion = Vector3.Dot(leftHandVelocity, leftHandPalmar);
                var rightHandPropulsion = Vector3.Dot(rightHandVelocity, rightHandPalmar);
                var leftHandContribution = (Mathf.Abs(Mathf.Abs(leftHandPropulsion) * 2 - leftHandSpeed) - leftHandSpeed) * leftHandPalmar;
                var rightHandContribution = (Mathf.Abs(Mathf.Abs(rightHandPropulsion) * 2 - rightHandSpeed) - rightHandSpeed) * rightHandPalmar;
                if (leftHandPropulsion < 0)
                {
                    leftHandContribution = -leftHandContribution;
                }
                if (rightHandPropulsion < 0)
                {
                    rightHandContribution = -rightHandContribution;
                }
                var velocity = Vector3.ProjectOnPlane(leftHandContribution + rightHandContribution, handSpan) * 4;
                velocity.y = 0;

                if (!isGlideActive &&
                    !SteamVR_Actions.valheim_StopGesturedLocomotion.GetState(SteamVR_Input_Sources.Any) &&
                    handHorizontalDistance > 1.4f &&
                    velocity.sqrMagnitude > 1f)
                {
                    isGlideActive = true;
                }

                if (!isGlideActive)
                {
                    return Vector3.zero;
                }

                return velocity;
            }
        }

        class MaximizingGesturedLocomotion : GesturedLocomotion
        {

            private GesturedLocomotion a;
            private GesturedLocomotion b;

            public MaximizingGesturedLocomotion(GesturedLocomotion a, GesturedLocomotion b)
            {
                this.a = a;
                this.b = b;
            }

            public override Vector3 GetTargetVelocityFromGestures(Player player, float deltaTime)
            {
                Vector3 vA = a.GetTargetVelocityFromGestures(player, deltaTime);
                Vector3 vB = b.GetTargetVelocityFromGestures(player, deltaTime);
                return (vA + vB).normalized * Mathf.Max(vA.magnitude, vB.magnitude);
            }
        }

        class LeftHandGesturedWalkRun : GesturedWalkRun {

            public LeftHandGesturedWalkRun()
            {
                new GameObject("LeftHandWalkingWheel").AddComponent<WalkRunIndicator>().Init(true, this);
            }

            protected override SteamVR_Input_Sources inputSource { get { return SteamVR_Input_Sources.LeftHand; } }
            protected override Vector3 handVelocity { get { return VRPlayer.leftHandPhysicsEstimator.GetVelocity(); } }
            protected override Transform handTransform { get { return VRPlayer.leftHand.transform; } }
            protected override Transform otherHandTransform { get { return VRPlayer.rightHand.transform; } }
        }

        class RightHandGesturedWalkRun : GesturedWalkRun {

            public RightHandGesturedWalkRun() 
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
            private const float DEACTIVATION_SPEED = 0.0625f;

            private Camera vrCam;
            private bool isWalkingOrRunningUsingGestures = false;

            protected abstract SteamVR_Input_Sources inputSource { get; }
            protected abstract Vector3 handVelocity { get; }
            protected abstract Transform handTransform { get; }

            protected abstract Transform otherHandTransform { get; }

            public Vector3 movementVerticalPlaneNormal { get; private set; }

            public override Vector3 GetTargetVelocityFromGestures(Player player, float deltaTime)
            {
                if (!VHVRConfig.IsGesturedWalkRunEnabled())
                {
                    return Vector3.zero;
                }

                // Use controller pointing as walking direction.
                Vector3 walkDirection = handTransform.forward - handTransform.up * 0.75f;

                walkDirection = Vector3.ProjectOnPlane(walkDirection, upDirection.Value).normalized;

                Vector3 handVelocity = this.handVelocity;
                float handSpeed = handVelocity.magnitude;
                Vector3 clampedHandVelocity = Vector3.ProjectOnPlane(handSpeed > 1 ? handVelocity / handSpeed : handVelocity, upDirection.Value);

                // Use hand velocity to adjust walk direction.
                if (Vector3.Dot(walkDirection, clampedHandVelocity) > 0) {
                    walkDirection += clampedHandVelocity * 0.5f;
                }
                else
                {
                    walkDirection -= clampedHandVelocity * 0.5f;
                }

                walkDirection = walkDirection.normalized;

                movementVerticalPlaneNormal = Vector3.Cross(upDirection.Value, walkDirection).normalized;
                Vector3 wheelDiameter = Vector3.ProjectOnPlane(handTransform.position - otherHandTransform.position, movementVerticalPlaneNormal);

                float walkSpeed =
                    Vector3.Dot(Vector3.Cross(wheelDiameter.normalized, handVelocity), movementVerticalPlaneNormal);

                if (ShouldStop(player, handSpeed))
                {
                    isWalkingOrRunningUsingGestures = false;
                }
                else if (ShouldStart(wheelDiameter, walkDirection, Mathf.Abs(walkSpeed)))
                {
                    isWalkingOrRunningUsingGestures = true;
                    GesturedGlide.isGlideActive = false;
                }

                if (vrCam == null)
                {
                    vrCam = CameraUtils.getCamera(CameraUtils.VR_CAMERA);
                }

                return isWalkingOrRunningUsingGestures ? ApplyHeadTiltStrafe(vrCam, walkDirection, walkSpeed) : Vector3.zero;
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

            private bool ShouldStart(Vector3 wheelDiameter, Vector3 walkDirection, float walkSpeed)
            {
                if  (SteamVR_Actions.valheim_StopGesturedLocomotion.GetState(SteamVR_Input_Sources.Any))
                {
                    return false;
                }
                if (walkSpeed < 0.5f || wheelDiameter.magnitude < 0.5f)
                {
                    return false;
                }
                if (!IsStepping() && walkSpeed < 2 && Vector3.ProjectOnPlane(wheelDiameter, upDirection.Value).magnitude < 1)
                {
                    return false;
                }
                float angle = Vector3.Angle(handTransform.forward - handTransform.up, upDirection.Value);
                return 60 < angle && angle < 120;
            }

            private bool IsStepping()
            {
                if (!VHVRConfig.TrackFeet())
                {
                    return false;
                }
                Vector3 footStep = VRPlayer.leftFoot.position - VRPlayer.rightFoot.position;
                if (Mathf.Abs(Vector3.Dot(footStep, upDirection.Value)) > 0.25f)
                {
                    return true;
                }

                if (VRPlayer.leftFootPhysicsEstimator != null &&
                    Mathf.Abs(Vector3.Dot(VRPlayer.leftFootPhysicsEstimator.GetVelocity(), upDirection.Value)) > 2)
                {
                    return true;
                }

                if (VRPlayer.rightFootPhysicsEstimator != null &&
                    Mathf.Abs(Vector3.Dot(VRPlayer.rightFootPhysicsEstimator.GetVelocity(), upDirection.Value)) > 2)
                {
                    return true;
                }

                return false;
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

                var leftHandDistal = VRPlayer.leftHand.transform.forward - VRPlayer.leftHand.transform.up;
                var rightHandDistal = VRPlayer.rightHand.transform.forward - VRPlayer.rightHand.transform.up;
                var handSpan = VRPlayer.rightHand.transform.position - VRPlayer.leftHand.transform.position;
                var handDistance = handSpan.magnitude;
                if (handDistance > 0.75f &&
                    Vector3.Dot(leftHandDistal, handSpan) < -handDistance &&
                    Vector3.Dot(rightHandDistal, handSpan) > handDistance)
                {
                    return true;
                }

                // Stop gestured walking if hand movements are slow.
                return isWalkingOrRunningUsingGestures && handSpeed < DEACTIVATION_SPEED;
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

        class WalkInPlace : GesturedLocomotion
        {
            public enum Pace
            {
                RETROGRADE = -1,
                STOP = 0,
                WALK = 1,
                RUN = 2
            }
            public static Pace pace { get; private set; } = Pace.STOP;
            private float stopWalkingCountdown = 0;
            private Camera vrCam;

            public override Vector3 GetTargetVelocityFromGestures(Player player, float deltaTime)
            {
                if (!VHVRConfig.IsGesturedWalkRunEnabled() || !VHVRConfig.TrackFeet() || !SteamVR_Actions.valheim_StopGesturedLocomotion.activeBinding)
                {
                    pace = Pace.STOP;
                    return Vector3.zero;
                }

                Vector3 leftFootVelocity = VRPlayer.leftFootPhysicsEstimator.GetVelocity();
                Vector3 rightFootVelocity = VRPlayer.rightFootPhysicsEstimator.GetVelocity();
                float leftFootElevation = VRPlayer.leftFootElevation;
                float rightFootElevation = VRPlayer.rightFootElevation;
                float walkSpeed = leftFootVelocity.sqrMagnitude + rightFootVelocity.sqrMagnitude;

                if (vrCam == null)
                {
                    vrCam = CameraUtils.getCamera(CameraUtils.VR_CAMERA);
                }

                Vector3 walkDirection =
                    Vector3.ProjectOnPlane(VRPlayer.leftFoot.forward + VRPlayer.rightFoot.forward, upDirection.Value).normalized;

                UpdatePace(leftFootVelocity, rightFootVelocity, leftFootElevation, rightFootElevation, walkDirection, walkSpeed, deltaTime);

                if (!SteamVR_Actions.valheim_StopGesturedLocomotion.GetState(SteamVR_Input_Sources.Any) &&
                    leftFootElevation > 0.0625f &&
                    rightFootElevation > 0.0625f &&
                    Vector3.Dot(leftFootVelocity, upDirection.Value) > 0.5f &&
                    Vector3.Dot(rightFootVelocity, upDirection.Value) > 0.5f &&
                    Vector3.Dot(VRPlayer.headPhysicsEstimator.GetVelocity(), upDirection.Value) > VHVRConfig.GesturedJumpMinSpeed()) {
                    // Jump
                    return upDirection.Value * 16;
                }

                if (pace == Pace.STOP)
                {
                    return Vector3.zero;
                }

                if (pace == Pace.RETROGRADE)
                {
                    walkDirection = -walkDirection;
                    walkSpeed = Mathf.Min(walkSpeed, 1);
                }
                else if (pace == Pace.WALK)
                {
                    walkSpeed = Mathf.Min(walkSpeed, 1);
                }

                return ApplyHeadTiltStrafe(vrCam, walkDirection, walkSpeed);
            }

            private void UpdatePace(
                Vector3 leftFootVelocity, Vector3 rightFootVelocity, float leftFootElevation, float rightFootElevation, Vector3 walkDirection, float walkSpeed, float deltaTime)
            {
                if (ShouldStop(walkSpeed, leftFootElevation, rightFootElevation, deltaTime)) {
                    pace = Pace.STOP;
                    return;
                }

                float leanForward =
                    Vector3.Dot(
                        vrCam.transform.position - Vector3.Lerp(VRPlayer.leftFoot.position, VRPlayer.rightFoot.position, 0.5f),
                        walkDirection);
                
                if (pace == Pace.STOP)
                {
                    if (SteamVR_Actions.valheim_StopGesturedLocomotion.GetState(SteamVR_Input_Sources.Any))
                    {
                        return;
                    }
                    if (Vector3.Dot(leftFootVelocity, upDirection.Value) > -0.25f && Vector3.Dot(rightFootVelocity, upDirection.Value) > -0.25f) {
                        return;
                    }
                    if (leanForward > -0.1f && leanForward < 0.1f)
                    {
                        return;
                    }
                    if (leftFootElevation < 0.125f && rightFootElevation < 0.125f)
                    {
                        return;
                    }
                }

                if (leanForward < -0.1f)
                {
                    pace = Pace.RETROGRADE;
                    return;
                }

                if (pace == Pace.RUN && leftFootElevation < 0.03f && rightFootElevation < 0.03 && walkSpeed < 0.5f) {
                    pace = Pace.WALK;
                    return;
                }

                if (leftFootElevation > 0.03f && rightFootElevation > 0.03f)
                {
                    if (pace == Pace.WALK || (pace == Pace.STOP && leanForward > 0.1f))
                    {
                        pace = Pace.RUN;
                        return;
                    }
                }

                if (pace != Pace.RUN && leanForward > 0.1f)
                {
                    pace = Pace.WALK;
                }
            }

            private bool ShouldStop(float walkSpeed, float leftFootElevation, float rightFootElevation, float deltaTime)
            {
                if (Player.m_localPlayer.m_attached)
                {
                    return true;
                }

                if (SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand) &&
                    SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand))
                {
                    return true;
                }

                if (leftFootElevation > 0.0625f || rightFootElevation > 0.0625f || walkSpeed > 0.01f)
                {
                    stopWalkingCountdown = 0.5f;
                    return false;
                }

                stopWalkingCountdown -= deltaTime;

                return stopWalkingCountdown <= 0;
            }
        }

        class SlideInPlace : GesturedLocomotion
        {
            public static bool IsSlidingInPlace { get; private set; } = false;

            public override Vector3 GetTargetVelocityFromGestures(Player player, float deltaTime)
            {
                if (!VHVRConfig.IsGesturedWalkRunEnabled() ||
                    !VHVRConfig.TrackFeet() ||
                    !SteamVR_Actions.valheim_StopGesturedLocomotion.activeBinding ||
                    Player.m_localPlayer.m_attached ||
                    SteamVR_Actions.valheim_StopGesturedLocomotion.GetState(SteamVR_Input_Sources.Any))
                {
                    return Vector3.zero;
                }

                Vector3 leftFootVelocity = VRPlayer.leftFootPhysicsEstimator.GetVelocity();
                Vector3 rightFootVelocity = VRPlayer.rightFootPhysicsEstimator.GetVelocity();
                float leftFootVerticalSpeed = Vector3.Dot(leftFootVelocity, upDirection.Value);
                float rightFootVerticalSpeed = Vector3.Dot(rightFootVelocity, upDirection.Value);
                if (leftFootVerticalSpeed > 0.125f || rightFootVerticalSpeed > 0.125f)
                {
                    IsSlidingInPlace = false;
                    return Vector3.zero;
                }

                float leftFootElevation = VRPlayer.leftFootElevation;
                float rightFootElevation = VRPlayer.rightFootElevation;
                float footHeightDifference = leftFootElevation - rightFootElevation;
                if (leftFootElevation > 0.0625f ||
                    rightFootElevation > 0.0625f ||
                    WalkInPlace.pace != WalkInPlace.Pace.STOP)
                {
                    IsSlidingInPlace = false;
                    return Vector3.zero;
                }

                Vector3 horizontalVelocity = leftFootVelocity + rightFootVelocity - upDirection.Value * (leftFootVerticalSpeed + rightFootVerticalSpeed);
                float slideSpeed = horizontalVelocity.magnitude;
                Vector3 movementDirection = -horizontalVelocity.normalized;

                if (slideSpeed < 0.03f)
                {
                    IsSlidingInPlace = false;
                }
                else if (slideSpeed > 0.25f)
                {
                    IsSlidingInPlace = true;
                }

                if (!IsSlidingInPlace)
                {
                    return Vector3.zero;
                }

                float headSlide = Vector3.Dot(VRPlayer.headPhysicsEstimator.GetVelocity(), movementDirection);

                if (headSlide < 0.5f)
                {
                    return Vector3.zero;
                }

                return movementDirection * (slideSpeed + headSlide);
            }
        }

        class GesturedDodgeRoll : GesturedLocomotion
        {
            private const float MIN_HAND_HEIGHT_RELATIVE_TO_EYE = -0.125f;
            private const float MIN_HEAD_HORIZONTAL_SPEED = 1f;
            private const float MAX_HEAD_VERTICAL_VELOCITY = -1.5f;
            private const float MAX_HEIGHT = 0.8f;
            private const float MIN_HAND_SPEED = 1.25f;
            private const float MIN_TILT = 15f;

            private Camera vrCam;

            public override Vector3 GetTargetVelocityFromGestures(Player player, float deltaTime)
            {
                if (!VHVRConfig.IsGesturedJumpEnabled() ||
                    player.IsAttached() ||
                    player.InDodge() ||
                    player.m_queuedDodgeTimer > 0 ||
                    !SteamVR_Actions.valheim_StopGesturedLocomotion.activeBinding ||
                    SteamVR_Actions.valheim_StopGesturedLocomotion.GetState(SteamVR_Input_Sources.LeftHand) ||
                    SteamVR_Actions.valheim_StopGesturedLocomotion.GetState(SteamVR_Input_Sources.RightHand))
                {
                    return Vector3.zero;
                }

                var isCrouching = player.IsCrouching();
                if (!isCrouching && Valve.VR.InteractionSystem.Player.instance.eyeHeight > MAX_HEIGHT * VRPlayer.referencePlayerHeight)
                {
                    return Vector3.zero;
                }

                if (vrCam == null)
                {
                    vrCam = CameraUtils.getCamera(CameraUtils.VR_CAMERA);
                }
                if (vrCam == null || Vector3.Angle(upDirection.Value, vrCam.transform.up) < MIN_TILT)
                {
                    return Vector3.zero;
                }

                Vector3 velocity = VRPlayer.headPhysicsEstimator.GetVelocity();
                if (!isCrouching && Vector3.Dot(velocity, upDirection.Value) > 0.5f)
                {
                    return Vector3.zero;
                }

                Vector3 horizontalVelocity = Vector3.ProjectOnPlane(velocity, upDirection.Value);
                Vector3 tiltDirection = Vector3.ProjectOnPlane(vrCam.transform.up, upDirection.Value).normalized;

                if (Vector3.Dot(horizontalVelocity, tiltDirection) < MIN_HEAD_HORIZONTAL_SPEED)
                {
                    return Vector3.zero;
                }

                if (!isCrouching)
                {
                    var verticalSpeed = Vector3.Dot(VRPlayer.headPhysicsEstimator.GetVelocity(), upDirection.Value);
                    if (verticalSpeed > MAX_HEAD_VERTICAL_VELOCITY)
                    {
                        return Vector3.zero;
                    }
                }

                if (!isCrouching &&
                    !IsHandAssistingDodge(VRPlayer.leftHandPhysicsEstimator) &&
                    !IsHandAssistingDodge(VRPlayer.rightHandPhysicsEstimator))
                {
                    return Vector3.zero;
                }

                return (horizontalVelocity.normalized - upDirection.Value) * 16f; 
            }

            private bool IsHandAssistingDodge(PhysicsEstimator handPhyicsEstimator)
            {
                return handPhyicsEstimator.GetVelocity().magnitude > MIN_HAND_SPEED &&
                    Vector3.Dot(handPhyicsEstimator.transform.position - vrCam.transform.position, upDirection.Value) > MIN_HAND_HEIGHT_RELATIVE_TO_EYE;               
            }
        }
    }
}
