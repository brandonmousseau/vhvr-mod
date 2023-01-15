using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ValheimVRMod.Scripts.Block;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts {
    public class FistCollision : MonoBehaviour {
        private const float MIN_DISTANCE = 0.2f;
        private const int MAX_SNAPSHOTS = 20;

        private GameObject colliderParent = null;
        private List<Vector3> snapshots = new List<Vector3>();
        private bool isRightHand;
        private HandGesture handGesture;
        public static FistCollision instance;
        private FistBlock fistBlock;
        private float fistRotation = 0;

        private static readonly int[] ignoreLayers = {
            LayerUtils.WATERVOLUME_LAYER,
            LayerUtils.WATER,
            LayerUtils.UI_PANEL_LAYER,
            LayerUtils.CHARARCTER_TRIGGER
        };

        private void Awake() {
            colliderParent = new GameObject();
            instance = this;
        }

        private void OnTriggerStay(Collider collider) {
            if (!isCollisionAllowed()) {
                return;
            }

            var maybePlayer = collider.GetComponentInParent<Player>();

            if (maybePlayer != null && maybePlayer == Player.m_localPlayer) {
                return;
            }

            if (!hasMomentum()) {
                return;
            }

            if (!tryHitTarget(collider.gameObject)) {
                return;
            }

            StaticObjects.lastHitPoint = transform.position;
            StaticObjects.lastHitDir = snapshots[snapshots.Count - 1] - snapshots[snapshots.Count - 5];
            StaticObjects.lastHitCollider = collider;

            var item = Player.m_localPlayer.m_unarmedWeapon.m_itemData;
            var attack = Player.m_localPlayer.m_unarmedWeapon.m_itemData.m_shared.m_attack;
            if (usingClaws() || usingDualKnives())
            {
                item = Player.m_localPlayer.GetRightItem();
                attack = item.m_shared.m_attack.Clone();
            }
            if (attack.Start(Player.m_localPlayer, null, null, Player.m_localPlayer.m_animEvent,
                null, item, null, 0.0f, 0.0f)) {
                if (isRightHand) {
                    VRPlayer.rightHand.hapticAction.Execute(0, 0.2f, 100, 0.5f, SteamVR_Input_Sources.RightHand);
                } else {
                    VRPlayer.leftHand.hapticAction.Execute(0, 0.2f, 100, 0.5f, SteamVR_Input_Sources.LeftHand);
                }
            }
        }

        private bool tryHitTarget(GameObject target) {

            // ignore certain Layers
            if (ignoreLayers.Contains(target.layer)) {
                return false;
            }

            var attackTargetMeshCooldown = target.GetComponent<AttackTargetMeshCooldown>();
            if (attackTargetMeshCooldown == null) {
                attackTargetMeshCooldown = target.AddComponent<AttackTargetMeshCooldown>();
            }

            return attackTargetMeshCooldown.tryTrigger(0.63f);
        }

        private void OnRenderObject() {
            transform.SetParent(colliderParent.transform);
            transform.localRotation = Quaternion.identity;
            transform.localPosition = Vector3.zero;
            transform.localScale = Vector3.one;
            transform.SetParent(Player.m_localPlayer.transform, true);
        }

        public void setColliderParent(Transform obj, bool rightHand) {

            isRightHand = rightHand;
            handGesture = obj.GetComponent<HandGesture>();
            colliderParent = new GameObject();
            colliderParent.transform.parent = obj;
            colliderParent.transform.localPosition = new Vector3(0, 0.003f, 0.00016f);
            colliderParent.transform.localScale *= 0.45f;
        }

        private bool isCollisionAllowed() {

            SteamVR_Input_Sources inputSource;
            
            if (isRightHand) {
                inputSource = SteamVR_Input_Sources.RightHand;
            } else {
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

        private void FixedUpdate() {
            snapshots.Add(transform.localPosition);
            if (snapshots.Count > MAX_SNAPSHOTS) {
                snapshots.RemoveAt(0);
            }
        }

        public bool hasMomentum() {

            if (!VHVRConfig.WeaponNeedsSpeed()) {
                return true;
            }

            foreach (Vector3 snapshot in snapshots) {
                if (Vector3.Distance(snapshot, transform.localPosition) > MIN_DISTANCE
                    && Vector3.Dot((snapshot - transform.localPosition).normalized, Vector3.forward) < 0) {
                    return true;
                }
            }
            return false;
        }

        public bool usingClaws() {
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
