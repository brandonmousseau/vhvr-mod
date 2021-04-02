using HarmonyLib;
using UnityEngine;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Patches
{
    // We ultimately want movement direction tied to the direction the player
    // body is facing. m_lookDir's only is to determine movement
    // direction. In original code it is set to the forward direction of the eyes,
    // but that would cause movement based on look direction instead.
    //
    // So we'll patch it here to set it to the forward direction of the Player
    // game object.
    [HarmonyPatch(typeof(Player), "SetMouseLook")]
    class Player_SetMouseLook_Patch
    {
        public static void Postfix(Player __instance, ref Vector3 ___m_lookDir)
        {
            if (VRPlayer.attachedToPlayer)
            {
                ___m_lookDir = __instance.gameObject.transform.forward;
            }
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
            GameObject vrCamObj = GameObject.Find("VRCamera");
            if (VRPlayer.attachedToPlayer && vrCamObj != null)
            {
                // Set the eye rotation equal to HMD rotation
                __instance.m_eye.rotation = vrCamObj.transform.rotation;
                // Update body position to what eye rotation used to be, but only horizontal plane
                __instance.transform.rotation = ___m_lookYaw * Quaternion.Euler(0f, 0f, 0f);
            } else if (!VRPlayer.attachedToPlayer)
            {
                // We still want to restrict camera movement via the mouse to the
                // horizontal plane and allow any vertical movement to be from
                // player head only.
                __instance.m_eye.rotation = ___m_lookYaw * Quaternion.Euler(0f, 0f, 0f);
            }
        }
    }

}
