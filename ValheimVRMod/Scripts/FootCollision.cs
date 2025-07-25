using System.Linq;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts
{
    public class FootCollision : MonoBehaviour
    {
        private static readonly int[] NONATTACKABLE_LAYERS = {
            LayerUtils.WATERVOLUME_LAYER,
            LayerUtils.WATER,
            LayerUtils.UI_PANEL_LAYER,
            LayerUtils.CHARARCTER_TRIGGER,
            LayerUtils.ITEM_LAYER,
        };

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
            Character character = null;
            if (collider.gameObject.layer == LayerUtils.CHARACTER)
            {
                character = collider.GetComponentInParent<Character>();
            }

            if (character == null || character.gameObject == Player.m_localPlayer.gameObject || character.m_tamed)
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
                player.IsSitting() ||
                NONATTACKABLE_LAYERS.Contains(collider.gameObject.layer) ||
                collider.GetComponentInParent<Player>() == player)
            {
                return;
            }

            if (collider.gameObject.layer != LayerUtils.CHARACTER && !SteamVR_Actions.valheim_Use.GetState(SteamVR_Input_Sources.Any))
            {
                // When kicking anything other than a character, require pressing the grip so that the attack does not accidentally happen too easily.
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

            var isCurrentlySecondaryAttack = FistCollision.LocalPlayerSecondaryAttackCooldown <= 0;
            var item = Player.m_localPlayer.m_unarmedWeapon.m_itemData;
            var attack = isCurrentlySecondaryAttack ? item.m_shared.m_attack : item.m_shared.m_secondaryAttack;

            // Always use the duration of the primary attack for target cooldown to allow primary attack immediately following a secondary attack.
            // The secondary attack cooldown is managed by FistCollision.LocalPlayerSecondaryAttackCooldown  instead.
            if (!tryHitTarget(collider.gameObject, isCurrentlySecondaryAttack, WeaponUtils.GetAttackDuration(item.m_shared.m_attack), speed))
            {
                return;
            }

            FistCollision.LocalPlayerSecondaryAttackCooldown = WeaponUtils.GetAttackDuration(attack);

            StaticObjects.lastHitPoint = transform.position;
            StaticObjects.lastHitDir = velocity.normalized;
            StaticObjects.lastHitCollider = collider;

            attack.Start(Player.m_localPlayer, null, null, Player.m_localPlayer.m_animEvent, null, item, null, 0.0f, 0.0f);
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

        private bool tryHitTarget(GameObject target, bool isSecondaryAttack, float duration, float speed)
        {
            var attackTargetMeshCooldown = target.GetComponent<AttackTargetMeshCooldown>();
            if (attackTargetMeshCooldown == null)
            {
                attackTargetMeshCooldown = target.AddComponent<AttackTargetMeshCooldown>();
            }

            return isSecondaryAttack ? attackTargetMeshCooldown.tryTriggerSecondaryAttack(duration) : attackTargetMeshCooldown.tryTriggerPrimaryAttack(duration, speed);
        }
    }
}

