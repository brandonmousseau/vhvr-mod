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
        // Where the view is held while the mouse turns the character under it.
        private static float heldViewYaw;
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
        // First person keeps the forced facing: there the character is the player's own body. So does the aim
        // view, where the character faces what it aims at even before vanilla knows of an attack.
        public static bool LeavesCharacterFacingToVanilla
        {
            get { return IsActive && !VRPlayer.inFirstPerson && !VRPlayer.inAimView; }
        }

        // What moving forward means, which goes by the same setting as the joystick's forward direction does with
        // motion controls: where the headset faces, where the crosshair is (standing in for the controllers, which
        // do the aiming with motion controls), or where the view was centered, i. e. the way from the camera to the
        // character as recentering and view snapping left it.
        public static Vector3 GetMoveForward(Vector3 fallback)
        {
            Player player = Player.m_localPlayer;
            return VHVRConfig.GetKeyboardForwardDirection(
                head: getHeadForward(fallback),
                aim: player.m_lookYaw * Vector3.forward,
                view: player.transform.rotation * ViewRotationFromCharacter * Vector3.forward);
        }

        private static Vector3 getHeadForward(Vector3 fallback)
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

        // Whether an attack is being aimed, or was until a moment ago, which brings a third person view into the
        // aim view like drawing a bow does, see VRPlayer.inAimView.
        public static bool IsAimingAttack { get; private set; }

        private enum HeldAttackState { None, Aiming, Canceled }

        // How long vanilla keeps an attack input queued, see Player.PlayerAttackInput().
        private const float ATTACK_QUEUE_TIME = 0.5f;
        // How long the view stays in the aim view once the button of an attack is let go of. Counted from the
        // button rather than from anything the attack does afterwards, such as how long its animation takes to
        // launch the projectile.
        private const float ATTACK_AIM_HOLD_TIME = 0.25f;
        private static HeldAttackState primaryAttackState;
        private static HeldAttackState secondaryAttackState;
        private static float primaryAttackHoldStartTime;
        private static float secondaryAttackHoldStartTime;
        private static bool isAttackReleased;
        private static float attackReleaseTime;
        private static bool wasAimingWithAttackButton;
        private static float aimHoldEndTime;

        // Makes aimed attacks (throwing, crossbows, the grappling hook, Dundr) come out
        // when the button is let go of rather than when it is pressed, so that there is time to aim while it is
        // held. Blocking while holding the button calls the attack off.
        //
        // The staffs shoot for as long as the button is held, which releasing on button up would get in the way of.
        // They attack as in vanilla and are aimed by holding the secondary attack button instead, which they have
        // no use for otherwise. Staffs with a continuous attack are aimed for as long as they shoot, too.
        //
        // Letting go of the button of an attack leaves the aim view after ATTACK_AIM_HOLD_TIME, or right away with
        // a crossbow. Letting go of a button that only aims, or calling the attack off, leaves it right away.
        public static void UpdateAttackControls(
            Player player, ref bool attack, ref bool attackHold, ref bool secondaryAttack, ref bool secondaryAttackHold, bool blockHold)
        {
            if (!IsActive || player != Player.m_localPlayer)
            {
                primaryAttackState = secondaryAttackState = HeldAttackState.None;
                IsAimingAttack = isAttackReleased = wasAimingWithAttackButton = false;
                aimHoldEndTime = 0;
                return;
            }

            var weapon = player.GetCurrentWeapon()?.m_shared;
            bool isSwingableStaff = weapon != null && Scripts.SwingableStaffManager.STAFF_NAMES.Contains(weapon.m_name);
            bool isAimingWithAttackButton =
                player.IsDrawingBow() ||
                (weapon != null && !isSwingableStaff && weapon.m_attack.m_loopingAttack && attackHold);

            // Of the staffs only Dundr is released on button up.
            bool isOtherStaff =
                weapon != null &&
                (weapon.m_skillType == Skills.SkillType.ElementalMagic || weapon.m_skillType == Skills.SkillType.BloodMagic) &&
                !EquipScript.IsDundr(player.GetCurrentWeapon());
            bool releasesPrimary =
                weapon != null && !isOtherStaff && weapon.m_name != "$item_fishingrod" && isReleasedOnButtonUp(weapon.m_attack);
            // E. g. throwing a spear, whose primary attack is left as it is.
            bool releasesSecondary = weapon != null && !isOtherStaff && isReleasedOnButtonUp(weapon.m_secondaryAttack);
            isAimingWithAttackButton |=
                updateHeldAttack(releasesPrimary, blockHold, ref primaryAttackState, ref primaryAttackHoldStartTime, ref attack, ref attackHold);
            isAimingWithAttackButton |=
                updateHeldAttack(
                    releasesSecondary, blockHold, ref secondaryAttackState, ref secondaryAttackHoldStartTime, ref secondaryAttack, ref secondaryAttackHold);
            // The secondary attack button aims a weapon without an aimed secondary attack, e. g. a staff, a
            // crossbow or a harpoon, without attacking.
            bool isOnlyAiming = (isOtherStaff || (releasesPrimary && !releasesSecondary)) && secondaryAttackHold;
            bool isLaunchingPrimary = releasesPrimary && attack;
            bool isLaunching = isLaunchingPrimary || (releasesSecondary && secondaryAttack);

            // For speeding up the animation of the attack, see UpdateAttackAnimationSpeed(). OnAttackTriggered()
            // ends this; the time out is for an attack that never starts, e. g. for lack of ammo or stamina.
            if (isLaunching)
            {
                isAttackReleased = true;
                attackReleaseTime = Time.time;
                releasedAttackAnimation = (isLaunchingPrimary ? weapon.m_attack : weapon.m_secondaryAttack).m_attackAnimation;
                releasedAttackHoldTime = Time.time - (isLaunchingPrimary ? primaryAttackHoldStartTime : secondaryAttackHoldStartTime);
                releasedAttackWindUpProgress = 0;
            }
            else if (isAttackReleased && Time.time > attackReleaseTime + ATTACK_QUEUE_TIME && !player.InAttack())
            {
                isAttackReleased = false;
            }

            bool isCanceled = primaryAttackState == HeldAttackState.Canceled || secondaryAttackState == HeldAttackState.Canceled;
            if (wasAimingWithAttackButton && !isAimingWithAttackButton && !isCanceled)
            {
                bool isCrossbow = weapon != null && weapon.m_skillType == Skills.SkillType.Crossbows;
                aimHoldEndTime = isCrossbow ? 0 : Time.time + ATTACK_AIM_HOLD_TIME;
            }
            wasAimingWithAttackButton = isAimingWithAttackButton;
            IsAimingAttack = isAimingWithAttackButton || isOnlyAiming || Time.time < aimHoldEndTime;
        }

        // To be called when an attack of the local player gets to the point where it launches its projectile.
        public static void OnAttackTriggered()
        {
            if (!IsActive)
            {
                return;
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
        // The view follows the mouse aim: right away with smooth turn. With snap turn it either steps once the aim
        // gets a snap turn angle away from where the view faces, or (see VHVRConfig.SmoothSnapTurn()) turns toward
        // the aim the faster the further away the aim is, see getAimYawFromViewAfterTurn(). Near where the view
        // faces the aim moves sideways in a view that stays put.
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
                heldViewYaw = characterYaw + appliedViewYawOffset;
                isHoldingView = true;
            }

            float snapAngle = VHVRConfig.SnapTurnEnabled() ? VHVRConfig.GetSnapTurnAngle() : 0;
            float aimYawFromView = Mathf.DeltaAngle(heldViewYaw, aimYaw);
            if (snapAngle <= 0)
            {
                heldViewYaw = aimYaw;
            }
            else if (VHVRConfig.SmoothSnapTurn())
            {
                heldViewYaw = aimYaw - getAimYawFromViewAfterTurn(aimYawFromView, snapAngle, Time.unscaledDeltaTime);
            }
            else if (Mathf.Abs(aimYawFromView) >= snapAngle)
            {
                heldViewYaw += Mathf.Sign(aimYawFromView) * snapAngle * Mathf.Floor(Mathf.Abs(aimYawFromView) / snapAngle);
            }
            setViewYawOffset(rig, Mathf.DeltaAngle(characterYaw, heldViewYaw));
        }

        // How far past the snap turn angle the aim can get from where the view faces at most.
        private const float MAX_AIM_YAW_BEYOND_SNAP_ANGLE = 15f;

        // Where the aim is from where the view faces once the view has turned toward it for the given time.
        //
        // The speed of the turn goes by how far away the aim is: none up to 2/3 of the snap turn angle, the smooth
        // snap speed at the snap turn angle, and unbounded at MAX_AIM_YAW_BEYOND_SNAP_ANGLE past it, i. e. with d
        // the angle of the aim from the view and s the smooth snap speed:
        //   s * MAX_AIM_YAW_BEYOND_SNAP_ANGLE * (d - deadZone) / ((maxAngle - d) * (snapAngle - deadZone))
        // The step is taken by the speed at its end rather than at its start, which changes too fast near the
        // maximum for that and would overshoot. With u the angle past the dead zone, that makes for a quadratic
        // equation of what is left of u after the step:
        //   u1 = u - dt * k * u1 / (maxU - u1)
        private static float getAimYawFromViewAfterTurn(float aimYawFromView, float snapAngle, float dt)
        {
            float deadZone = snapAngle * 2 / 3;
            float maxAngleBeyondDeadZone = snapAngle + MAX_AIM_YAW_BEYOND_SNAP_ANGLE - deadZone;
            float angleBeyondDeadZone = Mathf.Min(Mathf.Abs(aimYawFromView) - deadZone, maxAngleBeyondDeadZone);
            if (angleBeyondDeadZone <= 0)
            {
                return aimYawFromView;
            }

            // SmoothSnapSpeed is in degrees per hundredth of a second, see Player_SetMouseLook_Patch in ControlPatches.
            float speed = VHVRConfig.SmoothSnapSpeed() * 100f;
            float k = speed * MAX_AIM_YAW_BEYOND_SNAP_ANGLE / (snapAngle - deadZone);
            float b = maxAngleBeyondDeadZone + angleBeyondDeadZone + k * dt;
            float c = angleBeyondDeadZone * maxAngleBeyondDeadZone;
            // The smaller root, in the form that does not cancel out when it is small.
            float angleBeyondDeadZoneAfterTurn = 2 * c / (b + Mathf.Sqrt(b * b - 4 * c));
            return Mathf.Sign(aimYawFromView) * (deadZone + angleBeyondDeadZoneAfterTurn);
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
