using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using ValheimVRMod.VRCore;
using ValheimVRMod.Utilities;
using System.Collections.Generic;
using System;

namespace ValheimVRMod.Patches
{
    /// <summary>
    /// Properly sets animation parameters to have animations when moving in roomscale
    /// </summary>    
    [HarmonyPatch(typeof(Character), "UpdateWalking")]
    class Character_UpdateWalking_RoomscaleAnimationPatch
    {
        private static void Postfix(Character __instance)
        {
            if(VHVRConfig.NonVrPlayer() ||
                __instance != Player.m_localPlayer ||
                !VRPlayer.inFirstPerson)
            {
                return;
            }

            __instance.m_zanim.SetFloat(Player.s_forwardSpeed, __instance.m_animator.GetFloat(Player.s_forwardSpeed) + VRPlayer.roomscaleAnimationForwardSpeed * VRPlayer.ROOMSCALE_ANIMATION_WEIGHT);
            __instance.m_zanim.SetFloat(Player.s_sidewaySpeed, __instance.m_animator.GetFloat(Player.s_sidewaySpeed) + VRPlayer.roomscaleAnimationSideSpeed * VRPlayer.ROOMSCALE_ANIMATION_WEIGHT);
        }
    }

    /// <summary>
    /// Detach player from ship momentarily if roomscale movement is detected
    /// </summary>    
    [HarmonyPatch(typeof(Character), "ApplyGroundForce")]
    class Character_ApplyGroundForce_DetachIfRoomscaleMovement
    {
        private static void Prefix(Character __instance)
        {
            if(VHVRConfig.NonVrPlayer() ||
                __instance != Player.m_localPlayer ||
                !VRPlayer.inFirstPerson ||
                VRPlayer.roomscaleMovement == Vector3.zero ||
                !__instance.GetStandingOnShip())
            {
                return;
            }

            __instance.m_lastAttachBody = null;
        }
    }

    /// <summary>
    /// Applies the roomscale movement to the player rigidbody
    /// </summary>    
    [HarmonyPatch(typeof(Character), "SyncVelocity")]
    class Character_SyncVelocity_ApplyRoomscaleVelocity
    {
        private static void Prefix(Character __instance)
        {
            
            if(VHVRConfig.NonVrPlayer() ||
                __instance != Player.m_localPlayer ||
                !VRPlayer.inFirstPerson)
            {
                return;
            }

            //We need to calculate the required movement to not clip in the ground
            var groundMovement = __instance.IsOnGround() ? Vector3.ProjectOnPlane(VRPlayer.roomscaleMovement, __instance.m_lastGroundNormal) : VRPlayer.roomscaleMovement;
            __instance.m_body.position += groundMovement;
        }
    }
}
