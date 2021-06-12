using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts {
    public class BowLocalManager : BowManager {
        private const float attachRange = 0.2f;
        private GameObject arrow;
        private LineRenderer predictionLine;
        private float projectileVel;
        private float projectileVelMin;

        public static BowLocalManager instance;
        public static float attackDrawPercentage;
        public static Vector3 spawnPoint;
        public static Vector3 aimDir;

        public static bool isPulling;
        public static bool startedPulling;
        public static bool aborting;

        private GameObject arrowAttach = new GameObject();

        private void Start() {
            instance = this;
            rightHand = VRPlayer.rightHand.transform;
            predictionLine = new GameObject().AddComponent<LineRenderer>();
            predictionLine.widthMultiplier = 0.03f;
            predictionLine.positionCount = 20;
            predictionLine.material.color = Color.white;
            predictionLine.enabled = false;
            predictionLine.receiveShadows = false;
            predictionLine.shadowCastingMode = ShadowCastingMode.Off;
            predictionLine.lightProbeUsage = LightProbeUsage.Off;
            predictionLine.reflectionProbeUsage = ReflectionProbeUsage.Off;
            
            arrowAttach.transform.SetParent(rightHand, false);
        }

        protected new void OnDestroy() {
            base.OnDestroy();
            destroyArrow();
            Destroy(pullObj);
            Destroy(predictionLine);
            Destroy(arrowAttach);
        }

        private void destroyArrow() {
            if (arrow != null) {
                arrow.GetComponent<ZNetView>().Destroy();   
            }
        }
        
        /**
     * Need to use OnRenderObject instead of Update or LateUpdate,
     * because of VRIK Bone Updates happening in LateUpdate 
     */
        protected new void OnRenderObject() {
            
            if (!initialized) {
                return;
            }
            
            base.OnRenderObject();

            if (SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand)) {
                handlePulling();
            }

            if (SteamVR_Actions.valheim_Grab.GetStateUp(SteamVR_Input_Sources.RightHand)) {
                releaseString();
            }

            if (predictionLine.enabled) {
                updatePredictionLine();   
            }
        }
        
        /**
     * calculate predictionline of how the arrow will fly
     */
        private void updatePredictionLine() {
            if (!predictionLine.enabled) {
                return;
            }

            Vector3 vel = aimDir * Mathf.Lerp(projectileVelMin, projectileVel, attackDrawPercentage);

            float stepLength = 0.1f;
            float stepSize = 20;
            Vector3 pos = transform.position;
            List<Vector3> pointList = new List<Vector3>();

            for (int i = 0; i < stepSize; i++) {
                pointList.Add(pos);
                vel += Vector3.down * arrow.GetComponent<Projectile>().m_gravity * stepLength;
                pos += vel * stepLength;
            }

            predictionLine.positionCount = 20;
            predictionLine.SetPositions(pointList.ToArray());
        }

        private void handlePulling() {
            if (!pulling && !checkHandNearString()) {
                return;
            }

            arrowAttach.transform.rotation = pullObj.transform.rotation;
            spawnPoint = transform.position;
            aimDir = -transform.forward;
            attackDrawPercentage = pullPercentage();

            if (Player.m_localPlayer.GetStamina() <= 0) {
                releaseString(true);
            }
        }

        private void releaseString(bool withoutShoot = false) {
            if (!pulling) {
                return;
            }

            predictionLine.enabled = false;
            pulling = isPulling = false;
            attackDrawPercentage = pullPercentage();
            spawnPoint = transform.position;
            aimDir = -transform.forward;

            if (withoutShoot || arrow == null || attackDrawPercentage <= 0.0f) {
                if (arrow) {
                    arrowAttach.transform.localRotation = Quaternion.identity;
                    if (attackDrawPercentage <= 0.0f) {
                        aborting = true;
                    }
                }

                return;
            }
            // SHOOTING
            VRPlayer.leftHand.hapticAction.Execute(0, 0.2f, 100, 0.3f, SteamVR_Input_Sources.LeftHand);
            destroyArrow();
        }

        private float pullPercentage() {
            return (pullObj.transform.localPosition.z - pullStart.z) / maxPullLength;
        }

        private bool checkHandNearString() {
            if (Vector3.Distance(rightHand.position, transform.TransformPoint(pullStart)) >
                attachRange) {
                return false;
            }

            if (arrow != null) {
                startedPulling = true;
                isPulling = true;
                predictionLine.enabled = VHVRConfig.UseArrowPredictionGraphic();
            }

            return pulling = true;
        }

        public void toggleArrow() {
            if (arrow != null) {
                destroyArrow();
                return;
            }
            
            var ammoItem = Player.m_localPlayer.GetAmmoItem();
            
            if (ammoItem == null || ammoItem.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Ammo) {
                // out of ammo
                return;
            }

            var currentAttack = Player.m_localPlayer.GetCurrentWeapon().m_shared.m_attack;
            projectileVel = currentAttack.m_projectileVel + ammoItem.m_shared.m_attack.m_projectileVel;
            projectileVelMin = currentAttack.m_projectileVelMin + ammoItem.m_shared.m_attack.m_projectileVelMin;
            
            arrow = Instantiate(ammoItem.m_shared.m_attack.m_attackProjectile, arrowAttach.transform);
            // we need to disable the Projectile Component, else the arrow will shoot out of the hands like a New Year rocket
            arrow.GetComponent<Projectile>().enabled = false;
            // also Destroy the Trail, as this produces particles when moving with arrow in hand
            Destroy(findTrail(arrow.transform));
            arrow.transform.localRotation = Quaternion.identity;
            arrow.transform.localPosition = new Vector3(0, 0, 1.25f);
            arrowAttach.transform.localRotation = Quaternion.identity;
        }

        private GameObject findTrail(Transform transform) {

            foreach (ParticleSystem p in transform.GetComponentsInChildren<ParticleSystem>()) {
                var go = p.gameObject;
                if (go.name == "trail") {
                    return go;
                }
            }

            return null;
        }
        
        public bool isHoldingArrow() {
            return arrow != null;
        }
    }
}