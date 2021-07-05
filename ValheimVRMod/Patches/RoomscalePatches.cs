using HarmonyLib;
using UnityEngine;
using ValheimVRMod.VRCore;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Patches
{
    /// <summary>
    /// Properly sets animation parameters to have animation when moving in roomscale
    /// </summary>    
    [HarmonyPatch(typeof(Character), "UpdateWalking")]
    class Character_UpdateWalking_RoomscaleAnimationPatch
    {
        private static void Postfix(Character __instance)
        {
            if(__instance == Player.m_localPlayer && VRPlayer.inFirstPerson)
            {
                __instance.m_zanim.SetFloat(Player.forward_speed, __instance.m_animator.GetFloat(Player.forward_speed) + VRPlayer.roomscaleAnimationForwardSpeed);
                __instance.m_zanim.SetFloat(Player.sideway_speed, __instance.m_animator.GetFloat(Player.sideway_speed) + VRPlayer.roomscaleAnimationSideSpeed);
            }
        }
    }

    /// <summary>
    /// Applies the roomscale velocity to the player rigidbody
    /// </summary>    
    [HarmonyPatch(typeof(Character), "SyncVelocity")]
    class Character_SyncVelocity_ApplyRoomscaleVelocity
    {
        private static void Prefix(Character __instance)
        {
            if(__instance == Player.m_localPlayer && VRPlayer.inFirstPerson)
            {
                __instance.m_body.AddForce(VRPlayer.roomscaleVelocity, ForceMode.VelocityChange);
                //Need to move the ships backwards after this               
                if (__instance.m_lastGroundBody && __instance.m_lastGroundBody.gameObject.layer != __instance.gameObject.layer && __instance.m_lastGroundBody.mass > __instance.m_body.mass)
                {
                    float massFactor = __instance.m_body.mass / __instance.m_lastGroundBody.mass;
                    __instance.m_lastGroundBody.AddForceAtPosition(-VRPlayer.roomscaleVelocity * massFactor, __instance.transform.position, ForceMode.VelocityChange);
                }
            }
        }
    }
}