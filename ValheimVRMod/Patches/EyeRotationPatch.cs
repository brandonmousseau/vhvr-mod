using HarmonyLib;
using UnityEngine;
using ValheimVRMod.VRCore;
using ValheimVRMod.Utilities;
using System.Reflection;
using System.Collections.Generic;

namespace ValheimVRMod.Patches
{
    // We always want forward movement direction tied to the direction
    // the player body is facing. m_lookDir's only is to determine movement direction.
    // So we'll patch it here to set it to the forward direction of the Player
    // game object.
    //
    // If the player prefers to keep look direction and walk direction tied together,
    // we want to rotate the look yaw by the amount the head set has rotated since
    // the last update (so total yaw will be headset rotation + mouse/joystick rotation).
    // The player character should be rotated to whatever the lookYaw is. We need to
    // rotate the VRPlayer localRotation in the opposite direction by the same amount
    // to offset the affect updating the yaw has on rotating the player model, which
    // would create a positive feedback loop and constantly create a rotation if your
    // head isn't exactly centered.
    //
    [HarmonyPatch(typeof(Player), nameof(Player.SetMouseLook))]
    class Player_SetMouseLook_Patch
    {

        public static float? previousHeadLocalRotation;
        public static float? lastAttachmentHeading;
        public static Quaternion lastAttachRot;

        public static GameObject headLookRef;
        public static bool wasAttached;

        static Vector3 smoothCamUp;
        static Vector3 smoothCamUpVel;

        public static void Prefix(Player __instance, ref Quaternion ___m_lookYaw, CraftingStation ___m_currentStation)
        {
            if (VHVRConfig.NonVrPlayer() ||
                __instance != Player.m_localPlayer ||
                !VRPlayer.attachedToPlayer ||
                !VRPlayer.inFirstPerson ||
                Game.IsPaused() ||
                VRPlayer.ShouldPauseMovement ||
                !VHVRConfig.UseLookLocomotion() ||
                ___m_currentStation != null /* Not Crafting */)
            {
                return;
            }

            if (!headLookRef)
            {
                headLookRef = new GameObject();
            }
            /* Attached to something, like boat controls */
            if (__instance.IsAttached() && (VHVRConfig.ViewTurnWithMountedAnimal() || !__instance.IsRiding()))
            {
                //Apply ship rotation
                if (__instance.m_attached && __instance.m_attachPoint)
                {
                    // Rotate VRPlayer together with delta ship rotation

                    if (VHVRConfig.IsShipImmersiveCamera() && !__instance.IsRiding())
                    {
                        headLookRef.transform.SetParent(__instance.m_attachPoint);
                        if (!wasAttached)
                        {
                            headLookRef.transform.position = __instance.transform.position;
                            __instance.m_lookYaw = Quaternion.LookRotation(__instance.m_body.transform.forward, __instance.m_attachPoint.up);
                            headLookRef.transform.rotation = __instance.transform.rotation;
                            lastAttachRot = headLookRef.transform.rotation;
                            smoothCamUp = __instance.m_attachPoint.up;
                            wasAttached = true;
                        }
                        else
                        {
                            var newPlayerRot = headLookRef.transform.rotation;
                            __instance.m_body.transform.rotation *= Quaternion.Inverse(lastAttachRot) * newPlayerRot;
                            smoothCamUp = Vector3.SmoothDamp(smoothCamUp, __instance.m_attachPoint.up, ref smoothCamUpVel, 0.2f, 2f, Time.deltaTime);
                            __instance.m_lookYaw = Quaternion.LookRotation(__instance.m_body.transform.forward, smoothCamUp);
                            headLookRef.transform.rotation = __instance.m_body.transform.rotation;
                            lastAttachRot = headLookRef.transform.rotation;
                        }
                    }
                    else
                    {
                        var newPlayerHeading = __instance.m_attachPoint.forward;
                        newPlayerHeading.y = 0;
                        newPlayerHeading.Normalize();
                        var upTarget = __instance.transform.up;
                        if (__instance.IsAttachedToShip() || __instance.IsRiding())
                        {
                            upTarget = Vector3.up;
                        }
                        float newHeadingRotation = Quaternion.LookRotation(newPlayerHeading, upTarget).eulerAngles.y;
                        if (lastAttachmentHeading.HasValue)
                            ___m_lookYaw *= Quaternion.AngleAxis(newHeadingRotation - lastAttachmentHeading.Value, Vector3.up);
                        lastAttachmentHeading = newHeadingRotation;
                    }

                }
                return;
            }
            else
            {
                headLookRef.transform.parent = null;
                wasAttached = false;
            }

            if (Player.m_localPlayer.InDodge())
            {
                return;
            }

            // Calculate the current head local rotation
            float currentHeadLocalRotation = Valve.VR.InteractionSystem.Player.instance.hmdTransform.localRotation.eulerAngles.y;
            if (previousHeadLocalRotation.HasValue)
            {
                // Find the difference between the current rotation and previous rotation
                float deltaRotation = currentHeadLocalRotation - previousHeadLocalRotation.Value;

                // Rotate the look yaw by the amount the player rotated their head since last iteration
                ___m_lookYaw *= Quaternion.AngleAxis(deltaRotation, Vector3.up);

                // Rotate the VRPlayer to match the current yaw
                // to offset the rotation the VRPlayer will experience due to rotation of yaw.
                VRPlayer.instance.transform.localRotation *= Quaternion.AngleAxis(-deltaRotation, Vector3.up);
            }

            // Save the current rotation for use in next iteration
            previousHeadLocalRotation = currentHeadLocalRotation;
        }

        public static void Postfix(Player __instance, ref Vector3 ___m_lookDir)
        {
            if (VHVRConfig.NonVrPlayer() || __instance != Player.m_localPlayer || !VRPlayer.attachedToPlayer)
            {
                return;
            }
            ___m_lookDir = __instance.gameObject.transform.forward;
        }
    }

    // The second part of this patch updates the UpdateEyeRotation method, which
    // originally only updated the player "eye" to the pitch and yaw that were
    // being determined by the user's mouse input. Now however, the HMD is what
    // sets the direction of the player's gaze. This is important because what the player is
    // looking at is used when computing a lot of things, such as what is
    // rendered on screen plus some other gameplay related things (such as controlling what
    // the player is trying to interact with). So if we don't match these rotations, when
    // the player looks in a direction that doesn't match the m_eye rotation, things get weird,
    // ...ie disappearing trees and all kinds of graphical glitches.
    //
    // Since originally the method was being used to set the eye rotation (and then
    // eye rotation used to set m_lookDir), we'll update the Player GameObject rotation
    // to equal to current yaw from mouse input. That way, due to the SetMouseLook patch,
    // m_lookDir will get the value of the updated player body direction plus of course the
    // side effect of the body rotation being directly controlled by the mouse (or whatever
    // input that will be used).
    [HarmonyPatch(typeof(Player), nameof(Player.UpdateEyeRotation))]
    class Player_UpdateEyeRotation_Patch
    {
        public static void Postfix(Player __instance, Quaternion ___m_lookYaw)
        {
            if (VHVRConfig.NonVrPlayer() || __instance != Player.m_localPlayer || VRPlayer.instance == null)
            {
                return;
            }
            if (VRPlayer.attachedToPlayer && !Player.m_localPlayer.InDodge())
            {
                var hmdTransform = Valve.VR.InteractionSystem.Player.instance.hmdTransform;
                // Set the eye rotation equal to HMD rotation
                __instance.m_eye.rotation = hmdTransform.rotation;
            }
            else if (!VRPlayer.attachedToPlayer)
            {
                // We still want to restrict camera movement via the mouse to the
                // horizontal plane and allow any vertical movement to be from
                // player head only.
                __instance.m_eye.rotation = ___m_lookYaw;
            }
        }
    }

    // Force the Player body rotatoin to always equal the yaw.
    class Player_Rotation_Patch
    {

        [HarmonyPatch(typeof(Player), nameof(Player.Update))]
        class Player_Update_RotationPatch
        {
            public static void Postfix(Player __instance)
            {
                if (!ShouldFaceLookDirection(__instance))
                {
                    return;
                }
                __instance.FaceLookDirection();
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.LateUpdate))]
        class Player_LateUpdate_RotationPatch
        {
            public static void Postfix(Player __instance)
            {
                if (!ShouldFaceLookDirection(__instance))
                {
                    return;
                }
                __instance.FaceLookDirection();
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.FixedUpdate))]
        class Player_FixedUpdate_RotationPatch
        {
            public static void Postfix(Player __instance)
            {
                if (!ShouldFaceLookDirection(__instance))
                {
                    return;
                }
                __instance.FaceLookDirection();
            }
        }

        static bool ShouldFaceLookDirection(Player player)
        {
            // TODO: Consider disabling face-look-direction patch whenever VRPlayer.attachedToPlayer is false as opposed to just when PlayerCustomizaton.IsBarberGuiVisible().

            return !VHVRConfig.NonVrPlayer() && player == Player.m_localPlayer && !PlayerCustomizaton.IsBarberGuiVisible();
        }


        /// <summary>
        /// When interacting with an attachment point orient player in the direction of the attachment point
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.AttachStart))]

        class Player_AttachStart_Patch
        {
            static void Postfix(Player __instance, Transform attachPoint)
            {
                if (VHVRConfig.NonVrPlayer() ||
                    __instance != Player.m_localPlayer ||
                    !VRPlayer.attachedToPlayer ||
                    !VRPlayer.inFirstPerson)
                {
                    return;
                }

                if (attachPoint)
                {
                    // Rotate VRPlayer together with delta ship rotation
                    var attachmentHeading = attachPoint.transform.forward;
                    attachmentHeading.y = 0;
                    attachmentHeading.Normalize();
                    var upTarget = __instance.transform.up;
                    if (__instance.IsAttachedToShip() || __instance.IsRiding())
                    {
                        upTarget = Vector3.up;
                    }
                    __instance.m_lookYaw = Quaternion.LookRotation(attachmentHeading, upTarget);
                    VRPlayer.headPositionInitialized = false;
                    VRPlayer.vrPlayerInstance?.ResetRoomscaleCamera();
                    Player_SetMouseLook_Patch.lastAttachmentHeading = null;
                }
            }
        }

        /// <summary>
        /// With IsShipImmersiveCamera option, the camera will follow the rotation and tilt of the ship
        /// </summary>
        [HarmonyPatch(typeof(GameCamera), nameof(GameCamera.ApplyCameraTilt))]
        class GameCamera_ApplyCameraTilt_Patch
        {
            static GameObject headLookRef;
            static bool wasAttached;
            static Quaternion lastAttachRot;
            static Vector3 smoothCamUp;
            static Vector3 smoothCamUpVel;
            static void Postfix(Player player)
            {
                if (VHVRConfig.NonVrPlayer() ||
                    player != Player.m_localPlayer ||
                    !VRPlayer.attachedToPlayer ||
                    !VRPlayer.inFirstPerson || !VHVRConfig.isShipImmersiveCameraStanding())
                {
                    return;
                }

                if (!headLookRef)
                {
                    headLookRef = new GameObject();
                }
                if (!player.IsAttached())
                {
                    var ship = player.GetStandingOnShip();
                    var moveableBase = player.transform.parent;
                    if (ship || (moveableBase && moveableBase?.name == "MoveableBase"))
                    {
                        Transform referenceUp = null;
                        if (ship)
                        {
                            referenceUp = ship.transform;
                        }
                        else if (moveableBase && moveableBase?.name == "MoveableBase")
                        {
                            referenceUp = moveableBase.transform;
                        }

                        if (referenceUp == null)
                        {
                            return;
                        }
                        if (VHVRConfig.isShipImmersiveCameraStanding())
                        {
                            headLookRef.transform.SetParent(referenceUp);
                            headLookRef.transform.position = player.m_head.transform.position;
                            var targetUp = Vector3.up;
                            var targetforward = player.m_body.transform.forward;

                            if (!wasAttached)
                            {
                                player.m_lookYaw = Quaternion.LookRotation(targetforward, targetUp);
                                headLookRef.transform.rotation = player.m_body.transform.rotation;
                                lastAttachRot = headLookRef.transform.rotation;
                                smoothCamUp = targetUp;
                                wasAttached = true;
                            }
                            else
                            {
                                var newPlayerRot = headLookRef.transform.rotation;
                                player.m_body.transform.rotation *= Quaternion.Inverse(lastAttachRot) * newPlayerRot;
                                if (VHVRConfig.ShipImmersiveCameraType() == "ShipUp")
                                {
                                    targetUp = referenceUp.up;
                                    targetforward = player.m_body.transform.forward;
                                    targetforward = referenceUp.InverseTransformDirection(targetforward);
                                    targetforward.y = 0;
                                    targetforward = referenceUp.TransformDirection(targetforward);
                                }
                                else if (VHVRConfig.ShipImmersiveCameraType() == "WorldUp")
                                {
                                    targetforward = player.m_body.transform.forward;
                                    targetforward.y = 0;
                                }
                                smoothCamUp = Vector3.SmoothDamp(smoothCamUp, targetUp, ref smoothCamUpVel, 0.2f, 1f, Time.deltaTime);
                                player.m_lookYaw = Quaternion.LookRotation(targetforward, smoothCamUp);
                                headLookRef.transform.rotation = player.m_body.transform.rotation;
                                lastAttachRot = headLookRef.transform.rotation;
                            }
                        }
                    }
                    else if (wasAttached)
                    {
                        var targetforward = player.m_body.transform.forward;
                        targetforward.y = 0;
                        targetforward = targetforward.normalized;
                        player.m_lookYaw = Quaternion.LookRotation(targetforward, Vector3.up);
                        wasAttached = false;
                    }
                }
                else
                {
                    if (wasAttached)
                    {
                        var targetforward = player.m_body.transform.forward;
                        targetforward.y = 0;
                        targetforward = targetforward.normalized;
                        player.m_lookYaw = Quaternion.LookRotation(targetforward, Vector3.up);
                        wasAttached = false;
                    }
                }
            }
        }

        /// <summary>
        /// Orient the player to the body direction when detaching from objects
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.AttachStop))]

        class Player_AttachStop_Patch
        {
            static void Prefix(Player __instance)
            {
                if (VHVRConfig.NonVrPlayer() ||
                    __instance != Player.m_localPlayer ||
                    !VRPlayer.attachedToPlayer ||
                    !VRPlayer.inFirstPerson ||
                    !__instance.m_attached ||
                    !__instance.m_attachPoint)
                {
                    return;
                }

                //Recenter player on body
                var attachmentHeading = __instance.transform.forward;
                attachmentHeading.y = 0;
                attachmentHeading.Normalize();
                __instance.m_lookYaw = Quaternion.LookRotation(attachmentHeading, Vector3.up);
                VRPlayer.headPositionInitialized = false;
                VRPlayer.vrPlayerInstance?.ResetRoomscaleCamera();
                Player_SetMouseLook_Patch.previousHeadLocalRotation = null;
            }
        }


        /// <summary>
        /// Disables the pause menu auto camera rotation
        /// </summary>
        [HarmonyPatch(typeof(Game), nameof(Game.UpdatePause))]

        class Game_UpdatePause_Patch
        {
            private static MethodInfo MenuIsVisibleCall = AccessTools.Method(typeof(Menu), nameof(Menu.IsVisible));
            private static bool Nope() => false;
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var original = new List<CodeInstruction>(instructions);
                var patched = new List<CodeInstruction>();
                if (VHVRConfig.NonVrPlayer())
                {
                    return original;
                }

                foreach (var instruction in original)
                {
                    if (instruction.Calls(MenuIsVisibleCall))
                        patched.Add(CodeInstruction.Call(typeof(Game_UpdatePause_Patch), nameof(Game_UpdatePause_Patch.Nope)));
                    else
                        patched.Add(instruction);
                }
                return patched;
            }
        }

        /// <summary>
        /// This makes the mounts try to follow the hmd eyedir
        /// </summary>
        [HarmonyPatch(typeof(Sadle), nameof(Sadle.ApplyControlls))]

        class Sadle_ApplyControlls_Patch
        {
            static void Prefix(ref Vector3 lookDir)
            {
                if (VHVRConfig.NonVrPlayer())
                {
                    return;
                }

                //Recenter player on body
                lookDir = Valve.VR.InteractionSystem.Player.instance.hmdTransform.forward;
            }
        }
    }
}
