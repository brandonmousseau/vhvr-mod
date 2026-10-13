using ValheimVRMod.VRCore.Backends;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts
{
    public class ShipSteering : MonoBehaviour
    {
        private const float MIN_ROW_SPEED = 0.5f;
        private const float MIN_RUDDER_TURN_SPEED = 0.05f;
        private const float MAX_SAIL_PULL_ANGLE = 60f;
        private const float MIN_SAIL_PULL_DISTANCE = 0.25f;

        private HandGesture leftHandGesture;
        private HandGesture rightHandGesture;
        private bool isSingleGrabbing;
        private bool isDoubleGrabbing;
        private bool isSteering;
        private bool isOperatingSail;
        private Ship.Speed sailOperationStartSpeed;
        private Ship.Speed sailOperationTargetSpeed;
        private float sailOperationStartHandHeight;

        // Used when the top of the mast cannot be found in the ship's model, in the mast's local space.
        private const float FALLBACK_MAST_HEIGHT = 10f;
        private const float STEERING_WHEEL_RADIUS = 0.125f;
        private const float STEERING_WHEEL_DISTANCE = 0.5f;
        // Grabbing with both hands to row starts with one hand a moment ahead of the other, which should not
        // flash the wheel.
        private const float STEERING_WHEEL_SHOW_DELAY = 0.25f;
        private const int STEERING_WHEEL_RIM_SEGMENTS = 16;
        private const float PADDLE_LENGTH = 6f;

        private bool isRowing;
        private bool isSteeringWithLeftHand;
        private float steeringStartTime;
        private LineRenderer sailRope;
        // The rope comes up when the sail is grabbed with both hands and stays until it has been pulled or both
        // hands have let go of it.
        private bool isSailRopeShown;
        // Which hand is the lower one on the rope, kept for as long as both hands hold it so that it does not flip
        // over when the hands pass each other.
        private bool isLeftHandLowerOnSailRope;
        private bool isSailRopeHandOrderLatched;
        // Null unless the rope has been held by a single hand since it came up.
        private bool? isSailRopeHeldByLeftHandAlone;
        private bool isLeftGrabbing;
        private bool isRightGrabbing;
        private const float PADDLE_BLADE_WIDTH = 0.2f;
        private const float PADDLE_SHAFT_WIDTH = 0.03f;
        // Where along the paddle its line has points, from one tip to the other: the blades widen from the shaft
        // in the middle toward the tips.
        private static readonly float[] PADDLE_POINTS = new float[] { 0, 0.3f, 0.7f, 1 };
        private LineRenderer paddle;
        private Transform steeringWheel;
        // In the ship's local space, so that the wheel stays put on the ship while it is being turned.
        private Vector3 steeringWheelLocalCenter;
        private Vector3 steeringWheelLocalDirection;
        private Transform mast;
        private float mastHeight;

        private ShipControlls shipControls
        {
            get
            {
                var controller = Player.m_localPlayer?.m_doodadController;
                return (controller != null && controller is ShipControlls) ? (ShipControlls)controller : null;
            }
        }

        public void Initialize(HandGesture leftHandGesture, HandGesture rightHandGesture)
        {
            this.leftHandGesture = leftHandGesture;
            this.rightHandGesture = rightHandGesture;
        }

        private Vector3 upDirection
        {
            get { return VRPlayer.instance != null ? VRPlayer.instance.transform.up : shipControls.m_ship.transform.up; }
        }

        void Awake()
        {
            var material = Instantiate(VRAssetManager.GetAsset<Material>("StandardClone"));
            material.color = new Color(0.5f, 0.25f, 0);
            sailRope = CreateLine(transform, material, 3, 0.02f);
            paddle = CreateLine(transform, material, PADDLE_POINTS.Length, PADDLE_BLADE_WIDTH);
            var paddleWidth = new AnimationCurve();
            for (int i = 0; i < PADDLE_POINTS.Length; i++)
            {
                bool isTip = i == 0 || i == PADDLE_POINTS.Length - 1;
                paddleWidth.AddKey(PADDLE_POINTS[i], isTip ? 1 : PADDLE_SHAFT_WIDTH / PADDLE_BLADE_WIDTH);
            }
            paddle.widthCurve = paddleWidth;
            // A line facing the view by itself faces each eye separately, which the eyes cannot fuse into one shape
            // when it is this wide. It is turned to the head as a whole instead, see LateUpdate().
            paddle.alignment = LineAlignment.TransformZ;

            steeringWheel = new GameObject().transform;
            steeringWheel.parent = transform;
            var rim = CreateLine(steeringWheel, material, STEERING_WHEEL_RIM_SEGMENTS, 0.02f);
            rim.useWorldSpace = false;
            rim.loop = true;
            rim.enabled = true;
            for (int i = 0; i < STEERING_WHEEL_RIM_SEGMENTS; i++)
            {
                rim.SetPosition(
                    i, Quaternion.Euler(0, 360f * i / STEERING_WHEEL_RIM_SEGMENTS, 0) * Vector3.forward * STEERING_WHEEL_RADIUS);
            }
            for (int i = 0; i < 4; i++)
            {
                var spoke = CreateLine(steeringWheel, material, 2, 0.02f);
                spoke.useWorldSpace = false;
                spoke.enabled = true;
                var end = Quaternion.Euler(0, 45f * i, 0) * Vector3.forward * STEERING_WHEEL_RADIUS;
                spoke.SetPosition(0, end);
                spoke.SetPosition(1, -end);
            }
            steeringWheel.gameObject.SetActive(false);
        }

        private static LineRenderer CreateLine(Transform parent, Material material, int positionCount, float width)
        {
            var line = new GameObject().AddComponent<LineRenderer>();
            line.transform.SetParent(parent, false);
            line.useWorldSpace = true;
            line.positionCount = positionCount;
            line.sharedMaterial = material;
            line.widthMultiplier = width;
            line.enabled = false;
            return line;
        }

        // Shows what the hands are operating: a rope up to the top of the mast for the sail, a wheel for the
        // rudder, and a double bladed paddle for rowing.
        void LateUpdate()
        {
            var controls = shipControls;
            bool isActive = controls && VHVRConfig.IsGesturedSteeringEnabled() && VRPlayer.leftHand && VRPlayer.rightHand;
            // A grip is only good for one pull (see UpdateSailOperationTargetSpeed()), so the rope goes away once
            // that is done, until the sail is grabbed anew.
            bool isSailPullDone = isOperatingSail && sailOperationTargetSpeed != sailOperationStartSpeed;
            if (!isActive || isRowing || isSailPullDone || (!isLeftGrabbing && !isRightGrabbing))
            {
                isSailRopeShown = false;
                // The hands are ordered anew by where they are when the rope comes up again.
                isSailRopeHandOrderLatched = false;
                isSailRopeHeldByLeftHandAlone = null;
            }
            else if (isOperatingSail)
            {
                isSailRopeShown = true;
            }
            sailRope.enabled = isSailRopeShown;
            paddle.enabled = isActive && isRowing;
            steeringWheel.gameObject.SetActive(isActive && isSteering && Time.time - steeringStartTime >= STEERING_WHEEL_SHOW_DELAY);
            if (!isActive)
            {
                return;
            }

            var ship = controls.m_ship;
            Vector3 up = upDirection;
            Vector3 leftHand = GetGripPosition(VRPlayer.leftHand.transform);
            Vector3 rightHand = GetGripPosition(VRPlayer.rightHand.transform);

            if (sailRope.enabled)
            {
                if (!isLeftGrabbing || !isRightGrabbing)
                {
                    // Only the one hand still holds the rope.
                    isSailRopeHandOrderLatched = false;
                    isLeftHandLowerOnSailRope = !isLeftGrabbing;
                    isSailRopeHeldByLeftHandAlone = isLeftGrabbing;
                }
                else if (!isSailRopeHandOrderLatched)
                {
                    // A hand joining the one already on the rope takes it between that hand and the mast,
                    // wherever it is. Only hands grabbing the rope together are ordered by their height.
                    isLeftHandLowerOnSailRope = isSailRopeHeldByLeftHandAlone ?? Vector3.Dot(rightHand - leftHand, up) > 0;
                    isSailRopeHandOrderLatched = true;
                }
                Vector3 topHand = isLeftHandLowerOnSailRope ? rightHand : leftHand;
                sailRope.SetPosition(0, !isLeftGrabbing || !isRightGrabbing ? topHand : isLeftHandLowerOnSailRope ? leftHand : rightHand);
                sailRope.SetPosition(1, topHand);
                sailRope.SetPosition(2, ship.m_mastObject ? GetMastTip(ship) : topHand);
            }

            if (paddle.enabled)
            {
                Vector3 handSpan = rightHand - leftHand;
                float handDist = handSpan.magnitude;
                Vector3 extension = handSpan / handDist * Mathf.Max(PADDLE_LENGTH - handDist, 0) * 0.5f;
                Vector3 leftTip = leftHand - extension;
                Vector3 rightTip = rightHand + extension;
                for (int i = 0; i < PADDLE_POINTS.Length; i++)
                {
                    paddle.SetPosition(i, Vector3.Lerp(leftTip, rightTip, PADDLE_POINTS[i]));
                }
                Vector3 toHead =
                    VRPlayer.vrCam != null ?
                    Vector3.ProjectOnPlane(VRPlayer.vrCam.transform.position - (leftHand + rightHand) * 0.5f, handSpan) :
                    Vector3.zero;
                if (toHead.sqrMagnitude > 0.0001f)
                {
                    // The line shows on the side its transform's Z points away from.
                    paddle.transform.rotation = Quaternion.LookRotation(-toHead, handSpan);
                }
            }

            if (isSteering)
            {
                Vector3 center = ship.transform.TransformPoint(steeringWheelLocalCenter);
                Vector3 toHand = Vector3.ProjectOnPlane((isSteeringWithLeftHand ? leftHand : rightHand) - center, up);
                // Too close to the center the direction to the hand is all noise, the wheel stays as it is then.
                if (toHand.magnitude > 0.1f)
                {
                    steeringWheelLocalDirection = ship.transform.InverseTransformDirection(toHand.normalized);
                }
                Vector3 direction = Vector3.ProjectOnPlane(ship.transform.TransformDirection(steeringWheelLocalDirection), up);
                steeringWheel.SetPositionAndRotation(
                    center, Quaternion.LookRotation(direction.sqrMagnitude > 0.0001f ? direction : ship.transform.forward, up));
            }
        }

        // Whether at least one of the hands is turned the way a hand holding a rope that comes down from above is.
        private static bool IsEitherHandGrippingSailRope(Vector3 up)
        {
            return Vector3.Angle(VRPlayer.leftHand.transform.forward, up) < MAX_SAIL_PULL_ANGLE ||
                Vector3.Angle(VRPlayer.rightHand.transform.forward, up) < MAX_SAIL_PULL_ANGLE;
        }

        // Where the hand holds what is shown in it, which is a little behind where the controller is tracked.
        private static Vector3 GetGripPosition(Transform hand)
        {
            return hand.position - hand.forward * 0.125f;
        }

        // Puts the wheel in front of the seat at the height of the hand grabbing it.
        private void PlaceSteeringWheel()
        {
            var ship = shipControls.m_ship;
            var seat = Player.m_localPlayer.transform;
            Vector3 up = upDirection;
            Vector3 hand = GetGripPosition((isSteeringWithLeftHand ? VRPlayer.leftHand : VRPlayer.rightHand).transform);
            Vector3 center = seat.position + Vector3.ProjectOnPlane(seat.forward, up).normalized * STEERING_WHEEL_DISTANCE;
            center += up * Vector3.Dot(hand - center, up);
            steeringWheelLocalCenter = ship.transform.InverseTransformPoint(center);
            steeringWheelLocalDirection = ship.transform.InverseTransformDirection(seat.forward);
        }

        private Vector3 GetMastTip(Ship ship)
        {
            if (mast != ship.m_mastObject.transform)
            {
                mast = ship.m_mastObject.transform;
                mastHeight = FindMastHeight(ship);
            }
            return mast.TransformPoint(Vector3.up * mastHeight);
        }

        // The mast turns around its own vertical axis, so its top is the highest point of its meshes on that axis.
        private static float FindMastHeight(Ship ship)
        {
            var mast = ship.m_mastObject.transform;
            float height = 0;
            foreach (var meshFilter in mast.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter.sharedMesh == null ||
                    (ship.m_sailObject && meshFilter.transform.IsChildOf(ship.m_sailObject.transform)))
                {
                    continue;
                }
                var bounds = meshFilter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner =
                        bounds.center +
                        Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    height = Mathf.Max(height, mast.InverseTransformPoint(meshFilter.transform.TransformPoint(corner)).y);
                }
            }
            return height > 0 ? height : FALLBACK_MAST_HEIGHT;
        }

        void FixedUpdate()
        {
            if (!shipControls || !VHVRConfig.IsGesturedSteeringEnabled())
            {
                return;
            }

            var wasSingleGrabbing = isSingleGrabbing;
            var wasDoubleGrabbing = isDoubleGrabbing;
            isLeftGrabbing =
                leftHandGesture.isHandFree() && VRInputActions.valheim_Grab.GetState(VRInputSource.LeftHand);
            isRightGrabbing =
                rightHandGesture.isHandFree() && VRInputActions.valheim_Grab.GetState(VRInputSource.RightHand);
            isSingleGrabbing = isLeftGrabbing ^ isRightGrabbing;
            isDoubleGrabbing = isLeftGrabbing && isRightGrabbing;

            if (!isSingleGrabbing)
            {
                isSteering = false;
            }
            else if (!wasSingleGrabbing && !wasDoubleGrabbing)
            {
                var isHandBehindBack = isLeftGrabbing ? Utilities.Pose.isBehindBack(VRPlayer.leftHandBone) : Utilities.Pose.isBehindBack(VRPlayer.rightHandBone);
                if (!isHandBehindBack)
                {
                    isSteering = true;
                    isSteeringWithLeftHand = isLeftGrabbing;
                    steeringStartTime = Time.time;
                    PlaceSteeringWheel();
                }
            }

            var ship = shipControls.m_ship;

            Vector3 upDirection = this.upDirection;
            isRowing = false;

            if (!isDoubleGrabbing)
            {
                isOperatingSail = false;
            }
            else if (!wasDoubleGrabbing)
            {
                if (IsEitherHandGrippingSailRope(upDirection))
                {
                    Vector3 handSpan = VRPlayer.rightHand.transform.position - VRPlayer.leftHand.transform.position;
                    if (Vector3.Angle(handSpan, upDirection) < MAX_SAIL_PULL_ANGLE ||
                        Vector3.Angle(-handSpan, upDirection) < MAX_SAIL_PULL_ANGLE)
                    {
                        sailOperationTargetSpeed = sailOperationStartSpeed = ship.m_speed;
                        sailOperationStartHandHeight = GetHandHeight();
                        isOperatingSail = true;
                    }
                }
            }

            if (isOperatingSail)
            {
                UpdateSailOperationTargetSpeed(upDirection);
                ApplySpeedControl(sailOperationTargetSpeed);
                return;
            } 

            bool isUsingSail = ship.m_speed == Ship.Speed.Full || ship.m_speed == Ship.Speed.Half;
            if (!isUsingSail)
            {
                if (isDoubleGrabbing)
                {
                    isRowing = true;
                    ApplySpeedControl(GetRowingShipSpeed(upDirection, out int turnDirection));
                    if (turnDirection != 0)
                    {
                        ship.ApplyControlls(new Vector3(turnDirection, 0, 0));
                    }
                    return;
                }
                if (wasDoubleGrabbing)
                {
                    ApplySpeedControl(Ship.Speed.Stop);
                    return;
                }
            }

            if (isSteering)
            {
                float speed =
                    Vector3.Dot(
                        isLeftGrabbing ? VRPlayer.leftHandPhysicsEstimator.GetVelocity() : -VRPlayer.rightHandPhysicsEstimator.GetVelocity(),
                        ship.transform.forward);
                ship.ApplyControlls(
                    new Vector3(
                        speed < -MIN_RUDDER_TURN_SPEED ? -1 : speed > MIN_RUDDER_TURN_SPEED ? 1 : 0,
                        0,
                        0));
                return;
            }
        }

        private void UpdateSailOperationTargetSpeed(Vector3 upDirection)
        {
            if (sailOperationTargetSpeed != sailOperationStartSpeed)
            {
                // Already changed speed once in the current sail operation. Do not change speed again.
                return;
            }

            var handMovement = GetHandHeight() - sailOperationStartHandHeight;

            if (handMovement < -MIN_SAIL_PULL_DISTANCE)
            {
                switch (sailOperationStartSpeed)
                {
                    case Ship.Speed.Back:
                    case Ship.Speed.Stop:
                    case Ship.Speed.Slow:
                        sailOperationTargetSpeed = Ship.Speed.Half;
                        return;
                    default:
                        sailOperationTargetSpeed = Ship.Speed.Full;
                        return;
                }
            }

            if (handMovement > MIN_SAIL_PULL_DISTANCE)
            {
                switch (sailOperationStartSpeed)
                {
                    case Ship.Speed.Full:
                        sailOperationTargetSpeed = Ship.Speed.Half;
                        return;
                    default:
                        sailOperationTargetSpeed = Ship.Speed.Stop;
                        return;
                }
            }
        }

        private Ship.Speed GetRowingShipSpeed(Vector3 upDirection, out int turnDirection)
        {
            var ship = shipControls.m_ship;
            var lateral = Vector3.Cross(upDirection, ship.transform.forward).normalized;
            Vector3 saggitalArmSpanDirection =
                Vector3.ProjectOnPlane(VRPlayer.rightHand.transform.position - VRPlayer.leftHand.transform.position, lateral).normalized;
            float leftHandSpeed =
                Vector3.Dot(
                    Vector3.Cross(VRPlayer.leftHandPhysicsEstimator.GetAverageVelocityInSnapshots(), saggitalArmSpanDirection),
                    lateral);
            float rightHandSpeed =
                Vector3.Dot(
                    Vector3.Cross(saggitalArmSpanDirection, VRPlayer.rightHandPhysicsEstimator.GetAverageVelocityInSnapshots()),
                    lateral);
            float speed = leftHandSpeed + rightHandSpeed;
            float leftHandContribution = speed > 0 ? leftHandSpeed : -leftHandSpeed;
            float rightHandContribution = speed > 0 ? rightHandSpeed : -rightHandSpeed;
            if (leftHandContribution < 0.5f &&  rightHandContribution > 1)
            {
                turnDirection = -1;
            } 
            else if (leftHandContribution > 1 && rightHandContribution < 0.5f)
            {
                turnDirection = 1;
            }
            else if (leftHandContribution > 1.25f && rightHandContribution > 1.25f)
            {
                // Center the rudder.
                turnDirection = ship.m_rudderValue < 0 ? 1 : ship.m_rudderValue == 0 ? 0 : -1;
            }
            else
            {
                turnDirection = 0;
            }

            if (speed < -MIN_ROW_SPEED)
            {
                return Ship.Speed.Back;
            }

            if (speed > MIN_ROW_SPEED)
            {
                return Ship.Speed.Slow;
            }

            return Ship.Speed.Stop;
        }

        private void ApplySpeedControl(Ship.Speed targetSpeed)
        {
            var ship = shipControls.m_ship;

            if (ship.m_speed == Ship.Speed.Stop && targetSpeed == Ship.Speed.Back)
            {
                ship.Backward();
                return;
            }

            if (ship.m_speed == Ship.Speed.Back && targetSpeed == Ship.Speed.Stop)
            {
                ship.Forward();
                return;
            }

            if (shipControls.m_ship.m_speed < targetSpeed)
            {
                shipControls.m_ship.Forward();
            }
            else if (shipControls.m_ship.m_speed > targetSpeed)
            {
                shipControls.m_ship.Backward();
            }
        }

        private float GetHandHeight()
        {
            return VRPlayer.instance.transform.InverseTransformPoint(Vector3.Lerp(VRPlayer.leftHand.transform.position, VRPlayer.rightHand.transform.position, 0.5f)).y;
        }
    }
}
