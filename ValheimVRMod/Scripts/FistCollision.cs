using System.Linq;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts
{
    public class FistCollision : MonoBehaviour
    {
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

        private void OnTriggerStay(Collider collider)
        {
            if (!handGesture.isHandFree() || collider.gameObject.layer != LayerUtils.CHARACTER)
            {
                return;
            }

            var cooldown = collider.GetComponent<AttackTargetMeshCooldown>();
            if (cooldown != null && cooldown.inCoolDown())
            {
                return;
            }

            tryHitCollider(collider, requireJab: true);
        }

        private void OnTriggerEnter(Collider collider)
        {
            tryHitCollider(collider, requireJab: false);
        }


        private void tryHitCollider(Collider collider, bool requireJab)
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

            if (!hasMomentum(out float speed, out bool isJab))
            {
                return;
            }
            if (requireJab && !isJab)
            {
                return;
            }

            ItemDrop.ItemData item;
            bool isCurrentlySecondaryAttack = false;
            Attack attack;
            if (holdingTorchAsNonDominantHand())
            {
                item = Player.m_localPlayer.GetLeftItem();
                attack = item.m_shared.m_attack.Clone();
            }
            else
            {
                isCurrentlySecondaryAttack =
                    LocalPlayerSecondaryAttackCooldown <= 0 &&
                    RoomscaleSecondaryAttackUtils.IsSecondaryAttack(physicsEstimator, physicsEstimator);
                if (hasDualWieldingWeaponEquipped())
                {
                    item = Player.m_localPlayer.GetRightItem();
                    attack =
                        (isCurrentlySecondaryAttack ? item.m_shared.m_attack : item.m_shared.m_secondaryAttack).Clone();
                }
                else
                {
                    item = Player.m_localPlayer.m_unarmedWeapon.m_itemData;
                    attack =
                        isCurrentlySecondaryAttack ? item.m_shared.m_attack : item.m_shared.m_secondaryAttack;
                }
            }

            // Always use the duration of the primary attack for target cooldown to allow primary attack immediately following a secondary attack.
            // The secondary attack cooldown is managed by LocalPlayerSecondaryAttackCooldown in this class instead.
            if (!tryHitTarget(collider.gameObject, isCurrentlySecondaryAttack, WeaponUtils.GetAttackDuration(item.m_shared.m_attack), speed))
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

        private bool tryHitTarget(GameObject target, bool isSecondaryAttack, float duratrion, float speed)
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

            return isSecondaryAttack ? attackTargetMeshCooldown.tryTriggerSecondaryAttack(duratrion) : attackTargetMeshCooldown.tryTriggerPrimaryAttack(duratrion, speed);
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
            var newEquipType = holdingTorchAsNonDominantHand() ? EquipType.Torch : EquipScript.getRight();

            if (!Player.m_localPlayer || newEquipType == currentEquipType)
            {
                return;
            }

            currentEquipType = newEquipType;

            var colliderData = WeaponUtils.GetDualWieldLeftHandColliderData(newEquipType);

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

            return hasDualWieldingWeaponEquipped() || holdingTorchAsNonDominantHand();
        }

        private bool holdingTorchAsNonDominantHand()
        {
            if (isRightHand ^ VHVRConfig.LeftHanded())
            {
                return false;
            }
            return EquipScript.getLeft() == EquipType.Torch;
        }

        public bool hasMomentum(out float speed, out bool isJab)
        {
            var handVelocity = physicsEstimator.GetVelocity();
            if (handGesture.isHandFree())
            {
                speed = handVelocity.magnitude;
                isJab = Vector3.Angle(isRightHand ? VRPlayer.rightHandBone.up : VRPlayer.leftHandBone.up, handVelocity) < 30f && speed > 3f;
                return speed > VHVRConfig.SwingSpeedRequirement() * 0.45f;
            }

            var weaponOffsetDirection = (transform.position - physicsEstimator.transform.position).normalized;
            var weaponVelocity =
                WeaponUtils.GetWeaponVelocity(
                    handVelocity, physicsEstimator.GetAngularVelocity(), weaponOffsetDirection * WEAPON_OFFSET);

            speed = weaponVelocity.magnitude;
            isJab = false;
            return speed >= VHVRConfig.SwingSpeedRequirement();
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

