
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
            if (MagicWeaponManager.AttemptingAttack && !MagicWeaponManager.UseSwingForCurrentAttack())
            {
                // Swing-launch attack is managed in SwingLaunchManager.
                StartAttackIfRiding();
            }
            else if (EquipScript.getLeft() == EquipType.Crossbow && CrossbowManager.IsPullingTrigger())
            {
                StartAttackIfRiding(isSecondaryAttack: false, attackDrawPercentage: 1);
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
                player.m_currentAttackIsSecondary = false;
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
