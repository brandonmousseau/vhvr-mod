
using HarmonyLib;
using System.Reflection;
using ValheimVRMod.Scripts;
using Valve.VR;

namespace ValheimVRMod.Utilities
{

    public class MountedAttackUtils
    {
        public static readonly MethodInfo stopDoodadControlMethod = AccessTools.Method(typeof(Player), nameof(Player.StopDoodadControl));
        private static IDoodadController doodadController { get { return Player.m_localPlayer?.m_doodadController; } }

        public void UnmountIfJumping()
        {
            if (doodadController == null || !SteamVR_Actions.valheim_Jump.GetState(SteamVR_Input_Sources.Any))
            {
                return;
            }

            if (doodadController.IsValid())
            {
                doodadController.OnUseStop(Player.m_localPlayer);
            }
            Player.m_localPlayer.m_doodadController = null;
        }

        public static void CheckMountedMagicAndCrossbowAttack()
        {
            // Bail out before querying the managers when not riding: both SummonerManager#ConsumeAttemptingAttack
            // and IMagicStaffManager#AttemptingAttack are destructive reads, so polling them here on foot would
            // steal the attack from Player#SetControls, which is what actually triggers the attack when not riding.
            if (!IsRiding())
            {
                return;
            }

            var staff = MagicStaffManagers.Current;
            bool attemptingNonSwingAttack =
                (SummonerManager.instance != null && SummonerManager.instance.ConsumeAttemptingAttack()) ||
                (Protector.instance != null && Protector.instance.AttemptingAttack) ||
                (staff != null && staff.AttemptingAttack &&
                 !(SwingableStaffManager.instance != null && SwingableStaffManager.instance.UseSwingForCurrentAttack()));
            if (attemptingNonSwingAttack)
            {
                // Swing-launch attack is managed in SwingLaunchManager.
                StartAttackIfRiding();
            }
            else if (EquipScript.CurrentOffHandEquipType() == EquipType.Crossbow && CrossbowManager.IsPullingTrigger(out bool useSecondaryAttack))
            {
                StartAttackIfRiding(isSecondaryAttack: useSecondaryAttack, attackDrawPercentage: 1);
            }
        }

        public static bool IsRiding()
        {
            return doodadController?.IsValid() ?? false;
        }

        public static bool IsRidingMount()
        {
            return IsRiding() && doodadController is Sadle;
        }

        public static bool IsSteering()
        {
            return IsRiding() && !(doodadController is Sadle);
        }

        // Vanilla game does not support attacking while riding and this method forces initiating attack when riding.
        public static bool StartAttackIfRiding(bool isSecondaryAttack = false, float? attackDrawPercentage = null)
        {
            if (!IsRiding())
            {
                return false;
            }

            var player = Player.m_localPlayer;
            if (player.InAttack())
            {
                // Vanilla Humanoid#StartAttack refuses to start an attack while one is already playing. Without
                // the same check here, a caller that reports an attack attempt on every frame the trigger is held
                // (e.g. a summoner) would restart the attack each frame, leaving the weapon stuck replaying
                // the wind-up part of its animation instead of ever releasing its projectile.
                return false;
            }

            var weapon = player.GetCurrentWeapon();
            if (weapon == null)
            {
                return false;
            }

            Attack attack =
                isSecondaryAttack ? weapon.m_shared.m_secondaryAttack.Clone() : weapon.m_shared.m_attack.Clone();
            var playerRotation = player.transform.rotation;
            var bodyRotation = player.m_body.rotation;

            // The rest is cloned from vanilla game logic.
            if (attack.Start(
                player,
                player.m_body,
                player.m_zanim,
                player.m_animEvent,
                player.m_visEquipment,
                weapon,
                player.m_previousAttack,
                player.m_timeSinceLastAttack,
                attackDrawPercentage ?? player.GetAttackDrawPercentage()))
            {
                player.ClearActionQueue();
                player.StartAttackGroundCheck();
                player.m_currentAttack = attack;
                player.m_currentAttackIsSecondary = isSecondaryAttack;
                player.m_lastCombatTimer = 0f;
                // Restore the rotation since vanilla attack logic may have changed it.
                player.transform.rotation = playerRotation;
                player.m_body.rotation = bodyRotation;
                return true;
            }

            return false;
        }
    }
}
