using HarmonyLib;
using UnityEngine;
using ValheimVRMod.VRCore;
using ValheimVRMod.Utilities;
using Valve.VR.InteractionSystem;
using static ValheimVRMod.Utilities.LogUtils;

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

        private static float previousHeadLocalRotation = 0f;

        public static void Prefix(Player __instance, ref Quaternion ___m_lookYaw)
        {
            if (__instance != Player.m_localPlayer || !VRPlayer.attachedToPlayer || !VRPlayer.inFirstPerson || !VHVRConfig.UseLookLocomotion())
            {
                return;
            }
            // Calculate the current head local rotation
            float currentHeadLocalRotation = VRPlayer.instance.GetComponent<Valve.VR.InteractionSystem.Player>().hmdTransform.localRotation.eulerAngles.y;
            // Find the difference between the current rotation and previous rotation
            float difference = currentHeadLocalRotation - previousHeadLocalRotation;
            // Save the current rotation for use in next iteration
            previousHeadLocalRotation = currentHeadLocalRotation;
            // Rotate the look yaw by the amount the player rotated their head since last iteration
            ___m_lookYaw *= Quaternion.Euler(0f, difference, 0f);
            // Rotate the VRPlayer localRotation by the same amount in the opposite direction
            // to offset the rotation the VRPlayer will experience due to rotation of yaw.
            var localRot = VRPlayer.instance.transform.localRotation;
            localRot *= Quaternion.Euler(0f, -difference, 0f);
            VRPlayer.instance.transform.localRotation = localRot;
        }

        public static void Postfix(Player __instance, ref Vector3 ___m_lookDir, Quaternion ___m_lookYaw)
        {
            if (__instance != Player.m_localPlayer || !VRPlayer.attachedToPlayer)
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
        public static void Postfix(Player __instance, Quaternion ___m_lookYaw, float ___m_lookPitch)
        {
            if (__instance != Player.m_localPlayer || VRPlayer.instance == null ||
                VRPlayer.instance.GetComponent<Valve.VR.InteractionSystem.Player>() == null)
            {
                return;
            }
            var hmdTransform = VRPlayer.instance.GetComponent<Valve.VR.InteractionSystem.Player>().hmdTransform;
            if (VRPlayer.attachedToPlayer && hmdTransform != null)
            {
                // Set the eye rotation equal to HMD rotation
                __instance.m_eye.rotation = hmdTransform.transform.rotation;
                // Update body position to what eye rotation used to be, but only horizontal plane
                __instance.transform.rotation = ___m_lookYaw;
            } else if (!VRPlayer.attachedToPlayer)
            {
                // We still want to restrict camera movement via the mouse to the
                // horizontal plane and allow any vertical movement to be from
                // player head only.
                __instance.m_eye.rotation = ___m_lookYaw;
            }
        }
    }

}
