using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using ValheimVRMod.Scripts;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Patches {
    
    [HarmonyPatch(typeof(Attack), nameof(Attack.GetAttackOrigin))]
    class PatchAreaAttack {

        static bool Prefix(ref Transform __result,  ref Humanoid ___m_character) {
            if (___m_character != Player.m_localPlayer || !VHVRConfig.UseVrControls()) {
                return true;
            }
             
            __result = StaticObjects.rightWeaponCollider().transform;
            return false;
        }
    }
    
    
    [HarmonyPatch(typeof(Attack), nameof(Attack.Start))]
    class PatchAttackStart {

        /**
         * in Start Patch we put some logic from original Start method and some more logic from original DoMeleeAttack
         */

        private static float attackHeight ;
        private static float attackRange ;
        private static float attackOffset ;
        static bool Prefix(
            Humanoid character,
            CharacterAnimEvent animEvent,
            ItemDrop.ItemData weapon,
            ref Humanoid ___m_character,
            ref CharacterAnimEvent ___m_animEvent,
            ref ItemDrop.ItemData ___m_weapon,
            ref ItemDrop.ItemData ___m_ammoItem,
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
            if (character != Player.m_localPlayer || !VHVRConfig.UseVrControls() 
                                                  || __instance.m_attackType.ToString() == "Projectile" 
                                                  || EquipScript.getRight() == EquipType.Tankard) {
                return true;
            }

            ___m_character = character;
            ___m_animEvent = animEvent;
            ___m_weapon = weapon;
            attackHeight = ___m_attackHeight;
            attackRange = ___m_attackRange;
            attackOffset = ___m_attackOffset;
            ___m_attackHeight = 0;
            ___m_attackRange = 0;
            ___m_attackOffset = 0;
            __result = true;
            
            if (___m_attackMask == 0)
            {
                ___m_attackMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", nameof (character), "character_net", "character_ghost", "hitbox", "character_noenv", "vehicle");
                ___m_attackMaskTerrain = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "terrain", nameof (character), "character_net", "character_ghost", "hitbox", "character_noenv", "vehicle");
            }
            
            if (!AttackTargetMeshCooldown.staminaDrained) {
                float staminaUsage = (float) __instance.GetAttackStamina();
                if (staminaUsage > 0.0f && !character.HaveStamina(staminaUsage + 0.1f)) {
                    // FIXME: Mystlands probably changed this from StaminaBarNoStaminaFlash
                    if (character.IsPlayer())
                        Hud.instance.StaminaBarEmptyFlash();
                    __result = false;
                    ___m_attackHeight = attackHeight;
                    ___m_attackRange = attackRange;
                    ___m_attackOffset = attackOffset;
                    return false;
                }
            
                character.UseStamina(staminaUsage);
                AttackTargetMeshCooldown.staminaDrained = true;
            }

            Collider col = StaticObjects.lastHitCollider;
            Vector3 pos = StaticObjects.lastHitPoint;
            Vector3 dir = StaticObjects.lastHitDir;

            if (__instance.m_attackType == Attack.AttackType.Area) {
                __instance.OnAttackTrigger();
            }

            if (col == null || pos == null || dir == null)
            {
                return false;
            }
            
            doMeleeAttack(___m_character, ___m_weapon, ___m_ammoItem, __instance, ___m_hitEffect, ___m_specialHitSkill, ___m_specialHitType, ___m_lowerDamagePerHit, ___m_forceMultiplier, ___m_staggerMultiplier, ___m_damageMultiplier, ___m_attackChainLevels, ___m_currentAttackCainLevel, ___m_resetChainIfHit, ref ___m_nextAttackChainLevel, ___m_hitTerrainEffect, ___m_attackHitNoise, pos, col, dir, ___m_spawnOnTrigger);

            ___m_attackHeight = attackHeight;
            ___m_attackRange = attackRange;
            ___m_attackOffset = attackOffset;

            return false;
            
        }

        private static void doMeleeAttack(Humanoid ___m_character, ItemDrop.ItemData ___m_weapon, ItemDrop.ItemData ___m_ammoItem, Attack __instance,
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
                    
                    Character character = hitObject.GetComponent<Character>();
                    
                    if (character == null) {
                        hitOccured = !___m_weapon.m_shared.m_tamedOnly;
                    } else if ((___m_character.IsPlayer() ||  BaseAI.IsEnemy(___m_character, character)) &&
                               (___m_weapon.m_shared.m_tamedOnly || !___m_character.IsPlayer() || ___m_character.IsPVPEnabled() || BaseAI.IsEnemy(___m_character, character)) &&
                               (!___m_weapon.m_shared.m_tamedOnly || character.IsTamed()) &&
                               (!___m_weapon.m_shared.m_dodgeable || !character.IsDodgeInvincible())) {
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
                    
                    if(ButtonSecondaryAttackManager.isSecondaryAttackStarted && ButtonSecondaryAttackManager.secondaryHitList.Count >= 1)
                    {
                        randomSkillFactor /= (ButtonSecondaryAttackManager.secondaryHitList.Count - ButtonSecondaryAttackManager.terrainHitCount) * 0.75f;
                    }
                    else
                    {
                        randomSkillFactor /= 0.75f;
                    }
                }

                HitData hitData = new HitData();
                hitData.m_toolTier = (short) ___m_weapon.m_shared.m_toolTier;
                hitData.m_statusEffectHash = ___m_weapon.m_shared.m_attackStatusEffect
                    ? ___m_weapon.m_shared.m_attackStatusEffect.NameHash()
                    : 0;
                hitData.m_pushForce = ___m_weapon.m_shared.m_attackForce * randomSkillFactor * ___m_forceMultiplier;
                hitData.m_backstabBonus = ___m_weapon.m_shared.m_backstabBonus;
                hitData.m_staggerMultiplier = ___m_staggerMultiplier;
                hitData.m_dodgeable = ___m_weapon.m_shared.m_dodgeable;
                hitData.m_blockable = ___m_weapon.m_shared.m_blockable;
                hitData.m_skill = skill;
                hitData.m_damage = ___m_weapon.GetDamage();
                hitData.m_point = pos;
                hitData.m_dir = ButtonSecondaryAttackManager.hitDir == Vector3.zero ? (pos - Player.m_localPlayer.transform.position).normalized : ButtonSecondaryAttackManager.hitDir;
                hitData.m_hitCollider = col;
                hitData.SetAttacker(___m_character);
                hitData.m_damage.Modify(___m_damageMultiplier);
                hitData.m_damage.Modify(randomSkillFactor);
                hitData.m_damage.Modify(__instance.GetLevelDamageFactor());
                if (___m_attackChainLevels > 1 && ___m_currentAttackCainLevel == ___m_attackChainLevels - 1) {
                    hitData.m_damage.Modify(2f);
                    hitData.m_pushForce *= 1.2f;
                }
                if (___m_lowerDamagePerHit && !ButtonSecondaryAttackManager.isSecondaryAttackStarted)
                {
                    hitData.m_damage.Modify(AttackTargetMeshCooldown.calcDamageMultiplier());
                }

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

            if (___m_weapon.m_shared.m_spawnOnHitTerrain && WeaponCollision.isLastHitOnTerrain) {
                __instance.SpawnOnHitTerrain(pos, ___m_weapon.m_shared.m_spawnOnHitTerrain);
                WeaponCollision.isLastHitOnTerrain = false;
            }

            if (___m_weapon.m_shared.m_useDurability && ___m_character.IsPlayer() && !AttackTargetMeshCooldown.durabilityDrained)
            {
                ___m_weapon.m_durability -= ___m_weapon.m_shared.m_useDurabilityDrain;
                AttackTargetMeshCooldown.durabilityDrained = true;
            }
            ___m_character.AddNoise(___m_attackHitNoise);

            // FIXME: Setup now takes in input an additional ammo parameter, look into this
            if (___m_weapon.m_shared.m_spawnOnHit)
                Object.Instantiate(___m_weapon.m_shared.m_spawnOnHit, pos,
                        Quaternion.identity).GetComponent<IProjectile>()
                    ?.Setup(___m_character, zero, ___m_attackHitNoise, null, ___m_weapon, ___m_ammoItem);
            foreach (Skills.SkillType skill in skillTypeSet)
                ___m_character.RaiseSkill(skill, flag2 ? 1.5f : 1f);

            if (!___m_spawnOnTrigger)
                return;
            // FIXME: Setup now takes in input an additional ammo parameter, look into this
            Object.Instantiate(___m_spawnOnTrigger, zero,
                Quaternion.identity).GetComponent<IProjectile>()?.Setup(___m_character,
                ___m_character.transform.forward, -1f, null, ___m_weapon, ___m_ammoItem);

            return;
        }
    }

    [HarmonyPatch(typeof(Humanoid),nameof(Humanoid.GetAttackSpeedFactorMovement))]
    class Patch_AttackSpeedFactorMovement
    {
        static void Postfix(Humanoid __instance,ref float __result)
        {
            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls())
            {
                return ;
            }
            if (ButtonSecondaryAttackManager.isSecondaryAttackStarted)
                __result *= 0.2F;
            return;
        }
    }

}
