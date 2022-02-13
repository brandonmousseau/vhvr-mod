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
        private const float arrowLength = 1.25f;

        private GameObject arrow;
        private GameObject chargeIndicator;
        private GameObject vrikHandConnector;
        private LineRenderer predictionLine;
        private float projectileVel;
        private float projectileVelMin;
        private Outline outline;
        private ItemDrop.ItemData item;
        private Attack attack;
        private float attackDrawPercentage;

        public static BowLocalManager instance;
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

            createChargeIndicator();

            arrowAttach.transform.SetParent(mainHand, false);

            outline = gameObject.AddComponent<Outline>();
            outline.OutlineColor = Color.red;
            outline.OutlineWidth = 10;
            outline.OutlineMode = Outline.Mode.OutlineVisible;
            outline.enabled = false;

            vrikHandConnector = new GameObject();
            vrikHandConnector.transform.SetParent(mainHand, false);

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
            Destroy(chargeIndicator);
            Destroy(vrikHandConnector);
        }

        private void destroyArrow() {
            if (arrow != null) {
                arrow.GetComponent<ZNetView>().Destroy();
            }
        }

        private Hand getMainHand() {
            return VHVRConfig.LeftHanded() ? VRPlayer.leftHand : VRPlayer.rightHand;
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
            updateChargeIndicator();
        }

        private void updateOutline() {
            if (outline.enabled && Player.m_localPlayer.HaveStamina(getStaminaUsage() + 0.1f)) {
                outline.enabled = false;
            } else if (!outline.enabled && !Player.m_localPlayer.HaveStamina(getStaminaUsage() + 0.1f)) {
                outline.enabled = true;
            }
        }

        private void updateChargeIndicator() {
            if (VHVRConfig.RestrictBowDrawSpeed() && pulling && attackDrawPercentage < 1 && attackDrawPercentage > 0) {
                chargeIndicator.transform.localScale = new Vector3(0.05f * (1 - attackDrawPercentage), 0.0001f, 0.05f * (1 - attackDrawPercentage));
                chargeIndicator.GetComponent<MeshRenderer>().material.color = new Vector4(1, attackDrawPercentage, 0, 1);
                chargeIndicator.SetActive(true);
            } else {
                chargeIndicator.SetActive(false);
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

            vrikHandConnector.transform.position = pullObj.transform.position;
            arrowAttach.transform.rotation = pullObj.transform.rotation;
            arrowAttach.transform.position = pullObj.transform.position;
            spawnPoint = getArrowRestPosition();
            aimDir = getAimDir();
            if (arrow) {
                arrow.transform.position = pullObj.transform.position + aimDir * arrowLength;
                arrow.transform.rotation = Quaternion.LookRotation(aimDir, -transform.up);
            }
            var currDrawPercentage = pullPercentage();
            if (arrow != null && currDrawPercentage > attackDrawPercentage && !VHVRConfig.RestrictBowDrawSpeed()) {
                float additionalStaminaDrain = 15;
                Player.m_localPlayer.UseStamina((currDrawPercentage - attackDrawPercentage) * additionalStaminaDrain);
            }
            attackDrawPercentage = currDrawPercentage;
        }

        private void releaseString(bool withoutShoot = false) {
            if (!pulling) {
                return;
            }

            (VHVRConfig.LeftHanded() ? VRPlayer.vrikRef.solver.leftArm : VRPlayer.vrikRef.solver.rightArm).target.SetParent(mainHand, false);

            predictionLine.enabled = false;
            pulling = isPulling = false;
            attackDrawPercentage = pullPercentage();
            spawnPoint = getArrowRestPosition();
            aimDir = getAimDir();

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

            // SHOOTING FEEDBACK
            SteamVR_Input_Sources arrowHand = VHVRConfig.LeftHanded() ? SteamVR_Input_Sources.LeftHand : SteamVR_Input_Sources.RightHand;
            SteamVR_Input_Sources bowHand = VHVRConfig.LeftHanded() ? SteamVR_Input_Sources.RightHand : SteamVR_Input_Sources.LeftHand;
            getMainHand().hapticAction.Execute(0, 0.1f, 75, 0.9f, arrowHand);
            getMainHand().otherHand.hapticAction.Execute(0, 0.2f, 100, 0.3f, bowHand);
            destroyArrow();
        }

        private float pullPercentage() {
            return VHVRConfig.RestrictBowDrawSpeed() ? Math.Min(realLifePullPercentage, Player.m_localPlayer.GetAttackDrawPercentage()) : realLifePullPercentage;
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
            }

            (VHVRConfig.LeftHanded() ? VRPlayer.vrikRef.solver.leftArm : VRPlayer.vrikRef.solver.rightArm).target.SetParent(vrikHandConnector.transform, false);

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

        private void createChargeIndicator() {
            chargeIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            chargeIndicator.transform.SetParent(transform);
            chargeIndicator.transform.localPosition = new Vector3(0, -VHVRConfig.ArrowRestElevation() * 0.75f, 0);
            chargeIndicator.transform.localRotation = Quaternion.Euler(90, 0, 0);
            chargeIndicator.layer = LayerUtils.getWorldspaceUiLayer();
            chargeIndicator.SetActive(false);
            chargeIndicator.GetComponent<MeshRenderer>().material.color = new Vector4(0.5f, 0.5f, 0, 0.5f);
            chargeIndicator.GetComponent<MeshRenderer>().receiveShadows = false;
            chargeIndicator.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            chargeIndicator.GetComponent<MeshRenderer>().lightProbeUsage = LightProbeUsage.Off;
            chargeIndicator.GetComponent<MeshRenderer>().reflectionProbeUsage = ReflectionProbeUsage.Off;

            Destroy(chargeIndicator.GetComponent<Collider>());
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

        private Vector3 getArrowRestPosition() {
            return transform.position - transform.up * VHVRConfig.ArrowRestElevation() + transform.right * VHVRConfig.ArrowRestHorizontalOffset();
        }
        
        private Vector3 getAimDir() {
            return (getArrowRestPosition() - pullObj.transform.position).normalized;
        }        

        public bool isHoldingArrow() {
            return arrow != null;
        }
    }
}
