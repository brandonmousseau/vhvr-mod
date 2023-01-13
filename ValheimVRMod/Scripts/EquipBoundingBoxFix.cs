using System;
using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts
{
    // Component for fixing the undersized bounds of skinned mesh renderers of equipments so that they do not disappear while on-screen.
    public class EquipBoundingBoxFix : MonoBehaviour
    {
        // Equipments whose skinned mesh renderer's unmodded bounding box is too small that we need to expand it so that they do not disappear.
        private readonly static HashSet<string> EquipItemNames = new HashSet<string>(new string[] { "ArmorFenringChest", "FistFenrirClaw", "KnifeSkollAndHati" });

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

        public void RequestBoundingBoxFix(String itemName, GameObject itemInstance)
        {
            if (!EquipItemNames.Contains(itemName))
            {
                return;
            }

            foreach (SkinnedMeshRenderer renderer in itemInstance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                pendingRenderersToFix.Add(renderer);
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
