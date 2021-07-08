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

            __instance.m_zanim.SetFloat(Player.forward_speed, __instance.m_animator.GetFloat(Player.forward_speed) + VRPlayer.roomscaleAnimationForwardSpeed*VRPlayer.ROOMSCALE_ANIMATION_WEIGHT);
            __instance.m_zanim.SetFloat(Player.sideway_speed, __instance.m_animator.GetFloat(Player.sideway_speed) + VRPlayer.roomscaleAnimationSideSpeed*VRPlayer.ROOMSCALE_ANIMATION_WEIGHT);
        }
    }

    /// <summary>
    /// Detach player from ship momentarily if roomscale movement is detected
    /// </summary>    
    [HarmonyPatch(typeof(Character), "ApplyGroundForce")]
    class Character_ApplyGroundForce_DetachIfRoomscaleMovement
    {
        public static bool ShouldDetachFromShip(Character character) 
             => character == Player.m_localPlayer &&
                VRPlayer.inFirstPerson &&
                VRPlayer.roomscaleMovement != Vector3.zero;
        private static IEnumerable<CodeInstruction> Transpiler(ILGenerator generator, IEnumerable<CodeInstruction> instructions)
        {
            //Don't edit method for non-vr players
            if(VHVRConfig.NonVrPlayer()) return instructions;
            
            //Patch metod to detach player from ship if ShouldDetachFromShip is true (if there's roomscale movement)
            var patched = new List<CodeInstruction>();
            foreach(var instruction in instructions)
            {
                //If is ldarga.s targetVelocity (parameter #2: this, vel, targetVelocity)
                if(instruction.IsLdarga(2))
                {
                    //if (ShouldDetachFromShip(this))
                    patched.Add(new CodeInstruction(OpCodes.Ldarg_0));
                    patched.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Character_ApplyGroundForce_DetachIfRoomscaleMovement), 
                                nameof(Character_ApplyGroundForce_DetachIfRoomscaleMovement.ShouldDetachFromShip), new Type [] { typeof(Character) })));
                    var branchLabel = generator.DefineLabel();
                    instruction.labels.Add(branchLabel);
                    patched.Add(new CodeInstruction(OpCodes.Brfalse_S, branchLabel));

                    // m_lastAttachBody = null;
                    patched.Add(new CodeInstruction(OpCodes.Ldarg_0));
                    patched.Add(new CodeInstruction(OpCodes.Ldnull));
                    patched.Add(new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(Character), nameof(Character.m_lastAttachBody))));
                }
                patched.Add(instruction);
            }
            return patched;
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