using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts
{
    public class ShipSteering : MonoBehaviour
    {
        private const float ROW_RADIUS = 0.125f;
        private const float MIN_ROW_SPEED = 0.5f;
        private const float MAX_RUDDER_ANGLE = 45;
        private const float MIN_SAIL_OPERATION_DISTANCE = 0.25f;
        private const float RUDDER_CHANGE_THRESHOLD = 0.0625f;

        private HandGesture handGesture;
        private SteamVR_Input_Sources inputSource;
        private PhysicsEstimator handPhysicsEstimator;
        private bool isGrabbing;
        private Transform grabStart;
        private float grabStartRudderValue = 0;
        private float lastRudderValue = 0;
        private bool isOperatingSail = false;
        private Ship.Speed grabStartSpeed;

        private ShipControlls shipControls
        {
            get
            {
                var controller = Player.m_localPlayer?.m_doodadController;
                return (controller != null && controller is ShipControlls) ? (ShipControlls)controller : null;
            }
        }

        public void Initialize(HandGesture handGesture, SteamVR_Input_Sources inputSource, PhysicsEstimator handPhysicsEstimator)
        {
            this.handGesture = handGesture;
            this.inputSource = inputSource;
            this.handPhysicsEstimator = handPhysicsEstimator;
        }

        void FixedUpdate()
        {
            if (!shipControls)
            {
                return;
            }

            var wasGrabbing = isGrabbing;
            isGrabbing =
                handGesture.isHandFree() && SteamVR_Actions.valheim_Use.GetState(inputSource) && SteamVR_Actions.valheim_Grab.GetState(inputSource);

            if (!isGrabbing)
            {
                return;
            }

            if (!wasGrabbing)
            {
                OnGrabStart();
            }

            var ship = shipControls.m_ship;

            Ship.Speed desiredSpeed;
            float desiredSteerTurn = 0;
            if (isOperatingSail)
            {
                desiredSpeed = GetSailSpeed();
            }
            else
            {
                desiredSpeed = GetRowingShipSpeed();
                var rudderValue = GetRudderValue();
                if (Mathf.Abs(rudderValue - lastRudderValue) > RUDDER_CHANGE_THRESHOLD)
                {
                    lastRudderValue = rudderValue;
                }
                desiredSteerTurn = Mathf.Sign(lastRudderValue - ship.m_rudderValue);
            }

            if (shipControls.m_ship.m_speed == Ship.Speed.Stop && desiredSpeed == Ship.Speed.Back)
            {
                shipControls.m_ship.Backward();
            }
            else if (shipControls.m_ship.m_speed == Ship.Speed.Back && desiredSpeed == Ship.Speed.Stop)
            {
                shipControls.m_ship.Forward();
            }
            else if (shipControls.m_ship.m_speed < desiredSpeed)
            {
                shipControls.m_ship.Forward();
            }
            else if (shipControls.m_ship.m_speed > desiredSpeed)
            {
                shipControls.m_ship.Backward();
            }

            shipControls.m_ship.ApplyControlls(new Vector3(desiredSteerTurn, 0, 0));
        }


        private void OnGrabStart()
        {
            if (grabStart == null)
            {
                grabStart = new GameObject().transform;
            }
            grabStart.parent = VRPlayer.instance.transform;
            grabStart.SetPositionAndRotation(transform.position, transform.rotation);
            grabStartRudderValue = lastRudderValue = shipControls.m_ship.m_rudderValue;
            grabStartSpeed = shipControls.m_ship.m_speed;
            isOperatingSail = (Mathf.Abs(Vector3.Dot(grabStart.forward, shipControls.m_attachPoint.up)) < 0.7f);
        }

        private Ship.Speed GetRowingShipSpeed()
        {
            var ship = shipControls.m_ship;
            if (ship.m_speed != Ship.Speed.Back && ship.m_speed != Ship.Speed.Stop && ship.m_speed != Ship.Speed.Slow)
            {
                return ship.m_speed;
            }

            var rowCenter = grabStart.position - ship.transform.up * ROW_RADIUS;
            var rowOffset = Vector3.ProjectOnPlane(transform.position - rowCenter, ship.transform.right);
            var rowingSpeed = Vector3.Dot(Vector3.Cross(rowOffset.normalized, handPhysicsEstimator.GetVelocity()), ship.transform.right);
            if (rowingSpeed < -MIN_ROW_SPEED)
            {
                return Ship.Speed.Back;
            }
            else if (rowingSpeed < MIN_ROW_SPEED)
            {
                return Ship.Speed.Stop;
            }
            else
            {
                return Ship.Speed.Slow;
            }
        }

        private Ship.Speed GetSailSpeed()
        {
            var verticalMovement = Vector3.Dot(transform.position - grabStart.position, shipControls.m_ship.transform.up);
            if (verticalMovement < -MIN_SAIL_OPERATION_DISTANCE)
            {
                switch (grabStartSpeed)
                {
                    case Ship.Speed.Back:
                    case Ship.Speed.Stop:
                    case Ship.Speed.Slow:
                        return Ship.Speed.Half;
                    case Ship.Speed.Half:
                        return Ship.Speed.Full;
                }
            }
            else if (verticalMovement > MIN_SAIL_OPERATION_DISTANCE)
            {
                switch (grabStartSpeed)
                {
                    case Ship.Speed.Full:
                        return Ship.Speed.Half;
                    case Ship.Speed.Half:
                        return Ship.Speed.Stop;
                }
            }
            return shipControls.m_ship.m_speed;
        }

        private float GetRudderValue()
        {
            var ship = shipControls.m_ship;
            if (isOperatingSail)
            {
                return ship.m_rudderValue;
            }

            var startPointing = Vector3.ProjectOnPlane(grabStart.right, ship.transform.up);
            var currentPointing = Vector3.ProjectOnPlane(transform.right, ship.transform.up);
            var rudderAngle = Vector3.SignedAngle(currentPointing, startPointing, ship.transform.up);
            return Mathf.Clamp(grabStartRudderValue + rudderAngle / MAX_RUDDER_ANGLE, -1, 1);
        }
    }
}
