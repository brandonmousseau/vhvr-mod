using System.ComponentModel;
using System.Linq;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts
{
    public class WeaponCollision : MonoBehaviour
    {
        private const float MIN_STAB_SPEED = 4f;
        private const float MIN_HAMMER_SPEED = 1;
        private const float MIN_LONG_TOOL_SPEED = 1.5f;
        // The offset amount of the point on the weapon relative to the hand to calculate the speed of.
        // This is intentionally made much smaller than the possible full length of the weapon so that
        // small wrist rotation will not acccidentally trigger an attack when holding a long weapon.
        private const float WEAPON_ANGULAR_WEIGHT_OFFSET = 0.125f;
        private const string MOUTH_COLLIDER_NAME = "MouthCollider";

        private bool scriptActive;
        private GameObject colliderParent;
        private ItemDrop.ItemData item;
        private Attack attack;
        private Attack secondaryAttack;
        private bool isDominantHand;
        private Outline outline;
        private bool readyToDrink;
        private float postSlowAttackCountdown;
        private float twoHandedMultitargetSwipeCountdown = 0;
        private float twoHandedMultitargetSwipeDuration;
        private GameObject debugColliderIndicator;
        private bool isHoldingTankard { get { return isDominantHand && EquipScript.getRight() == EquipType.Tankard; } }

        public PhysicsEstimator physicsEstimator { get; private set; }
        public PhysicsEstimator mainHandPhysicsEstimator { get { return weaponWield.mainHand == VRPlayer.leftHand ? VRPlayer.leftHandPhysicsEstimator : VRPlayer.rightHandPhysicsEstimator; } }
        private bool itemIsTool;
        public static bool hasPendingToolUsageOutput;
        public static bool isDrinking;
        public LocalWeaponWield weaponWield;
        public static bool isLastHitOnTerrain;
        public bool isTwoHandedMultitargetSwipeActive { get { return twoHandedMultitargetSwipeCountdown > twoHandedMultitargetSwipeDuration * 0.5f; } }

        private static readonly int[] ignoreLayers = {
            LayerUtils.WATERVOLUME_LAYER,
            LayerUtils.WATER,
            LayerUtils.UI_PANEL_LAYER,
            LayerUtils.CHARARCTER_TRIGGER,
            LayerUtils.ITEM_LAYER,
        };

        private void Awake()
        {
            colliderParent = new GameObject();
            physicsEstimator = gameObject.AddComponent<PhysicsEstimator>();
            physicsEstimator.refTransform = CameraUtils.getCamera(CameraUtils.VR_CAMERA)?.transform.parent;

            if (VHVRConfig.ShowDebugColliders())
            {
                debugColliderIndicator = WeaponUtils.CreateDebugBox(transform);
            }
        }

        void Destroy()
        {
            Destroy(colliderParent);
            if (debugColliderIndicator != null) Destroy(debugColliderIndicator);
        }

        private void OnTriggerEnter(Collider collider)
        {
            if (!isCollisionAllowed())
            {
                return;
            }

            if (isHoldingTankard)
            {
                if (collider.name == MOUTH_COLLIDER_NAME)
                {
                    readyToDrink = true;
                }
                return;
            }

            if (VHVRConfig.ShowDebugColliders())
            {
                if (debugColliderIndicator == null)
                {
                    WeaponUtils.CreateDebugBox(transform);
                }
            }
            else if (debugColliderIndicator != null)
            {
                Destroy(debugColliderIndicator);
            }

            MaybeAttackCollider(collider, requireStab: false, requireStabOrBackSlash: false);
        }

        private void OnTriggerStay(Collider collider)
        {
            if (!isCollisionAllowed())
            {
                return;
            }

            if (isHoldingTankard) {
                CheckDrinking(collider);
                return;
            }

            if (itemIsTool)
            {
                switch (EquipScript.getRight())
                {
                    case EquipType.Cultivator:
                    case EquipType.Hoe:
                        if (isTerrain(collider.gameObject))
                        {
                            MaybeAttackCollider(collider, requireStab: false, requireStabOrBackSlash: false);
                        }
                        return;
                    case EquipType.Scythe:
                        if ((LayerUtils.HARVEST_RAY_MASK & (1 << collider.gameObject.layer)) != 0)
                        {
                            MaybeAttackCollider(collider, requireStab: false, requireStabOrBackSlash: false);
                        }
                        return;
                }
                return;
            }

            // Allow triggering a stab on a character target after contact.
            // This allows stabbing with long weapons that are likely to have been overlapping with the target before attacking. 
            MaybeStabCharacter(collider);
        }

        public static bool IsFriendly(Character character)
        {
            if (character.m_tamed || character.gameObject == Player.m_localPlayer.gameObject)
            {
                return true;
            }

            if (character.IsPlayer())
            {
                return Player.m_localPlayer == null || !Player.m_localPlayer.m_pvp || !character.GetComponent<Player>().m_pvp;
            }

            return character.m_baseAI != null && !character.m_baseAI.m_aggravated && character.m_faction == Character.Faction.Dverger;
        }

        private bool CheckDrinking(Collider collider)
        {
            if (!isHoldingTankard || collider.name != MOUTH_COLLIDER_NAME || !readyToDrink)
            {
                return false;
            }

            isDrinking = weaponWield.mainHand.transform.rotation.eulerAngles.x > 0 && weaponWield.mainHand.transform.rotation.eulerAngles.x < 90;
            if (!isDrinking)
            {
                return false;
            }

            readyToDrink = false;

            if (!BhapticsTactsuit.suitDisabled)
            {
                BhapticsTactsuit.PlaybackHaptics("Drinking");
            }

            return true;
        }

        private void MaybeStabCharacter(Collider collider) {

            if (collider.gameObject.layer != LayerUtils.CHARACTER)
            {
                return;
            }

            Character character = collider.GetComponentInParent<Character>();
            if (character == null || IsFriendly(character))
            {
                return;
            }

            var cooldown = collider.GetComponent<AttackTargetMeshCooldown>();
            if (cooldown != null && cooldown.inCoolDown())
            {
                return;
            }

            switch(EquipScript.getRight())
            {
                case EquipType.BattleAxe:
                case EquipType.Polearms:
                    MaybeAttackCollider(collider, requireStab: false, requireStabOrBackSlash: true);
                    return;
                case EquipType.Spear:
                case EquipType.Sword:
                    MaybeAttackCollider(collider, requireStab: true, requireStabOrBackSlash: true);
                    return;
            }
        }

        private void MaybeAttackCollider(Collider collider, bool requireStab, bool requireStabOrBackSlash) {

            if (collider.GetComponentInParent<Player>() == Player.m_localPlayer)
            {
                return;
            }

            if (item == null && !itemIsTool)
            {
                return;
            }

            if (!hasMomentum(out bool isStab, out bool isBackSlash, out float speed))
            {
                return;
            }

            if (requireStab && !isStab)
            {
                return;
            }

            if (requireStabOrBackSlash && !isStab && !isBackSlash)
            {
                return;
            }

            if (itemIsTool)
            {
                switch (EquipScript.getRight())
                {
                    case EquipType.Cultivator:
                    case EquipType.Hoe:
                        hasPendingToolUsageOutput = LocalWeaponWield.isCurrentlyTwoHanded();
                        return;
                    case EquipType.Hammer:
                        hasPendingToolUsageOutput = Player.m_localPlayer.InRepairMode();
                        return;
                    case EquipType.Scythe:
                        break;
                    default:
                        return;
                }
            }

            bool weaponHasMultitargetSwipe = EquipScript.getRight() == EquipType.BattleAxe || EquipScript.getRight() == EquipType.Polearms;
            bool isSlowAttack;
            if (postSlowAttackCountdown <= 0)
            {
                isSlowAttack = RoomscaleSecondaryAttackUtils.IsSecondaryAttack(this.physicsEstimator, this.mainHandPhysicsEstimator);
            }
            else
            {
                // Allow continuing an ongoing atgeir or battleaxe multitarget swipe until swipe timer finishes.
                isSlowAttack = weaponHasMultitargetSwipe && isTwoHandedMultitargetSwipeActive;
            }

            if (!tryHitTarget(collider.gameObject, isSlowAttack, speed))
            {
                return;
            }

            Attack currentAttack;
            if (EquipScript.getRight() == EquipType.BattleAxe)
            {
                currentAttack = !isSlowAttack && isStab ? secondaryAttack : attack;
            }
            else
            {
                currentAttack = isSlowAttack ? secondaryAttack : attack;
            }

            if (isSlowAttack && postSlowAttackCountdown <= 0)
            {
                postSlowAttackCountdown = WeaponUtils.GetAttackDuration(secondaryAttack);
            }
            if (weaponHasMultitargetSwipe && isSlowAttack && twoHandedMultitargetSwipeCountdown <= 0)
            {
                twoHandedMultitargetSwipeCountdown = twoHandedMultitargetSwipeDuration = WeaponUtils.GetAttackDuration(currentAttack);
            }

            StaticObjects.lastHitPoint = transform.position;
            StaticObjects.lastHitDir = physicsEstimator.GetVelocity().normalized;
            StaticObjects.lastHitCollider = collider;

            if (currentAttack.Start(Player.m_localPlayer, null, null,
                        Player.m_localPlayer.m_animEvent,
                        null, item, null, 0.0f, 0.0f))
            {
                if (isDominantHand)
                {
                    VRPlayer.dominantHand.hapticAction.Execute(0, 0.2f, 100, 0.5f, VRPlayer.dominantHandInputSource);
                }
                else
                {
                    VRPlayer.dominantHand.otherHand.hapticAction.Execute(0, 0.2f, 100, 0.5f, VRPlayer.nonDominantHandInputSource);
                }
                // bHaptics
                if (!BhapticsTactsuit.suitDisabled)
                {
                    BhapticsTactsuit.SwordRecoil(!VHVRConfig.LeftHanded());
                }
            }
        }

        private bool tryHitTarget(GameObject target, bool isSlowAttack, float speed)
        {
            // ignore certain Layers
            if (ignoreLayers.Contains(target.layer))
            {
                return false;
            }

            if (Player.m_localPlayer.m_blocking && !weaponWield.allowBlocking() && VHVRConfig.UseGrabButtonBlock())
            {
                return false;
            }

            if (Player.m_localPlayer.IsStaggering() || Player.m_localPlayer.InDodge())
            {
                return false;
            }
            if (ButtonSecondaryAttackManager.firstPos != Vector3.zero || ButtonSecondaryAttackManager.isSecondaryAttackStarted)
            {
                return false;
            }
            // if attack is vertical, we can only hit one target at a time
            if (attack.m_attackType != Attack.AttackType.Horizontal && AttackTargetMeshCooldown.isPrimaryTargetInCooldown())
            {
                return false;
            }

            if (isTerrain(target))
            {
                // Prevent hitting the terrain too easily.
                switch (EquipScript.getRight())
                {
                    case EquipType.BattleAxe:
                    case EquipType.Magic:
                    case EquipType.Polearms:
                        if (!LocalWeaponWield.IsDominantHandBehind)
                        {
                            return false;
                        }
                        break;
                    case EquipType.Axe:
                    case EquipType.Club:
                    case EquipType.Knife:
                    case EquipType.Spear:
                    case EquipType.SpearChitin:
                    case EquipType.Sword:
                        if (!SteamVR_Actions.valheim_Grab.GetState(VRPlayer.dominantHandInputSource))
                        {
                            return false;
                        }
                        break;
                    default:
                        break;
                }
                isLastHitOnTerrain = true;
            }
            else
            {
                isLastHitOnTerrain = false;
            }

            AttackTargetMeshCooldown attackTargetMeshCooldown = target.GetComponentInParent<AttackTargetMeshCooldown>();
            if (attackTargetMeshCooldown == null)
            {
                var cooldownObject = target;
                var character = target.GetComponentInParent<Character>();
                if (character != null)
                {
                    cooldownObject = character.gameObject;
                }
                else if (target.GetComponentInParent<MineRock5>() != null)
                {
                    cooldownObject = target.transform.parent.gameObject;
                }

                attackTargetMeshCooldown = cooldownObject.AddComponent<AttackTargetMeshCooldown>();
            }

            // Always use the target cooldown time of the fast attack to allow a fast attack immediately after a slow attack;
            // The slow attack cooldown time is managed by postSlowAttackCountdown in this class intead.
            float targetCooldownTime = Mathf.Min(WeaponUtils.GetAttackDuration(attack), WeaponUtils.GetAttackDuration(secondaryAttack));
            return isSlowAttack ?
                attackTargetMeshCooldown.tryTriggerSecondaryAttack(targetCooldownTime) :
                attackTargetMeshCooldown.tryTriggerPrimaryAttack(targetCooldownTime, speed);
        }

        private void OnRenderObject()
        {
            if (!isCollisionAllowed())
            {
                return;
            }
            transform.SetParent(colliderParent.transform);
            transform.localRotation = Quaternion.identity;
            transform.localPosition = Vector3.zero;
            transform.localScale = Vector3.one;
            transform.SetParent(Player.m_localPlayer.transform, true);
        }

        public void setColliderParent(MeshFilter meshFilter, Vector3 handPosition, string name, bool isDominantHand)
        {
            var meshTranform = meshFilter.transform;
            outline = meshTranform.parent.gameObject.AddComponent<Outline>();
            outline.OutlineMode = Outline.Mode.OutlineVisible;

            this.isDominantHand = isDominantHand;
            item = this.isDominantHand ? Player.m_localPlayer.GetRightItem() : Player.m_localPlayer.GetLeftItem();

            itemIsTool = (name == "Hammer" || EquipScript.getRight() == EquipType.Hoe || EquipScript.getRight() == EquipType.Cultivator || EquipScript.getRight() == EquipType.Scythe);

            if (colliderParent == null)
            {
                colliderParent = new GameObject();
            }

            switch (EquipScript.getRight())
            {
                case EquipType.Fishing:
                    setScriptActive(false);
                    return;
                case EquipType.Magic:
                case EquipType.SpearChitin:
                    if (this.isDominantHand)
                    {
                        item = Player.m_localPlayer.m_unarmedWeapon.m_itemData;
                        attack = secondaryAttack = Player.m_localPlayer.m_unarmedWeapon.m_itemData.m_shared.m_attack;
                        break;
                    }
                    attack = item.m_shared.m_attack.Clone();
                    secondaryAttack = item.m_shared.m_secondaryAttack.Clone();
                    break;
                default:
                    attack = item.m_shared.m_attack.Clone();
                    secondaryAttack = item.m_shared.m_secondaryAttack.Clone();
                    break;
            }
            try
            {
                WeaponColData colliderData = WeaponUtils.GetColliderData(name, item, meshFilter, handPosition);
                colliderParent.transform.parent = meshTranform;
                colliderParent.transform.localPosition = colliderData.pos;
                colliderParent.transform.localRotation = Quaternion.Euler(colliderData.euler);
                colliderParent.transform.localScale = colliderData.scale;

                if (attack.m_harvest)
                {
                    // Adjust collider size by farming skill level.
                    float skillFactor = Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Farming);
                    float radius = Mathf.Lerp(attack.m_harvestRadius, attack.m_harvestRadiusMaxLevel, skillFactor);
                    Vector3 harvestScale = colliderData.scale * radius;
                    colliderParent.transform.localScale = harvestScale;
                    Vector3 additionalOffset = (harvestScale - colliderData.scale) * 0.5f;
                    additionalOffset.x *= Mathf.Sign(colliderData.pos.x);
                    additionalOffset.y *= Mathf.Sign(colliderData.pos.y);
                    additionalOffset.z *= Mathf.Sign(colliderData.pos.z);
                    colliderParent.transform.localPosition += additionalOffset;
                }

                setScriptActive(true);
            }
            catch (InvalidEnumArgumentException)
            {
                LogUtils.LogWarning($"Collider not found for: {name}");
                setScriptActive(false);
            }
        }

        private void Update()
        {
            if (!outline || ButtonSecondaryAttackManager.isSecondaryAttackStarted || attack == null || item == null)
            {
                return;
            }

            bool inCooldown = AttackTargetMeshCooldown.isPrimaryTargetInCooldown();
            bool canDoPrimaryAttack =
                Player.m_localPlayer.HaveStamina(getStaminaUsage() + 0.1f) && (attack.m_attackType == Attack.AttackType.Horizontal || !inCooldown);
            if (!canDoPrimaryAttack)
            {
                outline.enabled = true;
                outline.OutlineColor = Color.red;
                outline.OutlineWidth = 5;
            }
            else if (postSlowAttackCountdown > 0.5f)
            {
                outline.enabled = true;
                outline.OutlineColor = Color.Lerp(new Color(1, 1, 0, 0), new Color(1, 1, 0, 0.5f), postSlowAttackCountdown - 0.5f);
                outline.OutlineWidth = 10;
            }
            else
            {
                outline.enabled = false;
            }
        }

        private float getStaminaUsage()
        {
            if (attack.m_attackStamina <= 0.0)
            {
                return 0.0f;
            }
            double attackStamina = attack.m_attackStamina;
            return (float)(attackStamina - attackStamina * 0.330000013113022 * Player.m_localPlayer.GetSkillFactor(item.m_shared.m_skillType));
        }

        private bool isCollisionAllowed()
        {
            return scriptActive && VRPlayer.inFirstPerson && colliderParent != null;
        }

        private void setScriptActive(bool scriptActive)
        {
            this.scriptActive = scriptActive;
        }

        private void FixedUpdate()
        {
            if (postSlowAttackCountdown > 0)
            {
                postSlowAttackCountdown -= Time.fixedDeltaTime;
            }
            if (twoHandedMultitargetSwipeCountdown > 0)
            {
                postSlowAttackCountdown -= Time.fixedDeltaTime;
            }

            if (!isCollisionAllowed())
            {
                return;
            }
        }

        private bool hasMomentum(out bool isStab, out bool isBackSlash, out float speed)
        {
            Vector3 velocity;
            if (weaponWield.twoHandedState == WeaponWield.TwoHandedState.SingleHanded)
            {
                velocity =
                    WeaponUtils.GetWeaponVelocity(
                        mainHandPhysicsEstimator.GetVelocity(),
                        mainHandPhysicsEstimator.GetAngularVelocity(),
                        LocalWeaponWield.weaponForward.normalized * WEAPON_ANGULAR_WEIGHT_OFFSET);
                speed = velocity.magnitude;
            }
            else
            {
                var leftHandVelocity = VRPlayer.leftHandPhysicsEstimator.GetVelocity();
                var rightHandVelocity = VRPlayer.rightHandPhysicsEstimator.GetVelocity();
                var direction = (leftHandVelocity + rightHandVelocity).normalized;
                var leftHandSpeed = Vector3.Dot(leftHandVelocity, direction);
                var rightHandSpeed = Vector3.Dot(rightHandVelocity, direction);
                speed = Mathf.Max(leftHandSpeed, rightHandSpeed);
                velocity = direction * speed;
            }

            isBackSlash = Vector3.Angle(velocity, LocalWeaponWield.weaponForward) > 135;
            isStab = !isBackSlash && WeaponCollision.isStab(velocity);

            if (weaponWield.twoHandedState == WeaponWield.TwoHandedState.SingleHanded &&
                EquipScript.getRight() == EquipType.Polearms &&
                !TwoHandedGeometry.LocalAtgeirGeometryProvider.UsingArmpitAnchor)
            {
                // When wielding polearms with only one hand without armpit anchor, make attack harder to trigger
                return isStab && speed > GetMinSpeed();
            }

            return isStab || speed > GetMinSpeed();
        }

        private float GetMinSpeed()
        {
            switch (EquipScript.getRight())
            {
                case EquipType.Hammer:
                    return MIN_HAMMER_SPEED;
                case EquipType.BattleAxe:
                case EquipType.Sledge:
                case EquipType.Polearms:
                    // Increase speed requirement when wielding certain two-handed weapons with only one hand.
                    return VHVRConfig.TwoHandedWield() ? VHVRConfig.SwingSpeedRequirement() * 1.5f : VHVRConfig.SwingSpeedRequirement();
                default:
                    return itemIsTool ? MIN_LONG_TOOL_SPEED : VHVRConfig.SwingSpeedRequirement();
            }
            
        }

        private static bool isStab(Vector3 velocity)
        {
            if (!WeaponUtils.IsStab(velocity, LocalWeaponWield.weaponForward, LocalWeaponWield.isCurrentlyTwoHanded())) {
                return false;
            }

            if (Vector3.Dot(velocity, LocalWeaponWield.weaponForward) < MIN_STAB_SPEED)
            {   
                return false;
            }
               
            // LogUtils.LogDebug("VHVR: stab detected on weapon direction: " + LocalWeaponWield.weaponForward);
            return true;
        }

        private static bool isTerrain(GameObject target)
        {
            return (target.GetComponentInParent<MineRock5>() == null ? target.transform : target.transform.parent).GetComponent<Heightmap>() != null;
        }
    }
}
