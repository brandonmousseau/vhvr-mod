using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Utilities
{
    // Where the local player aims when motion controls are disabled, i. e. when playing with mouse and keyboard or
    // a gamepad.
    //
    // The view is the headset's alone, but aiming is not: it is the look direction that vanilla keeps from the mouse,
    // yaw and pitch both, the same as on a flat screen. The mouse yaw turns the character and the view with it, so
    // the aim stays where it is in the view horizontally until the head turns away from it, while the mouse pitch
    // only moves the aim, the view's pitch being left to the head. Recentering lines the view up with the aim again,
    // see VRPlayer.maybeInitHeadPosition().
    static class MouseAim
    {
        public static bool IsActive
        {
            get
            {
                return !VHVRConfig.NonVrPlayer() &&
                    !VHVRConfig.UseVrControls() &&
                    VRPlayer.attachedToPlayer &&
                    Player.m_localPlayer != null;
            }
        }

        // What Player.UpdateEyeRotation() makes the eye rotation in vanilla.
        public static Quaternion Rotation
        {
            get
            {
                Player player = Player.m_localPlayer;
                return player.m_lookYaw * Quaternion.Euler(player.m_lookPitch, 0f, 0f);
            }
        }

        public static Vector3 Forward { get { return Rotation * Vector3.forward; } }

        // How far the camera rig is currently turned away from the character, on top of where recentering left it.
        private static float appliedViewYawOffset;
        // Where the view is held while the mouse turns the character under it, and where it is turning to.
        private static float heldViewYaw;
        private static float targetViewYaw;
        private static bool isHoldingView;

        // The view's rotation in the character's space on top of where recentering left it. Whatever places the
        // camera relative to the character, e. g. the third person distance, has to go by this rather than by the
        // character's own facing, or the camera swings around the character whenever the mouse moves the aim.
        public static Quaternion ViewRotationFromCharacter { get { return Quaternion.Euler(0f, appliedViewYawOffset, 0f); } }

        // To be called when the camera rig's rotation is set anew, discarding any snap turn offset it had.
        public static void OnRecentered()
        {
            appliedViewYawOffset = 0;
            isHoldingView = false;
        }

        // In third person the character's facing is left to vanilla, which turns it to where it moves and, while
        // attacking, blocking or drawing a bow, to where it aims, rather than being forced to follow the mouse.
        // First person keeps the forced facing: there the character is the player's own body.
        public static bool LeavesCharacterFacingToVanilla { get { return IsActive && !VRPlayer.inFirstPerson; } }

        // What moving forward means: where the headset faces, as is usual in VR, whichever way the aim, the view's
        // center or the character happens to face.
        public static Vector3 GetMoveForward(Vector3 fallback)
        {
            if (VRPlayer.vrCam == null)
            {
                return fallback;
            }
            Vector3 up = Player.m_localPlayer.transform.up;
            Vector3 forward = Vector3.ProjectOnPlane(VRPlayer.vrCam.transform.forward, up);
            if (forward.sqrMagnitude < 0.01f)
            {
                // Looking straight up or down, where the top of the head points the way instead.
                forward = Vector3.ProjectOnPlane(
                    VRPlayer.vrCam.transform.up * -Mathf.Sign(Vector3.Dot(VRPlayer.vrCam.transform.forward, up)), up);
            }
            return forward.sqrMagnitude < 0.0001f ? fallback : forward.normalized;
        }

        // Whether an attack is being aimed, which brings a third person view into first person like drawing a bow
        // does, see VRPlayer.effectiveHeadZoomLevel.
        public static bool IsAimingAttack { get; private set; }

        private static bool hasAimEndedWithRecoil;

        // Whether the view should leave first person without the usual wait now that aiming is over. Only true once
        // per attack.
        public static bool TakeAimEndedWithRecoil()
        {
            bool result = hasAimEndedWithRecoil;
            hasAimEndedWithRecoil = false;
            return result;
        }

        private enum HeldAttackState { None, Aiming, Canceled }

        // How long vanilla keeps an attack input queued, see Player.PlayerAttackInput().
        private const float ATTACK_QUEUE_TIME = 0.5f;
        private static HeldAttackState primaryAttackState;
        private static HeldAttackState secondaryAttackState;
        private static float primaryAttackHoldStartTime;
        private static float secondaryAttackHoldStartTime;
        private static bool isAttackReleased;
        private static float attackReleaseTime;

        // Makes aimed attacks (throwing, crossbows, the grappling hook, Dundr) come out
        // when the button is let go of rather than when it is pressed, so that there is time to aim in first person
        // while it is held. Blocking while holding the button calls the attack off.
        //
        // The staffs that can be swing-launched with motion controls shoot for as long as the button is held, which
        // releasing on button up would get in the way of. They attack as in vanilla and are aimed by holding the
        // secondary attack button instead, which they have no use for otherwise. Staffs with a continuous attack
        // are aimed for as long as they shoot.
        public static void UpdateAttackControls(
            Player player, ref bool attack, ref bool attackHold, ref bool secondaryAttack, ref bool secondaryAttackHold, bool blockHold)
        {
            if (!IsActive || player != Player.m_localPlayer)
            {
                primaryAttackState = secondaryAttackState = HeldAttackState.None;
                IsAimingAttack = isAttackReleased = false;
                return;
            }

            var weapon = player.GetCurrentWeapon()?.m_shared;
            bool isSwingableStaff = weapon != null && Scripts.SwingableStaffManager.STAFF_NAMES.Contains(weapon.m_name);
            bool isAiming =
                weapon != null &&
                (isSwingableStaff ? secondaryAttackHold : (weapon.m_attack.m_loopingAttack && attackHold));

            // Of the staffs only Dundr is released on button up.
            bool isOtherStaff =
                weapon != null &&
                (weapon.m_skillType == Skills.SkillType.ElementalMagic || weapon.m_skillType == Skills.SkillType.BloodMagic) &&
                !EquipScript.IsDundr(player.GetCurrentWeapon());
            bool releasesPrimary =
                weapon != null && !isOtherStaff && weapon.m_name != "$item_fishingrod" && isReleasedOnButtonUp(weapon.m_attack);
            // E. g. throwing a spear, whose primary attack is left as it is.
            bool releasesSecondary = weapon != null && !isOtherStaff && isReleasedOnButtonUp(weapon.m_secondaryAttack);
            isAiming |=
                updateHeldAttack(releasesPrimary, blockHold, ref primaryAttackState, ref primaryAttackHoldStartTime, ref attack, ref attackHold);
            isAiming |=
                updateHeldAttack(
                    releasesSecondary, blockHold, ref secondaryAttackState, ref secondaryAttackHoldStartTime, ref secondaryAttack, ref secondaryAttackHold);
            // The secondary attack button aims a weapon without an aimed secondary attack too, e. g. a harpoon.
            isAiming |= releasesPrimary && !releasesSecondary && secondaryAttackHold;
            bool isLaunchingPrimary = releasesPrimary && attack;
            bool isLaunching = isLaunchingPrimary || (releasesSecondary && secondaryAttack);

            // The view has to stay in first person until the projectile has left, not just until the button is let
            // go of. OnAttackTriggered() ends this; the time out is for an attack that never starts, e. g. for lack
            // of ammo or stamina.
            if (isLaunching)
            {
                isAttackReleased = true;
                hasAimEndedWithRecoil = false;
                attackReleaseTime = Time.time;
                releasedAttackAnimation = (isLaunchingPrimary ? weapon.m_attack : weapon.m_secondaryAttack).m_attackAnimation;
                releasedAttackHoldTime = Time.time - (isLaunchingPrimary ? primaryAttackHoldStartTime : secondaryAttackHoldStartTime);
                releasedAttackWindUpProgress = 0;
            }
            else if (isAttackReleased && Time.time > attackReleaseTime + ATTACK_QUEUE_TIME && !player.InAttack())
            {
                isAttackReleased = false;
            }
            IsAimingAttack = isAiming || isAttackReleased;
        }

        // To be called when an attack of the local player gets to the point where it launches its projectile.
        public static void OnAttackTriggered(bool hasRecoil)
        {
            if (isAttackReleased && hasRecoil)
            {
                // The recoil shoves the character back, and the first person view with it. Leaving first person
                // right away makes that one motion with the view gliding out rather than two in a row.
                IsAimingAttack = false;
                hasAimEndedWithRecoil = true;
            }
            if (isAttackReleased && releasedAttackWindUpProgress > 0)
            {
                attackWindUpTimes[releasedAttackAnimation] = releasedAttackWindUpProgress;
                shouldResetAttackAnimationSpeed = true;
            }
            isAttackReleased = false;
        }

        // How long each attack animation takes to get to launching its projectile at normal speed. Measured the
        // first time the attack is used, which therefore plays at normal speed.
        private static readonly Dictionary<string, float> attackWindUpTimes = new Dictionary<string, float>();
        private const float MIN_ATTACK_WIND_UP_TIME = 0.05f;
        private const float MAX_ATTACK_WIND_UP_SPEED = 20f;
        private static string releasedAttackAnimation;
        private static float releasedAttackHoldTime;
        private static float releasedAttackWindUpProgress;
        private static bool shouldResetAttackAnimationSpeed;

        // The time the attack button was held counts toward the wind up of the attack it releases: the animation is
        // sped up so that it launches the projectile after as much time as is left of its wind up, as if it had
        // started when the button was pressed. What follows the launch plays at normal speed again.
        //
        // To be called before CharacterAnimEvent.CustomFixedUpdate() of the local player.
        public static void UpdateAttackAnimationSpeed(Character character, Animator animator, float dt)
        {
            if (shouldResetAttackAnimationSpeed)
            {
                shouldResetAttackAnimationSpeed = false;
                animator.speed = 1f;
            }
            if (!isAttackReleased || !character.InAttack())
            {
                return;
            }
            if (attackWindUpTimes.TryGetValue(releasedAttackAnimation, out float windUpTime))
            {
                animator.speed =
                    Mathf.Min(
                        windUpTime / Mathf.Max(windUpTime - releasedAttackHoldTime, MIN_ATTACK_WIND_UP_TIME),
                        MAX_ATTACK_WIND_UP_SPEED);
            }
            releasedAttackWindUpProgress += dt * animator.speed;
        }

        private static bool isReleasedOnButtonUp(Attack attack)
        {
            return attack != null &&
                attack.m_attackType == Attack.AttackType.Projectile &&
                !string.IsNullOrEmpty(attack.m_attackAnimation) &&
                !attack.m_bowDraw &&
                !attack.m_loopingAttack;
        }

        // Returns whether the attack is being held back.
        private static bool updateHeldAttack(
            bool releasesOnButtonUp, bool cancel, ref HeldAttackState state, ref float holdStartTime, ref bool pressed, ref bool held)
        {
            if (!releasesOnButtonUp)
            {
                state = HeldAttackState.None;
                return false;
            }

            bool isHeld = held;
            pressed = held = false;
            if (!isHeld)
            {
                pressed = state == HeldAttackState.Aiming;
                state = HeldAttackState.None;
                return false;
            }
            if (cancel)
            {
                state = HeldAttackState.Canceled;
            }
            else if (state == HeldAttackState.None)
            {
                state = HeldAttackState.Aiming;
                holdStartTime = Time.time;
            }
            return state == HeldAttackState.Aiming;
        }

        // Keeps the view facing where it should while the character under it turns, by turning the camera rig,
        // which the character carries along, back around the character by as much.
        //
        // The view follows the mouse aim: right away with smooth turn, and with snap turn only once the aim gets a
        // snap turn angle away from where the view faces, in one step or as a quick turn (see
        // VHVRConfig.SmoothSnapTurn()). Until then the aim moves sideways in a view that stays put.
        //
        // To be called once everything that turns the character in a frame has run, i. e. right before rendering:
        // a turn that is only compensated a frame later shows as a jitter.
        public static void UpdateView(Transform rig)
        {
            if (!IsActive || rig == null || Player.m_localPlayer.IsAttached())
            {
                // Something else places the view, e. g. a ship. Resumes from whatever offset is left then.
                isHoldingView = false;
                return;
            }

            Player player = Player.m_localPlayer;
            if (Patches.Player_Rotation_Patch.ShouldFaceLookDirection(player))
            {
                // The look input may have come in after the character was last turned to it in this frame.
                player.transform.rotation = player.m_lookYaw;
            }
            float characterYaw = player.transform.eulerAngles.y;
            float aimYaw = player.m_lookYaw.eulerAngles.y;
            if (!isHoldingView)
            {
                heldViewYaw = targetViewYaw = characterYaw + appliedViewYawOffset;
                isHoldingView = true;
            }

            float snapAngle = VHVRConfig.SnapTurnEnabled() ? VHVRConfig.GetSnapTurnAngle() : 0;
            if (snapAngle <= 0)
            {
                heldViewYaw = targetViewYaw = aimYaw;
            }
            else
            {
                float aimYawFromTarget = Mathf.DeltaAngle(targetViewYaw, aimYaw);
                if (Mathf.Abs(aimYawFromTarget) >= snapAngle)
                {
                    targetViewYaw += Mathf.Sign(aimYawFromTarget) * snapAngle * Mathf.Floor(Mathf.Abs(aimYawFromTarget) / snapAngle);
                }
                // SmoothSnapSpeed is in degrees per hundredth of a second, see Player_SetMouseLook_Patch in ControlPatches.
                heldViewYaw =
                    VHVRConfig.SmoothSnapTurn() ?
                    Mathf.MoveTowardsAngle(heldViewYaw, targetViewYaw, VHVRConfig.SmoothSnapSpeed() * 100f * Time.unscaledDeltaTime) :
                    targetViewYaw;
            }
            setViewYawOffset(rig, Mathf.DeltaAngle(characterYaw, heldViewYaw));
        }

        // Turns the rig around the character rather than around itself, so that the camera keeps its place relative
        // to where the view faces.
        private static void setViewYawOffset(Transform rig, float offset)
        {
            if (offset != appliedViewYawOffset)
            {
                Quaternion turn = Quaternion.Euler(0f, offset - appliedViewYawOffset, 0f);
                rig.localRotation = turn * rig.localRotation;
                rig.localPosition = turn * rig.localPosition;
                appliedViewYawOffset = offset;
            }
        }
    }
}
