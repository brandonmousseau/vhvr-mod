using UnityEngine;
using ValheimVRMod.VRCore;
using Valve.VR;

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

        void FixedUpdate()
        {
            if (!shipControls)
            {
                return;
            }

            var wasSingleGrabbing = isSingleGrabbing;
            var wasDoubleGrabbing = isDoubleGrabbing;
            var isLeftGrabbing =
                leftHandGesture.isHandFree() && SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand);
            var isRightGrabbing =
                rightHandGesture.isHandFree() && SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand);
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
                }
            }

            var ship = shipControls.m_ship;

            Vector3 upDirection = VRPlayer.instance != null ? VRPlayer.instance.transform.up : ship.transform.up;

            if (!isDoubleGrabbing)
            {
                isOperatingSail = false;
            }
            else if (!wasDoubleGrabbing)
            {
                if (Vector3.Angle(VRPlayer.leftHandBone.right, upDirection) < MAX_SAIL_PULL_ANGLE ||
                    Vector3.Angle(-VRPlayer.rightHandBone.right, upDirection) < MAX_SAIL_PULL_ANGLE)
                {
                    Vector3 handSpan = VRPlayer.rightHandBone.position - VRPlayer.leftHandBone.position;
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
                    ApplySpeedControl(GetRowingShipSpeed(upDirection));
                    return;
                }
                else if (wasDoubleGrabbing)
                {
                    ApplySpeedControl(Ship.Speed.Stop);
                    return;
                }
            }

            if (isSteering)
            {
                float speed =
                    Vector3.Dot(
                        isLeftGrabbing ? -VRPlayer.leftHandPhysicsEstimator.GetVelocity() : VRPlayer.rightHandPhysicsEstimator.GetVelocity(),
                        ship.transform.forward);
                shipControls.m_ship.ApplyControlls(
                    new Vector3(
                        speed < -MIN_RUDDER_TURN_SPEED ? 1 : speed > MIN_RUDDER_TURN_SPEED ? -1 : 0,
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

        private Ship.Speed GetRowingShipSpeed(Vector3 upDirection)
        {
            var ship = shipControls.m_ship;
            var saggitalNormal = Vector3.Cross(upDirection, ship.transform.forward).normalized;
            Vector3 saggitalArmSpan =
                Vector3.ProjectOnPlane(VRPlayer.rightHandBone.position - VRPlayer.leftHandBone.position, saggitalNormal);
            float speed =
                Vector3.Dot(
                    Vector3.Cross(
                        saggitalArmSpan.normalized,
                        VRPlayer.rightHandPhysicsEstimator.GetAverageVelocityInSnapshots() - VRPlayer.leftHandPhysicsEstimator.GetAverageVelocityInSnapshots()),
                    saggitalNormal);

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
            return VRPlayer.instance.transform.InverseTransformPoint(Vector3.Lerp(VRPlayer.leftHandBone.position, VRPlayer.rightHandBone.position, 0.5f)).y;
        }
    }
}
