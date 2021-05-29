using System.Collections.Generic;
using System.ComponentModel;
using HarmonyLib;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts {
    public class WeaponCollision : MonoBehaviour {
        private const float MIN_DISTANCE = 0.3f;
        private const int MAX_SNAPSHOTS_BASE = 15;
        private const int MAX_SNAPSHOTS_FACTOR = -5;
        private const float COOLDOWN = 1.5f;

        private bool scriptActive;
        private GameObject colliderParent = new GameObject();
        private List<Vector3> snapshots = new List<Vector3>();

        public bool itemIsTool;
        
        private int maxSnapshots;
        private float colliderDistance;
        
        private void Awake() {
            StaticObjects.rightCooldown().maxCooldown = COOLDOWN;
        }

        private void OnTriggerEnter(Collider collider) {
            if (!isCollisionAllowed()) {
                return;
            }

            var maybePlayer = collider.GetComponentInParent<Player>();

            if (maybePlayer != null) {
                foreach (Player player in AccessTools.StaticFieldRefAccess<Player, List<Player>>("m_players"))
                {
                    if (player == Player.m_localPlayer) {
                        continue;
                    }
                    
                    Debug.Log("Logging Player " + player.GetPlayerName() + " his Children:");
                    LogUtils.LogChildTree(maybePlayer.transform);
                }    
            }
            

            if (maybePlayer != null && maybePlayer == Player.m_localPlayer) {
                return;
            }

            var item = Player.m_localPlayer.GetRightItem();

            if (item == null && !itemIsTool || !hasMomentum() || StaticObjects.rightCooldown().isInCooldown()) {
                return;
            }

            StaticObjects.lastHitPoint = transform.position;
            StaticObjects.lastHitCollider = collider;

            var attack = Player.m_localPlayer.GetRightItem().m_shared.m_attack.Clone();
            if (attack.Start(Player.m_localPlayer, null, null,
                AccessTools.FieldRefAccess<Player, CharacterAnimEvent>(Player.m_localPlayer, "m_animEvent"),
                null, Player.m_localPlayer.GetRightItem(), null, 0.0f, 0.0f)) {
                VRPlayer.rightHand.hapticAction.Execute(0, 0.2f, 100, 0.5f, SteamVR_Input_Sources.RightHand);
            }

            snapshots.Clear();
            StaticObjects.rightCooldown().startCooldown();
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

        public void setColliderParent(Transform obj, string name) {
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
                Debug.LogError("Invalid Weapon Data for: " + name);
            }
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