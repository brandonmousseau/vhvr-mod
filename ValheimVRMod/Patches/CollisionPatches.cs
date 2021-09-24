using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using ValheimVRMod.Scripts;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Patches {
    
    [HarmonyPatch(typeof(Attack), "GetAttackOrigin")]
    class PatchAreaAttack {

        static bool Prefix(ref Transform __result,  ref Humanoid ___m_character) {
            if (___m_character != Player.m_localPlayer || ! VHVRConfig.UseVrControls()) {
                return true;
            }
             
            __result = StaticObjects.rightWeaponCollider().transform;
            return false;
        }
    }
    
    
    [HarmonyPatch(typeof(Attack), "Start")]
    class PatchAttackStart {

        /**
         * in Start Patch we put some logic from original Start method and some more logic from original DoMeleeAttack
         */
        static bool Prefix(
            Humanoid character,
            CharacterAnimEvent animEvent,
            ItemDrop.ItemData weapon,
            ref Humanoid ___m_character,
            ref CharacterAnimEvent ___m_animEvent,
            ref ItemDrop.ItemData ___m_weapon,
            ref Attack __instance,
            ref EffectList ___m_hitEffect,
            ref Skills.SkillType ___m_specialHitSkill,
            ref DestructibleType ___m_specialHitType,
            bool ___m_lowerDamagePerHit,
            float ___m_forceMultiplier,
            float ___m_staggerMultiplier,
            float ___m_damageMultiplier,
            int ___m_attackChainLevels,
            int ___m_currentAttackCainLevel,
            ref DestructibleType ___m_resetChainIfHit,
            ref int ___m_nextAttackChainLevel,
            ref EffectList ___m_hitTerrainEffect,
            ref float ___m_attackHeight,
            ref float ___m_attackRange,
            ref float ___m_attackOffset,
            float ___m_attackHitNoise,
            GameObject ___m_spawnOnTrigger,
            ref int ___m_attackMask,
            ref int ___m_attackMaskTerrain,
            ref bool __result
        ) {
            // if character is not local player, use original Start method
            if (character != Player.m_localPlayer
                || __instance.m_attackType.ToString() == "Projectile" || !VHVRConfig.UseVrControls()) {
                return true;
            }

            ___m_character = character;
            ___m_animEvent = animEvent;
            ___m_weapon = weapon;
            ___m_attackHeight = 0;
            ___m_attackRange = 0;
            ___m_attackOffset = 0;
            __result = true;
            
            if (___m_attackMask == 0)
            {
                ___m_attackMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", nameof (character), "character_net", "character_ghost", "hitbox", "character_noenv", "vehicle");
                ___m_attackMaskTerrain = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "terrain", nameof (character), "character_net", "character_ghost", "hitbox", "character_noenv", "vehicle");
            }
            
            if (!MeshCooldown.staminaDrained) {
                float staminaUsage = (float) __instance.GetAttackStamina();
                if (staminaUsage > 0.0f && !character.HaveStamina(staminaUsage + 0.1f)) {
                    if (character.IsPlayer())
                        Hud.instance.StaminaBarNoStaminaFlash();
                    __result = false;
                    return false;
                }
            
                character.UseStamina(staminaUsage);
                MeshCooldown.staminaDrained = true;
            }

            Collider col = StaticObjects.lastHitCollider;
            Vector3 pos = StaticObjects.lastHitPoint;
            Vector3 dir = StaticObjects.lastHitDir;

            if (__instance.m_attackType == Attack.AttackType.Area) {
                __instance.OnAttackTrigger();
            }
            
            doMeleeAttack(___m_character, ___m_weapon, __instance, ___m_hitEffect, ___m_specialHitSkill, ___m_specialHitType, ___m_lowerDamagePerHit, ___m_forceMultiplier, ___m_staggerMultiplier, ___m_damageMultiplier, ___m_attackChainLevels, ___m_currentAttackCainLevel, ___m_resetChainIfHit, ref ___m_nextAttackChainLevel, ___m_hitTerrainEffect, ___m_attackHitNoise, pos, col, dir, ___m_spawnOnTrigger);
            return false;
            
        }

        private static void doMeleeAttack(Humanoid ___m_character, ItemDrop.ItemData ___m_weapon, Attack __instance,
            EffectList ___m_hitEffect, Skills.SkillType ___m_specialHitSkill, DestructibleType ___m_specialHitType,
            bool ___m_lowerDamagePerHit, float ___m_forceMultiplier, float ___m_staggerMultiplier, float ___m_damageMultiplier,
            int ___m_attackChainLevels, int ___m_currentAttackCainLevel, DestructibleType ___m_resetChainIfHit,
            ref int ___m_nextAttackChainLevel, EffectList ___m_hitTerrainEffect, float ___m_attackHitNoise, Vector3 pos,
            Collider col, Vector3 dir, GameObject ___m_spawnOnTrigger) {
            
            Vector3 zero = Vector3.zero;
            bool flag2 = false; //rename
            HashSet<Skills.SkillType> skillTypeSet = new HashSet<Skills.SkillType>();
            bool hitOccured = false;

            ___m_weapon.m_shared.m_hitEffect.Create(pos, Quaternion.identity);
            ___m_hitEffect.Create(pos, Quaternion.identity);

            GameObject hitObject = Projectile.FindHitObject(col);

            if (!(hitObject == ___m_character.gameObject)) {
                Vagon component1 = hitObject.GetComponent<Vagon>();
                if (!component1 || !component1.IsAttached(___m_character)) {
                    Character component2 = hitObject.GetComponent<Character>();
                    bool isEnemy = component2 != null ? BaseAI.IsEnemy(___m_character, component2) : false;
                    if ((___m_character.IsPlayer() || isEnemy) &&
                        (___m_weapon.m_shared.m_tamedOnly || !___m_character.IsPlayer() || ___m_character.IsPVPEnabled() || isEnemy) && 
                        (!___m_weapon.m_shared.m_tamedOnly || component2.IsTamed()) &&
                        (!___m_weapon.m_shared.m_dodgeable || !component2.IsDodgeInvincible())) {
                        hitOccured = true;
                    }
                }
            }

            if (!hitOccured) {
                return;
            }

            IDestructible component = hitObject.GetComponent<IDestructible>();

            if (component != null) {
                DestructibleType destructibleType = component.GetDestructibleType();
                Skills.SkillType skill = ___m_weapon.m_shared.m_skillType;

                if (___m_specialHitSkill != Skills.SkillType.None &&
                    (destructibleType & ___m_specialHitType) != DestructibleType.None) {
                    skill = ___m_specialHitSkill;
                }

                float randomSkillFactor = ___m_character.GetRandomSkillFactor(skill);

                if (___m_lowerDamagePerHit) {
                    randomSkillFactor /= 0.75f;
                }

                HitData hitData = new HitData();
                hitData.m_toolTier = ___m_weapon.m_shared.m_toolTier;
                hitData.m_statusEffect = ___m_weapon.m_shared.m_attackStatusEffect
                    ? ___m_weapon.m_shared.m_attackStatusEffect.name
                    : "";
                hitData.m_pushForce = ___m_weapon.m_shared.m_attackForce * randomSkillFactor * ___m_forceMultiplier;
                hitData.m_backstabBonus = ___m_weapon.m_shared.m_backstabBonus;
                hitData.m_staggerMultiplier = ___m_staggerMultiplier;
                hitData.m_dodgeable = ___m_weapon.m_shared.m_dodgeable;
                hitData.m_blockable = ___m_weapon.m_shared.m_blockable;
                hitData.m_skill = skill;
                hitData.m_damage = ___m_weapon.GetDamage();
                hitData.m_point = pos;
                hitData.m_dir = dir;
                hitData.m_hitCollider = col;
                hitData.SetAttacker(___m_character);
                hitData.m_damage.Modify(___m_damageMultiplier);
                hitData.m_damage.Modify(randomSkillFactor);
                hitData.m_damage.Modify(__instance.GetLevelDamageFactor());
                if (___m_attackChainLevels > 1 && ___m_currentAttackCainLevel == ___m_attackChainLevels - 1) {
                    hitData.m_damage.Modify(2f);
                    hitData.m_pushForce *= 1.2f;
                }
                hitData.m_damage.Modify(MeshCooldown.calcDamageMultiplier());

                ___m_character.GetSEMan().ModifyAttack(skill, ref hitData);
                if (component is Character)
                    flag2 = true;
                component.Damage(hitData);
                if ((destructibleType & ___m_resetChainIfHit) != DestructibleType.None)
                    ___m_nextAttackChainLevel = 0;
                skillTypeSet.Add(skill);
            }

            ___m_weapon.m_shared.m_hitTerrainEffect.Create(pos,
                Quaternion.identity); // Quaternion.identity might need to be replaced
            ___m_hitTerrainEffect.Create(pos, Quaternion.identity);

            if (___m_weapon.m_shared.m_spawnOnHitTerrain) {
                __instance.SpawnOnHitTerrain(pos, ___m_weapon.m_shared.m_spawnOnHitTerrain);
            }

            if (___m_weapon.m_shared.m_useDurability && ___m_character.IsPlayer())
                ___m_weapon.m_durability -= ___m_weapon.m_shared.m_useDurabilityDrain;
            ___m_character.AddNoise(___m_attackHitNoise);

            if (___m_weapon.m_shared.m_spawnOnHit)
                Object.Instantiate(___m_weapon.m_shared.m_spawnOnHit, pos,
                        Quaternion.identity).GetComponent<IProjectile>()
                    ?.Setup(___m_character, zero, ___m_attackHitNoise, null, ___m_weapon);
            foreach (Skills.SkillType skill in skillTypeSet)
                ___m_character.RaiseSkill(skill, flag2 ? 1.5f : 1f);

            if (!___m_spawnOnTrigger)
                return;
            Object.Instantiate(___m_spawnOnTrigger, zero,
                Quaternion.identity).GetComponent<IProjectile>()?.Setup(___m_character,
                ___m_character.transform.forward, -1f, null, ___m_weapon);

            return;
        }
    }
}