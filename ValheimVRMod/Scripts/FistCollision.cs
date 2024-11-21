using System.Linq;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;
using Valve.VR.InteractionSystem;

namespace ValheimVRMod.Scripts
{
    public class FistCollision : MonoBehaviour
    {
        private enum Grabbable
        {
            NONE = 0,
            ENVIRONMENT = 1,
            PICKABLE = 2,
            IMAGINARY_CLIMIBNG_HOLD = 3
        }

        private const float WEAPON_OFFSET = 0.125f;

        private bool isRightHand;
        private HandGesture handGesture;
        private PhysicsEstimator physicsEstimator { get { return isRightHand ? VRPlayer.rightHandPhysicsEstimator : VRPlayer.leftHandPhysicsEstimator; } }
        private SteamVR_Input_Sources inputSource { get { return isRightHand ? SteamVR_Input_Sources.RightHand : SteamVR_Input_Sources.LeftHand; } }
        private Hand thisHand {  get { return isRightHand ? VRPlayer.rightHand : VRPlayer.leftHand; } }

        private static float LocalPlayerSecondaryAttackCooldown = 0;

        private static readonly int[] NONATTACKABLE_LAYERS = {
            LayerUtils.WATERVOLUME_LAYER,
            LayerUtils.WATER,
            LayerUtils.UI_PANEL_LAYER,
            LayerUtils.CHARARCTER_TRIGGER,
        };

        public bool isGrabbingJumpingAid { get { return grabbed == Grabbable.ENVIRONMENT || grabbed == Grabbable.IMAGINARY_CLIMIBNG_HOLD; } }
        public Vector3 lastGrabOffsetFromHead { get; private set; }
        private Grabbable grabbed;
        private Pickable lastGrabbedPickable;
        private Vector3 lastGrabbedPoint;
        private float lastPetTime = float.NegativeInfinity;

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

            if (!SteamVR_Actions.valheim_Grab.GetState(inputSource) || !handGesture.isHandFree())
            {
                grabbed = Grabbable.NONE;
            }

            if (grabbed == Grabbable.PICKABLE)
            {
                if (Vector3.Distance(transform.position, lastGrabbedPoint) > 0.5f)
                {
                    grabbed = Grabbable.NONE;
                }
                else if (lastGrabbedPickable != null &&
                    Vector3.Dot(physicsEstimator.GetVelocity(), lastGrabOffsetFromHead) < -1)
                {
                    lastGrabbedPickable.Interact(Player.m_localPlayer, false, false);
                    lastGrabbedPickable = null;
                }
            }
        }

        private void OnTriggerStay(Collider collider)
        {
            if (handGesture.isHandFree() && SteamVR_Actions.valheim_Grab.GetStateDown(inputSource) && VRPlayer.vrCam != null)
            {
                Grabbable newGrabbable = GetGrabbable(collider.gameObject);
                if (newGrabbable != Grabbable.NONE)
                {
                    grabbed = newGrabbable;
                    lastGrabbedPoint = transform.position;
                    lastGrabOffsetFromHead = lastGrabbedPoint - VRPlayer.vrCam.transform.position;
                    thisHand.hapticAction.Execute(0, 0.25f, 100, 0.5f, inputSource);
                    if (grabbed == Grabbable.PICKABLE)
                    {
                        lastGrabbedPickable = collider.GetComponentInParent<Pickable>();
                    }
                }
            }

            if (grabbed == Grabbable.NONE && handGesture.isHandFree() && collider.gameObject.layer == LayerUtils.CHARACTER)
            {
                if (TryPet(collider))
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
        }

        private void OnTriggerEnter(Collider collider)
        {
            if (canAttackWithCollision())
            {
                tryHitCollider(collider, requireJab: false);
                return;
            }

            TryPushDoorOpen(collider);
        }

        private bool TryPet(Collider collider)
        {
            if (Player.m_localPlayer.IsRiding() || physicsEstimator.GetVelocity().magnitude < 0.5f)
            {
                return false;
            }
            
            var character = collider.GetComponentInParent<Character>();
            var tameable = character.GetComponent<Tameable>();
            if (!character.m_tamed || tameable == null)
            {
                return false;
            }
            
            thisHand.hapticAction.Execute(0, 0.25f, 100, 0.25f, inputSource);
            if (Time.time - lastPetTime > 3f)
            {
                lastPetTime = Time.time;
                tameable.m_petEffect.Create(tameable.transform.position, tameable.transform.rotation, null, 1f, -1);
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, character.GetHoverName() + " $hud_tamelove", 0, null);
            }

            return true;
        }

        private void TryPushDoorOpen(Collider collider)
        {
            var door = collider.GetComponentInParent<Door>();
            if (door == null || !door.CanInteract())
            {
                return;
            }

            Vector3 doorPushDirection = GetDoorPushDirection(door);
            Vector3 velocity = physicsEstimator.GetVelocity();
            
            if (Vector3.Angle(velocity, doorPushDirection) < 30 && Vector3.Dot(velocity, doorPushDirection) > 2f)
            {
                door.Interact(Player.m_localPlayer, false, false);
            }
        }

        private Vector3 GetDoorPushDirection(Door door)
        {
            bool isClosed = (door.m_nview.GetZDO().GetInt(ZDOVars.s_state, 0) == 0);
            if (isClosed)
            {
                return Vector3.Dot(door.transform.position - Player.m_localPlayer.transform.position, door.transform.forward) > 0 ?
                    door.transform.forward :
                    -door.transform.forward;
            }

            return -door.transform.right;
        }

        private Grabbable GetGrabbable(GameObject target)
        {
            if (!handGesture.isHandFree() || !SteamVR_Actions.valheim_Grab.GetStateDown(inputSource))
            {
                return Grabbable.NONE;
            }

            var pickable = target.GetComponentInParent<Pickable>();
            if (pickable != null && pickable.CanBePicked())
            {
                return Grabbable.PICKABLE;
            }

            if (target.layer == LayerUtils.TERRAIN ||
                target.layer == LayerUtils.PIECE ||
                target.layer == LayerUtils.STATIC_SOLID ||
                target.GetComponentInParent<StaticPhysics>() != null ||
                target.GetComponentInParent<TreeBase>() != null)
            {
                return Grabbable.ENVIRONMENT;
            }

            if (target.layer == 0)
            {
                var piece = target.GetComponentInParent<Piece>();
                if (piece != null && piece.gameObject.layer == LayerUtils.PIECE)
                {
                    return Grabbable.ENVIRONMENT;
                }
            }

            if (VHVRConfig.IsGesturedJumpEnabled() &&
                target.layer == LayerUtils.CHARACTER &&
                Valve.VR.InteractionSystem.Player.instance.eyeHeight < VRPlayer.referencePlayerHeight * 0.9f)
            {
                var leftHandOffset = VRPlayer.instance.transform.InverseTransformVector(VRPlayer.leftHand.transform.position - VRPlayer.vrCam.transform.position);
                var rightHandOffset = VRPlayer.instance.transform.InverseTransformVector(VRPlayer.rightHand.transform.position - VRPlayer.vrCam.transform.position);
                if (leftHandOffset.y > 0.25f &&
                    rightHandOffset.y > 0.25f &&
                    rightHandOffset.x - leftHandOffset.x < 0.15f &&
                    !Utilities.Pose.isBehindBack(VRPlayer.leftHand.transform) &&
                    !Utilities.Pose.isBehindBack(VRPlayer.rightHand.transform))
                {
                    return Grabbable.IMAGINARY_CLIMIBNG_HOLD;
                }
            }

            return Grabbable.NONE;
        }

        private void tryHitCollider(Collider collider, bool requireJab)
        {
            if (!canAttackWithCollision())
            {
                return;
            }

            if (collider.gameObject.layer == LayerUtils.TERRAIN && !SteamVR_Actions.valheim_Grab.GetState(inputSource))
            {
                // Prevent hitting terrain too easily.
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
                    (handGesture.isHandFree() ? RoomscaleSecondaryAttackUtils.IsHook(physicsEstimator) :
                    RoomscaleSecondaryAttackUtils.IsSecondaryAttack(physicsEstimator, physicsEstimator));
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
                thisHand.hapticAction.Execute(0, 0.2f, 100, 0.5f, inputSource);
            }
        }

        private bool tryHitTarget(GameObject target, bool isSecondaryAttack, float duratrion, float speed)
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
            if (!VRPlayer.inFirstPerson || transform.parent == null || grabbed != Grabbable.NONE)
            {
                return false;
            }

            if (handGesture.isHandFree())
            {
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

            return SteamVR_Actions.valheim_Grab.GetState(inputSource);
        }
    }
}

