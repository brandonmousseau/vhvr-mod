using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts
{
    public class FootCollision : MonoBehaviour
    {
        private PhysicsEstimator physicsEstimator;
        private GameObject debugColliderIndicator;

        private void Awake()
        {
            // TODO: Consider abstracting the shared logic between FootCollision and FistCollision into an abstract base class.
            if (VHVRConfig.ShowDebugColliders())
            {
                debugColliderIndicator = WeaponUtils.CreateDebugBox(transform);
            }
        }

        private void OnRenderObject()
        {
            // The collision object is affected by physics so its position and rotaiton
            // may need to force-updated to counteract the physics.
            transform.localPosition = new Vector3(0, 0.125f, 0);
            transform.localRotation = Quaternion.identity;

            if (VHVRConfig.ShowDebugColliders())
            {
                if (debugColliderIndicator == null)
                {
                    debugColliderIndicator = WeaponUtils.CreateDebugBox(transform);
                }
            }
            else if (debugColliderIndicator != null)
            {
                Destroy(debugColliderIndicator);
            }
        }

        private void OnTriggerEnter(Collider collider)
        {
            TryHit(collider);
        }

        private void OnTriggerStay(Collider collider)
        {
            if (IsRollingSnowball(collider))
            {
                // The snowball is big enough that a foot can end up inside it without ever entering it.
                TryHit(collider);
                return;
            }

            Character character = null;
            if (collider.gameObject.layer == LayerUtils.CHARACTER)
            {
                character = collider.GetComponentInParent<Character>();
            }

            if (character == null || character.gameObject == Player.m_localPlayer.gameObject || character.IsTamed())
            {
                return;
            }

            var cooldown = collider.GetComponent<AttackTargetMeshCooldown>();
            if (cooldown != null && cooldown.inCoolDown())
            {
                return;
            }

            TryHit(collider);
        }

        private void TryHit(Collider collider) {
            Player player = Player.m_localPlayer;
            if (!VRPlayer.inFirstPerson ||
                transform.parent == null ||
                !VHVRConfig.TrackFeet() ||
                player == null ||
                player.IsRiding() ||
                player.IsSitting())
            {
                return;
            }

            if (collider.gameObject.layer != LayerUtils.CHARACTER &&
                !IsRollingSnowball(collider) &&
                !SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.Any))
            {
                // When kicking anything other than a character or the rolling snowball, require pressing the grip so
                // that the attack does not accidentally happen too easily.
                return;
            }

            Vector3 step = VRPlayer.leftFoot.position - VRPlayer.rightFoot.position;
            if (Mathf.Abs(Vector3.Dot(step, VRPlayer.vrCam.transform.up)) < 0.125f && step.magnitude < 0.25f)
            {
                return;
            }

            if (physicsEstimator == null)
            {
                physicsEstimator = GetComponentInParent<PhysicsEstimator>();
            }

            var velocity = physicsEstimator.GetVelocity();
            var clampedLocalVelocity = VRPlayer.vrCam.transform.parent.InverseTransformVector(velocity);
            if (clampedLocalVelocity.y < 0)
            {
                clampedLocalVelocity.y = 0;
            }
            var speed = clampedLocalVelocity.magnitude;
            if (speed < 1 || speed < VHVRConfig.SwingSpeedRequirement())
            {
                return;
            }

            Kick(collider, transform.position, velocity, speed);
        }

        // Attacks the target with a kick, which is the secondary attack of the equipped fist weapon or, without one,
        // of the unarmed weapon, as in vanilla, where the fist weapon's damage makes kicks hit much harder. Falls
        // back to the primary attack while the kick is still cooling down. Also used by weapons without a real
        // attack against hostiles (the snow shovel). Returns whether the attack was started.
        public static bool Kick(Collider collider, Vector3 hitPoint, Vector3 velocity, float speed)
        {
            // Filtered here rather than in the callers since this is shared with weapons that have no real
            // attack of their own (the snow shovel), whose own attack path does no layer filtering at all.
            if (LayerUtils.IsNonAttackableLayer(collider.gameObject.layer) ||
                collider.GetComponentInParent<Player>() == Player.m_localPlayer)
            {
                return false;
            }

            var isCurrentlySecondaryAttack = FistCollision.LocalPlayerSecondaryAttackCooldown <= 0;
            ItemDrop.ItemData item;
            Attack attack;
            if (EquipScript.CurrentMainHandEquipType() == EquipType.Claws)
            {
                item = Player.m_localPlayer.GetRightItem();
                attack = (isCurrentlySecondaryAttack ? item.m_shared.m_secondaryAttack : item.m_shared.m_attack).Clone();
            }
            else
            {
                item = Player.m_localPlayer.m_unarmedWeapon.m_itemData;
                attack = isCurrentlySecondaryAttack ? item.m_shared.m_secondaryAttack : item.m_shared.m_attack;
            }

            // Always use the duration of the primary attack for target cooldown to allow primary attack immediately following a secondary attack.
            // The secondary attack cooldown is managed by FistCollision.LocalPlayerSecondaryAttackCooldown  instead.
            if (!WeaponCollision.CanHitCollider(collider, item) ||
                !tryHitTarget(collider.gameObject, isCurrentlySecondaryAttack, WeaponUtils.GetAttackDuration(item.m_shared.m_attack), speed))
            {
                return false;
            }

            FistCollision.LocalPlayerSecondaryAttackCooldown = WeaponUtils.GetAttackDuration(attack);

            StaticObjects.lastHitPoint = hitPoint;
            StaticObjects.lastHitDir = velocity.normalized;
            StaticObjects.lastHitCollider = collider;

            return attack.Start(Player.m_localPlayer, null, null, Player.m_localPlayer.m_animEvent, null, item, null, 0.0f, 0.0f);
        }

        void Destroy()
        {
            if (debugColliderIndicator != null) Destroy(debugColliderIndicator);
        }

        public void setColliderParent(Transform parent)
        {
            transform.parent = parent;
            transform.localScale = new Vector3(0.22f, 0.7f, 0.375f);
        }

        // The big snowball rolling around in the Deep North, which is a moving target rather than scenery and
        // therefore worth kicking without having to ask for it using the grip.
        private static bool IsRollingSnowball(Collider collider)
        {
            return collider.GetComponentInParent<SnowRoller>() != null;
        }

        private static bool tryHitTarget(GameObject target, bool isSecondaryAttack, float duration, float speed)
        {
            var attackTargetMeshCooldown = target.GetComponent<AttackTargetMeshCooldown>();
            if (attackTargetMeshCooldown == null)
            {
                attackTargetMeshCooldown = target.AddComponent<AttackTargetMeshCooldown>();
            }
            attackTargetMeshCooldown.showOutline = VHVRConfig.ShowNonTerrainAttackOutline();
            return isSecondaryAttack ? attackTargetMeshCooldown.tryTriggerSecondaryAttack(duration) : attackTargetMeshCooldown.tryTriggerPrimaryAttack(duration, speed);
        }
    }
}

