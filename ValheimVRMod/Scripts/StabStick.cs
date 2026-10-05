using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts
{
    // Keeps an enemy that was stabbed stuck on the weapon for a moment, dragging it along with the weapon until the
    // weapon is pulled out of it.
    public class StabStick
    {
        // How far the weapon may get away sideways from where it is stuck before the enemy comes off.
        private const float BREAK_DISTANCE = 0.75f;
        // How far the weapon may be pulled back from where it is stuck before the enemy comes off. Pulling back
        // slower than the enemy can follow drags it along instead.
        private const float WITHDRAW_DISTANCE = 0.5f;
        // How far the weapon can be pushed further into the enemy before it pushes the enemy ahead of it. The hand
        // is not stopped by the enemy, so a stab always follows through.
        private const float MAX_PENETRATION = 0.4f;
        private const float PULL_SPEED = 6f;
        private const float PULL_SMOOTH_DISTANCE = 0.15f;
        // Enemies up to this mass are dragged at full speed and heavier ones proportionally slower.
        private const float FULL_SPEED_MASS = 50f;
        private const float MAX_MASS = 500f;

        private Character character;
        // The point of the character that the weapon is stuck in, in the local space of the character.
        private Vector3 anchor;
        // The cooldown that the stab started on the character. The character comes off when it finishes.
        private AttackTargetMeshCooldown cooldown;

        public bool isSticking { get { return character != null; } }

        public void TryStick(Character target, Vector3 weaponPosition)
        {
            if (!VHVRConfig.UseKnockbackSwingDirection() || isSticking || !CanStick(target))
            {
                return;
            }

            // Only the owner of a character can move it.
            target.m_nview.ClaimOwnership();
            character = target;
            anchor = target.transform.InverseTransformPoint(weaponPosition);
            cooldown = target.GetComponent<AttackTargetMeshCooldown>();
        }

        public void Release()
        {
            character = null;
            cooldown = null;
        }

        // Drags the stuck character along with the weapon. Returns whether a character is still stuck afterwards.
        public bool FixedUpdate(Vector3 weaponPosition, Vector3 weaponForward)
        {
            if (!isSticking)
            {
                return false;
            }

            if (cooldown == null ||
                !cooldown.inCoolDown() ||
                !CanStick(character) ||
                !character.m_nview.IsOwner() ||
                Player.m_localPlayer.IsStaggering() ||
                Player.m_localPlayer.InDodge())
            {
                Release();
                return false;
            }

            weaponForward = weaponForward.normalized;
            Vector3 offset = weaponPosition - character.transform.TransformPoint(anchor);
            // Positive when the weapon is pushed further in, negative when it is pulled back.
            float depth = Vector3.Dot(offset, weaponForward);
            Vector3 sidewaysOffset = offset - depth * weaponForward;
            if (depth < -WITHDRAW_DISTANCE || sidewaysOffset.magnitude > BREAK_DISTANCE)
            {
                Release();
                return false;
            }

            // The character follows the weapon sideways and backwards, but only gets pushed ahead once the weapon
            // is all the way in.
            offset = sidewaysOffset + Mathf.Min(depth, Mathf.Max(depth - MAX_PENETRATION, 0)) * weaponForward;
            // Characters are not lifted off or pressed into the ground.
            offset.y = 0;

            // The knockback of the stab would otherwise throw the character off the weapon.
            character.m_pushForce = Vector3.zero;
            Rigidbody body = character.m_body;
            Utils.Pull(
                body,
                body.position + offset,
                0,
                PULL_SPEED * Mathf.Clamp01(FULL_SPEED_MASS / body.mass),
                1,
                PULL_SMOOTH_DISTANCE,
                noUpForce: true);
            return true;
        }

        private static bool CanStick(Character target)
        {
            return target != null &&
                target.m_body != null &&
                target.m_nview != null &&
                target.m_nview.IsValid() &&
                !target.IsPlayer() &&
                !target.IsBoss() &&
                !target.IsDead() &&
                !target.IsAttached() &&
                target.GetStandingOnShip() == null &&
                target.m_body.mass <= MAX_MASS &&
                !WeaponCollision.IsFriendly(target);
        }
    }
}
