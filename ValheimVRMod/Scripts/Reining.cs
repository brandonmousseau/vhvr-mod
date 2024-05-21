using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts
{
    public class Reining : MonoBehaviour
    {
        private const float MIN_TURNING_OFFSET = 0.3f;
        private const float SHAKE_SPEED_THRESHOLD = 1f;
        private const float REIN_ANGLE_TOLERANCE = 30f;
        private const float MAX_STOP_REIN_DISTNACE = 0.33f;
        private const float MIN_STOP_REIN_HAND_SPEED = 0.75f;
        private const float MIN_LEANING_DISTANCE_TO_START_GALLOPPING = 0.4f;
        private const float RUN_CUE_TIMEOUT = 0.5f;
        private static readonly Dictionary<string, Vector3> REIN_ATTACH_OFFSETS =
            new Dictionary<string, Vector3>
            {
                {
                    "default",
                    new Vector3(-0.375f, -0.25f, 1.5f)
                },
                {
                    "$enemy_lox",
                    new Vector3(-1, -0.5f, 1.5f)
                }
            };
                

        private Vector3 leftReinAttachLocalPosition;
        private Vector3 rightReinAttachLocalPosition;
        private LineRenderer lineRenderer;
        private bool isTurning;
        private Sadle.Speed leftHandSlowDownResult = Sadle.Speed.Stop;
        private Sadle.Speed rightHandSlowDownResult = Sadle.Speed.Stop;
        private float leftRunCueCountDown = Mathf.NegativeInfinity;
        private float rightRunCueCountDown = Mathf.NegativeInfinity;

        private Sadle sadle 
        { 
            get
            {
                var controller = Player.m_localPlayer?.m_doodadController;
                return (controller != null && controller is Sadle) ? (Sadle)controller : null;
            }
        }

        public HandGesture leftHandGesture;
        public HandGesture rightHandGesture;

        public static Vector3 targetDirection { get; private set; }
        public static bool shouldOverrideSpeedOrDirection { get; private set; }
        public static bool turnInPlace { get; private set; }

        void Awake()
        {
            lineRenderer = new GameObject().AddComponent<LineRenderer>();
            lineRenderer.transform.parent = transform;
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 4;
            lineRenderer.material = Instantiate(VRAssetManager.GetAsset<Material>("StandardClone"));
            lineRenderer.material.color = new Color(0.25f, 0.125f, 0);
            lineRenderer.widthMultiplier = 0.03f;
            lineRenderer.enabled = false;
            lineRenderer.loop = true;
        }

        void Update()
        {
            if (!sadle)
            {
                lineRenderer.enabled = false;
                return;
            }

            var isLeftHandReining = IsReining(leftHandGesture, SteamVR_Input_Sources.LeftHand);
            var isRightHandReining = IsReining(rightHandGesture, SteamVR_Input_Sources.RightHand);

            UpdateReinVisuals(isLeftHandReining, isRightHandReining);

            var wasTurning = isTurning;
            targetDirection =
                GetCuedTargetDirection(isLeftHandReining, isRightHandReining, out isTurning, out bool areHandsPullingInOppositeDirections);

            if (leftRunCueCountDown > 0)
            {
                leftRunCueCountDown -= Time.deltaTime;
            }
            if (rightRunCueCountDown > 0)
            {
                rightRunCueCountDown -= Time.deltaTime;
            }

            var targetSpeed = GetCuedTargetSpeed(isLeftHandReining, isRightHandReining);
            if (areHandsPullingInOppositeDirections)
            {
                targetSpeed = Sadle.Speed.Stop;
            }
            if (wasTurning || isTurning)
            {
                if (targetSpeed == Sadle.Speed.Stop)
                {
                    targetSpeed = Sadle.Speed.Turn;
                }
            }
            else if (targetSpeed == Sadle.Speed.Turn)
            {
                targetSpeed = Sadle.Speed.Stop;
            }

            turnInPlace = (targetSpeed == Sadle.Speed.Turn);

            shouldOverrideSpeedOrDirection = (wasTurning || isLeftHandReining || isRightHandReining);
            if (!shouldOverrideSpeedOrDirection)
            {
                return;
            }

            var stickOutput =
                (targetSpeed == Sadle.Speed.NoChange || turnInPlace) ?
                Vector3.zero :
                (targetSpeed == Sadle.Speed.Stop ?  -Vector3.forward :  Vector3.forward);

            sadle.ApplyControlls(
                stickOutput, (Vector3)targetDirection, targetSpeed == Sadle.Speed.Run, autoRun: false, turnInPlace);
        }

        public void SetReinAttach()
        {
            if (!sadle)
            {
                return;
            }

            var mountName = sadle.m_monsterAI.m_character.m_name;
            Vector3 offset;
            if (REIN_ATTACH_OFFSETS.ContainsKey(mountName))
            {
                LogUtils.LogDebug("Setting rein attach position for + " + mountName);
                offset = REIN_ATTACH_OFFSETS[mountName];
            }
            else
            {
                LogUtils.LogWarning("Cannot find rein attach position for + " + mountName);
                offset = REIN_ATTACH_OFFSETS["default"];
            }

            leftReinAttachLocalPosition =
                sadle.m_attachPoint.InverseTransformVector(
                    sadle.m_attachPoint.TransformDirection(offset.normalized) * offset.magnitude);
            rightReinAttachLocalPosition =
                sadle.m_attachPoint.InverseTransformVector(
                    sadle.m_attachPoint.TransformDirection(Vector3.Reflect(offset.normalized, Vector3.right)) *
                    offset.magnitude);
        }

        private void UpdateReinVisuals(bool isLeftHandReining, bool isRightHandReining)
        {
            if (!isLeftHandReining && !isRightHandReining)
            {
                lineRenderer.enabled = false;
                return;
            }

            var leftReinAttach = sadle.m_attachPoint.TransformPoint(leftReinAttachLocalPosition);
            var rightReinAttach = sadle.m_attachPoint.TransformPoint(rightReinAttachLocalPosition);

            Vector3 leftReinGrip = Vector3.zero;
            Vector3 rightReinGrip = Vector3.zero;
            if (isLeftHandReining)
            {
                leftReinGrip = VRPlayer.leftHandBone.position + VRPlayer.leftHandBone.up * 0.0625f;
                if (!isRightHandReining)
                {
                    rightReinGrip = Vector3.Lerp(leftReinGrip, rightReinAttach, 0.125f);
                }
            }
            if (isRightHandReining)
            {
                rightReinGrip = VRPlayer.rightHandBone.position + VRPlayer.rightHandBone.up * 0.0625f;
                if (!isLeftHandReining) {
                    leftReinGrip = Vector3.Lerp(rightReinGrip, leftReinAttach, 0.125f);
                }
            }

            lineRenderer.enabled = true;
            lineRenderer.SetPosition(0, leftReinAttach);
            lineRenderer.SetPosition(1, leftReinGrip);
            lineRenderer.SetPosition(2, rightReinGrip);
            lineRenderer.SetPosition(3, rightReinAttach);
        }

        private Sadle.Speed GetCuedTargetSpeed(bool isLeftHandReining, bool isRightHandReining)
        {
            var leaningVector = CameraUtils.getCamera(CameraUtils.VR_CAMERA).transform.position - sadle.m_attachPoint.position;
            leaningVector.y = 0;
            var leaning = leaningVector.magnitude;

            var leftHandCuedSpeed =
                 GetCuedTargetSpeed(
                     isLeftHandReining, VRPlayer.leftHandPhysicsEstimator, leaning, ref leftHandSlowDownResult, ref leftRunCueCountDown);
            var rightHandCuedSpeed =
                 GetCuedTargetSpeed(
                     isRightHandReining, VRPlayer.rightHandPhysicsEstimator, leaning, ref rightHandSlowDownResult, ref rightRunCueCountDown);

            if (isLeftHandReining && isRightHandReining)
            {
                var bothHandsAreCuingStop =
                    (leftHandCuedSpeed == Sadle.Speed.Stop && rightHandCuedSpeed == Sadle.Speed.Stop);
                // During two-handed reining, do not stop unless both hands are cueing stop.
                if (!bothHandsAreCuingStop && leftHandCuedSpeed == Sadle.Speed.Stop)
                {
                    leftHandCuedSpeed = Sadle.Speed.NoChange;
                }
                if (!bothHandsAreCuingStop && rightHandCuedSpeed == Sadle.Speed.Stop)
                {
                    rightHandCuedSpeed = Sadle.Speed.NoChange;
                }
            }
            
            if (leftHandCuedSpeed == Sadle.Speed.NoChange)
            {
                leftHandCuedSpeed = rightHandCuedSpeed;
            }
            else if (rightHandCuedSpeed == Sadle.Speed.NoChange)
            {
                rightHandCuedSpeed = leftHandCuedSpeed;
            }

            var cuedSpeed = rightHandCuedSpeed > leftHandCuedSpeed ? rightHandCuedSpeed : leftHandCuedSpeed;

            return cuedSpeed == Sadle.Speed.NoChange ? sadle.m_speed : cuedSpeed;
        }

        private Sadle.Speed GetCuedTargetSpeed(
            bool isReining, PhysicsEstimator handPhysicsEstimator, float leaning, ref Sadle.Speed slowDownResult, ref float runCueCountDown) {
            if (!isReining)
            {
                slowDownResult = Sadle.Speed.Stop;
                runCueCountDown = Mathf.NegativeInfinity;
                return Sadle.Speed.NoChange;
            }

            if (sadle.m_speed == Sadle.Speed.Run)
            {
                slowDownResult = Sadle.Speed.Walk;
            }
            else if (sadle.m_speed == Sadle.Speed.Stop)
            {
                slowDownResult = Sadle.Speed.Stop;
            }

            var handOffset = handPhysicsEstimator.transform.position - sadle.m_attachPoint.position;
            handOffset.y = 0;
            var handOffsetAmount = handOffset.magnitude;

            var v = handPhysicsEstimator.GetAverageVelocityInSnapshots();
            if (v.y < -SHAKE_SPEED_THRESHOLD && Vector3.Angle(v, Vector3.down) < REIN_ANGLE_TOLERANCE && handOffsetAmount > MAX_STOP_REIN_DISTNACE)
            {
                bool shouldRun = 
                    (runCueCountDown > RUN_CUE_TIMEOUT || leaning >= MIN_LEANING_DISTANCE_TO_START_GALLOPPING || sadle.m_speed == Sadle.Speed.Run);
                runCueCountDown = RUN_CUE_TIMEOUT;
                return shouldRun ? Sadle.Speed.Run : Sadle.Speed.Walk;
            }

            if (v.y > SHAKE_SPEED_THRESHOLD && runCueCountDown > 0)
            {
                runCueCountDown = RUN_CUE_TIMEOUT * 2;
            }

            var handHorizontalVelocity = v;
            handHorizontalVelocity.y = 0;
            var handHorizontalSpped = handHorizontalVelocity.magnitude;

            if (Vector3.Angle(handHorizontalVelocity, -GetCurrentDirection()) < REIN_ANGLE_TOLERANCE &&
                handHorizontalSpped > MIN_STOP_REIN_HAND_SPEED &&
                handOffsetAmount < MAX_STOP_REIN_DISTNACE)
            {
                return slowDownResult;
            }

            if (handHorizontalSpped < MIN_STOP_REIN_HAND_SPEED)
            {
                if (sadle.m_speed != Sadle.Speed.Run)
                {
                    slowDownResult = Sadle.Speed.Stop;
                }
            }
            return Sadle.Speed.NoChange;
        }

        private Vector3 GetCuedTargetDirection(
            bool isLeftHandReining, bool isRightHandReining, out bool shouldTurn, out bool areHandsPullingInOppositeDirections)
        {
            var currentDirection = GetCurrentDirection();
            areHandsPullingInOppositeDirections = false;

            if (!isLeftHandReining && !isRightHandReining)
            {
                shouldTurn = false;
                return currentDirection;
            }

            int turnDirection = 0;
            Vector3 cuedTargetDirection = Vector3.zero;

            if (isLeftHandReining && isRightHandReining)
            {
                var handSpan = VRPlayer.rightHand.transform.position - VRPlayer.leftHand.transform.position;
                handSpan.y = 0;
                cuedTargetDirection = Vector3.Cross(handSpan, Vector3.up);
                turnDirection = GetTurnDirection(currentDirection, cuedTargetDirection);
            }

            if (turnDirection == 0)
            {
                var leftHandCuedTargetDirection =
                    GetOneHandedCuedTargetDirection(VRPlayer.leftHandBone, currentDirection, isLeftHandReining);
                var rightHandCuedTargetDirection =
                    GetOneHandedCuedTargetDirection(VRPlayer.rightHandBone, currentDirection, isRightHandReining);
                int leftHandCuedTurn = GetTurnDirection(currentDirection, leftHandCuedTargetDirection);
                int rightHandCuedTurn = GetTurnDirection(currentDirection, rightHandCuedTargetDirection);
                areHandsPullingInOppositeDirections = (leftHandCuedTurn * rightHandCuedTurn < 0);
                turnDirection = leftHandCuedTurn + rightHandCuedTurn;
                if (turnDirection != 0)
                {
                    cuedTargetDirection = Vector3.Lerp(leftHandCuedTargetDirection, rightHandCuedTargetDirection, rightHandCuedTurn / turnDirection);
                }
            }

            shouldTurn = (turnDirection != 0);
            return shouldTurn ? cuedTargetDirection.normalized : currentDirection;
        }

        private Vector3 GetOneHandedCuedTargetDirection(Transform hand, Vector3 currentDirection, bool isReining)
        {
            if (!isReining)
            {
                return currentDirection;
            }
            var cuedTargetDirection = hand.position - sadle.m_attachPoint.position;
            cuedTargetDirection.y = 0;
            return cuedTargetDirection;
        }

        // Returns -1 if turning left, 0 if not turning, 1, if turning right.
        private static int GetTurnDirection(Vector3 currentDirection, Vector3 targetDirection)
        {
            var lateralOffset = Vector3.Dot(Vector3.Cross(targetDirection, currentDirection), Vector3.up);
            return lateralOffset <= -MIN_TURNING_OFFSET ? -1 : (lateralOffset < MIN_TURNING_OFFSET ? 0 : 1);
        }

        private bool IsReining(HandGesture handGesture, SteamVR_Input_Sources inputSource)
        {
            return handGesture.isHandFree() && SteamVR_Actions.valheim_Grab.GetState(inputSource);
        }

        private Vector3 GetCurrentDirection()
        {
            Vector3 v = sadle.m_attachPoint.forward;
            v.y = 0;
            return v;
        }
    }
}
