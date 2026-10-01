using UnityEngine;
using ValheimVRMod.Utilities;

using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.VRCore.UI
{
    /**
     * Shows an NPC text as a bubble above the talker's head in world space.
     *
     * This is used for texts the NPC says on its own, e.g. a dvergr muttering to itself, Haldor greeting the
     * player walking by, or a raven's idle remarks. Vanilla draws those next to the talker on the screen,
     * which in VR would mean drawing them on the GUI panel far away from the NPC that is speaking. Instead
     * the vanilla text GUI is moved onto a world space canvas that is kept above the talker and facing the
     * player, like the enemy huds.
     *
     * The dialog the player gets by interacting with Hugin or Munin is left on the GUI panel where it is
     * easier to read, see Chat_SetNpcText_Patch.
     */
    class NpcTextBubble : MonoBehaviour
    {
        // The world size of one pixel of the text GUI per meter of distance between the bubble and the
        // player, i. e. the bubble keeps the same apparent size regardless of how far away the NPC is.
        // Roughly matches the scale that EnemyHudManager uses so that both are about equally legible.
        private const float WORLD_SIZE_PER_PIXEL_PER_METER = 0.0006f;
        private const float NPC_TEXT_BUBBLE_SCALE = 2;

        private GameObject gui;
        private GameObject talker;
        private Vector3 offset;

        public static bool IsAttachedTo(Chat.NpcText npcText)
        {
            return npcText.m_gui != null && npcText.m_gui.GetComponentInParent<NpcTextBubble>() != null;
        }

        /**
         * Moves the GUI of the given NPC text onto a bubble above its talker. Returns false if that is not
         * possible, in which case the text is left untouched and stays on the GUI panel.
         */
        public static bool Create(Chat.NpcText npcText)
        {
            if (npcText.m_gui == null || npcText.m_go == null)
            {
                return false;
            }

            if (IsAttachedTo(npcText))
            {
                // Already on a bubble, moving it onto a second one would leave the first one orphaned.
                return true;
            }

            Camera uiCamera = CameraUtils.getWorldspaceUiCamera();
            if (uiCamera == null)
            {
                LogWarning("Cannot create NPC text bubble without the world space UI camera.");
                return false;
            }

            int uiLayer = LayerUtils.getWorldspaceUiLayer();
            var bubbleObject = new GameObject("VRNpcTextBubble");
            bubbleObject.layer = uiLayer;
            Canvas canvas = bubbleObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = uiCamera;

            Transform guiTransform = npcText.m_gui.transform;
            guiTransform.SetParent(bubbleObject.transform, false);
            guiTransform.localPosition = Vector3.zero;
            guiTransform.localRotation = Quaternion.identity;
            guiTransform.localScale = Vector3.one;
            SetLayerRecursively(guiTransform, uiLayer);

            var bubble = bubbleObject.AddComponent<NpcTextBubble>();
            bubble.gui = npcText.m_gui;
            bubble.talker = npcText.m_go;
            bubble.offset = npcText.m_offset;
            // Place the bubble before it is first rendered instead of letting it show up at the origin.
            bubble.UpdatePose();
            return true;
        }

        void LateUpdate()
        {
            if (gui == null)
            {
                // The text has timed out or was cleared and Chat has destroyed its GUI.
                Destroy(gameObject);
                return;
            }
            // Keep the text centered on the bubble in case its animator moves it around, the same way
            // vanilla overrides its position every frame.
            gui.transform.localPosition = Vector3.zero;
            UpdatePose();
        }

        private void UpdatePose()
        {
            Camera uiCamera = CameraUtils.getWorldspaceUiCamera();
            if (talker == null || uiCamera == null)
            {
                // Keep the last pose, Chat clears texts whose talker is gone anyway.
                return;
            }

            Vector3 position = talker.transform.position + offset;
            Vector3 cameraPosition = uiCamera.transform.position;
            transform.position = position;
            // Face the player. The canvas is read from the side its forward direction points away from.
            Vector3 lookDirection = position - cameraPosition;
            if (lookDirection.sqrMagnitude > 0)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            }
            transform.localScale =
                Vector3.one * WORLD_SIZE_PER_PIXEL_PER_METER * lookDirection.magnitude * NPC_TEXT_BUBBLE_SCALE;
        }

        private static void SetLayerRecursively(Transform transform, int layer)
        {
            transform.gameObject.layer = layer;
            foreach (Transform child in transform)
            {
                SetLayerRecursively(child, layer);
            }
        }
    }
}
