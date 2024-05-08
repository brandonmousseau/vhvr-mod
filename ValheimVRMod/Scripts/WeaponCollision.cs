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
        private const float MIN_SPEED = 3f;
        private const float MIN_STAB_SPEED = 1f;

        private bool scriptActive;
        private GameObject colliderParent;
        private ItemDrop.ItemData item;
        private Attack attack;
        private Attack secondaryAttack;
        private bool isDominantHand;
        private Outline outline;
        private bool readyToDrink;
        private float postSecondaryAttackCountdown;
        private GameObject debugColliderIndicator;

        public PhysicsEstimator physicsEstimator { get; private set; }
        public PhysicsEstimator mainHandPhysicsEstimator { get { return weaponWield.mainHand == VRPlayer.leftHand ? VRPlayer.leftHandPhysicsEstimator : VRPlayer.rightHandPhysicsEstimator; } }
        public float twoHandedMultitargetSwipeCountdown { get; private set; } = 0;
        public bool itemIsTool;
        public static bool isDrinking;
        public LocalWeaponWield weaponWield;
        public static bool isLastHitOnTerrain;

        private static readonly int[] ignoreLayers = {
            LayerUtils.WATERVOLUME_LAYER,
            LayerUtils.WATER,
            LayerUtils.UI_PANEL_LAYER,
            LayerUtils.CHARARCTER_TRIGGER
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
            if (debugColliderIndicator) Destroy(debugColliderIndicator);
        }

        private bool CheckDrinking(Collider collider)
        {
            if (!isDominantHand || EquipScript.getRight() != EquipType.Tankard || collider.name != "MouthCollider" || !readyToDrink)
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

        private void OnTriggerEnter(Collider collider)
        {
            if (!isCollisionAllowed())
            {
                return;
            }

            if (isDominantHand &&
                EquipScript.getRight() == EquipType.Tankard &&
                collider.name == "MouthCollider")
            {
                readyToDrink = true;
            }

            MaybeAttackCollider(collider, requireStab: false);
        }

        private void OnTriggerStay(Collider collider)
        {
            if (!isCollisionAllowed())
            {
                return;
            }

            if (CheckDrinking(collider)) {
                return;
            }

            // Allow triggering a stab on a character target after contact.
            // This allows stabbing with long weapons that are likely to have been overlapping with the target before attacking. 
            MaybeStabCharacter(collider);
        }

        private void MaybeStabCharacter(Collider collider) {

            var targetCharacter = collider.GetComponentInParent<Character>();
            if (targetCharacter == null)
            {
                return;
            }

            var cooldown = targetCharacter.GetComponent<AttackTargetMeshCooldown>();
            if (cooldown != null && cooldown.inCoolDown())
            {
                return;
            }

            MaybeAttackCollider(collider, requireStab: true);
        }

        private void MaybeAttackCollider(Collider collider, bool requireStab) {

            if (Player.m_localPlayer != null &&
                Player.m_localPlayer == collider.GetComponentInParent<Player>())
            {
                return;
            }

            if (item == null && !itemIsTool)
            {
                return;
            }

            if (!isStab())
            {
                if (requireStab || !hasMomentum())
                {
                    return;
                }
            }

            bool isSecondaryAttack = postSecondaryAttackCountdown <= 0 && RoomscaleSecondaryAttackUtils.IsSecondaryAttack(this.physicsEstimator, this.mainHandPhysicsEstimator);
            if (EquipScript.getRight() == EquipType.Polearms)
            {
                // Allow continuing an ongoing atgeir secondary attack (multitarget swipe) until cooldown finishes.
                isSecondaryAttack = postSecondaryAttackCountdown > 0 || isSecondaryAttack;
            }

            if (!tryHitTarget(collider.gameObject, isSecondaryAttack))
            {
                return;
            }

            if (isSecondaryAttack && postSecondaryAttackCountdown <= 0)
            {
                postSecondaryAttackCountdown = WeaponUtils.GetAttackDuration(secondaryAttack);
            }

            Attack currentAttack = isSecondaryAttack ? secondaryAttack : attack;

            if (WeaponUtils.IsTwoHandedMultitargetSwipe(currentAttack) && twoHandedMultitargetSwipeCountdown <= 0)
            {
                twoHandedMultitargetSwipeCountdown = WeaponUtils.GetAttackDuration(currentAttack);
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
                //bHaptics
                if (!BhapticsTactsuit.suitDisabled)
                {
                    BhapticsTactsuit.SwordRecoil(!VHVRConfig.LeftHanded());
                }
            }
        }

        private bool tryHitTarget(GameObject target, bool isSecondaryAttack)
        {
            // ignore certain Layers
            if (ignoreLayers.Contains(target.layer))
            {
                return false;
            }

            if (Player.m_localPlayer.m_blocking && !weaponWield.allowBlocking() && VHVRConfig.BlockingType() == "GrabButton")
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

            isLastHitOnTerrain = false;

            if (target.GetComponentInParent<MineRock5>() != null &&
                target.transform.parent.GetComponent<Heightmap>() != null)
            {
                isLastHitOnTerrain = true;
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

            if (isSecondaryAttack)
            {
                // Use the target cooldown time of the primary attack if it is shorter to allow a primary attack immediately after secondary attack;
                // The secondary attack cooldown time is managed by postSecondaryAttackCountdown in this class intead.
                float targetCooldownTime = Mathf.Min(WeaponUtils.GetAttackDuration(attack), WeaponUtils.GetAttackDuration(secondaryAttack));
                return attackTargetMeshCooldown.tryTriggerSecondaryAttack(targetCooldownTime);
            }

            return attackTargetMeshCooldown.tryTriggerPrimaryAttack(WeaponUtils.GetAttackDuration(attack));
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

            attack = item.m_shared.m_attack.Clone();
            secondaryAttack = item.m_shared.m_secondaryAttack.Clone();

            itemIsTool = (name == "Hammer");

            if (colliderParent == null)
            {
                colliderParent = new GameObject();
            }

            switch(EquipScript.getRight())
            {
                case EquipType.Cultivator:
                case EquipType.Hoe:
                case EquipType.Fishing:
                case EquipType.Magic:
                case EquipType.SpearChitin:
                    setScriptActive(false);
                    return;
            }

            try
            {
                WeaponColData colliderData = WeaponUtils.GetColliderData(name, item, meshFilter, handPosition);
                colliderParent.transform.parent = meshTranform;
                colliderParent.transform.localPosition = colliderData.pos;
                colliderParent.transform.localRotation = Quaternion.Euler(colliderData.euler);
                colliderParent.transform.localScale = colliderData.scale;
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
            if (!outline || ButtonSecondaryAttackManager.isSecondaryAttackStarted)
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
            else if (postSecondaryAttackCountdown > 0.5f)
            {
                outline.enabled = true;
                outline.OutlineColor = Color.Lerp(new Color(1, 1, 0, 0), new Color(1, 1, 0, 0.5f), postSecondaryAttackCountdown - 0.5f);
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
            if (postSecondaryAttackCountdown > 0)
            {
                postSecondaryAttackCountdown -= Time.fixedDeltaTime;
            }
            if (twoHandedMultitargetSwipeCountdown > 0)
            {
                postSecondaryAttackCountdown -= Time.fixedDeltaTime;
            }

            if (!isCollisionAllowed())
            {
                return;
            }
        }

        private bool hasMomentum()
        {
            float handSpeed;
            if (weaponWield.twoHandedState == WeaponWield.TwoHandedState.SingleHanded)
            {
                handSpeed =
                    weaponWield.isLeftHandWeapon() ^ VHVRConfig.LeftHanded() ?
                    VRPlayer.leftHandPhysicsEstimator.GetAverageVelocityInSnapshots().magnitude :
                    VRPlayer.rightHandPhysicsEstimator.GetAverageVelocityInSnapshots().magnitude;
            }
            else
            {
                handSpeed =
                    Mathf.Max(
                        VRPlayer.leftHandPhysicsEstimator.GetAverageVelocityInSnapshots().magnitude,
                        VRPlayer.rightHandPhysicsEstimator.GetAverageVelocityInSnapshots().magnitude);
            }

            return handSpeed >= MIN_SPEED * VHVRConfig.SwingSpeedRequirement();
        }

        private bool isStab()
        {
            Vector3 attackVelocity = mainHandPhysicsEstimator == null ? Vector3.zero : mainHandPhysicsEstimator.GetAverageVelocityInSnapshots();

            if (!WeaponUtils.IsStab(attackVelocity, LocalWeaponWield.weaponForward, LocalWeaponWield.isCurrentlyTwoHanded())) {
                return false;
            }

            if (Vector3.Dot(attackVelocity, LocalWeaponWield.weaponForward) < MIN_STAB_SPEED * VHVRConfig.SwingSpeedRequirement())
            {
                return false;
            }
               
            LogUtils.LogDebug("VHVR: stab detected on weapon direction: " + LocalWeaponWield.weaponForward);
            return true;
        }
    }
}
