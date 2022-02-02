using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;
using Valve.VR.InteractionSystem;

namespace ValheimVRMod.Scripts {
    public class BowLocalManager : BowManager {
        private const float attachRange = 0.2f;
        private const float maxAimNoise = (float) Math.PI / 9;

        private GameObject arrow;
        private LineRenderer predictionLine;
        private float projectileVel;
        private float projectileVelMin;
        private Outline outline;
        private Outline arrowOutline;
        private ItemDrop.ItemData item;
        private Attack attack;
        private float fullChargeDurationSecond;
        private float drawStartTimeSecond;

        private Vector3 reusedMainHandVelocity;
        private Vector3 reusedMainHandAngularVelocity;

        public static BowLocalManager instance;
        public static float attackDrawPercentage;
        // Vanilla-style restriction applied and current charge progress.
        public static float chargePercentage = 0;
        public static Vector3 spawnPoint;
        public static Vector3 aimDir;

        public static bool isPulling;
        public static bool startedPulling;
        public static bool aborting;

        private readonly GameObject arrowAttach = new GameObject();
        

        private void Start() {
            instance = this;
            mainHand = getMainHand().transform;
            predictionLine = new GameObject().AddComponent<LineRenderer>();
            predictionLine.widthMultiplier = 0.03f;
            predictionLine.positionCount = 20;
            predictionLine.material.color = Color.white;
            predictionLine.enabled = false;
            predictionLine.receiveShadows = false;
            predictionLine.shadowCastingMode = ShadowCastingMode.Off;
            predictionLine.lightProbeUsage = LightProbeUsage.Off;
            predictionLine.reflectionProbeUsage = ReflectionProbeUsage.Off;
            
            arrowAttach.transform.SetParent(mainHand, false);
            
            outline = gameObject.AddComponent<Outline>();
            outline.OutlineColor = Color.red;
            outline.OutlineWidth = 10;
            outline.OutlineMode = Outline.Mode.OutlineVisible;
            outline.enabled = false;

            item = Player.m_localPlayer.GetLeftItem();
            if (item != null) {
                attack = item.m_shared.m_attack.Clone();
            }
            
        }

        protected new void OnDestroy() {
            base.OnDestroy();
            destroyArrow();
            Destroy(predictionLine);
            Destroy(arrowAttach);
        }

        private void destroyArrow() {
            if (arrow != null) {
                arrow.GetComponent<ZNetView>().Destroy();   
            }
        }

        private Hand getMainHand() {
            return VHVRConfig.LeftHanded() ? VRPlayer.leftHand : VRPlayer.rightHand;
        }

        private static float getFullChargeDurationSecond(float skillPercentage) {
            // In non-vr full draw takes 0.8s to 3.3s. In VR it takes around 0.5s to grab and nock an arrow, so the remaining charging time should be 0.3s to 2.8s.
            return Mathf.Lerp(2.8f, 0.3f, skillPercentage);
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

            var inputSource = VHVRConfig.LeftHanded() ? SteamVR_Input_Sources.LeftHand : SteamVR_Input_Sources.RightHand;
            
            if (SteamVR_Actions.valheim_Grab.GetState(inputSource)) {
                handlePulling();
            }

            if (SteamVR_Actions.valheim_Grab.GetStateUp(inputSource)) {
                releaseString();
            }

            if (predictionLine.enabled) {
                updatePredictionLine();   
            }

            updateOutline();
        }

        private void updateOutline() {
            
            if (outline.enabled && Player.m_localPlayer.HaveStamina(getStaminaUsage() + 0.1f)) {
                outline.enabled = false;
            } else if (! outline.enabled && ! Player.m_localPlayer.HaveStamina(getStaminaUsage() + 0.1f)) {
                outline.enabled = true;
            }

            if (arrow == null) {
                return;
            }

            if (pulling && chargePercentage < 1) {
                // Use outline color to hint the charge progress to the player.
                arrowOutline.OutlineColor = new Vector4(1, 0, 0, 1 - chargePercentage);
                arrowOutline.enabled = true;
            } else {
                arrowOutline.enabled = false;
            }
        }
        
        private float getStaminaUsage() {
            
            if (attack.m_attackStamina <= 0.0) {
                return 0.0f;   
            }
            double attackStamina = attack.m_attackStamina;
            return (float) (attackStamina - attackStamina * 0.330000013113022 * Player.m_localPlayer.GetSkillFactor(item.m_shared.m_skillType));
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
            Vector3 pos = getArrowRestPosition();
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

            if (Player.m_localPlayer.GetStamina() <= 0) {
                releaseString(true);
                return;
            }

            arrowAttach.transform.rotation = pullObj.transform.rotation;
            arrowAttach.transform.position = pullObj.transform.position;
            spawnPoint = getArrowRestPosition();
            aimDir = -transform.forward;
            var currDrawPercentage = pullPercentage();
            if (arrow != null && currDrawPercentage > attackDrawPercentage) {
                // Even with RestrictBowDrawSpeed enabled, charging duration is shorter than the full draw duration on non-vr so some amount of stamina drain should be added to compensate.
                float additionalStaminaDrain = VHVRConfig.RestrictBowDrawSpeed() ? 3 : 15;
                Player.m_localPlayer.UseStamina((currDrawPercentage - attackDrawPercentage) * (currDrawPercentage + attackDrawPercentage) * additionalStaminaDrain);
            }
            updateChargePercentage();
            attackDrawPercentage = currDrawPercentage;
        }

        private void updateChargePercentage() {
            float currentTimeSecond = Time.time;
            if (attackDrawPercentage <= 0 || drawStartTimeSecond > currentTimeSecond) {
                // Reset charge progress.
                fullChargeDurationSecond = getFullChargeDurationSecond(Player.m_localPlayer.GetSkillFactor(item.m_shared.m_skillType));
                drawStartTimeSecond = currentTimeSecond;
                return;
            }

            if (!VHVRConfig.RestrictBowDrawSpeed()) {
                chargePercentage = 1;
                return;
            }

            chargePercentage = Math.Min((currentTimeSecond - drawStartTimeSecond) / fullChargeDurationSecond, 1);
        }

        private void releaseString(bool withoutShoot = false) {
            if (!pulling) {
                return;
            }

            predictionLine.enabled = false;
            pulling = isPulling = false;
            attackDrawPercentage = pullPercentage();
            spawnPoint = getArrowRestPosition();
            aimDir = -transform.forward;

            if (withoutShoot || arrow == null || attackDrawPercentage <= 0.0f) {
                if (arrow) {
                    arrowAttach.transform.localRotation = Quaternion.identity;
                    arrowAttach.transform.localPosition = Vector3.zero;
                    if (attackDrawPercentage <= 0.0f) {
                        aborting = true;
                    }
                }

                return;
            }

            if (chargePercentage < 1) {
                // Add noise to the shooting direction to penalize premature releases.
                float aimNoise = UnityEngine.Random.Range(0, 1 - chargePercentage) * maxAimNoise;
                getMainHand().GetEstimatedPeakVelocities(out reusedMainHandVelocity, out reusedMainHandAngularVelocity);
                aimDir = Vector3.RotateTowards(aimDir, target: reusedMainHandVelocity, maxRadiansDelta: -aimNoise, maxMagnitudeDelta: 0);
            }

            // SHOOTING
            getMainHand().hapticAction.Execute(0, 0.2f, 100, 0.3f,
                VHVRConfig.LeftHanded() ? SteamVR_Input_Sources.LeftHand : SteamVR_Input_Sources.RightHand);
            destroyArrow();
        }

        private float pullPercentage() {
            return (pullObj.transform.localPosition.z - pullStart.z) / (maxPullLength - pullStart.z);
        }

        private bool checkHandNearString() {
            if (Vector3.Distance(mainHand.position, transform.TransformPoint(pullStart)) >
                attachRange) {
                return false;
            }

            if (arrow != null) {
                startedPulling = true;
                isPulling = true;
                predictionLine.enabled = VHVRConfig.UseArrowPredictionGraphic();
                attackDrawPercentage = 0;
                chargePercentage = 0;
                drawStartTimeSecond = Time.time;
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

            try {
                arrow = Instantiate(ammoItem.m_shared.m_attack.m_attackProjectile, arrowAttach.transform);
            } catch {
                return;
            }

            arrowOutline = arrow.AddComponent<Outline>();
            arrowOutline.OutlineColor = Color.red;
            arrowOutline.OutlineWidth = 10;
            arrowOutline.OutlineMode = Outline.Mode.OutlineVisible;
            arrowOutline.enabled = false;

            // we need to disable the Projectile Component, else the arrow will shoot out of the hands like a New Year rocket
            arrow.GetComponent<Projectile>().enabled = false;
            // also Destroy the Trail, as this produces particles when moving with arrow in hand
            Destroy(findTrail(arrow.transform));
            Destroy(arrow.GetComponentInChildren<Collider>());
            arrow.transform.localRotation = Quaternion.identity;
            arrow.transform.localPosition = new Vector3(0, 0, 1.25f);
            foreach (ParticleSystem particleSystem in arrow.GetComponentsInChildren<ParticleSystem>()) {
                particleSystem.transform.localScale *= VHVRConfig.ArrowParticleSize();
            }
            arrowAttach.transform.localRotation = Quaternion.identity;
            arrowAttach.transform.localPosition = Vector3.zero;

            var currentAttack = Player.m_localPlayer.GetCurrentWeapon().m_shared.m_attack;
            projectileVel = currentAttack.m_projectileVel + ammoItem.m_shared.m_attack.m_projectileVel;
            projectileVelMin = currentAttack.m_projectileVelMin + ammoItem.m_shared.m_attack.m_projectileVelMin;
            
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

        private Vector3 getArrowRestPosition()
        {
            return transform.position - transform.up * VHVRConfig.ArrowRestElevation();
        }

        public bool isHoldingArrow() {
            return arrow != null;
        }
    }
}
