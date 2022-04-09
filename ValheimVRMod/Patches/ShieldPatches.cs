using HarmonyLib;
using ValheimVRMod.Scripts.Block;
using ValheimVRMod.Scripts;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;
using Valve.VR.InteractionSystem;

namespace ValheimVRMod.Patches {
    
    /**
     * we check blocking by measuring if shield is forwarding to the attack hit with a Vector3.Dot of > 0.5 
     */
    [HarmonyPatch(typeof(Humanoid), "BlockAttack")]
    class PatchBlockAttack {
        
        private static Hand hand;
        private static SteamVR_Input_Sources handSource;
        
        static void Prefix(Humanoid __instance, ref HitData hit, ref float ___m_blockTimer) {

            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls()) {
                return;
            }
            ___m_blockTimer = ShieldBlock.instance?.blockTimer ?? WeaponBlock.instance?.blockTimer ?? Block.blockTimerNonParry;
            hit.m_dir = -__instance.transform.forward;
            if (ShieldBlock.instance?.isBlocking() ?? false) { 
                hand = VRPlayer.leftHand;
                handSource = SteamVR_Input_Sources.LeftHand;
            } 
            else if (WeaponBlock.instance?.isBlocking() ?? false) {
                hand = VRPlayer.rightHand;
                handSource = SteamVR_Input_Sources.RightHand;
            }
            else {
                hit.m_dir = __instance.transform.forward;
            }
        }
        
        static void Postfix(Humanoid __instance, bool __result, ref float ___m_blockTimer) {

            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls() || !__result) {
                return;
            }
            
            if (___m_blockTimer < ShieldBlock.blockTimerTolerance) {
                hand.hapticAction.Execute(0, 0.4f, 100, 0.5f, handSource);
                hand.hapticAction.Execute(0.4f, 0.7f, 100, 0.2f, handSource);
            }
            else {
                hand.hapticAction.Execute(0, 0.2f, 100, 0.5f, handSource);
            }
            
            ShieldBlock.instance?.block();
            WeaponBlock.instance?.block();
        }
    }
    
    [HarmonyPatch(typeof(Humanoid), "IsBlocking")]
    class PatchIsBlocking {
        static bool Prefix(Humanoid __instance, ref bool __result) {

            if (__instance != Player.m_localPlayer || (EquipScript.getRight() == EquipType.Fishing && FishingManager.instance && FishingManager.isFishing) || !VHVRConfig.UseVrControls()) {
                return true;
            }

            __result = 
                (ShieldBlock.instance?.isBlocking() ?? false) || 
                (WeaponBlock.instance?.isBlocking() ?? false);
            
            return false;
        }
    }
    
    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    class PatchRPCDamager {
        static void Prefix(Character __instance, HitData hit) {

            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls()) {
                return;
            }
            
            ShieldBlock.instance?.setBlocking(hit.m_dir);
            WeaponBlock.instance?.setBlocking(hit.m_dir);

        }
        
        static void Postfix(Character __instance) {

            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls()) {
                return;
            }

            ShieldBlock.instance?.resetBlocking();
            WeaponBlock.instance?.resetBlocking();

        }
    }
    
}
