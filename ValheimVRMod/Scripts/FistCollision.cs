using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts {
    public class FistCollision : MonoBehaviour {
        private const float MIN_DISTANCE = 0.2f;
        private const int MAX_SNAPSHOTS = 20;
        private const float COOLDOWN = 1.5f;
        
        private GameObject colliderParent = new GameObject();
        private List<Vector3> snapshots = new List<Vector3>();
        private CooldownScript cooldownScript;
        private bool isRightHand;
        private HandGesture handGesture;

        private void OnTriggerEnter(Collider collider) {
            if (!isCollisionAllowed()) {
                return;
            }

            // ignore water
            if (collider.gameObject.layer == LayerUtils.WATERVOLUME_LAYER || collider.gameObject.layer == LayerUtils.WATER) {
                return;
            }
            
            var maybePlayer = collider.GetComponentInParent<Player>();

            if (maybePlayer != null && maybePlayer == Player.m_localPlayer) {
                return;
            }

            if (!hasMomentum() || cooldownScript.isInCooldown()) {
                return;
            }

            StaticObjects.lastHitPoint = transform.position;
            StaticObjects.lastHitCollider = collider;

            var attack = Player.m_localPlayer.m_unarmedWeapon.m_itemData.m_shared.m_attack;
            if (attack.Start(Player.m_localPlayer, null, null,
                AccessTools.FieldRefAccess<Player, CharacterAnimEvent>(Player.m_localPlayer, "m_animEvent"),
                null, Player.m_localPlayer.m_unarmedWeapon.m_itemData, null, 0.0f, 0.0f)) {
                if (isRightHand) {
                    VRPlayer.rightHand.hapticAction.Execute(0, 0.2f, 100, 0.5f, SteamVR_Input_Sources.RightHand);    
                } else {
                    VRPlayer.leftHand.hapticAction.Execute(0, 0.2f, 100, 0.5f, SteamVR_Input_Sources.LeftHand);
                }
                
            }

            snapshots.Clear();
            cooldownScript.maxCooldown = COOLDOWN;
            cooldownScript.startCooldown();
        }

        private void OnRenderObject() {
            transform.SetParent(colliderParent.transform);
            transform.localRotation = Quaternion.identity;
            transform.localPosition = Vector3.zero;
            transform.localScale = Vector3.one;
            transform.SetParent(Player.m_localPlayer.transform, true);

        }

        public void setColliderParent(Transform obj, CooldownScript cds, bool rightHand) {

            isRightHand = rightHand;
            handGesture = obj.GetComponent<HandGesture>();
            cooldownScript = cds;
            colliderParent = new GameObject();
            colliderParent.transform.parent = obj;
            colliderParent.transform.localPosition = new Vector3(0, 0.00078f, 0.00016f);
            colliderParent.transform.localRotation = Quaternion.identity;
            colliderParent.transform.localScale = new Vector3(0.0013f, 0.001f, 0.0007f);
        }

        private bool isCollisionAllowed() {

            SteamVR_Input_Sources inputSource;
            
            if (isRightHand) {
                inputSource = SteamVR_Input_Sources.RightHand;
            } else {
                inputSource = SteamVR_Input_Sources.LeftHand;
            }

           
            return VRPlayer.inFirstPerson && colliderParent != null && handGesture.isUnequiped() 
                   && SteamVR_Actions.valheim_Grab.GetState(inputSource)
                   && (isRightHand && SteamVR_Actions.valheim_Use.GetState(inputSource) 
                   || !isRightHand && SteamVR_Actions.valheim_UseLeft.GetState(inputSource));
        }

        private void FixedUpdate() {
            snapshots.Add(transform.localPosition);
            if (snapshots.Count > MAX_SNAPSHOTS) {
                snapshots.RemoveAt(0);
            }
        }
        
        public bool hasMomentum() {
            foreach (Vector3 snapshot in snapshots) {
                if (Vector3.Distance(snapshot, transform.localPosition) > MIN_DISTANCE) {
                    return true;
                }
            }

            return false;
        }
    }
}