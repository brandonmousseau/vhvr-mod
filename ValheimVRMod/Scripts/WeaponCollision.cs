using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using ValheimVRMod.Scripts.Block;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts {
    public class WeaponCollision : MonoBehaviour {
        private const float MIN_SPEED = 1.5f;

        private bool scriptActive;
        private GameObject colliderParent;
        private ItemDrop.ItemData item;
        protected Attack attack { get; private set; }
        protected bool isRightHand { get; private set; }
        protected Outline outline { get; private set; }
        private bool hasDrunk;

        public PhysicsEstimator physicsEstimator { get; private set; }
        public bool itemIsTool;
        public static bool isDrinking;
        public bool isLastHitOnTerrain;

        private float colliderDistance;

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
        }

        void Destroy()
        {
            Destroy(colliderParent);
        }

        private void OnTriggerStay(Collider collider) {

            if (!isCollisionAllowed()) {
                return;
            }

            if (!isRightHand || EquipScript.getRight() != EquipType.Tankard || collider.name != "MouthCollider" || hasDrunk) {
                return;
            }

            isDrinking = hasDrunk = VRPlayer.dominantHand.transform.rotation.eulerAngles.x > 0 && VRPlayer.dominantHand.transform.transform.rotation.eulerAngles.x < 90;

            //bHaptics
            if (isDrinking && !BhapticsTactsuit.suitDisabled)
            {
                BhapticsTactsuit.PlaybackHaptics("Drinking");
            }

        }

        private void OnTriggerEnter(Collider collider)
        {
            if (!isCollisionAllowed()) {
                return;
            }

            if (isRightHand && EquipScript.getRight() == EquipType.Tankard) {
                if (collider.name == "MouthCollider" && hasDrunk) {
                    hasDrunk = false;
                }

                return;
            }

            var maybePlayer = collider.GetComponentInParent<Player>();

            if (maybePlayer != null && maybePlayer == Player.m_localPlayer) {
                return;
            }

            if (item == null && !itemIsTool || !hasMomentum()) {
                return;
            }

            Attack currentAttack = null;
            if (isAttackAvailable(collider.gameObject))
            {
                isLastHitOnTerrain = false;
                GameObject target = collider.gameObject;
                if (target.GetComponentInParent<MineRock5>() != null)
                {
                    target = target.transform.parent.gameObject;
                }

                if (target.GetComponent<Heightmap>() != null)
                {
                    isLastHitOnTerrain = true;
                }
                var character = target.GetComponentInParent<Character>();
                if (character != null)
                {
                    target = character.gameObject;
                }
                var attackTargetMeshCooldown = target.GetComponent<AttackTargetMeshCooldown>();
                if (attackTargetMeshCooldown == null)
                {
                    attackTargetMeshCooldown = target.AddComponent<AttackTargetMeshCooldown>();
                }
                currentAttack = tryHitTarget(attackTargetMeshCooldown);
            }
            if (currentAttack == null) {
                return;
            }

            StaticObjects.lastHitPoint = transform.position;
            StaticObjects.lastHitDir = physicsEstimator.GetVelocity().normalized;
            StaticObjects.lastHitCollider = collider;

            if (currentAttack.Start(Player.m_localPlayer, null, null,
                        Player.m_localPlayer.m_animEvent,
                        null, item, null, 0.0f, 0.0f))
            {
                if (isRightHand) {
                    VRPlayer.rightHand.hapticAction.Execute(0, 0.2f, 100, 0.5f, SteamVR_Input_Sources.RightHand);
                }
                else {
                    VRPlayer.leftHand.hapticAction.Execute(0, 0.2f, 100, 0.5f, SteamVR_Input_Sources.LeftHand);
                }
                //bHaptics
                if (!BhapticsTactsuit.suitDisabled)
                {
                    BhapticsTactsuit.SwordRecoil(!VHVRConfig.LeftHanded());
                }
            }
        }

        protected virtual bool isAttackAvailable(GameObject target) {

            // ignore certain Layers
            if (ignoreLayers.Contains(target.layer)) {
                return false;
            }

            if (Player.m_localPlayer.IsStaggering() || Player.m_localPlayer.InDodge())
            {
                return false;
            }
            // if attack is vertical, we can only hit one target at a time
            if (attack.m_attackType != Attack.AttackType.Horizontal && AttackTargetMeshCooldown.isPrimaryTargetInCooldown()) {
                return false;
            }
            return true;
        }

        protected virtual Attack tryHitTarget(AttackTargetMeshCooldown attackTargetMeshCooldown)
        {   
            bool attackSucceeded = attackTargetMeshCooldown.tryTriggerPrimaryAttack(WeaponUtils.GetAttackDuration(attack));

            return attackSucceeded ? attack : null;
        }

        private void OnRenderObject() {
            if (!isCollisionAllowed()) {
                return;
            }
            transform.SetParent(colliderParent.transform);
            transform.localRotation = Quaternion.identity;
            transform.localPosition = Vector3.zero;
            transform.localScale = Vector3.one;
            transform.SetParent(Player.m_localPlayer.transform, true);
        }

        public void setColliderParent(Transform obj, string name, bool rightHand) {
            outline = obj.parent.gameObject.AddComponent<Outline>();
            outline.OutlineMode = Outline.Mode.OutlineVisible;

            isRightHand = rightHand;
            if (isRightHand) {
                item = Player.m_localPlayer.GetRightItem();
            }
            else {
                item = Player.m_localPlayer.GetLeftItem();
            }

            attack = item.m_shared.m_attack.Clone();

            itemIsTool = name == "Hammer";

            if (colliderParent == null) {
                colliderParent = new GameObject();
            }

            try {
                WeaponColData colliderData = WeaponUtils.getForName(name, item);
                colliderParent.transform.parent = obj;
                colliderParent.transform.localPosition = colliderData.pos;
                colliderParent.transform.localRotation = Quaternion.Euler(colliderData.euler);
                colliderParent.transform.localScale = colliderData.scale;
                colliderDistance = Vector3.Distance(colliderParent.transform.position, obj.parent.position);
                setScriptActive(true);
            }
            catch (InvalidEnumArgumentException)
            {
                LogUtils.LogWarning($"Collider not found for: {name}");
                setScriptActive(false);
            }
        }

        private void Update() {
            outlineTarget();
        }
        protected virtual bool outlineTarget()
        {
            if (!outline)
            {
                return false;
            }

            bool inCooldown = AttackTargetMeshCooldown.isPrimaryTargetInCooldown();
            bool canDoPrimaryAttack =
                Player.m_localPlayer.HaveStamina(getStaminaUsage() + 0.1f) && (attack.m_attackType == Attack.AttackType.Horizontal || !inCooldown);
            if (!canDoPrimaryAttack)
            {
                outline.enabled = true;
                outline.OutlineColor = Color.red;
                outline.OutlineWidth = 5;
                return true;
            }
            else
            {
                outline.enabled = false;
                return false;
            }

        }

        private float getStaminaUsage() {

            if (attack.m_attackStamina <= 0.0) {
                return 0.0f;
            }
            double attackStamina = attack.m_attackStamina;
            return (float)(attackStamina - attackStamina * 0.330000013113022 * Player.m_localPlayer.GetSkillFactor(item.m_shared.m_skillType));
        }
        
        private bool isCollisionAllowed() {
            return scriptActive && VRPlayer.inFirstPerson && colliderParent != null;
        }

        private void setScriptActive(bool scriptActive) {
            this.scriptActive = scriptActive;
        }

        public bool hasMomentum() {
            if (!VHVRConfig.WeaponNeedsSpeed()) {
                return true;
            }

            if (physicsEstimator.GetAverageVelocityInSnapshots().magnitude > MIN_SPEED + colliderDistance * 2)
            {
                return true;
            }

            return false;
        }
    }
}
