using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.VRCore;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts
{
    // Component for fixing the undersized bounds of skinned mesh renderers of equipments so that they do not disappear while on-screen.
    public class EquipBoundingBoxFix : MonoBehaviour
    {
        // Armors whose skinned mesh renderer's unmodded bounding box is too small that we need to expand it so that they do not disappear.
        private readonly static HashSet<string> ARMOR_NAMES = new HashSet<string>(new string[] { "$item_chest_fenris" });

        // The world-space size of the bounding box enforced on the equipments of non-player characters, big enough to
        // cover a character wherever its skeleton draws the equipment.
        private const float NON_PLAYER_EQUIPMENT_BOUNDS_SIZE = 4f;

        private SkinnedMeshRenderer playerBodyMeshRenderer;
        private HashSet<SkinnedMeshRenderer> pendingRenderersToFix = new HashSet<SkinnedMeshRenderer>();

        public static EquipBoundingBoxFix GetInstanceForPlayer(Player player)
        {
            if (player == null)
            {
                return null;
            }
            return player.gameObject.GetComponent<EquipBoundingBoxFix>() ?? player.gameObject.AddComponent<EquipBoundingBoxFix>();
        }

        void Update()
        {
            if (!VRPlayer.inFirstPerson || !EnsureBodyRenderer())
            {
                return;
            }

            // The body has bounds big enough that we can use it to calculate desired bounds of the equipments.
            Vector3 center = playerBodyMeshRenderer.bounds.center;
            Vector3 extents = playerBodyMeshRenderer.bounds.extents;
            Vector3[] playerBoundVertices = new Vector3[] {
                    center + extents,
                    center - extents,
                    center + Vector3.Reflect(extents, Vector3.right),
                    center - Vector3.Reflect(extents, Vector3.right),
                    center + Vector3.Reflect(extents, Vector3.up),
                    center - Vector3.Reflect(extents, Vector3.up),
                    center + Vector3.Reflect(extents, Vector3.forward),
                    center - Vector3.Reflect(extents, Vector3.forward)};

            foreach (SkinnedMeshRenderer renderer in pendingRenderersToFix)
            {
                if (renderer == null)
                {
                    continue;
                }
                Bounds localBounds = renderer.localBounds;
                // Expand the bounds of the equipment to encapsulate the bounds of the player body.
                foreach (Vector3 p in playerBoundVertices)
                {
                    localBounds.Encapsulate(renderer.transform.InverseTransformPoint(p));
                }
                renderer.localBounds = localBounds;
            }

            pendingRenderersToFix.Clear();
        }

        public void RequestBoundingBoxFix(GameObject itemInstance)
        {
            foreach (SkinnedMeshRenderer renderer in itemInstance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                pendingRenderersToFix.Add(renderer);
            }
        }

        // Expands the bounds of the skinned meshes of an item equipped on a non-player character (e. g. a training
        // dummy) so that the item does not vanish from one eye while still on screen. There is no instance of this
        // component on such a character to track the equipments frame by frame like for the local player, and no body
        // renderer to derive the bounds from either, so the bounds are simply expanded to a fixed size around the
        // item's own origin, which is where the character carrying it is.
        public static void FixNonPlayerEquipmentBoundingBox(GameObject instance)
        {
            SkinnedMeshRenderer[] renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>();

            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                // The bounds are in the local space of the renderer, so divide the desired world size by the smallest
                // dimension of the scale to make sure the box is big enough along every axis.
                Vector3 scale = renderer.transform.lossyScale;
                float minScale = Mathf.Min(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                if (minScale < 1e-6f)
                {
                    continue;
                }
                Bounds localBounds = renderer.localBounds;
                localBounds.Encapsulate(new Bounds(Vector3.zero, Vector3.one * NON_PLAYER_EQUIPMENT_BOUNDS_SIZE / minScale));
                renderer.localBounds = localBounds;
            }
        }

        public void RequestArmorBoundingBoxFixIfNeeded(GameObject itemInstance, string itemName)
        {
            if (ARMOR_NAMES.Contains(itemName))
            {
                RequestBoundingBoxFix(itemInstance);
            }
        }

        private bool EnsureBodyRenderer()
        {
            if (playerBodyMeshRenderer != null)
            {
                return true;
            }

            SkinnedMeshRenderer[] playerSkinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer renderer in playerSkinnedMeshRenderers)
            {
                if (renderer.gameObject.name == "body")
                {
                    playerBodyMeshRenderer = renderer;
                    return true;
                }
            }

            return false;
        }
    }
}
