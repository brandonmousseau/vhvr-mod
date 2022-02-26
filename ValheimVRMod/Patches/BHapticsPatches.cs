using System;
using System.Linq;
using HarmonyLib;
using ValheimVRMod.Utilities;
using ValheimVRMod.Scripts;
using UnityEngine;
using System.Threading;
using System.Collections.Generic;

using static ValheimVRMod.Utilities.LogUtils;

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
            if (__instance != Player.m_localPlayer || TactsuitVR.suitDisabled)
            {
                return;
            }
            TactsuitVR.PlaybackHaptics("Eating");
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
            if (TactsuitVR.suitDisabled || __instance.m_character != Player.m_localPlayer)
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
                TactsuitVR.StartThreadHaptic(EffectName);
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
            if (TactsuitVR.suitDisabled || __instance.m_character != Player.m_localPlayer)
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
                TactsuitVR.StopThreadHaptic(name);
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
            if (TactsuitVR.suitDisabled)
            {
                return;
            }

            List<Player> list = new List<Player>();
            Player.GetPlayersInRange( __instance.transform.position, 10f, list);
            foreach (Player item in list)
            {
                if (item == Player.m_localPlayer)
                {
                    TactsuitVR.PlaybackHaptics("SuperPower");
                }
            }
        }
    }
    /**
    * on arrow release
    */
    [HarmonyPatch(typeof(Attack), "OnAttackTrigger")]
    class Attack_ArrowThrowing_Patch
    {
        public static void Postfix(Attack __instance)
        {
            if (__instance.m_character != Player.m_localPlayer || TactsuitVR.suitDisabled)
            {
                return;
            }
            if (EquipScript.getLeft() == EquipType.Bow)
            {
                TactsuitVR.PlaybackHaptics(VHVRConfig.LeftHanded() ? "ArrowThrowLeft" : "ArrowThrowRight", 2.0f); 
                // arms tactosy
                TactsuitVR.PlaybackHaptics(VHVRConfig.LeftHanded() ? "Recoil_L" : "Recoil_R", 2.0f);
            }
        }
    }
    /**
    * on bow string pull
    */
    [HarmonyPatch(typeof(BowManager), "pullString")]
    class BowManager_pullString_Patch
    {
        public static void Postfix(float ___realLifePullPercentage)
        {
            if (TactsuitVR.suitDisabled)
            {
                return;
            }
            if (___realLifePullPercentage == 0)
            {
                return;
            }
            TactsuitVR.StartThreadHaptic(VHVRConfig.LeftHanded() ? "BowStringLeft" : "BowStringRight",
                ___realLifePullPercentage * 1.5f, true);
            // ARMS TACTOSY
            TactsuitVR.StartThreadHaptic(VHVRConfig.LeftHanded() ? "Recoil_L" : "Recoil_R",
                ___realLifePullPercentage * 1.5f, true);
        }
    }
    /**
    * on bow string stop
    */
    [HarmonyPatch(typeof(BowLocalManager), "OnRenderObject")]
    class BowLocalManager_OnRenderObject_Patch
    {
        public static void Postfix(bool ___isPulling)
        {
            if (TactsuitVR.suitDisabled)
            {
                return;
            }
            if (!___isPulling)
            {
                TactsuitVR.StopThreadHaptic(VHVRConfig.LeftHanded() ? "BowStringLeft" : "BowStringRight");
            }
        }
    }
    /**
    * on getting arrow from your back
    */
    [HarmonyPatch(typeof(BowLocalManager), "toggleArrow")]
    class BowLocalManager_toggleArrow_Patch
    {
        public static void Prefix(GameObject ___arrow)
        {
            if (TactsuitVR.suitDisabled)
            {
                return;
            }
            if (___arrow != null)
            {
                TactsuitVR.PlaybackHaptics(VHVRConfig.LeftHanded() ?
                    "HolsterArrowLeftShoulder" : "HolsterArrowRightShoulder");
                return;
            }
            var ammoItem = Player.m_localPlayer.GetAmmoItem();
            if (ammoItem == null || ammoItem.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Ammo)
            {
                // out of ammo
                return;
            }
            TactsuitVR.PlaybackHaptics(VHVRConfig.LeftHanded() ?
               "UnholsterArrowLeftShoulder" : "UnholsterArrowRightShoulder");
        }
    }

    /**
     * Player low Health
     */
    [HarmonyPatch(typeof(Character), "SetHealth")]
    class Character_LowHealth_Patch
    {
        public static void Postfix(Character __instance)
        {
            if (__instance != Player.m_localPlayer || TactsuitVR.suitDisabled)
            {
                return;
            }
            int hlth = Convert.ToInt32(__instance.GetHealth() * 100 / __instance.GetMaxHealth());
            switch (hlth)
            {
                case int n when hlth < 20 && hlth > 15:
                    TactsuitVR.StopThreadHaptic("HeartBeatFast");
                    TactsuitVR.StartThreadHaptic("HeartBeat");
                    break;
                case int n when hlth <= 15 && hlth > 0:
                    TactsuitVR.StopThreadHaptic("HeartBeat");
                    TactsuitVR.StartThreadHaptic("HeartBeatFast", 1.0f, false, 3000);
                    break;
                case int n when hlth <= 0:
                    TactsuitVR.StopThreadHaptic("HeartBeat");
                    TactsuitVR.StopThreadHaptic("HeartBeatFast");
                    TactsuitVR.PlaybackHaptics("Death");
                    break;
                default:
                    TactsuitVR.StopThreadHaptic("HeartBeat");
                    TactsuitVR.StopThreadHaptic("HeartBeatFast");
                    break;
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

            if (__instance != Player.m_localPlayer || EquipScript.getLeft() != EquipType.Shield || !VHVRConfig.UseVrControls())
            {
                return;
            }
            if (__result)
            {
                TactsuitVR.PlaybackHaptics(VHVRConfig.LeftHanded() ?
                    "BlockVest_R" : "BlockVest_L");
                // arms tactosy
                TactsuitVR.PlaybackHaptics(VHVRConfig.LeftHanded() ?
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
        public static void Prefix(Player __instance)
        {

            if (__instance != Player.m_localPlayer || TactsuitVR.suitDisabled)
            {
                return;
            }
            TactsuitVR.PlaybackHaptics("Death");
        }
        public static void Postfix(Player __instance)
        {

            if (__instance != Player.m_localPlayer || TactsuitVR.suitDisabled)
            {
                return;
            }
            TactsuitVR.StopAllHapticFeedback();
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

            if (__instance != Player.m_localPlayer || TactsuitVR.suitDisabled)
            {
                return;
            }
            var coords = TactsuitVR.getAngleAndShift(Player.m_localPlayer, hit.m_point);
            TactsuitVR.PlayBackHit("Impact", coords.Key, coords.Value);
        }
    }

    /**
     * OnTriggerEnter Attack succeded against any object with any weapon
     */
    [HarmonyPatch(typeof(WeaponCollision), "OnTriggerEnter")]
    class WeaponCollision_OnTriggerEnter_Patch
    {
        public static void Postfix( bool ___hasAttackedSuccess)
        {

            if ( !___hasAttackedSuccess || TactsuitVR.suitDisabled)
            {
                return;
            }
            TactsuitVR.SwordRecoil(!VHVRConfig.LeftHanded());
        }
    }

    /**
     * On starting teleporting
     */
    [HarmonyPatch(typeof(Player), "TeleportTo")]
    class Player_TeleportTo_Patch
    {
        public static void Postfix(Player __instance, bool __result)
        {
            if (__instance != Player.m_localPlayer || TactsuitVR.suitDisabled || !__result)
            {
                return;
            }
            if (__instance.m_teleporting)
            {
                TactsuitVR.PlaybackHaptics("PassPortalFront");
                TactsuitVR.PlaybackHaptics("PassPortalTactosy");
                //trying to wait for this effect to finish before starting teleport effect
                Thread.Sleep(2000);
                TactsuitVR.StartThreadHaptic("Teleporting", 1.0f, false, 5000);
            }
        }
    }
    /**
     * On ending teleporting
     */
    [HarmonyPatch(typeof(Player), "UpdateTeleport")]
    class Player_UpdateTeleport_Patch
    {
        public static void Prefix(Player __instance, out bool __state)
        {
            __state = false;
            if (__instance != Player.m_localPlayer || TactsuitVR.suitDisabled)
            {
                return;
            }
            __state = __instance.m_teleporting;
        }
        public static void Postfix(Player __instance, bool __state)
        {
            if (__instance != Player.m_localPlayer || TactsuitVR.suitDisabled)
            {
                return;
            }
            if (__state && !__instance.m_teleporting)
            {
                TactsuitVR.StopThreadHaptic("Teleporting", new string[] {"PassPortalBack", "PassPortalTactosy"});
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
            if (TactsuitVR.suitDisabled)
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
                    TactsuitVR.StartThreadHaptic("ApproachPortal");
                }
                if (!closeTo && myPortal && myPortal == __instance)
                {
                    TactsuitVR.StopThreadHaptic("ApproachPortal");
                    myPortal = null;
                }
            } else
            {
                TactsuitVR.StopThreadHaptic("ApproachPortal");
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
            if (TactsuitVR.suitDisabled)
            {
                return;
            }
            TactsuitVR.PlaybackHaptics("Thunder", 0.8f);
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
            if (TactsuitVR.suitDisabled)
            {
                return;
            }
            if (__result && !alt)
            {
                TactsuitVR.PlaybackHaptics("Petting");
                //tactosy
                TactsuitVR.PlaybackHaptics(VHVRConfig.LeftHanded() ? "Petting_L" : "Petting_R");
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
                    TactsuitVR.StopThreadHaptic("RideHorse");
                    TactsuitVR.StopThreadHaptic("RideHorseSlow");
                    break;
                case Sadle.Speed.Walk:
                    TactsuitVR.StartThreadHaptic("RideHorseSlow", (swimming) ? 0.4f : 1.0f, false, 3500);
                    TactsuitVR.StopThreadHaptic("RideHorse");
                    break;
                case Sadle.Speed.Turn:
                    TactsuitVR.StartThreadHaptic("RideHorseSlow", (swimming) ? 0.4f : 1.0f, false, 3500);
                    TactsuitVR.StopThreadHaptic("RideHorse");
                    break;
                case Sadle.Speed.Run:
                    TactsuitVR.StartThreadHaptic("RideHorse", (swimming) ? 0.4f : 1.0f, false, 3500);
                    TactsuitVR.StopThreadHaptic("RideHorseSlow");
                    break;
                case Sadle.Speed.NoChange:
                    break;
                default:
                    TactsuitVR.StopThreadHaptic("RideHorse");
                    TactsuitVR.StopThreadHaptic("RideHorseSlow");
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
                if (!__instance.IsLocalUser() || TactsuitVR.suitDisabled)
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
                        __instance.m_character.IsSwiming());

                    //attacking
                    if (__instance.m_character.InAttack())
                    {
                        Humanoid hum = __instance.m_character as Humanoid;
                        if ( !attackStarted && hum.m_currentAttack.m_wasInAttack )
                        {
                            attackStarted = true;
                            if (hum.m_currentAttack.m_attackType == Attack.AttackType.Area)
                            {
                                TactsuitVR.PlaybackHaptics("LoxGroundAttackTactosy");
                                TactsuitVR.PlaybackHaptics("LoxGroundAttack");
                            } else
                            {
                                TactsuitVR.PlaybackHaptics("LoxAttackTactosy");
                                TactsuitVR.PlaybackHaptics("LoxAttack");
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
                if (player != Player.m_localPlayer || TactsuitVR.suitDisabled)
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
            if (TactsuitVR.suitDisabled)
            {
                return;
            }
            // only if it is a ship and the player is onboard
            Ship component = __instance.GetComponent<Ship>();
            if (component != null && component.IsPlayerInBoat(Player.m_localPlayer))
            {
                TactsuitVR.PlaybackHaptics("ShipDamage");
            }
        }
    }

    /**
     * When repairing stuff
     */
    [HarmonyPatch(typeof(WearNTear), "Repair")]
    class WearNTear_Repair_Patch
    {
        public static void Postfix(WearNTear __instance)
        {
            if (TactsuitVR.suitDisabled)
            {
                return;
            }
            // only if it is local player
            Piece component = __instance.GetComponent<Piece>();
            if (component != null && component.IsCreator())
            {
                TactsuitVR.PlaybackHaptics((VHVRConfig.LeftHanded()) ? "Hammer_L" : "Hammer_R");
                TactsuitVR.PlaybackHaptics((VHVRConfig.LeftHanded()) ? "HammerTactosy_L" : "HammerTactosy_R");
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
            if (TactsuitVR.suitDisabled)
            {
                return;
            }
            // only if it is local player
            Piece component = __instance.GetComponent<Piece>();
            if (component != null && component.IsCreator())
            {
                TactsuitVR.PlaybackHaptics((VHVRConfig.LeftHanded()) ? "Hammer_L" : "Hammer_R");
                TactsuitVR.PlaybackHaptics((VHVRConfig.LeftHanded()) ? "HammerTactosy_L" : "HammerTactosy_R");
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
        private static int envDelay = 12;
        public static void Postfix(EnvSetup ___m_currentEnv)
        {
            if (TactsuitVR.suitDisabled || !Player.m_localPlayer)
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
                    TactsuitVR.StartThreadHaptic("Raining", 0.7f, false, 3000, 1.0f, 12000);
                }
                else
                {
                    TactsuitVR.StopThreadHapticDelayed("Raining", 12000);
                }
            }
            if (rain.Contains(currentEnv))
            {
                //are we in shelter
                if (Player.m_localPlayer.InShelter())
                {
                    TactsuitVR.StopThreadHaptic("Raining");
                }
                else
                {
                    //are we in env rain starting delay ?
                    int startDelay = (int)new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds() - envStarted;
                    if (startDelay > envDelay)
                    {
                        TactsuitVR.StartThreadHaptic("Raining", 0.7f, false, 3000);
                    }
                    else
                    {
                        TactsuitVR.StartThreadHaptic("Raining", 0.7f, false, 3000, envDelay - startDelay);
                    }
                }
            }
        }
    }
    /**
     * When summoning boss phase 
     */
    [HarmonyPatch(typeof(OfferingBowl), "SpawnBoss")]
    class OfferingBowl_SpawnBoss_Patch
    {
        public static void Postfix(OfferingBowl __instance)
        {
            if (TactsuitVR.suitDisabled)
            {
                return;
            }
            // is Local Player near ?
            bool closeTo = (Vector3.Distance(Player.m_localPlayer.transform.position, __instance.transform.position) < (double)20f);
            if (closeTo)
            {
                Thread EffectThread = new Thread(() =>
                {
                    TactsuitVR.StartThreadHaptic("BossSummon");
                    Thread.Sleep(5000);
                    TactsuitVR.StartThreadHaptic("BossSummon", 0.4f);
                    Thread.Sleep(7000);
                    TactsuitVR.StopThreadHaptic("BossSummon", new string[] { "BossAppearance" });
                });
                EffectThread.Start();
            }
        }
    }
    
    /**
     * When selecting boss power at the altars 
     */
    [HarmonyPatch(typeof(BossStone), "DelayedAttachEffects_Step1")]
    class BossStone_DelayedAttachEffects_Step1_Patch
    {
        public static void Postfix(BossStone __instance)
        {
            if (TactsuitVR.suitDisabled)
            {
                return;
            }
            TactsuitVR.PlaybackHaptics("ActivateGuardianPower");
        }
    }
    /**
     * Boss animations / attacks
     */
    [HarmonyPatch(typeof(Attack), "OnAttackTrigger")]
    class Attack_OnAttackTrigger_Patch
    {
        public static Dictionary<string, float> rangeBoss = new Dictionary<string, float>()
        {
            {"boss_eikthyr",  20f},
            {"boss_elder", 20f },
            {"boss_bonemass",  20f},
            {"boss_moder",  20f},
            {"boss_yagluth",  20f},
        };
        public static void Postfix(Attack __instance)
        {
            if (TactsuitVR.suitDisabled || !__instance.m_character.IsBoss())
            {
                return;
            }
            LogInfo("BOSS ATTACK " + __instance.m_attackAnimation + " " +
                __instance.m_character.m_name
                + " " + __instance.m_character.m_bossEvent);
            //range limit arbitrary for all bosses, easier...
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
                        TactsuitVR.PlaybackHaptics("EikthyrElectric");
                    }
                    if (__instance.m_attackAnimation == "attack_stomp")
                    {
                        TactsuitVR.PlaybackHaptics("EikthyrElectric");
                    }
                    break;
            }
        }
    }
}
