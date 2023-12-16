using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ValheimVRMod.Scripts.Block;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;
using Valve.VR.InteractionSystem;

namespace ValheimVRMod.Scripts
{
    public class FistCollision : MonoBehaviour
    {
        public static FistCollision instance;
        private const float MIN_SPEED = 5f;
        private GameObject colliderParent = null;
        private bool isRightHand;
        private HandGesture handGesture;
        private Hand thisHand { get { return isRightHand ? VRPlayer.rightHand : VRPlayer.leftHand; } }
        private PhysicsEstimator physicsEstimator { get { return isRightHand ? VRPlayer.rightHandPhysicsEstimator : VRPlayer.leftHandPhysicsEstimator; } }

        private float fistRotation = 0;
        private static float LocalPlayerSecondaryAttackCooldown = 0;

        private static readonly int[] ignoreLayers = {
            LayerUtils.WATERVOLUME_LAYER,
            LayerUtils.WATER,
            LayerUtils.UI_PANEL_LAYER,
            LayerUtils.CHARARCTER_TRIGGER
        };

        private void Awake()
        {
            colliderParent = new GameObject();
            instance = this;
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
            if (!isCollisionAllowed())
            {
                return;
            }

            var maybePlayer = collider.GetComponentInParent<Player>();

            if (maybePlayer != null && maybePlayer == Player.m_localPlayer)
            {
                return;
            }

            if (!hasMomentum())
            {
                return;
            }

            bool isCurrentlySecondaryAttack = LocalPlayerSecondaryAttackCooldown <= 0 && RoomscaleSecondaryAttackUtils.IsSecondaryAttack(physicsEstimator, physicsEstimator);
            bool usingWeapon = usingClaws() || usingDualKnives();
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
            transform.SetParent(colliderParent.transform);
            transform.localRotation = Quaternion.identity;
            transform.localPosition = Vector3.zero;
            transform.localScale = Vector3.one;
            transform.SetParent(Player.m_localPlayer.transform, true);
        }

        public void setColliderParent(Transform obj, bool rightHand)
        {

            isRightHand = rightHand;
            handGesture = obj.GetComponent<HandGesture>();
            colliderParent = new GameObject();
            colliderParent.transform.parent = obj;
            colliderParent.transform.localPosition = new Vector3(0, 0.003f, 0.00016f);
            colliderParent.transform.localScale *= 0.45f;
        }

        private bool isCollisionAllowed()
        {

            SteamVR_Input_Sources inputSource;

            if (isRightHand)
            {
                inputSource = SteamVR_Input_Sources.RightHand;
            }
            else
            {
                inputSource = SteamVR_Input_Sources.LeftHand;
            }

            var isUnequipedWithFistGesture =
                handGesture.isUnequiped()
                && SteamVR_Actions.valheim_Grab.GetState(inputSource);

            var isEquippedWithFistGesture =
                (usingClaws() || usingDualKnives()) && SteamVR_Actions.valheim_Grab.GetState(inputSource);

            return VRPlayer.inFirstPerson && colliderParent != null &&
                   (isEquippedWithFistGesture || isUnequipedWithFistGesture);
        }


        public bool hasMomentum()
        {

            if (!VHVRConfig.WeaponNeedsSpeed())
            {
                return true;
            }

            return physicsEstimator.GetVelocity().magnitude > MIN_SPEED;
        }

        public bool usingClaws()
        {
            var item = EquipScript.getRight();
            return item.Equals(EquipType.Claws) || item.Equals(EquipType.DualKnives);
        }

        public bool usingDualKnives()
        {
            var item = EquipScript.getRight();
            if (item.Equals(EquipType.DualKnives))
            {
                transform.transform.localRotation = Quaternion.Euler(new Vector3(fistRotation, 0, 0));
            }
            else
            {
                transform.transform.localRotation = Quaternion.identity;
            }
            return item.Equals(EquipType.DualKnives);
        }

        public bool usingFistWeapon()
        {
            SteamVR_Input_Sources inputSource;

            if (isRightHand)
            {
                inputSource = SteamVR_Input_Sources.RightHand;
            }
            else
            {
                inputSource = SteamVR_Input_Sources.LeftHand;
            }

            var isUnequipedWithFistGesture =
                handGesture.isUnequiped()
                && SteamVR_Actions.valheim_Grab.GetState(inputSource);

            var isEquippedWithFistGesture =
                (usingClaws() || usingDualKnives()) && SteamVR_Actions.valheim_Grab.GetState(inputSource);

            return (isEquippedWithFistGesture || isUnequipedWithFistGesture);
        }
    }
}

