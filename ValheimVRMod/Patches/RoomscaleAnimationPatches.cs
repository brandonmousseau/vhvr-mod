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
}