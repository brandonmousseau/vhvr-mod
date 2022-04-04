using ValheimVRMod.VRCore.UI;
using HarmonyLib;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using ValheimVRMod.Utilities;

using static ValheimVRMod.Utilities.LogUtils;
using System.Runtime.CompilerServices;

namespace ValheimVRMod.Patches
{

    // Need this patch to ensure dynamically created pins are positioned
    // correctly in world space coordinates so they are rendered on the VRHUD
    // canvases.
    [HarmonyPatch(typeof(Minimap), nameof(Minimap.UpdatePins))]
    class Minimap_UpdatePins_SetParentPatch
    {

        private static MethodInfo setParentMethod = AccessTools.Method(typeof(Transform), "SetParent", new Type[] { typeof(Transform) });

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            if (VHVRConfig.NonVrPlayer())
            {
                return instructions;
            }
            var original = new List<CodeInstruction>(instructions);
            var patched = new List<CodeInstruction>();
            for (int i = 0; i < original.Count; i++)
            {
                var instruction = original[i];
                if (instruction.Calls(setParentMethod))
                {
                    // Push "false" onto evaluation stack
                    patched.Add(new CodeInstruction(OpCodes.Ldc_I4_0));
                    // Call SetParent method that uses the bool input
                    patched.Add(CodeInstruction.Call(typeof(Transform), "SetParent", new Type[] { typeof(Transform), typeof(bool) }));
                } else
                {
                    patched.Add(instruction);
                }
            }
            return patched;
        }
    }

    /**
     * This is required because for the VRHud we move the Minimap's Canvas
     * from the default position/rotation, so setting the absolute rotation
     * of the player icon doesn't work anymore. This changes it to use
     * the local rotation instead so it'll work regardless of canvas
     * position.
     */
    [HarmonyPatch(typeof(Minimap), nameof(Minimap.UpdatePlayerMarker))]
    public class MinimapPlayerMarkerPatch
    {
        static void Postfix(Minimap __instance, Player player, Quaternion playerRot)
        {
            if (VHVRConfig.NonVrPlayer())
            {
                return;
            }
            __instance.m_smallMarker.localRotation = Quaternion.Euler(0f, 0f, -playerRot.eulerAngles.y);
            Ship controlledShip = player.GetControlledShip();
            if (controlledShip)
            {
                __instance.m_smallShipMarker.localRotation = Quaternion.Euler(0f, 0f, -controlledShip.transform.rotation.eulerAngles.y);
            }
        }
    }

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
            if (! VHVRConfig.NonVrPlayer()) {
                __result = SoftwareCursor.simulatedMousePosition;
            }
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
            if (VHVRConfig.NonVrPlayer()) {
                return true;
            }
            
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
            if (VHVRConfig.NonVrPlayer()) {
                return;
            }
            
            throw new NotImplementedException("Stub for reverse patch.");
        }
    }

    // Without these patches, where an object is placed is controlled by the
    // direction of the player camera. In VR, this meant the player has to
    // physically look at the exact spot they want to place an object. It ended
    // up being very uncomfortable & hard to do for building. With this patch,
    // the vectors being used for the Raycast are swapped out with my own that allows
    // for controlling the position of the placed objects using controls more suited
    // for comfort in VR.
    // This set of patches also updates the vectors that are used to determine what
    // the current object that is being hovered over is. Since originally it is
    // using the MainCamera, the patch updates it to use the VR Camera so the thing
    // object being selected is the center of the players view.
    class Player_RaycastVector_Patches
    {

        static Vector3 getStartingPositionPlaceMode()
        {
            return PlaceModeRayVectorProvider.startingPosition;
        }

        static Vector3 getRayDirectionPlaceMode()
        {
            return PlaceModeRayVectorProvider.rayDirection;
        }

        static Vector3 getStartingPositionCameraFacing()
        {
            Camera vrCam = CameraUtils.getCamera(CameraUtils.VR_CAMERA);
            if (vrCam == null)
            {
                return Vector3.zero;
            }
            return vrCam.transform.position;
        }

        static Vector3 getRayDirectionCameraFacing()
        {
            Camera vrCam = CameraUtils.getCamera(CameraUtils.VR_CAMERA);
            if (vrCam == null)
            {
                return Vector3.zero;
            }
            return vrCam.transform.forward;
        }

        [HarmonyPatch(typeof(Player), "PieceRayTest")]
        class Player_PieceRaytest_Patch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                if (VHVRConfig.NonVrPlayer()) {
                    return instructions;
                }
                return GetRaycastPatchedInstructions(instructions, 4, nameof(getStartingPositionPlaceMode), nameof(getRayDirectionPlaceMode));
            }
        }

        [HarmonyPatch(typeof(Player), "RemovePiece")]
        class Player_RemovePiece_Patch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                if (VHVRConfig.NonVrPlayer()) {
                    return instructions;
                }
                return GetRaycastPatchedInstructions(instructions, 5, nameof(getStartingPositionPlaceMode), nameof(getRayDirectionPlaceMode));
            }
        }

        [HarmonyPatch(typeof(Player), "UpdateWearNTearHover")]
        class Player_UpdateWearNTearHover_Patch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                if (VHVRConfig.NonVrPlayer()) {
                    return instructions;
                }
                return GetRaycastPatchedInstructions(instructions, 5, nameof(getStartingPositionPlaceMode), nameof(getRayDirectionPlaceMode));
            }
        }

        static IEnumerable<CodeInstruction> GetRaycastPatchedInstructions(IEnumerable<CodeInstruction> instructions, int popOffset, string startingPosition, string rayDirection)
        {
            return Rayscast_VectorReplace_Transpiler.GetRaycastPatchedInstructions(instructions, typeof(Player_RaycastVector_Patches),
                startingPosition, rayDirection, popOffset);
        }

        static IEnumerable<CodeInstruction> GetRaycastAllPatchedInstructions(IEnumerable<CodeInstruction> instructions, int popOffset, string startingPosition, string rayDirection)
        {
            return Rayscast_VectorReplace_Transpiler.GetRaycastAllPatchedInstructions(instructions, typeof(Player_RaycastVector_Patches),
                startingPosition, rayDirection, popOffset);
        }
        class Rayscast_VectorReplace_Transpiler
        {

            private static MethodInfo raycastMethod = AccessTools.Method(typeof(Physics), "Raycast",
                new Type[] { typeof(Vector3), typeof(Vector3), typeof(RaycastHit).MakeByRefType(), typeof(float), typeof(int) });

            private static MethodInfo raycastAllMethod = AccessTools.Method(typeof(Physics), "RaycastAll",
                new Type[] { typeof(Vector3), typeof(Vector3), typeof(float), typeof(int) });


            public static IEnumerable<CodeInstruction> GetRaycastPatchedInstructions(IEnumerable<CodeInstruction> instructions,
                                                                                Type methodType,
                                                                                string startingPosition,
                                                                                string rayDirection,
                                                                                int popOffset)
            {
                return GetPatchedInstructions(instructions, methodType, startingPosition, rayDirection, popOffset, raycastMethod);
            }

            public static IEnumerable<CodeInstruction> GetRaycastAllPatchedInstructions(IEnumerable<CodeInstruction> instructions,
                                                                        Type methodType,
                                                                        string startingPosition,
                                                                        string rayDirection,
                                                                        int popOffset)
            {
                return GetPatchedInstructions(instructions, methodType, startingPosition, rayDirection, popOffset, raycastAllMethod);
            }

            private static IEnumerable<CodeInstruction> GetPatchedInstructions(IEnumerable<CodeInstruction> instructions,
                                                                                Type methodType,
                                                                                string startingPosition,
                                                                                string rayDirection,
                                                                                int popOffset,
                                                                                MethodInfo raycastMethod)
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

    // This set of patches is used to inject some method calls into the
    // existing EnemyHud code so that the mirror EnemyHud data being used
    // to generate worlspace UI is updated properly
    class EnemyHud_Patches
    {

        // This patch is used to add a duplicate of whatever
        // HUD is added to the base class to the EnemyHudManager
        // mirror class.
        [HarmonyPatch(typeof(EnemyHud), "ShowHud")]
        class EnemyHud_ShowHud_Patch
        {
            public static void Prefix(Character c, bool isMount, GameObject ___m_baseHudPlayer, GameObject ___m_baseHud, GameObject ___m_baseHudMount, GameObject ___m_baseHudBoss)
            {
                if (VHVRConfig.NonVrPlayer()) {
                    return;
                }
                EnemyHudManager.instance.AddEnemyHud(c, isMount, ___m_baseHudPlayer, ___m_baseHud, ___m_baseHudMount, ___m_baseHudBoss);
            }
        }

        // The UpdateHuds method is responsible for updating values
        // of any active Enemy huds (ie, health, level, alert status etc) as
        // well as removing any huds that should no longer active. Rather
        // than duplicate this logic for our mirror hud, we'll insert some
        // method calls to our EnemyHudManager class to update the values
        // at the right points. This requires the use of a transpiler to insert
        // the method calls at the right place in the code.
        [HarmonyPatch(typeof(EnemyHud), "UpdateHuds")]
        class EnemyHud_UpdateHuds_Patch
        {
            private static FieldInfo guiField = AccessTools.Field(typeof(EnemyHud.HudData), nameof(EnemyHud.HudData.m_gui));
            private static MethodInfo destroyMethod =
                AccessTools.Method(typeof(UnityEngine.Object), "Destroy", new Type[] { typeof(UnityEngine.Object) });
            private static MethodInfo getHealthPercentageMethod =
                AccessTools.Method(typeof(Character), nameof(Character.GetHealthPercentage));
            private static MethodInfo getLevelMethod =
                AccessTools.Method(typeof(Character), nameof(Character.GetLevel));
            private static MethodInfo isAlertedMethod =
                AccessTools.Method(typeof(BaseAI), nameof(BaseAI.IsAlerted));
            private static MethodInfo getMaxStaminaMethod =
                AccessTools.Method(typeof(Sadle), nameof(Sadle.GetMaxStamina));
            private static MethodInfo localizeMethod =
                AccessTools.Method(typeof(Localization), nameof(Localization.Localize), new[] { typeof(string) });
            private static MethodInfo setActiveMethod =
                AccessTools.Method(typeof(GameObject), nameof(GameObject.SetActive));
            private static MethodInfo enemyHudRemoveMethod =
                AccessTools.Method(AccessTools.Field(typeof(EnemyHud), nameof(EnemyHud.m_huds)).FieldType, "Remove", new Type[] { typeof(Character) });
            private static MethodInfo worldToScreenPointMethod =
                AccessTools.Method(typeof(Camera), nameof(Camera.WorldToScreenPoint), new Type[] { typeof(Vector3) });

            private static bool patchedSetActiveTrue = false;
            private static bool patchedSetActiveFalse = false;

            private static void LoadCharacterField(ref List<CodeInstruction> patched)
            {
                patched.Add(new CodeInstruction(OpCodes.Ldloc_S, 6));
                patched.Add(CodeInstruction.LoadField(typeof(EnemyHud.HudData), nameof(EnemyHud.HudData.m_character)));
            }

            private static void AssertCharacter(Character c, [CallerMemberName] string caller = "")
            {
                if (c is null) Debug.LogError($"ASSERT FAILED: Character c is null in {caller}");
            }

            private static void DestroyHud(Character c)
            {
                AssertCharacter(c);
                EnemyHudManager.instance.DestroyHudGui(c);
            }

            // Some wrapper methods to use as the Transpiler's Call targets
            private static void RemoveHud(Character c)
            {
                AssertCharacter(c);
                EnemyHudManager.instance.RemoveEnemyHud(c);
            }

            private static float UpdateHealth(float health, Character c)
            {
                AssertCharacter(c);
                EnemyHudManager.instance.UpdateHealth(c, health);
                // Return health so that it gets put back onto the
                // evaluation stack right after we use it
                return health;
            }

            private static int UpdateLevel(int level, Character c)
            {
                AssertCharacter(c);
                EnemyHudManager.instance.UpdateLevel(c, level);
                // Return level so that it gets put back onto the
                // evaluation stack right after we use it
                return level;
            }

            private static bool UpdateAlertAndAware(bool alerted, bool haveTarget, Character c)
            {
                AssertCharacter(c);
                bool aware = !alerted & haveTarget;
                UpdateAlerted(c, alerted);
                UpdateAware(c, aware);
                // We will call this right before alerted should be stored
                // into a local variable, so return the variable to put it
                // back onto eval stack.
                return alerted;
            }

            private static float UpdateMount(float maxStamina, float stamina, Character c)
            {
                AssertCharacter(c);
                EnemyHudManager.instance.UpdateMount(c, maxStamina, stamina);
                // We will call this right before alerted should be stored
                // into a local variable, so return the variable to put it
                // back onto eval stack.
                return maxStamina;
            }

            private static string UpdateName(string name, Character c)
            {
                AssertCharacter(c);
                EnemyHudManager.instance.UpdateName(c, name);
                // We will call this right before alerted should be stored
                // into a local variable, so return the variable to put it
                // back onto eval stack.
                return name;
            }

            private static void UpdateAlerted(Character c, bool alerted)
            {
                AssertCharacter(c);
                EnemyHudManager.instance.UpdateAlerted(c, alerted);
            }

            private static void UpdateAware(Character c, bool aware)
            {
                AssertCharacter(c);
                EnemyHudManager.instance.UpdateAware(c, aware);
            }

            private static bool UpdateActive(bool active, Character c)
            {
                AssertCharacter(c);
                EnemyHudManager.instance.SetHudActive(c, active);
                return active;
            }

            private static Vector3 UpdateLocation(Vector3 worldToScreenPoint, Character c)
            {
                AssertCharacter(c);
                EnemyHudManager.instance.UpdateHudCoordinates(c);
                return worldToScreenPoint;
            }

            // Need to insert method calls to UpdateHudCoordinates, RemoveEnemyHud, UpdateHealth,
            // UpdateLevel, UpdateAlerted, and UpdateAware and SetActive.
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                if (VHVRConfig.NonVrPlayer()) {
                    return instructions;
                }
                
                patchedSetActiveTrue = false;
                patchedSetActiveFalse = false;
                var original = new List<CodeInstruction>(instructions);
                var patched = new List<CodeInstruction>();
                for (int i = 0; i < original.Count; i++) {
                    patched.Add(original[i]);
                    MaybeAddDestroyHudInstructions(original, ref patched, i);
                    MaybeAddUpdateHealthInstructions(original, ref patched, i);
                    MaybeAddUpdateLevelInstructions(original, ref patched, i);
                    MaybeAddAIAlertnessUpdateInstructions(original, ref patched, i);
                    MaybeAddMountUpdateInstructions(original, ref patched, i);
                    MaybeAddNameplateUpdateInstructions(original, ref patched, i);
                    MaybeAddSetActiveInstructions(original, ref patched, i);
                    MaybeAddRemoveEnemyHudInstruction(original, ref patched, i);
                    MaybeAddUpdateHudLocationInstructions(original, ref patched, i);
                }
                return patched;
            }

            private static void MaybeAddUpdateHudLocationInstructions(List<CodeInstruction> original, ref List<CodeInstruction> patched, int i)
            {
                var instruction = original[i];
                if (instruction.Calls(worldToScreenPointMethod))
                {
                    // Reposition our mirror hud to the world space coordinates
                    LoadCharacterField(ref patched);
                    patched.Add(CodeInstruction.Call(typeof(EnemyHud_UpdateHuds_Patch), nameof(UpdateLocation)));
                }
            }

            private static void MaybeAddRemoveEnemyHudInstruction(List<CodeInstruction> original, ref List<CodeInstruction> patched, int i)
            {
                if (i - 1 < 0)
                {
                    return;
                }
                var instruction = original[i];
                var previousInstruction = original[i - 1];
                if (instruction.opcode == OpCodes.Pop && previousInstruction.Calls(enemyHudRemoveMethod))
                {
                    // Need to remove our mirror from the enemy hud dictionary
                    patched.Add(new CodeInstruction(OpCodes.Ldloc_3));
                    patched.Add(CodeInstruction.Call(typeof(EnemyHud_UpdateHuds_Patch), nameof(RemoveHud)));
                }
            }

            private static void MaybeAddDestroyHudInstructions(List<CodeInstruction> original, ref List<CodeInstruction> patched, int i)
            {
                var instruction = original[i];
                if (instruction.Calls(destroyMethod))
                {
                    // The current hud is being destroyed here so
                    // lets destroy out mirror gui too.
                    LoadCharacterField(ref patched);
                    patched.Add(CodeInstruction.Call(typeof(EnemyHud_UpdateHuds_Patch), nameof(DestroyHud)));
                }
            }

            private static void MaybeAddUpdateHealthInstructions(List<CodeInstruction> original, ref List<CodeInstruction> patched, int i)
            {
                var instruction = original[i];
                if (instruction.Calls(getHealthPercentageMethod))
                {
                    // Health percentage method just called, so health percentage is on eval
                    // stack. Load the character field and then call UpdateHealth
                    LoadCharacterField(ref patched);
                    patched.Add(CodeInstruction.Call(typeof(EnemyHud_UpdateHuds_Patch), nameof(UpdateHealth)));
                }
            }

            private static void MaybeAddUpdateLevelInstructions(List<CodeInstruction> original, ref List<CodeInstruction> patched, int i)
            {
                var instruction = original[i];
                if (instruction.Calls(getLevelMethod))
                {
                    // GetLevel method just called, so character's level is on eval stack.
                    // Load the character field and call UpdateLevel
                    LoadCharacterField(ref patched);
                    patched.Add(CodeInstruction.Call(typeof(EnemyHud_UpdateHuds_Patch), nameof(UpdateLevel)));
                }
            }

            private static void MaybeAddAIAlertnessUpdateInstructions(List<CodeInstruction> original, ref List<CodeInstruction> patched, int i)
            {
                var instruction = original[i];
                if (instruction.Calls(isAlertedMethod))
                {
                    // IsAlerted was just called, so it is on the eval stack. "HaveTarget" was stored
                    // into V_9, so load it onto the eval stack + the Character reference and call UpdateAlertAndAware
                    patched.Add(new CodeInstruction(OpCodes.Ldloc_S, 9)); // ldloc.s V_9
                    LoadCharacterField(ref patched);
                    patched.Add(CodeInstruction.Call(typeof(EnemyHud_UpdateHuds_Patch), nameof(UpdateAlertAndAware)));
                }
            }

            private static void MaybeAddMountUpdateInstructions(List<CodeInstruction> original, ref List<CodeInstruction> patched, int i)
            {
                var instruction = original[i];
                if (instruction.Calls(getMaxStaminaMethod))
                {
                    // MaxStamina was just called, so it is on the eval stack. "GetStamina" was stored into V_11.
                    // Before storing the value of GetStamina we detour to UpdateMound
                    patched.Add(new CodeInstruction(OpCodes.Ldloc_S, 11)); // ldloc.s V_9
                    LoadCharacterField(ref patched);
                    patched.Add(CodeInstruction.Call(typeof(EnemyHud_UpdateHuds_Patch), nameof(UpdateMount)));
                }
            }

            private static void MaybeAddNameplateUpdateInstructions(List<CodeInstruction> original, ref List<CodeInstruction> patched, int i)
            {
                var instruction = original[i];
                if (instruction.Calls(localizeMethod))
                {
                    // Localize was just called, so it is on the eval stack.
                    // We detour to UpdateName and then leave the localization on the eval stack again
                    LoadCharacterField(ref patched);
                    patched.Add(CodeInstruction.Call(typeof(EnemyHud_UpdateHuds_Patch), nameof(UpdateName)));
                }
            }

            private static void MaybeAddSetActiveInstructions(List<CodeInstruction> original, ref List<CodeInstruction> patched, int i)
            {
                var instruction = original[i];
                // Find instruction with opcode ldc.i4.1, verify next instruction is "SetActive(bool)" and previous function
                // loaded the gui field. If these conditions met, call our own SetActive function with (true).
                // For the second time we find SetActive, we'll then set it to false to disable the original enemy huds
                // except for the boss hud.
                if (instruction.opcode.Equals(OpCodes.Ldc_I4_1) && !patchedSetActiveTrue)
                {
                    if ((i + 1) < original.Count && (i - 1) >= 0) {
                        var previousInstruction = original[i - 1];
                        var nextInstruction = original[i + 1];
                        if (previousInstruction.Is(OpCodes.Ldfld, guiField) && nextInstruction.Is(OpCodes.Callvirt, setActiveMethod))
                        {
                            if (!patchedSetActiveTrue)
                            {
                                // First time we encountered the SetActive(true) call, so mirror it to our EnemeyHudsManager
                                patchedSetActiveTrue = true;
                                LoadCharacterField(ref patched);
                                patched.Add(CodeInstruction.Call(typeof(EnemyHud_UpdateHuds_Patch), nameof(UpdateActive)));
                            } else
                            {
                                // Second time we encounter it, so lets now call SetActive(false) on the original EnemyHud
                                // to make sure it is never visible. The current instruction loaded "true" onto the evalutation
                                // stack, so lets pop that off, and load false in its place.
                                patched.Add(new CodeInstruction(OpCodes.Pop)); // Pop off "true"
                                patched.Add(new CodeInstruction(OpCodes.Ldc_I4_0)); // Put false onto eval stack
                            }
                        }
                    }
                }

                // Find instruction with opcode ldc.i4.0, verify next instruction is "SetActive(bool)" and previous function
                // loaded the gui field. If these conditions met, call our own SetActive function with (false).
                // We only do this once for the SetActive false case, so set a flag to indicate it is done.
                if (instruction.opcode.Equals(OpCodes.Ldc_I4_0) && !patchedSetActiveFalse)
                {
                    if ((i + 1) < original.Count && (i - 1) >= 0)
                    {
                        var previousInstruction = original[i - 1];
                        var nextInstruction = original[i + 1];
                        if (previousInstruction.Is(OpCodes.Ldfld, guiField) && nextInstruction.Is(OpCodes.Callvirt, setActiveMethod))
                        {
                            patchedSetActiveFalse = true;
                            LoadCharacterField(ref patched);
                            patched.Add(CodeInstruction.Call(typeof(EnemyHud_UpdateHuds_Patch), nameof(UpdateActive)));
                        }
                    }
                }
            }
        }
    }

    // In the base game, ship rudder control is positioned on the rudder using
    // a worldspace -> screen space conversion, which doesn't work in VR. Instead
    // let's just move the rudder control UI to just below the ship wind indicator.
    [HarmonyPatch(typeof(Hud), "UpdateShipHud")]
    class Hud_UpdateShipHud_Patch
    {

        public static void Postfix(Hud __instance)
        {
            if (VHVRConfig.NonVrPlayer()) {
                return;
            }
            
            __instance.m_shipControlsRoot.transform.position =
                new Vector3(__instance.m_shipWindIndicatorRoot.transform.position.x,
                __instance.m_shipWindIndicatorRoot.transform.position.y -
                __instance.m_shipWindIndicatorRoot.GetComponent<RectTransform>().rect.height * 1.25f);
        }

    }

    // Minimap player marker should face the direction the player body
    // is facing, which will always match the actual forward movement
    // direction, rather than the direction the player is looking.
    [HarmonyPatch(typeof(Minimap), "UpdatePlayerMarker")]
    class Minimap_UpdatePlayerMarker_RotationPatch
    {
        public static void Prefix(Player player, ref Quaternion playerRot)
        {
            if (VHVRConfig.NonVrPlayer()) {
                return;
            }
            
            if (player != Player.m_localPlayer)
            {
                return;
            }
            playerRot = player.transform.rotation;
        }
    }
    
    // remove stupid keyboard/mouse hints:
    // for some reason after Hearth&Home "Awake" isn't called on the cloned hud, so to be sure we destroy it in Update
    [HarmonyPatch(typeof(KeyHints), "Update")]
    class PatchKeyHints {

        public static void Prefix(ref KeyHints __instance) {
            if (VHVRConfig.NonVrPlayer()) {
                return;
            }
            GameObject.Destroy(__instance);
        }
    }

    [HarmonyPatch(typeof(FejdStartup), "SetupGui")]
    class PatchFejd {
        public static void Postfix(FejdStartup __instance) {
            if (VHVRConfig.NonVrPlayer()) {
                return;
            }
            ConfigSettings.instantiate(__instance.m_mainMenu.transform.Find("MenuList"), __instance.m_mainMenu.transform, __instance.m_settingsPrefab);
        }
    }
    
    [HarmonyPatch(typeof(Menu), "Start")]
    class PatchMenu {
        public static void Postfix(Menu __instance) {
            if (VHVRConfig.NonVrPlayer()) {
                return;
            }
            ConfigSettings.instantiate(__instance.m_menuDialog, __instance.transform, __instance.m_settingsPrefab);
        }
    }    
    
    [HarmonyPatch(typeof(Settings), "UpdateBindings")]
    class PatchEndBindKey {
        public static void Postfix() {
            if (VHVRConfig.NonVrPlayer()) {
                return;
            }
            ConfigSettings.updateBindings();
        }
    }

    [HarmonyPatch(typeof(HotkeyBar), nameof(HotkeyBar.Update))]
    class HotkeyBarHidePatch
    {
        static bool Prefix(HotkeyBar __instance)
        {
            if (VHVRConfig.HideHotbar())
            {
                clearHotbar(__instance);
                return false;
            } else
            {
                return true;
            }
        }

        private static void clearHotbar(HotkeyBar hotkeyBar)
        {
            foreach (var element in hotkeyBar.m_elements)
            {
                UnityEngine.Object.Destroy(element.m_go);
            }
            hotkeyBar.m_elements.Clear();
        }
    }

    [HarmonyPatch(typeof(Hud),nameof(Hud.UpdateStamina))]
    class StaminaPatch
    {
        private static void Postfix(RectTransform ___m_staminaBar2Root, float ___m_staminaHideTimer, Animator ___m_staminaAnimator)
        {
            if (VHVRConfig.NonVrPlayer())
            {
                return;
            }
            if (VHVRConfig.AlwaysShowStamina() || SettingCallback.configRunning)
            {
                ___m_staminaAnimator.SetBool("Visible", true);
            }
            if (VHVRConfig.StaminaPanelPlacement() == "Legacy" || VHVRConfig.UseLegacyHud()) 
            {
                return;
            }
            RectTransform rectTransform = ___m_staminaBar2Root.transform as RectTransform;
            rectTransform.anchoredPosition = new Vector2(0f, 130f);
        }
    }
}

