using UnityEngine;

namespace ValheimVRMod.Utilities
{
    // Keeps a camera placed away from the player, such as the flat screen follow and spectator cameras or the VR
    // view in third person, from ending up behind something that hides the player. Modeled on the vanilla
    // GameCamera.RayTestPoint: a sphere cast so that the camera stops short of a surface rather than on it, backed
    // by a thin ray for gaps the sphere does not fit through, both against the layers vanilla's own third person
    // camera is blocked by.
    static class CameraObstructionUtils
    {
        // Vanilla uses 0.2 for its fallback test. Its main test uses 0.35, which pulls the camera in whenever it
        // merely passes close by something, and is more than a camera that isn't directly controlled needs.
        private const float SPHERE_RADIUS = 0.2f;
        // How far short of a surface the thin ray stops the camera, so that the near clip plane does not reach
        // through it. Vanilla uses its minimum near clip plane distance.
        private const float RAY_MARGIN = 0.1f;

        // Used before GameCamera exists, and as a safeguard should its mask ever include layers that the player's
        // own body or held items are on.
        private static readonly int FALLBACK_LAYER_MASK =
            (1 << 0) | // Default
            (1 << LayerUtils.PIECE) |
            (1 << LayerUtils.TERRAIN) |
            (1 << LayerUtils.STATIC_SOLID);
        private static readonly int NEVER_BLOCKING_LAYER_MASK =
            (1 << LayerUtils.VHVR_WEAPON) |
            (1 << LayerUtils.CHARACTER) |
            (1 << LayerUtils.ITEM_LAYER) |
            (1 << LayerUtils.CHARARCTER_TRIGGER) |
            (1 << LayerUtils.WEAPON_LAYER) |
            LayerUtils.HANDS_LAYER_MASK |
            LayerUtils.UI_PANEL_LAYER_MASK |
            LayerUtils.WORLDSPACE_UI_LAYER_MASK;

        // Returns where along the line from the subject to the desired camera position the camera can go without
        // something coming between the two.
        public static Vector3 ClampToAvoidObstruction(Vector3 subject, Vector3 desiredCameraPosition)
        {
            Vector3 offset = desiredCameraPosition - subject;
            float maxDistance = offset.magnitude;
            if (maxDistance < 0.001f)
            {
                return desiredCameraPosition;
            }
            Vector3 direction = offset / maxDistance;

            int layerMask = GetLayerMask();
            float distance = maxDistance;
            foreach (var hit in Physics.SphereCastAll(subject, SPHERE_RADIUS, direction, maxDistance, layerMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance < distance && !IsLocalPlayer(hit.collider))
                {
                    distance = hit.distance;
                }
            }
            // A sphere cast misses whatever the sphere already overlaps where it starts, e. g. a wall the player is
            // standing against, and does not fit through narrow gaps. The thin ray covers both.
            foreach (var hit in Physics.RaycastAll(subject, direction, maxDistance, layerMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance - RAY_MARGIN < distance && !IsLocalPlayer(hit.collider))
                {
                    distance = Mathf.Max(hit.distance - RAY_MARGIN, 0);
                }
            }

            return subject + direction * distance;
        }

        private static int GetLayerMask()
        {
            int mask = GameCamera.instance != null ? GameCamera.instance.m_blockCameraMask.value : FALLBACK_LAYER_MASK;
            return mask & ~NEVER_BLOCKING_LAYER_MASK;
        }

        private static bool IsLocalPlayer(Collider collider)
        {
            var player = Player.m_localPlayer;
            if (player == null)
            {
                return false;
            }
            return (collider.attachedRigidbody != null && collider.attachedRigidbody.gameObject == player.gameObject) ||
                collider.GetComponentInParent<Player>() == player;
        }
    }
}
