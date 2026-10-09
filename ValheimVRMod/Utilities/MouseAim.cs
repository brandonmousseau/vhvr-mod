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

        // How far the camera rig is currently turned away from the character for the snap turn, on top of where
        // recentering left it.
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

        // With snap turn enabled, the mouse turns the character and the aim as usual but not the view: the camera
        // rig, which the character carries along, is turned back around the character by as much, so the aim moves
        // sideways in a view that stays put. Once the aim gets a snap turn angle away from where the view faces,
        // the view catches up with it, in one step or as a quick turn (see VHVRConfig.SmoothSnapTurn()).
        //
        // To be called once everything that turns the character in a frame has run, i. e. right before rendering:
        // a turn that is only compensated a frame later shows as a jitter.
        public static void UpdateSnapTurn(Transform rig)
        {
            if (!IsActive || rig == null || Player.m_localPlayer.IsAttached())
            {
                // Something else places the view, e. g. a ship. Resumes from whatever offset is left then.
                isHoldingView = false;
                return;
            }

            float snapAngle = VHVRConfig.SnapTurnEnabled() ? VHVRConfig.GetSnapTurnAngle() : 0;
            if (snapAngle <= 0)
            {
                setViewYawOffset(rig, 0);
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
            if (!isHoldingView)
            {
                heldViewYaw = targetViewYaw = characterYaw + appliedViewYawOffset;
                isHoldingView = true;
            }

            float aimYawFromTarget = Mathf.DeltaAngle(targetViewYaw, characterYaw);
            if (Mathf.Abs(aimYawFromTarget) >= snapAngle)
            {
                targetViewYaw += Mathf.Sign(aimYawFromTarget) * snapAngle * Mathf.Floor(Mathf.Abs(aimYawFromTarget) / snapAngle);
            }
            // SmoothSnapSpeed is in degrees per hundredth of a second, see Player_SetMouseLook_Patch in ControlPatches.
            heldViewYaw =
                VHVRConfig.SmoothSnapTurn() ?
                Mathf.MoveTowardsAngle(heldViewYaw, targetViewYaw, VHVRConfig.SmoothSnapSpeed() * 100f * Time.unscaledDeltaTime) :
                targetViewYaw;
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
