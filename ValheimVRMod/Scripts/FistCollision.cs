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

        public static float LocalPlayerSecondaryAttackCooldown = 0;
        public static bool ShouldSecondaryKnifeHoldInverse { get; private set; }

        private static readonly int[] NONATTACKABLE_LAYERS = {
            LayerUtils.WATERVOLUME_LAYER,
            LayerUtils.WATER,
            LayerUtils.UI_PANEL_LAYER,
            LayerUtils.CHARARCTER_TRIGGER,
            LayerUtils.ITEM_LAYER,
        };

        public bool isGrabbingJumpingAid { get { return lastGrabbedType == Grabbable.ENVIRONMENT || lastGrabbedType == Grabbable.IMAGINARY_CLIMIBNG_HOLD; } }
        public Vector3 lastGrabOffsetFromHead { get; private set; }
        private Grabbable lastGrabbedType;
        private Pickable grabbedPickable;
        private Vector3 lastGrabbedPoint;
        private float lastPetTime = float.NegativeInfinity;

        private EquipType? currentEquipType = null;
        private Vector3 desiredPosition;
        private Quaternion desiredRotation;
        private GameObject debugColliderIndicator;
        private WeaponColData colliderData;

        private void Awake()
        {
            if (VHVRConfig.ShowDebugColliders())
            {
                debugColliderIndicator = WeaponUtils.CreateDebugSphere(transform);
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

            if (handGesture == null)
            {
                return;
            }

            if (!SteamVR_Actions.valheim_Grab.GetState(inputSource) || !handGesture.isHandFree())
            {
                lastGrabbedType = Grabbable.NONE;
            }

            if (lastGrabbedType == Grabbable.PICKABLE)
            {
                if (Vector3.Distance(transform.position, lastGrabbedPoint) > 0.5f)
                {
                    grabbedPickable = null;
                }
                else if (grabbedPickable != null && physicsEstimator.GetVelocity().magnitude > 0.5f)
                {
                    grabbedPickable.Interact(Player.m_localPlayer, false, false);
                    grabbedPickable = null;
                }
            }
        }

        private void OnTriggerStay(Collider collider)
        {
            if (handGesture == null)
            {
                return;
            }

            if (handGesture.isHandFree() && SteamVR_Actions.valheim_Grab.GetStateDown(inputSource) && VRPlayer.vrCam != null)
            {
                Grabbable newGrabbable = GetGrabbable(collider.gameObject);
                if (newGrabbable != Grabbable.NONE)
                {
                    lastGrabbedType = newGrabbable;
                    lastGrabbedPoint = transform.position;
                    lastGrabOffsetFromHead = lastGrabbedPoint - VRPlayer.vrCam.transform.position;
                    thisHand.hapticAction.Execute(0, 0.25f, 100, 0.5f, inputSource);
                    if (lastGrabbedType == Grabbable.PICKABLE)
                    {
                        grabbedPickable = collider.GetComponentInParent<Pickable>();
                    }
                }
            }

            if (lastGrabbedType != Grabbable.NONE || !handGesture.isHandFree())
            {
                return;
            }

            Character character = null;
            if (collider.gameObject.layer == LayerUtils.CHARACTER)
            {
                character = collider.GetComponentInParent<Character>();
            }

            if (TryPet(collider, character))
            {
                return;
            }

            if (character == null || character.gameObject == Player.m_localPlayer.gameObject)
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
            if (handGesture == null)
            {
                return;
            }

            if (canAttackWithCollision())
            {
                // When using bare hands or claws to attack anything other than an enemy character,
                // require both pressing trigger and grip so that the attack does not accidentally happen too easily.
                if (handGesture.isHandFree() && !SteamVR_Actions.valheim_Use.GetState(inputSource) && !SteamVR_Actions.valheim_UseLeft.GetState(inputSource)) {
                    if (collider.gameObject.layer != LayerUtils.CHARACTER)
                    {
                        return;
                    }
                    Character character = collider.GetComponentInParent<Character>();
                    if (character == null || WeaponCollision.IsFriendly(character))
                    {
                        return;
                    }
                }
                tryHitCollider(collider, requireJab: false);
                return;
            }

            TryPushDoorOpen(collider);
        }

        void Destroy()
        {
            if (debugColliderIndicator != null) Destroy(debugColliderIndicator);
        }

        private bool TryPet(Collider collider, Character character)
        {
            string hoverName;
            Transform target = collider.transform;
            EffectList petEffect = null;
            if (character != null)
            {
                if (character.gameObject == Player.m_localPlayer.gameObject ||
                    !character.m_tamed ||
                    !WeaponCollision.IsFriendly(character))
                {
                    return false;
                }

                hoverName = character.GetHoverName();
                target = character.transform; 
                Tameable tameable = character.GetComponentInChildren<Tameable>();
                if (tameable != null)
                {
                    petEffect = tameable.m_petEffect;
                }
            }
            else if (collider.gameObject.layer != 0)
            {
                return false;
            }
            else 
            {
                Trader trader = collider.GetComponent<Trader>();
                if (trader != null)
                {
                    hoverName = trader.GetHoverName();
                    target = trader.transform;
                    if (hoverName == "")
                    {
                        petEffect = trader.m_randomTalkFX;
                    }
                }
                else if (collider.transform.parent == null)
                {
                    return false;
                }
                else
                {
                    Petable petable = collider.transform.parent.GetComponent<Petable>();
                    if (petable != null)
                    {
                        hoverName = petable.GetHoverName();
                        petEffect = petable.m_petEffect;
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            if (Player.m_localPlayer.IsRiding() || physicsEstimator.GetVelocity().magnitude < 0.5f)
            {
                return true;
            }

            thisHand.hapticAction.Execute(0, 0.25f, 100, 0.25f, inputSource);

            if (Time.time - lastPetTime > 3f)
            {
                lastPetTime = Time.time;
                if (petEffect != null)
                {
                    petEffect.Create(target.position, target.rotation, null, 1f, -1);
                }
                if (hoverName != "")
                {
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, hoverName + " $hud_tamelove", 0, null);
                }
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

            switch (target.layer)
            {
                case LayerUtils.TERRAIN:
                case LayerUtils.PIECE:
                case LayerUtils.STATIC_SOLID:
                    return Grabbable.ENVIRONMENT;
                case 0:
                    Transform parent = target.transform.parent;
                    if (parent == null)
                    {
                        break;
                    }
                    if (parent.GetComponent<StaticPhysics>() != null ||
                        parent.GetComponent<Piece>() != null ||
                        parent.GetComponent<TreeBase>() != null ||
                        parent.GetComponent<RuneStone>() != null ||
                        parent.GetComponent<Vegvisir>() != null ||
                        parent.GetComponent<OfferingBowl>() != null)
                    {
                        return Grabbable.ENVIRONMENT;
                    }
                    
                    // LogUtils.LogDebug("Cannot grab " + parent.name + " on layer " + parent.gameObject.layer);
                    // LogUtils.LogComponents(parent.transform);
                    break;
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
            if (NONATTACKABLE_LAYERS.Contains(collider.gameObject.layer))
            {
                return;
            }
            if (collider.gameObject.layer == LayerUtils.TERRAIN && !SteamVR_Actions.valheim_Grab.GetState(inputSource))
            {
                // Prevent hitting terrain too easily.
                return;
            }
            if (collider.GetComponentInParent<Player>() == Player.m_localPlayer)
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
            if (holdingSecondaryWeapon())
            {
                if (EquipScript.getLeft() != EquipType.Torch &&
                    EquipScript.getRight() != EquipType.Torch &&
                    EquipScript.getRight() != EquipType.None)
                {
                    item = Player.m_localPlayer.GetRightItem();
                }
                else
                {
                    item = Player.m_localPlayer.GetLeftItem();
                }
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

        private bool tryHitTarget(GameObject target, bool isSecondaryAttack, float duration, float speed)
        {
            var attackTargetMeshCooldown = target.GetComponent<AttackTargetMeshCooldown>();
            if (attackTargetMeshCooldown == null)
            {
                attackTargetMeshCooldown = target.AddComponent<AttackTargetMeshCooldown>();
            }

            return isSecondaryAttack ? attackTargetMeshCooldown.tryTriggerSecondaryAttack(duration) : attackTargetMeshCooldown.tryTriggerPrimaryAttack(duration, speed);
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
            var newEquipType =
                hasDualWieldingWeaponEquipped() || (isRightHand ^ VHVRConfig.LeftHanded()) ?
                EquipScript.getRight() :
                EquipScript.getLeft();

            if (!Player.m_localPlayer || newEquipType == currentEquipType)
            {
                RotateColliderForSecondaryWeapon();
                return;
            }

            currentEquipType = newEquipType;

            colliderData = WeaponUtils.GetDualWieldLeftHandColliderData(newEquipType);

            desiredPosition =
                isRightHand ?
                Vector3.Reflect(colliderData.pos, Vector3.right) :
                colliderData.pos;
            desiredRotation = Quaternion.Euler(colliderData.euler);
            transform.localScale = colliderData.scale;

            if (VHVRConfig.ShowDebugColliders())
            {
                if (debugColliderIndicator == null)
                {
                    WeaponUtils.CreateDebugSphere(transform);
                }
            }
            else if (debugColliderIndicator != null)
            {
                Destroy(debugColliderIndicator);
            }
        }

        private bool canAttackWithCollision()
        {
            if (!VRPlayer.inFirstPerson || transform.parent == null || lastGrabbedType != Grabbable.NONE)
            {
                return false;
            }

            if (handGesture.isHandFree() || holdingShield())
            {
                return SteamVR_Actions.valheim_Grab.GetState(inputSource);
            }

            return hasDualWieldingWeaponEquipped() || holdingSecondaryWeapon();
        }

        private bool holdingSecondaryWeapon()
        {
            if (isRightHand ^ VHVRConfig.LeftHanded())
            {
                return false;
            }
            return EquipScript.getLeft() == EquipType.Torch || EquipScript.getLeft() == EquipType.Knife;
        }

        private bool holdingShield()
        {
            if (isRightHand ^ VHVRConfig.LeftHanded())
            {
                return false;
            }
            return EquipScript.getLeft() == EquipType.Shield;
        }

        public bool hasMomentum(out float speed, out bool isJab)
        {
            var handVelocity = physicsEstimator.GetVelocity();
            if (handGesture.isHandFree())
            {
                speed = handVelocity.magnitude;
                Transform handTransform = (isRightHand ? VRPlayer.rightHand : VRPlayer.leftHand).transform;
                isJab = Vector3.Angle(handTransform.forward - handTransform.up, handVelocity) < 30f && speed > 3f;
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
            if (!handGesture.isHandFree() && !hasDualWieldingWeaponEquipped() && !holdingSecondaryWeapon())
            {
                return false;
            }

            return SteamVR_Actions.valheim_Grab.GetState(inputSource);
        }

        private void RotateColliderForSecondaryWeapon()
        {
            if (isRightHand ^ VHVRConfig.LeftHanded())
            {
                return;
            }

            if (EquipScript.getLeft() != EquipType.Knife)
            {
                ShouldSecondaryKnifeHoldInverse = false;
                return;
            }

            ShouldSecondaryKnifeHoldInverse = WeaponUtils.MaybeFlipKnife(ShouldSecondaryKnifeHoldInverse, !isRightHand);

            desiredPosition =
                isRightHand ^ ShouldSecondaryKnifeHoldInverse ?
                Vector3.Reflect(colliderData.pos, Vector3.right) :
                colliderData.pos;
        }
    }
}

