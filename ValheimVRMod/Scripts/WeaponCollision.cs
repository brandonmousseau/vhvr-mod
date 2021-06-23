using System.Collections.Generic;
using System.ComponentModel;
using HarmonyLib;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts {
    public class WeaponCollision : MonoBehaviour {
        private const float MIN_DISTANCE = 0.2f;
        private const int MAX_SNAPSHOTS_BASE = 20;
        private const int MAX_SNAPSHOTS_FACTOR = -5;

        private bool scriptActive;
        private GameObject colliderParent = new GameObject();
        private List<Vector3> snapshots = new List<Vector3>();
        private ItemDrop.ItemData item;
        private Attack attack;
        private bool isRightHand;
        private Outline outline;
        private float hitTime;

        public bool itemIsTool;
        
        private int maxSnapshots;
        private float colliderDistance;

        private void OnTriggerEnter(Collider collider) {
            if (!isCollisionAllowed()) {
                return;
            }

            // ignore water and UI panel
            if (collider.gameObject.layer == LayerUtils.WATERVOLUME_LAYER 
                || collider.gameObject.layer == LayerUtils.WATER
                || collider.gameObject.layer == LayerUtils.UI_PANEL_LAYER) {
                return;
            }

            var maybePlayer = collider.GetComponentInParent<Player>();

            if (maybePlayer != null && maybePlayer == Player.m_localPlayer) {
                return;
            }

            if (item == null && !itemIsTool || !hasMomentum()) {
                return;
            }

            if (!tryHitEnemy(collider.gameObject)) {
                return;
            }
            
            StaticObjects.lastHitPoint = transform.position;
            StaticObjects.lastHitCollider = collider;
            
            if (attack.Start(Player.m_localPlayer, null, null,
                AccessTools.FieldRefAccess<Player, CharacterAnimEvent>(Player.m_localPlayer, "m_animEvent"),
                null, item, null, 0.0f, 0.0f)) {
                if (isRightHand) {
                    VRPlayer.rightHand.hapticAction.Execute(0, 0.2f, 100, 0.5f, SteamVR_Input_Sources.RightHand);
                }
                else {
                    VRPlayer.leftHand.hapticAction.Execute(0, 0.2f, 100, 0.5f, SteamVR_Input_Sources.LeftHand);
                }
            }
        }

        /**
         * https://valheim.fandom.com/wiki/Weapons
         * time = sum attackspeed / sum modifiers
         */
        private bool tryHitEnemy(GameObject target) {

            var enemyHitValidator = target.GetComponent<MeshCooldown>();
            if (enemyHitValidator == null) {
                enemyHitValidator = target.AddComponent<MeshCooldown>();
            }
            
            return enemyHitValidator.tryTrigger(hitTime);
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
            outline.OutlineColor = Color.red;
            outline.OutlineWidth = 5;
            outline.OutlineMode = Outline.Mode.OutlineVisible;
            
            isRightHand = rightHand;
            if (isRightHand) {
                item = Player.m_localPlayer.GetRightItem();   
            }
            else {
                item = Player.m_localPlayer.GetLeftItem();
            }

            if (item != null) {
                attack = item.m_shared.m_attack.Clone();
            }
            
            switch (attack.m_attackAnimation) {
                case "unarmed_attack":
                    hitTime = 0.63f;
                    break;
                case "atgeir_attack":
                    hitTime = 0.81f;
                    break;
                case "battleaxe_attack":
                    hitTime = 0.87f;
                    break;
                case "knife_stab":
                    hitTime = 0.49f;
                    break;
                case "swing_longsword":
                case "spear_poke":
                    hitTime = 0.63f;
                    break;
                case "swing_pickaxe":
                    hitTime = 1.3f;
                    break;
                case "swing_sledge":
                    hitTime = 2.15f;
                    break;
                case "swing_axe":
                    hitTime = 0.64f;
                    break;
                default:
                    hitTime = 0.63f;
                    break;
            }

            itemIsTool = name == "Hammer";

            if (colliderParent == null) {
                colliderParent = new GameObject();
            }

            try {
                WeaponColData colliderData = WeaponUtils.getForName(name);
                colliderParent.transform.parent = obj;
                colliderParent.transform.localPosition = colliderData.pos;
                colliderParent.transform.localRotation = Quaternion.Euler(colliderData.euler);
                colliderParent.transform.localScale = colliderData.scale;
                colliderDistance = Vector3.Distance(colliderParent.transform.position, obj.parent.position);
                maxSnapshots = (int) (MAX_SNAPSHOTS_BASE + MAX_SNAPSHOTS_FACTOR * colliderDistance);
                setScriptActive(true);
            }
            catch (InvalidEnumArgumentException) {
                setScriptActive(false);
            }
        }

        private void Update() {

            if (!outline) {
                return;
            }
            
            if (outline.enabled && Player.m_localPlayer.HaveStamina(getStaminaUsage() + 0.1f)) {
                outline.enabled = false;
            } else if (! outline.enabled && ! Player.m_localPlayer.HaveStamina(getStaminaUsage() + 0.1f)) {
                outline.enabled = true;
            }
        }

        private float getStaminaUsage() {
            
            if (attack.m_attackStamina <= 0.0) {
                return 0.0f;   
            }
            double attackStamina = attack.m_attackStamina;
            return (float) (attackStamina - attackStamina * 0.330000013113022 * Player.m_localPlayer.GetSkillFactor(item.m_shared.m_skillType));
        }

        private bool isCollisionAllowed() {
            return scriptActive && VRPlayer.inFirstPerson && colliderParent != null;
        }

        private void setScriptActive(bool active) {
            scriptActive = active;

            if (!active) {
                snapshots.Clear();
            }
        }

        
        
        private void FixedUpdate() {
            if (!isCollisionAllowed()) {
                return;
            }
            
            snapshots.Add(transform.localPosition);
            // little calculation to get needed speed based on weapon collider (e.g. atgeir needs mor speed than sword)
            if (snapshots.Count > maxSnapshots) {
                snapshots.RemoveAt(0);
            }
        }
        
        public bool hasMomentum() {
            foreach (Vector3 snapshot in snapshots) {
                if (Vector3.Distance(snapshot, transform.localPosition) > MIN_DISTANCE + colliderDistance / 2) {
                    return true;
                }
            }

            return false;
        }
    }
}