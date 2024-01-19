using HarmonyLib;
using ValheimVRMod.Scripts.Block;
using ValheimVRMod.Scripts;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;
using Valve.VR.InteractionSystem;
using UnityEngine;
using System.Collections;

namespace ValheimVRMod.Patches {
    
    /**
     * we check blocking by measuring if shield is forwarding to the attack hit with a Vector3.Dot of > 0.5 
     */
    [HarmonyPatch(typeof(Humanoid), "BlockAttack")]
    class PatchBlockAttack {
        
        private static HandHapticTrigger handHapticTrigger = HandHapticTrigger.None;
        private enum HandHapticTrigger
        {
            LeftHand,
            RightHand,
            BothHand,
            None
        }
        
        static void Prefix(Humanoid __instance, ref HitData hit, ref float ___m_blockTimer) {

            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls()) {
                return;
            }

            if(VHVRConfig.BlockingType() != "GrabButton")
            {
                if (FistCollision.instance.usingFistWeapon())
                {
                    ___m_blockTimer = FistBlock.instance?.blockTimer ?? Block.blockTimerNonParry;
                }
                else if (WeaponBlock.instance && (WeaponBlock.instance.weaponWield.allowBlocking() || WeaponBlock.instance.weaponWield.isLeftHandWeapon()))
                {
                    ___m_blockTimer = WeaponBlock.instance?.blockTimer ?? Block.blockTimerNonParry;
                }
                else
                {
                    ___m_blockTimer = ShieldBlock.instance?.blockTimer ?? Block.blockTimerNonParry;
                }
            }
            hit.m_dir = -__instance.transform.forward;
            handHapticTrigger = HandHapticTrigger.None;
            if (ShieldBlock.instance?.isBlocking() ?? false)
            {
                handHapticTrigger = VHVRConfig.LeftHanded() ? HandHapticTrigger.RightHand : HandHapticTrigger.LeftHand;
            }
            else if (WeaponBlock.instance?.isBlocking() ?? false)
            {
                handHapticTrigger = HandHapticTrigger.BothHand;
            }
            else if (FistBlock.instance?.isBlocking() ?? false)
            {
                handHapticTrigger = HandHapticTrigger.BothHand;
            }
            else
            { 
                hit.m_dir = __instance.transform.forward;
            }
        }
        
        static void Postfix(Humanoid __instance, bool __result, ref float ___m_blockTimer) {

            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls() || !__result) {
                return;
            }
            float delay = 0f;
            float duration = 0f;
            float freq = 100f;
            float amplitude = 0f;
            if (___m_blockTimer < Block.blockTimerTolerance) {
                duration = 0.7f;
                amplitude = 0.3f;
            }
            else {
                duration = 0.2f;
                amplitude = 0.2f;
            }

            if (__instance.m_staggerDamage < __instance.GetStaggerTreshold()) 
            {
                switch (handHapticTrigger)
                {
                    case HandHapticTrigger.BothHand:
                        VRPlayer.leftHand.hapticAction.Execute(delay, duration, freq, amplitude, SteamVR_Input_Sources.LeftHand);
                        VRPlayer.rightHand.hapticAction.Execute(delay, duration, freq, amplitude, SteamVR_Input_Sources.RightHand);
                        break;
                    case HandHapticTrigger.LeftHand:
                        VRPlayer.leftHand.hapticAction.Execute(delay, duration, freq, amplitude, SteamVR_Input_Sources.LeftHand);
                        break;
                    case HandHapticTrigger.RightHand:
                        VRPlayer.rightHand.hapticAction.Execute(delay, duration, freq, amplitude, SteamVR_Input_Sources.RightHand);
                        break;
                }
            }
            

            

            ShieldBlock.instance?.block();
            WeaponBlock.instance?.block();
            FistBlock.instance?.block();
        }
    }
    
    [HarmonyPatch(typeof(Humanoid), "IsBlocking")]
    class PatchIsBlocking {
        static bool Prefix(Humanoid __instance, ref float ___m_blockTimer, ref bool __result) {

            if (__instance != Player.m_localPlayer || (FishingManager.instance && FishingManager.isFishing) || !VHVRConfig.UseVrControls()) {
                return true;
            }
            if(VHVRConfig.BlockingType() == "GrabButton")
            {
                WeaponBlock.instance?.UpdateGrabParry();
                ShieldBlock.instance?.UpdateGrabParry();
                FistBlock.instance?.UpdateGrabParry();
                if (WeaponBlock.instance?.wasResetTimer == true|| ShieldBlock.instance?.wasResetTimer == true)
                {
                    ___m_blockTimer = 0;
                    WeaponBlock.instance?.resetTimer();
                    ShieldBlock.instance?.resetTimer();
                    FistBlock.instance?.resetTimer();
                }
            }
                
            __result = 
                (ShieldBlock.instance?.isBlocking() ?? false) || 
                (WeaponBlock.instance?.isBlocking() ?? false) ||
                (FistBlock.instance?.isBlocking() ?? false);

            return false;
        }
    }
    [HarmonyPatch(typeof(Hud),nameof(Hud.UpdateStagger))]
    class PatchStagger
    {
        static void Postfix(Hud __instance, Player player)
        {
            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls())
            {
                return;
            }
            __instance.m_staggerAnimator.SetBool("Visible", player.GetStaggerPercentage()>0f);
        }
    }

    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    class PatchRPCDamager {
        static void Prefix(Character __instance, HitData hit) {

            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls()) {
                return;
            }

            FistBlock.instance?.setBlocking(hit);
            ShieldBlock.instance?.setBlocking(hit);
            WeaponBlock.instance?.setBlocking(hit);
        }
        
        static void Postfix(Character __instance) {

            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls()) {
                return;
            }

            ShieldBlock.instance?.resetBlocking();
            WeaponBlock.instance?.resetBlocking();
            FistBlock.instance?.resetBlocking();
        }
    }
    [HarmonyPatch(typeof(Character),nameof(Character.AddStaggerDamage))]
    class PatchStaggerDamage
    {
        static void Postfix(Character __instance)
        {
            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls())
            {
                return;
            }

            if (__instance.m_staggerDamage >= __instance.GetStaggerTreshold())
            {
                VRPlayer.vrPlayerInstance.TriggerHandVibration(1.2f);
            }
        }
    }
}
