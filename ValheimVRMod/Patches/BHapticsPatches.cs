using System;
using System.Linq;
using HarmonyLib;
using ValheimVRMod.Utilities;
using ValheimVRMod.Scripts;
using UnityEngine;
using System.Threading;
using System.Collections.Generic;

namespace ValheimVRMod.Patches
{
    /**
     * When player is eating food
     */
    [HarmonyPatch(typeof(Player), "EatFood")]
    class Player_EatingFood_Patch
    {
        public static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer || BhapticsTactsuit.suitDisabled)
            {
                return;
            }
            BhapticsTactsuit.PlaybackHaptics("Eating");
        }
    }

    /**
     * When a status Effect starts, creates and starts thread
     */
    [HarmonyPatch(typeof(StatusEffect), "TriggerStartEffects")]
    class StatusEffect_Start_Patch
    {
        public static void Postfix(StatusEffect __instance)
        {
            if (BhapticsTactsuit.suitDisabled || __instance.m_character != Player.m_localPlayer)
            {
                return;
            }
            string EffectName = "";
            switch (__instance.m_name)
            {
                case "$se_puke_name":
                    EffectName = "Vomit";
                    break;
                case "$se_poison_name":
                    EffectName = "Poison";
                    break;
                case "$se_burning_name":
                    EffectName = "Flame";
                    break;
                case "$se_freezing_name":
                    EffectName = "Freezing";
                    break;
            }
            if (EffectName != "")
            {
                BhapticsTactsuit.StartThreadHaptic(EffectName);
            }
        }
    }

    /**
     * When a statusEffect stops, stops thread corresponding to effect name
     */
    [HarmonyPatch(typeof(StatusEffect), "Stop")]
    class StatusEffect_Stop_Patch
    {
        public static void Postfix(StatusEffect __instance)
        {
            if (BhapticsTactsuit.suitDisabled || __instance.m_character != Player.m_localPlayer)
            {
                return;
            }
            string name = "";
            switch (__instance.m_name)
            {
                case "$se_puke_name":
                    name = "Vomit";
                    break;
                case "$se_poison_name":
                    name = "Poison";
                    break;
                case "$se_burning_name":
                    name = "Flame";
                    break;
                case "$se_freezing_name":
                    name = "Freezing";
                    break;
            }
            if (name != "")
            {
                BhapticsTactsuit.StopThreadHaptic(name);
            }
        }
    }

    /**
     * When any player is using guardian power
     */
    [HarmonyPatch(typeof(Player), "ActivateGuardianPower")]
    class Player_GuardianPower_Patch
    {
        public static void Postfix(Player __instance)
        {
            if (BhapticsTactsuit.suitDisabled)
            {
                return;
            }

            if (Player.IsPlayerInRange(__instance.transform.position, 10f, Player.m_localPlayer.GetPlayerID()))
            {
                BhapticsTactsuit.PlaybackHaptics("SuperPower");
            }
        }
    }
    /**
    * on arrow release
    */
    [HarmonyPatch(typeof(Attack), nameof(Attack.OnAttackTrigger))]
    class Attack_ArrowThrowing_Patch
    {
        public static void Postfix(Attack __instance)
        {
            if (__instance.m_character != Player.m_localPlayer || BhapticsTactsuit.suitDisabled)
            {
                return;
            }
            if (EquipScript.getLeft() == EquipType.Bow)
            {
                BhapticsTactsuit.PlaybackHaptics(VHVRConfig.LeftHanded() ? "ArrowThrowLeft" : "ArrowThrowRight", 2.0f); 
                // arms tactosy
                BhapticsTactsuit.PlaybackHaptics(VHVRConfig.LeftHanded() ? "Recoil_L" : "Recoil_R", 2.0f);
            }
        }
    }

    /**
    * on bow string pull => BowManager.cs l.278 
    */

    /**
    * on bow string stop => BowLocalManager.cs l.106
    */

    /**
    * on getting arrow from your back => BowLocalManager.cs l.248
    */

    /**
     * Player low Health
     */
    [HarmonyPatch(typeof(Character), "SetHealth")]
    class Character_LowHealth_Patch
    {
        public static void Postfix(Character __instance)
        {
            if (__instance != Player.m_localPlayer || BhapticsTactsuit.suitDisabled)
            {
                return;
            }
            int hlth = Convert.ToInt32(__instance.GetHealth() * 100 / __instance.GetMaxHealth());
            if (hlth < 20 && hlth > 15)
            {
                BhapticsTactsuit.StopThreadHaptic("HeartBeatFast");
                BhapticsTactsuit.StartThreadHaptic("HeartBeat");
            }
            else if (hlth <= 15 && hlth > 0)
            {
                BhapticsTactsuit.StopThreadHaptic("HeartBeat");
                BhapticsTactsuit.StartThreadHaptic("HeartBeatFast", 1.0f, false, 0);
            }
            else if (hlth <= 0)
            {
                BhapticsTactsuit.StopThreadHaptic("HeartBeat");
                BhapticsTactsuit.StopThreadHaptic("HeartBeatFast");
            }
            else
            {
                BhapticsTactsuit.StopThreadHaptic("HeartBeat");
                BhapticsTactsuit.StopThreadHaptic("HeartBeatFast");
            }
        }
    }

    /**
     * On blocking with shield
     */
    [HarmonyPatch(typeof(Humanoid), "BlockAttack")]
    class Humanoid_BlockAttack_Patch
    {
        public static void Postfix(Humanoid __instance, bool __result)
        {

            if (BhapticsTactsuit.suitDisabled || __instance != Player.m_localPlayer || EquipScript.getLeft() != EquipType.Shield || !VHVRConfig.UseVrControls())
            {
                return;
            }
            if (__result)
            {
                BhapticsTactsuit.PlaybackHaptics(VHVRConfig.LeftHanded() ?
                    "BlockVest_R" : "BlockVest_L");
                // arms tactosy
                BhapticsTactsuit.PlaybackHaptics(VHVRConfig.LeftHanded() ?
                    "Block_R" : "Block_L");
            }
        }
    }

    /**
     * OnDeath player, kill all haptics threads just in case
     */
    [HarmonyPatch(typeof(Player), "OnDeath")]
    class Player_OnDeath_Patch
    {
        public static void Postfix(Player __instance)
        {

            if (__instance != Player.m_localPlayer || BhapticsTactsuit.suitDisabled)
            {
                return;
            }
            BhapticsTactsuit.PlaybackHaptics("Death");
            BhapticsTactsuit.StopAllHapticFeedback(new string[] {"Death"});
        }
    }

    /**
     * When damage applied to player
     */
    [HarmonyPatch(typeof(Character), "ApplyDamage")]
    class Character_ApplyDamage_Patch
    {
        public static void Postfix(Character __instance, HitData hit)
        {

            if (__instance != Player.m_localPlayer || BhapticsTactsuit.suitDisabled)
            {
                return;
            }
            var coords = BhapticsTactsuit.getAngleAndShift(Player.m_localPlayer, hit.m_point);
            BhapticsTactsuit.PlayBackHit("Impact", coords.angle, coords.shift);
        }
    }

    /**
     * OnTriggerEnter Attack succeded against any object with any weapon
     * WeaponCollision.cs l.103
     */

    /**
     * On starting teleporting
     */
    [HarmonyPatch(typeof(Player), "TeleportTo")]
    class Player_TeleportTo_Patch
    {
        public static void Postfix(Player __instance, bool __result)
        {
            if (__instance != Player.m_localPlayer || BhapticsTactsuit.suitDisabled || !__result)
            {
                return;
            }
            if (__instance.m_teleporting)
            {
                BhapticsTactsuit.PlaybackHaptics("PassPortalFront");
                BhapticsTactsuit.PlaybackHaptics("PassPortalTactosy");
                //wait for this effect to finish before starting teleport effect, last param of this method
                BhapticsTactsuit.StartThreadHaptic("Teleporting", 1.0f, false, 5000, 1.0f, 1000);
            }
        }
    }
    /**
     * On ending teleporting
     */
    [HarmonyPatch(typeof(Player), "UpdateTeleport")]
    class Player_UpdateTeleport_Patch
    {
        public static void Postfix(Player __instance, bool __state)
        {
            if (__instance != Player.m_localPlayer || BhapticsTactsuit.suitDisabled)
            {
                return;
            }
            if (!__instance.m_teleporting)
            {
                BhapticsTactsuit.StopThreadHaptic("Teleporting", new string[] {"PassPortalBack", "PassPortalTactosy"});
            }
        }
    }
    /**
     * On getting close to any portal
     */
    [HarmonyPatch(typeof(TeleportWorld), "UpdatePortal")]
    class TeleportWorld_UpdatePortal_Patch
    {
        private static bool closeTo = false;
        private static TeleportWorld myPortal;
        public static void Postfix(TeleportWorld __instance)
        {
            if (BhapticsTactsuit.suitDisabled)
            {
                return;
            }
            // am I in range of any active portal, not necessarely the closest
            if (__instance && Player.m_localPlayer 
                && !Player.m_localPlayer.IsTeleporting() && __instance.m_proximityRoot)
            {
                float myDistance = Vector3.Distance(Player.m_localPlayer.transform.position, __instance.m_proximityRoot.position);
                closeTo = (myDistance < (double)__instance.m_activationRange) && __instance.m_target_found.m_active;
                if(closeTo)
                {
                    myPortal = __instance;
                }
                if (closeTo && !Player.m_localPlayer.IsTeleporting())
                {
                    BhapticsTactsuit.StartThreadHaptic("ApproachPortal");
                }
                if (!closeTo && myPortal && myPortal == __instance)
                {
                    BhapticsTactsuit.StopThreadHaptic("ApproachPortal");
                    myPortal = null;
                }
            } else
            {
                BhapticsTactsuit.StopThreadHaptic("ApproachPortal");
                myPortal = null;
            }
        }
    }
    /**
     * When thunder storms
     */
    [HarmonyPatch(typeof(Thunder), "DoThunder")]
    class Thunder_DoThunder_Patch
    {
        public static void Postfix()
        {
            if (BhapticsTactsuit.suitDisabled)
            {
                return;
            }
            BhapticsTactsuit.PlaybackHaptics("Thunder", 0.8f);
        }
    }
    /**
     * When petting tamed character
     */
    [HarmonyPatch(typeof(Tameable), "Interact")]
    class Tameable_Interact_Patch
    {
        public static void Postfix(bool __result, bool alt)
        {
            if (BhapticsTactsuit.suitDisabled)
            {
                return;
            }
            if (__result && !alt)
            {
                BhapticsTactsuit.PlaybackHaptics("Petting");
                //tactosy
                BhapticsTactsuit.PlaybackHaptics(VHVRConfig.LeftHanded() ? "Petting_L" : "Petting_R");
            }
        }
    }

    public class Riding
    {
        public static void speedActive(Sadle.Speed speed, bool swimming = false)
        {
            switch (speed)
            {
                case Sadle.Speed.Stop:
                    BhapticsTactsuit.StopThreadHaptic("RideHorse");
                    BhapticsTactsuit.StopThreadHaptic("RideHorseSlow");
                    break;
                case Sadle.Speed.Walk:
                    BhapticsTactsuit.StartThreadHaptic("RideHorseSlow", (swimming) ? 0.4f : 1.0f, false, 3500);
                    BhapticsTactsuit.StopThreadHaptic("RideHorse");
                    break;
                case Sadle.Speed.Turn:
                    BhapticsTactsuit.StartThreadHaptic("RideHorseSlow", (swimming) ? 0.4f : 1.0f, false, 3500);
                    BhapticsTactsuit.StopThreadHaptic("RideHorse");
                    break;
                case Sadle.Speed.Run:
                    BhapticsTactsuit.StartThreadHaptic("RideHorse", (swimming) ? 0.4f : 1.0f, false, 3500);
                    BhapticsTactsuit.StopThreadHaptic("RideHorseSlow");
                    break;
                case Sadle.Speed.NoChange:
                    break;
                default:
                    BhapticsTactsuit.StopThreadHaptic("RideHorse");
                    BhapticsTactsuit.StopThreadHaptic("RideHorseSlow");
                    break;
            }
        }
        /*
         * When riding using controls
         */
        [HarmonyPatch(typeof(Sadle), "FixedUpdate")]
        class Sadle_FixedUpdate_Patch
        {
            private static bool attackStarted = false;
            public static void Postfix(Sadle __instance)
            {
                if (!__instance.IsLocalUser() || BhapticsTactsuit.suitDisabled)
                {
                    return;
                }
                if (__instance.m_tambable.HaveRider())
                {
                    // moving
                    Sadle.Speed speed = Sadle.Speed.NoChange;
                    if (__instance.m_speed == Sadle.Speed.Stop) {
                        if (__instance.m_character.IsRunning())
                        {
                            speed = Sadle.Speed.Run;
                        } else if (__instance.m_character.IsWalking())
                        {
                            speed = Sadle.Speed.Walk;
                        }
                    }
                    speedActive((speed == Sadle.Speed.NoChange) ? __instance.m_speed : speed,
                        __instance.m_character.IsSwimming());

                    //attacking
                    if (__instance.m_character.InAttack())
                    {
                        Humanoid hum = __instance.m_character as Humanoid;
                        if ( !attackStarted && hum.m_currentAttack.m_wasInAttack )
                        {
                            attackStarted = true;
                            if (hum.m_currentAttack.m_attackType == Attack.AttackType.Area)
                            {
                                BhapticsTactsuit.PlaybackHaptics("LoxGroundAttackTactosy");
                                BhapticsTactsuit.PlaybackHaptics("LoxGroundAttack");
                            } else
                            {
                                BhapticsTactsuit.PlaybackHaptics("LoxAttackTactosy");
                                BhapticsTactsuit.PlaybackHaptics("LoxAttack");
                            }
                        }
                    } else
                    {
                        attackStarted = false;
                    }
                }

            }
        }
        /**
         * When releasing sadle controls
         */
        [HarmonyPatch(typeof(Sadle), "OnUseStop")]
        class Sadle_OnUseStop_Patch
        {
            public static void Postfix(Sadle __instance, Player player)
            {
                if (player != Player.m_localPlayer || BhapticsTactsuit.suitDisabled)
                {
                    return;
                }
                speedActive(Sadle.Speed.Stop);
            }
        }
    }

    /**
     * When ship gets damaged
     */
    [HarmonyPatch(typeof(WearNTear), "Damage")]
    class WearNTear_Damage_Patch
    {
        public static void Postfix(WearNTear __instance)
        {
            if (BhapticsTactsuit.suitDisabled)
            {
                return;
            }
            // only if it is a ship and the player is onboard
            Ship component = __instance.GetComponent<Ship>();
            if (component != null && component.IsPlayerInBoat(Player.m_localPlayer))
            {
                BhapticsTactsuit.PlaybackHaptics("ShipDamage");
            }
        }
    }

    /**
     * When repairing stuff
     */
    [HarmonyPatch(typeof(WearNTear), "Repair")]
    class WearNTear_Repair_Patch
    {
        public static void Postfix(WearNTear __instance, bool __result)
        {
            if (BhapticsTactsuit.suitDisabled)
            {
                return;
            }
            // only if it is local player
            Piece component = __instance.GetComponent<Piece>();
            if (__result && component != null && component == Player.m_localPlayer.GetHoveringPiece())
            {
                BhapticsTactsuit.PlaybackHaptics((VHVRConfig.LeftHanded()) ? "Hammer_L" : "Hammer_R");
                BhapticsTactsuit.PlaybackHaptics((VHVRConfig.LeftHanded()) ? "HammerTactosy_L" : "HammerTactosy_R");
            }
        }
    }
    /**
     * When placing object with hammer
     */
    [HarmonyPatch(typeof(WearNTear), "OnPlaced")]
    class WearNTear_OnPlaced_Patch
    {
        public static void Postfix(WearNTear __instance)
        {
            if (BhapticsTactsuit.suitDisabled)
            {
                return;
            }
            // only if it is local player
            Piece component = __instance.GetComponent<Piece>();
            if (component != null && component.IsCreator())
            {
                BhapticsTactsuit.PlaybackHaptics((VHVRConfig.LeftHanded()) ? "Hammer_L" : "Hammer_R");
                BhapticsTactsuit.PlaybackHaptics((VHVRConfig.LeftHanded()) ? "HammerTactosy_L" : "HammerTactosy_R");
            }
        }
    }
    
    /**
     * When it is raining and not in shelter
     * Delayed when starting
     */
    [HarmonyPatch(typeof(EnvMan), "SetEnv")]
    class EnvMan_SetEnv_Patch
    {
        private static readonly string[] rain = { "ThunderStorm", "Rain" };
        private static string currentEnv = "";
        private static int envStarted = 0;
        //when env is changing, there is a delay between effective change of env in the code,
        //and rain actually showing on screen
        private static int envDelay = 12;
        public static void Postfix(EnvSetup ___m_currentEnv)
        {
            if (BhapticsTactsuit.suitDisabled || !Player.m_localPlayer)
            {
                return;
            }
            // is it raining ?
            //Env just been changed
            if (currentEnv != ___m_currentEnv.m_name)
            {
                currentEnv = ___m_currentEnv.m_name;
                envStarted = (int)new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
                if (rain.Contains(currentEnv)) 
                {
                    BhapticsTactsuit.StartThreadHaptic("Raining", 0.7f, false, 3000, 1.0f, 12000);
                }
                else
                {
                    BhapticsTactsuit.StopThreadHapticDelayed("Raining", 12000);
                }
            }
            if (rain.Contains(currentEnv))
            {
                //are we in shelter
                if (Player.m_localPlayer.InShelter())
                {
                    BhapticsTactsuit.StopThreadHaptic("Raining");
                }
                else
                {
                    //are we in env rain starting delay ?
                    int startDelay = (int)new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds() - envStarted;
                    if (startDelay > envDelay)
                    {
                        BhapticsTactsuit.StartThreadHaptic("Raining", 0.7f, false, 3000);
                    }
                    else
                    {
                        BhapticsTactsuit.StartThreadHaptic("Raining", 0.7f, false, 3000, envDelay - startDelay);
                    }
                }
            }
        }
    }
    
    /*
     * Looking for a specific effect to cover local and remote
     * effects triggered by players
     * Based on visual effect triggered not code logic prior to it
     */
   [HarmonyPatch(typeof(EffectList), "Create")]
    class OfferingBowl_SpawnBoss_Patch
    {
        public static void Postfix(Array __result)
        {
            if (BhapticsTactsuit.suitDisabled || __result == null || __result.Length == 0)
            {
                return;
            }
            foreach (GameObject obj in __result)
            {
                switch (obj.name)
                {
                    case "vfx_prespawn(Clone)":
                        Thread EffectThread = new Thread(() =>
                        {
                            BhapticsTactsuit.StartThreadHaptic("BossSummon");
                            Thread.Sleep(5000);
                            BhapticsTactsuit.StartThreadHaptic("BossSummon", 0.4f);
                            Thread.Sleep(7000);
                            BhapticsTactsuit.StopThreadHaptic("BossSummon", new string[] { "BossAppearance" });
                        });
                        EffectThread.Start();
                        break;
                    case "vfx_corpse_destruction_medium(Clone)":
                        bool closeTo = (Vector3.Distance(Player.m_localPlayer.transform.position, obj.transform.position) < (double)20f);
                        if (closeTo)
                            BhapticsTactsuit.PlaybackHaptics("Explosion");
                        break;
                }
            }
        }
    }
    
    /**
     * When selecting boss power at the altars 
     */
    [HarmonyPatch(typeof(Player), "SetGuardianPower")]
    class Player_SetGuardianPower_Patch
    {
        public static void Postfix(Player __instance)
        {
            if (BhapticsTactsuit.suitDisabled || 
                Player.m_localPlayer != __instance || __instance.m_isLoading)
            {
                return;
            }
            BhapticsTactsuit.PlaybackHaptics("ActivateGuardianPower");
        }
    }
    /**
     * Boss animations / attacks
     */
    [HarmonyPatch(typeof(Attack), nameof(Attack.OnAttackTrigger))]
    class Attack_OnAttackTrigger_Patch
    {
        public static Dictionary<string, float> rangeBoss = new Dictionary<string, float>()
        {
            {"boss_eikthyr",  20f},
            {"boss_gdking", 40f },
            {"boss_bonemass",  20f},
            {"boss_moder",  40f},
            {"boss_goblinking",  60f},
        };
        public static void Postfix(Attack __instance)
        {
            if (BhapticsTactsuit.suitDisabled || !__instance.m_character.IsBoss())
            {
                return;
            }
            //range limit
            float range = (rangeBoss.ContainsKey(__instance.m_character.m_bossEvent)) ? rangeBoss[__instance.m_character.m_bossEvent] : 20f;
            bool closeTo = (Vector3.Distance(Player.m_localPlayer.transform.position,
                __instance.m_character.transform.position) <
                (double)range);
            if (!closeTo)
            {
                return;
            }
            //only for special attacks, all others are managed if you get hit
            switch(__instance.m_character.m_bossEvent)
            {
                case "boss_eikthyr":
                    if (__instance.m_attackAnimation == "attack2")
                    {
                        BhapticsTactsuit.PlaybackHaptics("EikthyrElectric");
                    }
                    if (__instance.m_attackAnimation == "attack_stomp")
                    {
                        BhapticsTactsuit.PlaybackHaptics("EikthyrElectric");
                    }
                    break;
                case "boss_gdking":
                    if (__instance.m_attackAnimation == "spawn")
                    {
                        BhapticsTactsuit.PlaybackHaptics("ElderSpawn");
                    }
                    if (__instance.m_attackAnimation == "stomp")
                    {
                        BhapticsTactsuit.PlaybackHaptics("ElderStomp");
                    }
                    if (__instance.m_attackAnimation == "shoot")
                    {
                        BhapticsTactsuit.PlaybackHaptics("ElderShoot");
                    }
                    break;
                case "boss_bonemass":
                    if (__instance.m_attackAnimation == "aoe")
                    {
                        BhapticsTactsuit.PlaybackHaptics("Bonemass1");
                    }
                    break;
                case "boss_moder":
                    if (__instance.m_attackAnimation == "attack_iceball")
                    {
                        BhapticsTactsuit.PlaybackHaptics("ModerIceBalls");
                    }
                    if (__instance.m_attackAnimation == "attack_breath")
                    {
                        BhapticsTactsuit.PlaybackHaptics("ModerBeam");
                    }
                    break;
                case "boss_goblinking":
                    if (__instance.m_attackAnimation == "beam")
                    {
                        BhapticsTactsuit.PlaybackHaptics("YagluthBeam");
                    }
                    if (__instance.m_attackAnimation == "nova")
                    {
                        BhapticsTactsuit.PlaybackHaptics("YagluthNova");
                    }
                    if (__instance.m_attackAnimation == "cast1")
                    {
                        BhapticsTactsuit.PlaybackHaptics("YagluthMeteor");
                    }
                    break;
            }
        }
    }
}
