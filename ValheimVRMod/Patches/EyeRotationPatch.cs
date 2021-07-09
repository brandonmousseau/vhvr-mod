using HarmonyLib;
using UnityEngine;
using ValheimVRMod.VRCore;
using ValheimVRMod.Utilities;

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
    [HarmonyPatch(typeof(Player), "SetMouseLook")]
    class Player_SetMouseLook_Patch
    {

        public static float? previousHeadLocalRotation;
        public static float? lastAttachmentHeading;

        public static void Prefix(Player __instance, ref Quaternion ___m_lookYaw, CraftingStation ___m_currentStation)
        {
            if (VHVRConfig.NonVrPlayer() ||
                __instance != Player.m_localPlayer ||
                !VRPlayer.attachedToPlayer ||
                !VRPlayer.inFirstPerson ||
                !VHVRConfig.UseLookLocomotion() ||
                ___m_currentStation != null /* Not Crafting */)
            {
                return;
            }

            /* Not attached to something, like boat controls */
            if(__instance.IsAttached())
            {
                //Apply ship rotation
                if(__instance.m_attached && __instance.m_attachPoint)
                {
                    // Rotate VRPlayer together with delta ship rotation
                    var newPlayerHeading = __instance.m_attachPoint.forward;
                    newPlayerHeading.y = 0;
                    newPlayerHeading.Normalize();
                    float newHeadingRotation = Quaternion.LookRotation(newPlayerHeading, __instance.transform.up).eulerAngles.y;
                    if(lastAttachmentHeading.HasValue)
                        ___m_lookYaw *= Quaternion.AngleAxis(newHeadingRotation - lastAttachmentHeading.Value, Vector3.up);
                    lastAttachmentHeading = newHeadingRotation;
                }
                return;
            }


            // Calculate the current head local rotation
            float currentHeadLocalRotation = Valve.VR.InteractionSystem.Player.instance.hmdTransform.localRotation.eulerAngles.y;
            if(previousHeadLocalRotation.HasValue)
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
    [HarmonyPatch(typeof(Player), "UpdateEyeRotation")]
    class Player_UpdateEyeRotation_Patch
    {
        public static void Postfix(Player __instance, Quaternion ___m_lookYaw)
        {
            if (VHVRConfig.NonVrPlayer() || __instance != Player.m_localPlayer || VRPlayer.instance == null)
            {
                return;
            }
            if (VRPlayer.attachedToPlayer)
            {
                var hmdTransform = Valve.VR.InteractionSystem.Player.instance.hmdTransform;
                // Set the eye rotation equal to HMD rotation
                __instance.m_eye.rotation = hmdTransform.rotation;
            } else if (!VRPlayer.attachedToPlayer)
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

        [HarmonyPatch(typeof(Player), "Update")]
        class Player_Update_RotationPatch
        {
            public static void Postfix(Player __instance)
            {
                if (VHVRConfig.NonVrPlayer() || __instance != Player.m_localPlayer)
                {
                    return;
                }
                __instance.FaceLookDirection();
            }
        }

        [HarmonyPatch(typeof(Player), "LateUpdate")]
        class Player_LateUpdate_RotationPatch
        {
            public static void Postfix(Player __instance)
            {
                if (VHVRConfig.NonVrPlayer() || __instance != Player.m_localPlayer)
                {
                    return;
                }
                __instance.FaceLookDirection();
            }
        }

        [HarmonyPatch(typeof(Player), "FixedUpdate")]
        class Player_FixedUpdate_RotationPatch
        {
            public static void Postfix(Player __instance)
            {
                if (VHVRConfig.NonVrPlayer() || __instance != Player.m_localPlayer)
                {
                    return;
                }
                __instance.FaceLookDirection();
            }
        }

        
        /// <summary>
        /// When interacting with an attachment point orient player in the direction of the attachment point
        /// </summary>
        [HarmonyPatch(typeof(Player), "AttachStart")]
        
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

                if(attachPoint)
                {
                    // Rotate VRPlayer together with delta ship rotation
                    var attachmentHeading = attachPoint.transform.forward;
                    attachmentHeading.y = 0;
                    attachmentHeading.Normalize();

                    __instance.m_lookYaw = Quaternion.LookRotation(attachmentHeading, __instance.transform.up);

                    Player_SetMouseLook_Patch.lastAttachmentHeading = null;
                }
            }
        }

        /// <summary>
        /// Orient the player to the body direction when detaching from objects
        /// </summary>
        [HarmonyPatch(typeof(Player), "AttachStop")]
        
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
                float deltaRotation = __instance.transform.rotation.eulerAngles.y - Valve.VR.InteractionSystem.Player.instance.hmdTransform.rotation.eulerAngles.y;
                VRPlayer.instance.transform.localRotation *= Quaternion.AngleAxis(deltaRotation, Vector3.up);
                Player_SetMouseLook_Patch.previousHeadLocalRotation = null;
            }
        }
    }
}
