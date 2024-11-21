using System.Linq;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts
{
    public class FootCollision : MonoBehaviour
    {
        private static readonly int[] NONATTACKABLE_LAYERS = {
            LayerUtils.WATERVOLUME_LAYER,
            LayerUtils.WATER,
            LayerUtils.UI_PANEL_LAYER,
            LayerUtils.CHARARCTER_TRIGGER,
        };

        private PhysicsEstimator physicsEstimator;

        private void Awake()
        {
            if (VHVRConfig.ShowDebugColliders())
            {
                WeaponUtils.CreateDebugSphere(transform);
            }
        }


        private void OnRenderObject()
        {
            // The collision object is affected by physics so its position and rotaiton
            // may need to force-updated to counteract the physics.
            transform.localPosition = Vector3.zero;
        }

        private void OnTriggerEnter(Collider collider)
        {
            if (!VRPlayer.inFirstPerson ||
                transform.parent == null ||
                !VHVRConfig.TrackFeet() ||
                collider.gameObject.layer == LayerUtils.TERRAIN ||
                collider.GetComponentInParent<Player>() == Player.m_localPlayer ||
                Player.m_localPlayer == null ||
                Player.m_localPlayer.IsRiding())
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
            if (speed < 6f)
            {
                return;
            }

            var item = Player.m_localPlayer.m_unarmedWeapon.m_itemData;
            var attack = item.m_shared.m_secondaryAttack;

            // Always use the duration of the primary attack for target cooldown to allow primary attack immediately following a secondary attack.
            // The secondary attack cooldown is managed by LocalPlayerSecondaryAttackCooldown in this class instead.
            if (!tryHitTarget(collider.gameObject, WeaponUtils.GetAttackDuration(item.m_shared.m_attack), speed))
            {
                return;
            }

            FistCollision.LocalPlayerSecondaryAttackCooldown = WeaponUtils.GetAttackDuration(attack);

            StaticObjects.lastHitPoint = transform.position;
            StaticObjects.lastHitDir = velocity.normalized;
            StaticObjects.lastHitCollider = collider;

            attack.Start(Player.m_localPlayer, null, null, Player.m_localPlayer.m_animEvent, null, item, null, 0.0f, 0.0f);
        }

        private bool tryHitTarget(GameObject target, float duration, float speed)
        {
            // ignore certain Layers
            if (NONATTACKABLE_LAYERS.Contains(target.layer))
            {
                return false;
            }

            var attackTargetMeshCooldown = target.GetComponent<AttackTargetMeshCooldown>();
            if (attackTargetMeshCooldown == null)
            {
                attackTargetMeshCooldown = target.AddComponent<AttackTargetMeshCooldown>();
            }

            return attackTargetMeshCooldown.tryTriggerSecondaryAttack(duration);
        }

        public void setColliderParent(Transform parent)
        {
            transform.parent = parent;
            transform.localScale = Vector3.one * 0.3f;
        }
    }
}

