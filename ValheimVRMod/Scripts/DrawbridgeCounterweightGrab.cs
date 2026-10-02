using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;
using Valve.VR.InteractionSystem;

namespace ValheimVRMod.Scripts
{
    // Operates a drawbridge by its counterweights, as an alternative to hovering and interacting with it: grab one of
    // them with an empty hand and pull it up to lower the bridge, or pull it down to raise it. Each grab operates the
    // bridge at most once; grab again to operate it again.
    //
    // The drawbridge is a vanilla Door (open is lowered, closed is raised), and its counterweights are the colliders
    // under its "weights" child. The push gesture that opens doors (see FistCollision) deliberately does not apply
    // to it.
    public class DrawbridgeCounterweightGrab : MonoBehaviour
    {
        // How far from the fist a counterweight can be and still be grabbed.
        private const float GRAB_RADIUS = 0.15f;
        // How far the hand has to be pulled up or down to operate the bridge.
        private const float MIN_PULL_DISTANCE = 0.25f;

        private HandGesture leftHandGesture;
        private HandGesture rightHandGesture;
        private readonly GrabState leftGrab = new GrabState();
        private readonly GrabState rightGrab = new GrabState();
        private readonly Collider[] overlaps = new Collider[32];

        private class GrabState
        {
            // The drawbridge whose counterweight is held, or null.
            public Door drawbridge;
            public float startHandHeight;
        }

        public void Initialize(HandGesture leftHandGesture, HandGesture rightHandGesture)
        {
            this.leftHandGesture = leftHandGesture;
            this.rightHandGesture = rightHandGesture;
        }

        // In Update rather than FixedUpdate, which can miss or repeat the frame a grip is pressed.
        void Update()
        {
            if (Player.m_localPlayer == null || VRPlayer.instance == null || leftHandGesture == null || rightHandGesture == null)
            {
                return;
            }
            updateHand(leftGrab, leftHandGesture, StaticObjects.leftFist().transform, VRPlayer.leftHand, SteamVR_Input_Sources.LeftHand);
            updateHand(rightGrab, rightHandGesture, StaticObjects.rightFist().transform, VRPlayer.rightHand, SteamVR_Input_Sources.RightHand);
        }

        private void updateHand(GrabState grab, HandGesture handGesture, Transform fist, Hand hand, SteamVR_Input_Sources inputSource)
        {
            if (!SteamVR_Actions.valheim_Grab.GetState(inputSource) || !handGesture.isHandFree())
            {
                grab.drawbridge = null;
                return;
            }

            if (SteamVR_Actions.valheim_Grab.GetStateDown(inputSource))
            {
                grab.drawbridge = findGrabbedDrawbridge(fist.position);
                if (grab.drawbridge != null)
                {
                    grab.startHandHeight = getHandHeight(fist);
                    hand.hapticAction.Execute(0, 0.25f, 100, 0.5f, inputSource);
                    LogUtils.LogDebug("Grabbed the counterweight of " + grab.drawbridge.name);
                }
            }

            if (grab.drawbridge == null)
            {
                return;
            }

            float pull = getHandHeight(fist) - grab.startHandHeight;
            bool isLowered = isOpen(grab.drawbridge);
            if ((pull > MIN_PULL_DISTANCE && !isLowered) || (pull < -MIN_PULL_DISTANCE && isLowered))
            {
                grab.drawbridge.Interact(Player.m_localPlayer, false, false);
                hand.hapticAction.Execute(0, 0.4f, 100, 0.8f, inputSource);
                // Done with this grab, so that holding on does not operate the bridge again.
                grab.drawbridge = null;
            }
        }

        private Door findGrabbedDrawbridge(Vector3 position)
        {
            int count = Physics.OverlapSphereNonAlloc(position, GRAB_RADIUS, overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Door drawbridge = getDrawbridgeOfCounterweight(overlaps[i]);
                if (drawbridge != null)
                {
                    return drawbridge;
                }
            }
            return null;
        }

        // The drawbridge the collider is a counterweight of, or null if it is not one.
        private static Door getDrawbridgeOfCounterweight(Collider collider)
        {
            Door door = collider.GetComponentInParent<Door>();
            if (door == null || !door.name.ToLowerInvariant().Contains("drawbridge"))
            {
                return null;
            }
            for (Transform t = collider.transform.parent; t != null && t != door.transform; t = t.parent)
            {
                if (t.name == "weights")
                {
                    return door;
                }
            }
            return null;
        }

        private static bool isOpen(Door door)
        {
            return door.m_nview != null && door.m_nview.IsValid() && door.m_nview.GetZDO().GetInt(ZDOVars.s_state, 0) != 0;
        }

        // Relative to the VR rig, so that moving the player does not count as pulling.
        private static float getHandHeight(Transform fist)
        {
            return VRPlayer.instance.transform.InverseTransformPoint(fist.position).y;
        }
    }
}
