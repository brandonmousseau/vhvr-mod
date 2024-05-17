using System.Linq;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts
{
    public class FistCollision : MonoBehaviour
    {
        private const float MIN_SPEED = 5f;
        private const float WEAPON_OFFSET = 0.125f;

        private bool isRightHand;
        private HandGesture handGesture;
        private PhysicsEstimator physicsEstimator { get { return isRightHand ? VRPlayer.rightHandPhysicsEstimator : VRPlayer.leftHandPhysicsEstimator; } }

        private static float LocalPlayerSecondaryAttackCooldown = 0;

        private static readonly int[] ignoreLayers = {
            LayerUtils.WATERVOLUME_LAYER,
            LayerUtils.WATER,
            LayerUtils.UI_PANEL_LAYER,
            LayerUtils.CHARARCTER_TRIGGER
        };

        private EquipType? currentEquipType = null;
        private Vector3 desiredPosition;
        private Quaternion desiredRotation;

        private void Awake()
        {
            if (VHVRConfig.ShowDebugColliders())
            {
                WeaponUtils.CreateDebugSphere(transform);
            }
        }

        void FixedUpdate()
        {
            // There are two instances of fist collision, only have the right hand one update the static field LocalPlayerSecondaryAttackCooldown
            // so that it is not double-updated.
            if (LocalPlayerSecondaryAttackCooldown > 0 && isRightHand)
            {
                LocalPlayerSecondaryAttackCooldown -= Time.fixedDeltaTime;
            }
        }

        private void OnTriggerEnter(Collider collider)
        {
            if (!canAttackWithCollision())
            {
                return;
            }

            var maybePlayer = collider.GetComponentInParent<Player>();
            if (maybePlayer != null && maybePlayer == Player.m_localPlayer)
            {
                return;
            }

            if (Player.m_localPlayer.IsRiding())
            {
                var targetCharacter = collider.GetComponentInChildren<Character>();
                var doodadController = Player.m_localPlayer.GetDoodadController();
                if (doodadController is Sadle && ((Sadle)doodadController).m_monsterAI.m_character == targetCharacter)
                {
                    // Do not attack the animal that the player is riding.
                    return;
                }
            }

            if (!hasMomentum())
            {
                return;
            }

            bool isCurrentlySecondaryAttack = LocalPlayerSecondaryAttackCooldown <= 0 && RoomscaleSecondaryAttackUtils.IsSecondaryAttack(physicsEstimator, physicsEstimator);
            bool usingWeapon = hasDualWieldingWeaponEquipped();
            var item = usingWeapon ? Player.m_localPlayer.GetRightItem() : Player.m_localPlayer.m_unarmedWeapon.m_itemData;
            Attack primaryAttack = item.m_shared.m_attack;
            Attack attack = isCurrentlySecondaryAttack ? item.m_shared.m_secondaryAttack : primaryAttack;
            if (usingWeapon)
            {
                attack = attack.Clone();
            }

            // Always use the duration of the primary attack for target cooldown to allow primary attack immediately following a secondary attack.
            // The secondary attack cooldown is managed by LocalPlayerSecondaryAttackCooldown in this class instead.
            if (!tryHitTarget(collider.gameObject, isCurrentlySecondaryAttack, WeaponUtils.GetAttackDuration(primaryAttack)))
            {
                return;
            }

            if (isCurrentlySecondaryAttack)
            {
                LocalPlayerSecondaryAttackCooldown = WeaponUtils.GetAttackDuration(attack);
            }

            StaticObjects.lastHitPoint = transform.position;
            StaticObjects.lastHitDir = physicsEstimator.GetVelocity().normalized;
            StaticObjects.lastHitCollider = collider;

            if (attack.Start(Player.m_localPlayer, null, null, Player.m_localPlayer.m_animEvent,
                null, item, null, 0.0f, 0.0f))
            {
                if (isRightHand)
                {
                    VRPlayer.rightHand.hapticAction.Execute(0, 0.2f, 100, 0.5f, SteamVR_Input_Sources.RightHand);
                }
                else
                {
                    VRPlayer.leftHand.hapticAction.Execute(0, 0.2f, 100, 0.5f, SteamVR_Input_Sources.LeftHand);
                }
            }
        }

        private bool tryHitTarget(GameObject target, bool isSecondaryAttack, float duratrion)
        {

            // ignore certain Layers
            if (ignoreLayers.Contains(target.layer))
            {
                return false;
            }

            var attackTargetMeshCooldown = target.GetComponent<AttackTargetMeshCooldown>();
            if (attackTargetMeshCooldown == null)
            {
                attackTargetMeshCooldown = target.AddComponent<AttackTargetMeshCooldown>();
            }

            return isSecondaryAttack ? attackTargetMeshCooldown.tryTriggerSecondaryAttack(duratrion) : attackTargetMeshCooldown.tryTriggerPrimaryAttack(duratrion);
        }

        private void OnRenderObject()
        {

            refreshColliderData();

            // The collision object is affected by physics so its position and rotaiton
            // may need to force-updated to counteract the physics.
            transform.localPosition = desiredPosition;
            transform.localRotation = desiredRotation;
        }

        public void setColliderParent(Transform parent, HandGesture handGesture, bool isRightHand)
        {
            transform.parent = parent;
            this.handGesture = handGesture;
            this.isRightHand = isRightHand;
            currentEquipType = null;
        }

        private void refreshColliderData()
        {
            if (!Player.m_localPlayer || EquipScript.getRight() == currentEquipType)
            {
                return;
            }

            currentEquipType = EquipScript.getRight();

            var colliderData =
                WeaponUtils.GetDualWieldLeftHandColliderData(Player.m_localPlayer?.GetRightItem());

            desiredPosition =
                isRightHand ?
                Vector3.Reflect(colliderData.pos, Vector3.right) :
                colliderData.pos;
            desiredRotation = Quaternion.Euler(colliderData.euler);
            transform.localScale = colliderData.scale;
        }

        private bool canAttackWithCollision()
        {
            if (!VRPlayer.inFirstPerson || transform.parent == null)
            {
                return false;
            }

            if (handGesture.isHandFree())
            {
                SteamVR_Input_Sources inputSource = isRightHand ? SteamVR_Input_Sources.RightHand : SteamVR_Input_Sources.LeftHand;
                return SteamVR_Actions.valheim_Grab.GetState(inputSource);
            }

            return hasDualWieldingWeaponEquipped();
        }

        public bool hasMomentum()
        {
            var handVelocity = physicsEstimator.GetVelocity();
            var minSpeed = MIN_SPEED * VHVRConfig.SwingSpeedRequirement();

            if (EquipScript.getRight() == EquipType.Claws || !hasDualWieldingWeaponEquipped())
            {
                return handVelocity.magnitude >= minSpeed;
            }

            var weaponOffsetDirection = (transform.position - physicsEstimator.transform.position).normalized;
            var weaponVelocity =
                WeaponUtils.GetWeaponVelocity(
                    handVelocity, physicsEstimator.GetAngularVelocity(), weaponOffsetDirection * WEAPON_OFFSET);

            return weaponVelocity.magnitude >= minSpeed;
        }

        public static bool hasDualWieldingWeaponEquipped()
        {
            var equipType = EquipScript.getRight();
            return equipType.Equals(EquipType.Claws) ||
                equipType.Equals(EquipType.DualAxes) ||
                equipType.Equals(EquipType.DualKnives);
        }

        public bool blockingWithFist()
        {
            if (!handGesture.isHandFree() && !hasDualWieldingWeaponEquipped())
            {
                return false;
            }

            SteamVR_Input_Sources inputSource = isRightHand ? SteamVR_Input_Sources.RightHand : SteamVR_Input_Sources.LeftHand;

            return SteamVR_Actions.valheim_Grab.GetState(inputSource);
        }
    }
}

