using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using ValheimVRMod.Scripts.Block;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts {
    public class WeaponCollision : MonoBehaviour {
        private const float MIN_DISTANCE = 0.2f;
        private const float MIN_DISTANCE_STAB = 0.25f;
        private const float MIN_DISTANCE_STAB_TWOHAND = 0.22f;
        private const int MAX_SNAPSHOTS_BASE = 20;
        private const int MAX_SNAPSHOTS_FACTOR = -5;
        private const float MAX_STAB_ANGLE = 20f;
        private const float MAX_STAB_ANGLE_TWOHAND = 40f;

        private bool scriptActive;
        private GameObject colliderParent;
        private List<Vector3> snapshots;
        private List<Vector3> snapshotsC;
        private ItemDrop.ItemData item;
        private Attack attack;
        private bool isRightHand;
        private Outline outline;
        private float hitTime;
        private bool hasDrunk;

        public bool itemIsTool;
        public static bool isDrinking;
        public WeaponWield weaponWield;
        
        private int maxSnapshots;
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
            snapshots = new List<Vector3>();
            snapshotsC = new List<Vector3>();
        }

        private void OnTriggerStay(Collider collider) {

            if (!isCollisionAllowed()) {
                return;
            }

            if (!isRightHand || EquipScript.getRight() != EquipType.Tankard || collider.name != "MouthCollider" || hasDrunk) {
                return;
            }

            var mainHand = VHVRConfig.LeftHanded() ? VRPlayer.leftHand : VRPlayer.rightHand;
            
            isDrinking = hasDrunk = 
                mainHand.transform.rotation.eulerAngles.x > 0 
                && mainHand.transform.rotation.eulerAngles.x < 90;

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

            if (!tryHitTarget(collider.gameObject)) {
                return;
            }
            
            StaticObjects.lastHitPoint = transform.position;
            StaticObjects.lastHitDir = snapshots[snapshots.Count - 1] - snapshots[snapshots.Count - 5];
            StaticObjects.lastHitCollider = collider;
            
            if (attack.Start(Player.m_localPlayer, null, null,
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
        private bool tryHitTarget(GameObject target) {

            // ignore certain Layers
            if (ignoreLayers.Contains(target.layer)) {
                return false;
            }

            if (Player.m_localPlayer.m_blocking && !weaponWield.allowBlocking() && VHVRConfig.BlockingType() == "GrabButton")
            {
                return false;
            }
            if (Player.m_localPlayer.IsStaggering())
            {
                return false;
            }

            // if attack is vertical, we can only hit one target at a time
            if (attack.m_attackType != Attack.AttackType.Horizontal  && 
                MeshCooldown.sharedInstance != null && MeshCooldown.sharedInstance.inCoolDown()) {
                return false;
            }

            if (target.GetComponentInParent<MineRock5>() != null) {
                target = target.transform.parent.gameObject;
            }
            
            var character = target.GetComponentInParent<Character>();
            if (character != null) {
                target = character.gameObject;
            }
            
            var meshCooldown = target.GetComponent<MeshCooldown>();
            if (meshCooldown == null) {
                meshCooldown = target.AddComponent<MeshCooldown>();
            }
            
            return meshCooldown.tryTrigger(hitTime);
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
            
            attack = item.m_shared.m_attack.Clone();

            switch (attack.m_attackAnimation) {
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
                WeaponColData colliderData = WeaponUtils.getForName(name,item);
                colliderParent.transform.parent = obj;
                colliderParent.transform.localPosition = colliderData.pos;
                colliderParent.transform.localRotation = Quaternion.Euler(colliderData.euler);
                colliderParent.transform.localScale = colliderData.scale;
                colliderDistance = Vector3.Distance(colliderParent.transform.position, obj.parent.position);
                maxSnapshots = (int) (MAX_SNAPSHOTS_BASE + MAX_SNAPSHOTS_FACTOR * colliderDistance);
                setScriptActive(true);
            }
            catch (InvalidEnumArgumentException)
            {
                LogUtils.LogWarning($"Collider not found for: {name}");
                setScriptActive(false);
            }
        }


        private void Update() {
            
            if (!outline) {
                return;
            }

            var inCooldown = MeshCooldown.sharedInstance != null && MeshCooldown.sharedInstance.inCoolDown();

            if (outline.enabled && Player.m_localPlayer.HaveStamina(getStaminaUsage() + 0.1f)
                                && (attack.m_attackType == Attack.AttackType.Horizontal || !inCooldown)) {
                outline.enabled = false;
            }
            else if (!outline.enabled && (!Player.m_localPlayer.HaveStamina(getStaminaUsage() + 0.1f)
                                          || attack.m_attackType != Attack.AttackType.Horizontal && inCooldown)) {
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
                snapshotsC.Clear();
            }
        }

        
        
        private void FixedUpdate() {
            if (!isCollisionAllowed()) {
                return;
            }
            
            snapshots.Add(transform.localPosition);
            snapshotsC.Add(GetHandPosition());
            if (snapshots.Count > maxSnapshots) {
                snapshots.RemoveAt(0);
            }
            if (snapshotsC.Count > maxSnapshots) {
                snapshotsC.RemoveAt(0);
            }
        }

        public bool hasMomentum() {
            
            if (!VHVRConfig.WeaponNeedsSpeed()) {
                return true;
            }

            foreach (Vector3 snapshot in snapshots) {
                if (Vector3.Distance(snapshot, transform.localPosition) > MIN_DISTANCE + colliderDistance / 2) {
                    return true;
                }
            }

            if (WeaponWield.isCurrentlyTwoHanded())
            {
                foreach (Vector3 snapshot in snapshots)
                {
                    if (Vector3.Distance(snapshot, transform.localPosition) > MIN_DISTANCE_STAB_TWOHAND && isStab())
                    {
                        return true;
                    }
                }
            }
            else
            {
                foreach (Vector3 snapshot in snapshotsC)
                {
                    if (Vector3.Distance(snapshot, GetHandPosition()) > MIN_DISTANCE_STAB && isStab())
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        public float SwingAngle() {
            float angle = Vector3.Angle(snapshotsC[0] - snapshotsC[snapshotsC.Count - 1], snapshotsC[0] - Player.m_localPlayer.transform.InverseTransformPoint(transform.position));
            return angle;
        }

        public bool isStab() {
            return WeaponWield.isCurrentlyTwoHanded() ? (SwingAngle() < MAX_STAB_ANGLE_TWOHAND) : (SwingAngle() < MAX_STAB_ANGLE);
        }
        private Vector3 GetHandPosition() {
            if (isRightHand) {
                return Player.m_localPlayer.transform.InverseTransformPoint(VRPlayer.rightHand.transform.position);
            }
            else {
                return Player.m_localPlayer.transform.InverseTransformPoint(VRPlayer.leftHand.transform.position);
            }
        }
    }
}