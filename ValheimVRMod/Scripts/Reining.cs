using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts
{
    public class Reining : MonoBehaviour
    {
        private const float MIN_TURNING_OFFSET = 0.25f;
        private const float SHAKE_SPEED_THRESHOLD = -1f;
        private const float MAX_STOP_REIN_RETRACTING_ANGLE = 30f;
        private const float MAX_STOP_REIN_DISTNACE = 0.25f;
        private const float MIN_STOP_REIN_HAND_SPEED = 0.75f;
        private const float MIN_LEANING_DISTANCE_TO_START_GALLOPPING = 0.4f;
        private static readonly Dictionary<string, Vector3> REIN_ATTACH_OFFSETS =
            new Dictionary<string, Vector3>
            {
                {
                    "default",
                    new Vector3(-0.375f, -1.5f, -0.125f)
                },
                {
                    "$enemy_lox",
                    new Vector3(-0.75f, -1f, -0.5f)
                }
            };
                

        private Vector3 leftReinAttachLocalPosition;
        private Vector3 rightReinAttachLocalPosition;
        private LineRenderer lineRenderer;
        private bool isTurning;

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
        {   lineRenderer = new GameObject().AddComponent<LineRenderer>();
            lineRenderer.transform.parent = transform;
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 4;
            lineRenderer.material = Instantiate(VRAssetManager.GetAsset<Material>("StandardClone"));
            lineRenderer.material.color = new Color(0.25f, 0.125f, 0);
            lineRenderer.widthMultiplier = 0.03f;
            lineRenderer.enabled = false;
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
            if (isLeftHandReining || isRightHandReining)
            {
                var leftReinAttach = sadle.transform.TransformPoint(leftReinAttachLocalPosition);
                var rightReinAttach = sadle.transform.TransformPoint(rightReinAttachLocalPosition);
                lineRenderer.enabled = true;
                lineRenderer.SetPosition(0, leftReinAttach);
                lineRenderer.SetPosition(1, isLeftHandReining ? VRPlayer.leftHandBone.position : leftReinAttach);
                lineRenderer.SetPosition(2, isRightHandReining ? VRPlayer.rightHandBone.position : rightReinAttach);
                lineRenderer.SetPosition(3, rightReinAttach);
            }
            else
            {
                lineRenderer.enabled = false;
            }
            
            bool changeDirection = UpdateTargetDirection(isLeftHandReining, isRightHandReining);

            var targetSpeed = GetTargetSpeed(isLeftHandReining, isRightHandReining);
            if (targetSpeed == Sadle.Speed.Stop && changeDirection)
            {
                targetSpeed = Sadle.Speed.Turn;
            }
   
            shouldOverrideSpeedOrDirection = changeDirection || (targetSpeed != Sadle.Speed.NoChange && targetSpeed != sadle.m_speed);
            turnInPlace = (targetSpeed == Sadle.Speed.Turn);
            
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
                sadle.transform.InverseTransformVector(
                    sadle.transform.TransformDirection(offset.normalized) * offset.magnitude);
            rightReinAttachLocalPosition =
                sadle.transform.InverseTransformVector(
                    sadle.transform.TransformDirection(Vector3.Reflect(offset.normalized, Vector3.right)) *
                    offset.magnitude);
        }

        private Sadle.Speed GetTargetSpeed(bool isLeftHandReining, bool isRightHandReining)
        {
            var leaningVector = CameraUtils.getCamera(CameraUtils.VR_CAMERA).transform.position - sadle.m_attachPoint.position;
            leaningVector.y = 0;
            var leaning = leaningVector.magnitude;

            var leftHandReiningSpeed =
                isLeftHandReining ? GetReiningSpeed(VRPlayer.leftHandPhysicsEstimator, leaning) : Sadle.Speed.NoChange;
            var rightHandReiningSpeed =
                isRightHandReining ? GetReiningSpeed(VRPlayer.rightHandPhysicsEstimator, leaning) : Sadle.Speed.NoChange;

            if (leftHandReiningSpeed == Sadle.Speed.NoChange)
            {
                if (rightHandReiningSpeed != Sadle.Speed.NoChange)
                {
                    return rightHandReiningSpeed;
                }
                return sadle.m_speed == Sadle.Speed.Turn ? Sadle.Speed.Stop : sadle.m_speed;
            }
            else if (rightHandReiningSpeed == Sadle.Speed.NoChange)
            {
                return leftHandReiningSpeed;
            }

            return rightHandReiningSpeed > leftHandReiningSpeed ? rightHandReiningSpeed : leftHandReiningSpeed;
        }

        private Sadle.Speed GetReiningSpeed(PhysicsEstimator handPhysicsEstimator, float leaning) {
            var v = handPhysicsEstimator.GetAverageVelocityInSnapshots();
            if (v.y < SHAKE_SPEED_THRESHOLD)
            {
                return (leaning >= MIN_LEANING_DISTANCE_TO_START_GALLOPPING || sadle.m_speed == Sadle.Speed.Run) ?
                    Sadle.Speed.Run :
                    Sadle.Speed.Walk;
            }

            var retractingVector = transform.position - handPhysicsEstimator.transform.position;
            retractingVector.y = 0;
            var horizontalVelocity = v;
            horizontalVelocity.y = 0;
            if (Vector3.Angle(horizontalVelocity, retractingVector) < MAX_STOP_REIN_RETRACTING_ANGLE &&
                horizontalVelocity.magnitude > MIN_STOP_REIN_HAND_SPEED)
            {
                if (retractingVector.magnitude < MAX_STOP_REIN_DISTNACE)
                {
                    return Sadle.Speed.Stop;
                }

                if (sadle.m_speed == Sadle.Speed.Run)
                {
                    // Slow down
                    return Sadle.Speed.Walk;
                } 
            }

            return Sadle.Speed.NoChange;
        }


        private bool UpdateTargetDirection(bool isLeftHandReining, bool isRightHandReining)
        {
            var currentDirection = GetCurrentDirection();
            var leftHandReiningDirection = GetReiningDirection(VRPlayer.leftHand.transform, isLeftHandReining, out int leftReinTurning);
            var rightHandReiningDirection = GetReiningDirection(VRPlayer.rightHand.transform, isRightHandReining, out int rightReinTurning);

            var wasTurning = isTurning;
            if (leftReinTurning + rightReinTurning == 0)
            {
                targetDirection = currentDirection;
                isTurning = false;
                return wasTurning;
            }

            isTurning = true;

            if (rightReinTurning == 0)
            {
                targetDirection = leftHandReiningDirection;
            }
            else if (leftReinTurning == 0)
            {
                targetDirection = rightHandReiningDirection;
            }
            else
            {
                targetDirection = Vector3.Lerp(leftHandReiningDirection, rightHandReiningDirection, 0.5f);
            }

            return true;
        }

        private Vector3 GetReiningDirection(Transform hand, bool isReining, out int turnDirection)
        {
            turnDirection = 0;

            if (!sadle)
            {
                return Vector3.zero;
            }

            var currentDirection = GetCurrentDirection();

            if (!isReining)
            {
                return currentDirection;
            }

            var handOffset = hand.position - transform.position;
            handOffset.y = 0;
            var lateralOffset = Vector3.Cross(handOffset, currentDirection).y;

            if (Mathf.Abs(lateralOffset) < MIN_TURNING_OFFSET)
            {
                return currentDirection;
            }

            turnDirection = (int)Mathf.Sign(lateralOffset);
            return handOffset.normalized;
        }

        private bool IsReining(HandGesture handGesture, SteamVR_Input_Sources inputSource)
        {
            return handGesture.areHandsFree() && SteamVR_Actions.valheim_Grab.GetState(inputSource);
        }

        private Vector3 GetCurrentDirection()
        {
            Vector3 v = sadle.m_monsterAI.m_character.transform.forward;
            v.y = 0;
            return v;
        }
    }
}
