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
        private const float MIN_SAIL_PULL_SPEED = 0.3f;

        private HandGesture leftHandGesture;
        private HandGesture rightHandGesture;
        private bool isSingleGrabbing;
        private bool isDoubleGrabbing;
        private bool isSteering;
        private bool isOperatingSail;
        private Ship.Speed sailOperationStartSpeed;

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
                leftHandGesture.isHandFree() && !Utilities.Pose.isBehindBack(VRPlayer.leftHandBone) && SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand);
            var isRightGrabbing =
                rightHandGesture.isHandFree() && !Utilities.Pose.isBehindBack(VRPlayer.rightHandBone) && SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand);
            isSingleGrabbing = isLeftGrabbing ^ isRightGrabbing;
            isDoubleGrabbing = isLeftGrabbing && isRightGrabbing;

            if (!isSingleGrabbing)
            {
                isSteering = false;
            }
            else if (!wasSingleGrabbing && !wasDoubleGrabbing)
            {
                isSteering = true;
            }

            var ship = shipControls.m_ship;

            Vector3 upDirection = VRPlayer.instance != null ? VRPlayer.instance.transform.up : ship.transform.up;

            if (!isDoubleGrabbing)
            {
                isOperatingSail = false;
            }
            else if (!wasDoubleGrabbing &&
                Vector3.Angle(VRPlayer.leftHandBone.right, upDirection) < MAX_SAIL_PULL_ANGLE &&
                Vector3.Angle(-VRPlayer.rightHandBone.right, upDirection) < MAX_SAIL_PULL_ANGLE)
            {
                Vector3 handSpan = VRPlayer.rightHandBone.position - VRPlayer.leftHandBone.position;
                if (Vector3.Angle(handSpan, upDirection) < MAX_SAIL_PULL_ANGLE ||
                    Vector3.Angle(-handSpan, upDirection) < MAX_SAIL_PULL_ANGLE)
                {
                    sailOperationStartSpeed = ship.m_speed;
                    isOperatingSail = true;
                }
            }

            if (isSteering)
            {
                Vector3 handVelocity = isLeftGrabbing ? VRPlayer.leftHandPhysicsEstimator.GetVelocity() : VRPlayer.rightHandPhysicsEstimator.GetVelocity();
                float speed = Vector3.Dot(handVelocity, ship.transform.forward);
                shipControls.m_ship.ApplyControlls(new Vector3(speed < -MIN_RUDDER_TURN_SPEED ? 1 : speed > MIN_RUDDER_TURN_SPEED ? -1 : 0, 0, 0));
                return;
            }

            if (isOperatingSail)
            {
                ChangeSpeed(GetSailSpeed(upDirection));
                return;
            }

            bool isUsingSail = ship.m_speed == Ship.Speed.Full || ship.m_speed == Ship.Speed.Half;
            if (isUsingSail)
            {
                return;
            }

            if (isDoubleGrabbing)
            {
                ChangeSpeed(GetRowingShipSpeed(upDirection));
            }
            else if (wasDoubleGrabbing)
            {
                ChangeSpeed(Ship.Speed.Stop);
            }
        }

        private Ship.Speed GetSailSpeed(Vector3 upDirection)
        {
            var leftHandSpeed = Vector3.Dot(VRPlayer.leftHandPhysicsEstimator.GetVelocity(), upDirection);
            var rightHandSpeed = Vector3.Dot(VRPlayer.rightHandPhysicsEstimator.GetVelocity(), upDirection);

            if (leftHandSpeed < -MIN_SAIL_PULL_SPEED && rightHandSpeed < -MIN_SAIL_PULL_SPEED)
            {
                switch (sailOperationStartSpeed)
                {
                    case Ship.Speed.Back:
                    case Ship.Speed.Stop:
                    case Ship.Speed.Slow:
                        return Ship.Speed.Half;
                    default:
                        return Ship.Speed.Full;
                }
            }

            if (leftHandSpeed > MIN_SAIL_PULL_SPEED && rightHandSpeed > MIN_SAIL_PULL_SPEED)
            {
                switch (sailOperationStartSpeed)
                {
                    case Ship.Speed.Full:
                        return Ship.Speed.Half;
                    default:
                        return Ship.Speed.Stop;
                }
            }

            return shipControls.m_ship.m_speed;
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
                        VRPlayer.rightHandPhysicsEstimator.GetVelocity() - VRPlayer.leftHandPhysicsEstimator.GetVelocity()),
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

        private void ChangeSpeed(Ship.Speed desiredSpeed)
        {
            var ship = shipControls.m_ship;

            if (ship.m_speed == Ship.Speed.Stop && desiredSpeed == Ship.Speed.Back)
            {
                ship.Backward();
                return;
            }

            if (ship.m_speed == Ship.Speed.Back && desiredSpeed == Ship.Speed.Stop)
            {
                ship.Forward();
                return;
            }

            if (shipControls.m_ship.m_speed < desiredSpeed)
            {
                shipControls.m_ship.Forward();
            }
            else if (shipControls.m_ship.m_speed > desiredSpeed)
            {
                shipControls.m_ship.Backward();
            }
        }
    }
}
