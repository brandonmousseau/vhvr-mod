using System;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using ValheimVRMod.VRCore;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore.UI;
using Valve.VR;
using UnityEngine.UI;

using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.Patches
{
    // Set of patches used to enable hands to independently be used
    // for interacting with game world objects rather than
    // camera raycasts
    public class HandBasedInteractionPatches
    {

        public static Vector3 currentHitPositionRight = Vector3.zero;
        public static Vector3 currentHitPositionLeft = Vector3.zero;
        private static GameObject leftHover;

        [HarmonyPatch(typeof(Humanoid), "UseItem")]
        class Humanoid_UseItem_Patch
        {

            private static MethodInfo getHoverObject = AccessTools.Method(typeof(Humanoid), "GetHoverObject");

            // If the original hover is valid, return it, otherwise return the left
            // hover. The right hand will take priority for the purpose of this
            // method.
            static GameObject getPatchedHoverObject(GameObject hover)
            {
                if (hover)
                {
                    return hover;
                }
                return leftHover;
            }

            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var original = new List<CodeInstruction>(instructions);
                if (!VHVRConfig.UseVrControls())
                {
                    return original;
                }
                var patched = new List<CodeInstruction>();
                foreach (var instruction in original)
                {
                    patched.Add(instruction);
                    if (instruction.Calls(getHoverObject))
                    {
                        patched.Add(CodeInstruction.Call(typeof(Humanoid_UseItem_Patch), nameof(Humanoid_UseItem_Patch.getPatchedHoverObject)));
                    }
                }
                return patched;
            }
        }

        // Update the left hand clone cross hair with the correct information
        [HarmonyPatch(typeof(Hud), "UpdateCrosshair")]
        class Hud_UpdateCrosshair_Patch
        {

            private static Color defaultColor = new Color(1f, 1f, 1f, 0.5f);
            private static Color highlightedColor = Color.yellow;

            static void Postfix(Hud __instance, Player player)
            {
                if (!VHVRConfig.UseVrControls() || CrosshairManager.instance.hoverNameCloneLeftHand == null) {
                    return;
                }
                Hoverable hoverable;
                if (leftHover)
                {
                    hoverable = leftHover.GetComponentInParent<Hoverable>();
                } else
                {
                    hoverable = null;
                }
                Text hoverText = CrosshairManager.instance.hoverNameCloneLeftHand.GetComponent<Text>();
                Image crosshair = CrosshairManager.instance.crosshairCloneLeftHand.GetComponent<Image>();
                if (hoverText == null)
                {
                    LogError("Left Hand HoverText is null.");
                    return;
                }
                if (hoverable == null || TextViewer.instance.IsVisible())
                {
                    hoverText.text = "";
                    if (crosshair)
                    {
                        crosshair.color = defaultColor;
                    }
                } else
                {
                    hoverText.text = hoverable.GetHoverText();
                    if (crosshair)
                    {
                        crosshair.color = hoverText.text.Length > 0 ? highlightedColor : defaultColor;
                    }
                }
            }
        }

        // Check for the left hand use button being used and trigger
        // interactions based on it
        [HarmonyPatch(typeof(Player), "Update")]
        class Player_Update_Patch
        {
            static void Postfix(Player __instance, IDoodadController ___m_doodadController)
            {
                if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls() || __instance.GetMoveDir() != Vector3.zero)
                {
                    return;
                }
                var useAction = VRControls.instance.useLeftHandAction;
                if (useAction == null)
                {
                    LogWarning("Left Hand Use Action not initialized.");
                    return;
                }
                if (!useAction.GetStateDown(SteamVR_Input_Sources.LeftHand))
                {
                    if (useAction.GetState(SteamVR_Input_Sources.LeftHand) && leftHover)
                    {
                        __instance.Interact(leftHover, true, false);
                    }
                } else if (leftHover)
                {
                    __instance.Interact(leftHover, false, false);
                } else if (___m_doodadController != null)
                {
                    __instance.StopDoodadControl();
                }
            }
        }

        // Set the left/right hand hover objects based on
        // laser pointer vectors
        [HarmonyPatch(typeof(Player), "FindHoverObject")]
        class Player_FindHoverObject_Patch
        {

            private static float raycastDistanceLimit = 50f;

            static void Postfix(Player __instance, ref GameObject hover, int ___m_interactMask)
            {
                if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls())
                {
                    currentHitPositionRight = Vector3.zero;
                    currentHitPositionLeft = Vector3.zero;
                    return;
                }
                UpdateHoverObject(__instance, ref hover, ___m_interactMask, VRPlayer.rightPointer, ref currentHitPositionRight);
                UpdateHoverObject(__instance, ref leftHover, ___m_interactMask, VRPlayer.leftPointer, ref currentHitPositionLeft);
            }


            private static void UpdateHoverObject(Player instance, ref GameObject hoverReference, int mask, Valve.VR.Extras.SteamVR_LaserPointer pointer, ref Vector3 hitPosition)
            {
                if (pointer == null)
                {
                    hitPosition = Vector3.zero;
                    return;
                }
                hoverReference = null;
                var startingPosition = pointer.rayStartingPosition;
                var rayDirection = pointer.rayDirection;
                RaycastHit[] raycastHitArray1 = Physics.RaycastAll(startingPosition, rayDirection * Vector3.forward, raycastDistanceLimit, mask);
                Array.Sort(raycastHitArray1, (RaycastHit x, RaycastHit y) => x.distance.CompareTo(y.distance));
                int num = 0;
                hitPosition = startingPosition + rayDirection * Vector3.forward * raycastDistanceLimit;
                while (num < raycastHitArray1.Length)
                {
                    RaycastHit raycastHit = raycastHitArray1[num];
                    if (!raycastHit.collider.attachedRigidbody || !(raycastHit.collider.attachedRigidbody.gameObject == instance.gameObject))
                    {
                        if (Vector3.Distance(instance.m_eye.position, raycastHit.point) >= instance.m_maxInteractDistance)
                        {
                            break;
                        }
                        hitPosition = raycastHit.point;
                        if (raycastHit.collider.GetComponent<Hoverable>() != null)
                        {
                            hoverReference = raycastHit.collider.gameObject;
                            return;
                        }
                        //MoveableBase is the gameobject name for Valheim Raft Mod object
                        if (raycastHit.collider.attachedRigidbody && raycastHit.collider.attachedRigidbody.name == "MoveableBase")
                        {
                            hoverReference = raycastHit.collider.gameObject;
                            return;
                        }
                        if (!raycastHit.collider.attachedRigidbody)
                        {
                            hoverReference = raycastHit.collider.gameObject;
                            return;
                        }
                        hoverReference = raycastHit.collider.attachedRigidbody.gameObject;
                        return;
                    }
                    else
                    {
                        num++;
                    }
                }
            }
        }


    }
}
