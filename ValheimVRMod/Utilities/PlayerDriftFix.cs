using UnityEngine;
using ValheimVRMod.VRCore;
using ValheimVRMod.VRCore.UI;

namespace ValheimVRMod.Utilities
{
    public class PlayerDriftFix : MonoBehaviour
    {
        private Vector3 lastKnownFixedPosition;
        private float driftRemovalTimer = 0;
        private Player player { get { return _player != null ? _player : (_player = GetComponent<Player>()); } }
        private Player _player;

        private void FixedUpdate()
        {
            if (shouldRemoveDrift(Time.fixedDeltaTime))
            {
                driftRemovalTimer += Time.fixedDeltaTime;
            }
            else
            {
                driftRemovalTimer = 0;
            }

            if (driftRemovalTimer > 0.25f)
            {
                // Remove drift
                transform.position = lastKnownFixedPosition;
            }
            else
            {
                lastKnownFixedPosition = transform.position;
            }

        }

        public bool shouldRemoveDrift(float deltaTime)
        {
            if (VRControls.smoothWalkX != 0 || VRControls.smoothWalkY != 0 ||
                player == null || player.IsAttached() || !player.IsOnGround() ||
                VRPlayer.roomscaleMovement != Vector3.zero)
            {
                return false;
            }

            if (VRPlayer.gesturedLocomotionManager != null)
            {
                if (VRPlayer.gesturedLocomotionManager.stickOutputX != 0 ||
                    VRPlayer.gesturedLocomotionManager.stickOutputY != 0)
                {
                    return false;
                }
            }

            float distanceTolerance = deltaTime;
            var d = transform.position - lastKnownFixedPosition;
            if (Mathf.Abs(d.x) > distanceTolerance ||
                Mathf.Abs(d.y) > distanceTolerance ||
                Mathf.Abs(d.z) > distanceTolerance)
            {
                return false;
            }

            const float SPEED_TOLERANCE = 1f / 512f;
            var v = player.GetVelocity();
            if (Mathf.Abs(v.x) > SPEED_TOLERANCE ||
                Mathf.Abs(v.y) > SPEED_TOLERANCE ||
                Mathf.Abs(v.z) > SPEED_TOLERANCE)
            {
                return false;
            }

            return true;
        }
    }
}
