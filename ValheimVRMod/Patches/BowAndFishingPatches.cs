using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using ValheimVRMod.Scripts;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Patches {

    /**
     * Set the Draw Percentage to how much the Bow String is Pulled
     */
    [HarmonyPatch(typeof(Humanoid), "GetAttackDrawPercentage")]
    class PatchGetAttackDrawPercentage {
        static bool Prefix(Humanoid __instance, ref float __result) {

            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls()) {
                return true;
            }

            if (EquipScript.CurrentOffHandEquipType() == EquipType.Bow && VHVRConfig.RestrictBowDrawSpeed() == "None") {
                __result = BowLocalManager.instance.GetAttackPercentage();
                return false;
            }

            if (EquipScript.CurrentMainHandEquipType() == EquipType.Fishing) {
                __result = FishingManager.attackDrawPercentage;
                return false;
            }

            return true;
        }

        static void Postfix(Humanoid __instance, ref float __result) {
            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls()) {
                return;
            }

            if (EquipScript.CurrentOffHandEquipType() != EquipType.Bow || VHVRConfig.RestrictBowDrawSpeed() == "None" || BowLocalManager.instance == null)
            {
                return;
            }

            if(__result > BowLocalManager.instance.timeBasedChargePercentage)
            {
                BowLocalManager.instance.timeBasedChargePercentage = __result;
            }

            // Only clamp the charge upon releasing, not during pulling.
            if (!BowLocalManager.instance.pulling)
            {
                // Since the attack draw percentage is not patched in the prefix, we need to clamp it here in case the real life pull percentage is smaller than the unpatched attack draw percentage.
                __result = Math.Min(__result, BowLocalManager.realLifePullPercentage);
            }
        }
    }

    [HarmonyPatch(typeof(Player), "UpdateWeaponLoading")]
    class PatchUpdateWeaponLoading
    {
        static void Postfix(Player __instance, ItemDrop.ItemData weapon, ref float dt)
        {
            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls())
            {
                return;
            }
            
            if (EquipScript.CurrentOffHandEquipType() == EquipType.Crossbow && CrossbowMorphManager.instance != null)
            {
                CrossbowMorphManager.instance.UpdateWeaponLoading(__instance, dt);
            }
        }

    }

    [HarmonyPatch(typeof(Player), "QueueReloadAction")]
    class PatchQueueReloadAction
    {
        static bool hasReloadedDundrDuringCurrentTwoHandedWield = false;

        static bool Prefix(Player __instance)
        {
            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls())
            {
                return true;
            }

            if (EquipScript.CurrentOffHandEquipType() == EquipType.Crossbow)
            {
                return CrossbowManager.CanQueueReloadAction();
            }

            if (VHVRConfig.OneHandedBow() || !VHVRConfig.CrossbowManualReload())
            {
                return true;
            }

            if (EquipScript.IsDundrEquipped())
            {
                if (!LocalWeaponWield.isCurrentlyTwoHanded())
                {
                    hasReloadedDundrDuringCurrentTwoHandedWield = false;
                    // Do not charge Dundr if holding it with only one hand.
                    return false;
                }
                if (hasReloadedDundrDuringCurrentTwoHandedWield)
                {
                    // Do not charge Dundr if it has already been charged once during current two-handed wield.
                    return false;
                }
                hasReloadedDundrDuringCurrentTwoHandedWield = true;
                return true;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Attack), nameof(Attack.Start))]
    class DestoryBoltAfterCrossbowAttackPatch
    {
        static void Postfix(Attack __instance, bool __result)
        {
            if (__result &&
                __instance.m_character == Player.m_localPlayer &&
                VHVRConfig.UseVrControls() &&
                EquipScript.CurrentOffHandEquipType() == EquipType.Crossbow &&
                CrossbowMorphManager.instance != null &&
                !CrossbowMorphManager.instance.shouldAutoReload)
            {
                CrossbowMorphManager.instance.destroyBolt();
            }
        }
    }

    /**
        * Manipulate Position and Direction of the Arrow SpawnPoint
        */
    [HarmonyPatch(typeof(Attack), nameof(Attack.GetProjectileSpawnPoint))]
    class PatchGetProjectileSpawnPoint {
        static bool Prefix(Attack __instance, out Vector3 spawnPoint, out Vector3 aimDir, Humanoid ___m_character) {

            spawnPoint = Vector3.zero;
            aimDir = Vector3.zero;
            
            if (___m_character != Player.m_localPlayer || !VHVRConfig.UseVrControls()) {
                return true;
            }

            // Summoners may be held in either hand. Other magic items that are not staves (see Protector) leave aiming
            // to vanilla.
            var summoner = SummonerManager.instance;
            if (summoner != null)
            {
                spawnPoint = summoner.GetProjectileSpawnPoint(__instance);
                aimDir = summoner.AimDir;
                return false;
            }

            switch(EquipScript.CurrentOffHandEquipType()) {
                case EquipType.Bow:
                    spawnPoint = BowLocalManager.spawnPoint;
                    aimDir = BowLocalManager.aimDir;
                    return false;
                case EquipType.Crossbow:
                    spawnPoint = CrossbowManager.GetBoltSpawnPoint(__instance);
                    aimDir = CrossbowManager.AimDir;
                    return false;
            }

            switch (EquipScript.CurrentMainHandEquipType()) {

                case EquipType.Fishing:
                    spawnPoint = FishingManager.spawnPoint;
                    aimDir = FishingManager.aimDir;
                    return false;
                case EquipType.Spear:
                case EquipType.SpearChitin:
                case EquipType.ThrowObject:
                    spawnPoint = ThrowableManager.spawnPoint;
                    aimDir = WeaponUtils.GetThrowAimDir(__instance, ThrowableManager.aimDir, ThrowableManager.handSpeed);
                    return false;
                case EquipType.Magic:
                    var staff = MagicStaffManagers.Current;
                    if (staff == null)
                    {
                        return true;
                    }
                    spawnPoint = staff.GetProjectileSpawnPoint(__instance);
                    aimDir = staff.AimDir;
                    return false;
                case EquipType.RuneSkyheim:
                    spawnPoint = VRPlayer.rightHand.transform.position;
                    aimDir = VRPlayer.rightPointer.rayDirection * Vector3.forward;
                    return false;
            }

            if (EquipScript.IsThrowable(___m_character.GetRightItem()))
            {
                spawnPoint = ThrowableManager.spawnPoint;
                aimDir = WeaponUtils.GetThrowAimDir(__instance, ThrowableManager.aimDir, ThrowableManager.handSpeed);
                return false;
            }
            return true;

        }
    }

    /**
     * Remove Crosshair 
     */
    [HarmonyPatch(typeof(Hud), "UpdateCrosshair")]
    class PatchUpdateCrosshair {
        static void Prefix(ref float bowDrawPercentage) {
            if (VHVRConfig.UseVrControls())
            {
                bowDrawPercentage = 0;
            }
        }
    }

    /**
    * remove character facing and inaccuracy for projectile stuff
    */
    [HarmonyPatch(typeof(Attack), "FireProjectileBurst")]
    class PatchFireProjectileBurst {
        
        static void Prefix(ref Attack __instance, ref ItemDrop.ItemData ___m_ammoItem, Humanoid ___m_character) {
            if (___m_character != Player.m_localPlayer || !VHVRConfig.UseVrControls()) {
                return;
            }

            __instance.m_useCharacterFacing = false;
            __instance.m_launchAngle = 0;

            if (EquipScript.IsHandThrownWeaponEquipped())
            {
                // The throw speed is handed to vanilla relative to the attack's max projectile speed (see
                // WeaponUtils.GetThrowAimDir), so vanilla must not substitute a random speed for that max.
                __instance.m_randomVelocity = false;
            }

            if (VHVRConfig.RestrictBowDrawSpeed() == "None" || EquipScript.CurrentOffHandEquipType() != EquipType.Bow)
            {
                __instance.m_projectileAccuracyMin = 0;
                if (___m_ammoItem != null)
                {
                    ___m_ammoItem.m_shared.m_attack.m_projectileAccuracyMin = 0;
                }
                return;
            }

            if (!VHVRConfig.BowAccuracyIgnoresDrawLength())
            {
                return;
            }

            float currentSpreadFactor = 1 - Mathf.Sqrt(BowLocalManager.instance.GetAttackPercentage());
            if (currentSpreadFactor <= 0)
            {
                return;
            }

            float desiredSpreadFactor = 1 - Mathf.Sqrt(BowLocalManager.instance.timeBasedChargePercentage);
            float accuracyAdjustment = desiredSpreadFactor / currentSpreadFactor;
            float minSpread = __instance.m_projectileAccuracy;

            // We scale the max spread (i. e. m_projectileAccuracyMin) to compensate for the difference between desiredSpreadFactor and currentSpreadFactor.
            __instance.m_projectileAccuracyMin = Mathf.Lerp(minSpread, __instance.m_projectileAccuracyMin, accuracyAdjustment);
            if (___m_ammoItem != null)
            {
                ___m_ammoItem.m_shared.m_attack.m_projectileAccuracyMin = Mathf.Lerp(___m_ammoItem.m_shared.m_attack.m_projectileAccuracy, ___m_ammoItem.m_shared.m_attack.m_projectileAccuracyMin, accuracyAdjustment);
            }
    }
    }
    
    /**
    * Fix RodTop to get the VR's transform instead of original model's transform
    */
    [HarmonyPatch(typeof(FishingFloat), "GetRodTop")]
    class PatchGetRodTop {
        
        static bool Prefix(ref Transform __result, Character owner) {

            if (owner != Player.m_localPlayer || FishingManager.fixedRodTop == null || !VHVRConfig.UseVrControls()) {
                return true;
            }

            __result = FishingManager.fixedRodTop.transform;
            return false;
        }
    }
    
        
    /**
     * Remove bow pulling animation
     */
    [HarmonyPatch(typeof(Player), "PlayerAttackInput")]
    class PatchPlayerAttackInput {
        private static readonly MethodInfo SetBoolCall =
            AccessTools.Method(typeof(ZSyncAnimation), nameof(ZSyncAnimation.SetBool), new[] { typeof(string), typeof(bool) });

        private static void SetBoolWithoutPulling(ZSyncAnimation animation, string name, bool value)
        {
            animation.SetBool(name, false);
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                yield return VHVRConfig.UseVrControls() && instruction.Calls(SetBoolCall)
                    ? instruction.ReplaceCallWith(typeof(PatchPlayerAttackInput), nameof(SetBoolWithoutPulling))
                    : instruction;
            }
        }
    }

    [HarmonyPatch(typeof(Attack), nameof(Attack.OnAttackTrigger))]
    class Attack_Crossbow_Pushback
    {
        private static float recoilPushback = 0f;
        public static void Prefix(Attack __instance)
        {
            if (__instance.m_character != Player.m_localPlayer || !VHVRConfig.UseVrControls())
            {
                return;
            }
 
            if (__instance.m_recoilPushback <= 0f)
            {
                return;
            }

            if (EquipScript.CurrentOffHandEquipType() == EquipType.Crossbow || EquipScript.IsDundrEquipped())
            {
                recoilPushback = __instance.m_recoilPushback;
                __instance.m_recoilPushback = 0f;
            }
        }

        public static void Postfix(Attack __instance)
        {
            if (__instance.m_character != Player.m_localPlayer || !VHVRConfig.UseVrControls())
            {
                return;
            }

            if (recoilPushback <= 0f)
            {
                return;
            }

            if (EquipScript.CurrentOffHandEquipType() == EquipType.Crossbow)
            {
                __instance.m_character.ApplyPushback(-CrossbowManager.AimDir, recoilPushback);
                recoilPushback = 0f;
            }
            else if (EquipScript.IsDundrEquipped())
            {
                __instance.m_character.ApplyPushback(-ShootingStaffManager.instance.AimDir, recoilPushback);
                recoilPushback = 0f;
            }
        }
    }

    // Vanilla attaches a deployed grappling hook's chain to the left hand bone every Update, when the bone may still be
    // in its animated pose rather than where the VR crossbow is rendered. Attach it to the front of the crossbow as
    // last rendered instead (see CrossbowManager#grapplingChainAttachPoint, which also refreshes it at render time).
    [HarmonyPatch(typeof(GrapplingPoint), "UpdateLinePosition")]
    class GrapplingPoint_UpdateLinePosition_Patch
    {
        static void Postfix(GrapplingPoint __instance, LineRenderer ___m_lineRenderer)
        {
            if (__instance != GrapplingPoint.m_localGrappler || !VHVRConfig.UseVrControls() || ___m_lineRenderer == null)
            {
                return;
            }

            var attachPoint = CrossbowManager.grapplingChainAttachPoint;
            if (attachPoint.HasValue)
            {
                ___m_lineRenderer.SetPosition(1, attachPoint.Value);
            }
        }
    }
}
