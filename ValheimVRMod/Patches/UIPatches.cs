using ValheimVRMod.VRCore.UI;
using HarmonyLib;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;

using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.Patches
{
    /**
    * The purpose of this patch is to update the base
    * "mousePosition" getter, which is what all the rest of
    * the UnityEngine uses to determine current mouse
    * position, with the simulated mouse position.
    */
    [HarmonyPatch(typeof(Input), "get_mousePosition")]
    class Input_get_mousePosition_Patch
    {
        public static void Postfix(ref Vector3 __result)
        {
            __result = SoftwareCursor.simulatedMousePosition;
        }
    }

    // This patch replaces the method used to determine where on the UI to print
    // the NPC text. Rather than use the transpiler I'm just replacing the whole method.
    // The reason the original doesn't work is because it assumes the GUI is being printed as
    // Screen Space overlay and uses a WorldToScreenSpace function that doesn't work correctly
    // with the changes I needed to make to get the GUI working right in VR. This version isn't
    // perfect, but it should keep the NPC text on the screen for the duration it should remain
    // there.
    [HarmonyPatch(typeof(Chat), "UpdateNpcTexts")]
    class Chat_UpdateNpcTexts_Patch
    {
        public static bool Prefix(Chat __instance, List<Chat.NpcText> ___m_npcTexts, float dt)
        {
            Chat.NpcText npcText = null;
            Camera mainCamera = Utils.GetMainCamera();
            foreach (Chat.NpcText mNpcText in ___m_npcTexts)
            {
                if (mNpcText.m_go)
                {
                    if (mNpcText.m_timeout)
                    {
                        mNpcText.m_ttl -= dt;
                        if (mNpcText.m_ttl <= 0f)
                        {
                            mNpcText.SetVisible(false);
                            if (mNpcText.IsVisible())
                            {
                                continue;
                            }
                            npcText = mNpcText;
                            continue;
                        }
                    }
                    Vector3 mGo = mNpcText.m_go.transform.position + mNpcText.m_offset;
                    Vector3 screenPoint = mainCamera.WorldToScreenPoint(mGo);
                    if (screenPoint.x < 0f || screenPoint.x > (float)mainCamera.pixelWidth || screenPoint.y < 0f || screenPoint.y > (float)mainCamera.pixelHeight || screenPoint.z < 0f)
                    {
                        mNpcText.SetVisible(false);
                    }
                    else
                    {
                        mNpcText.SetVisible(true);
                        RectTransform mGui = mNpcText.m_gui.transform as RectTransform;
                        float screenpointX = screenPoint.x;
                        Rect rect = mGui.rect;
                        float halfWidth = rect.width / 2f;
                        float screenWidth = (float)Screen.width;
                        rect = mGui.rect;
                        screenPoint.x = Mathf.Clamp(screenpointX, halfWidth, screenWidth - halfWidth);
                        float screenpointY = screenPoint.y;
                        rect = mGui.rect;
                        float halfHeight = rect.height / 2f;
                        float screenHeight = (float)Screen.height;
                        rect = mGui.rect;
                        screenPoint.y = Mathf.Clamp(screenpointY, halfHeight, screenHeight - rect.height);
                        screenPoint.z = 0f;
                        mNpcText.m_gui.transform.position = screenPoint;
                    }
                    if (Vector3.Distance(mainCamera.transform.position, mGo) <= mNpcText.m_cullDistance)
                    {
                        continue;
                    }
                    mNpcText.SetVisible(false);
                    if (npcText != null || mNpcText.IsVisible())
                    {
                        continue;
                    }
                    npcText = mNpcText;
                }
                else
                {
                    mNpcText.m_gui.SetActive(false);
                    if (npcText != null)
                    {
                        continue;
                    }
                    npcText = mNpcText;
                }
            }
            if (npcText != null)
            {
                Chat_ClearNpcText_ReversePatch.ReversePatchClearNpcText(__instance, npcText);
            }
            return false;
        }
    }

    // Need to call this private method from above patch
    [HarmonyPatch]
    class Chat_ClearNpcText_ReversePatch
    {
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(Chat), "ClearNpcText", new Type[] { typeof(Chat.NpcText) })]
        public static void ReversePatchClearNpcText(object instance, Chat.NpcText npcText)
        {
            throw new NotImplementedException("Stub for reverse patch.");
        }
    }

    // Without this patch, where an object is placed is controlled by the
    // direction of the player camera. In VR, this meant the player has to
    // physically look at the exact spot they want to place an object. It ended
    // up being very uncomfortable & hard to do for building. With this patch,
    // the vectors being used for the Raycast are swapped out with my own that allows
    // for controlling the position of the placed objects using controls more suited
    // for comfort in VR.
    class Player_PlaceMode_RaycastPatch
    {

        static Vector3 getStartingPosition()
        {
            return PlaceModeRayVectorProvider.startingPosition;
        }

        static Vector3 getRayDirection ()
        {
            return PlaceModeRayVectorProvider.rayDirection;
        }

        [HarmonyPatch(typeof(Player), "PieceRayTest")]
        class Player_PieceRaytest_Patch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return GetPatchedInstructions(instructions, 4);
            }
        }

        [HarmonyPatch(typeof(Player), "RemovePiece")]
        class Player_RemovePiece_Patch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return GetPatchedInstructions(instructions, 5);
            }
        }

        [HarmonyPatch(typeof(Player), "UpdateWearNTearHover")]
        class Player_UpdateWearNTearHover_Patch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return GetPatchedInstructions(instructions, 5);
            }
        }

        static IEnumerable<CodeInstruction> GetPatchedInstructions(IEnumerable<CodeInstruction> instructions, int popOffset)
        {
            return Rayscast_VectorReplace_Transpiler.GetPatchedInstructions(instructions, typeof(Player_PlaceMode_RaycastPatch),
                "getStartingPosition", "getRayDirection", popOffset);
        }
    }

    class Rayscast_VectorReplace_Transpiler
    {

        private static MethodInfo raycastMethod = AccessTools.Method(typeof(Physics), "Raycast",
            new Type[] { typeof(Vector3), typeof(Vector3), typeof(RaycastHit).MakeByRefType(), typeof(float), typeof(int) });

        public static IEnumerable<CodeInstruction> GetPatchedInstructions(IEnumerable<CodeInstruction> instructions,
                                                                            Type methodType,
                                                                            string startingPosition,
                                                                            string rayDirection,
                                                                            int popOffset)
        {
            if (raycastMethod == null)
            {
                LogError("Raycast MethodInfo is null");
                return instructions;
            }
            var original = new List<CodeInstruction>(instructions);
            var patched = new List<CodeInstruction>();
            int startPopIndex = 0;
            bool foundIndex = false;
            for (int i = 0; i < original.Count; i++)
            {
                var instruction = original[i];
                if (instruction.opcode == OpCodes.Call && instruction.Calls(raycastMethod))
                {
                    // We will pop off the first two elements from evaluation stack
                    // starting at this index and then add the values
                    // from PlaceModeRayVectorProvider
                    startPopIndex = i - popOffset;
                    foundIndex = true;
                    break;
                }
            }
            if (!foundIndex)
            {
                LogError("Could not find Raycast Method call.");
                return instructions;
            }
            for (int i = 0; i < original.Count; i++)
            {
                patched.Add(original[i]);
                if (i == startPopIndex)
                {
                    patched.Add(new CodeInstruction(OpCodes.Pop)); // Pop GameCamera.instance.transform.forward
                    patched.Add(new CodeInstruction(OpCodes.Pop)); // Pop GameCamera.instance.transform.position
                    // Now call methods from PlaceModeRayVectorProvider to get new values onto
                    // the evaluation stack.
                    patched.Add(CodeInstruction.Call(methodType, startingPosition));
                    patched.Add(CodeInstruction.Call(methodType, rayDirection));
                }
            }
            return patched;
        }
    }

}
