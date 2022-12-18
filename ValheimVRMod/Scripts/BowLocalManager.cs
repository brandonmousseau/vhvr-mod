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
        private LineRenderer predictionLine;
        private float projectileVel;
        private float projectileVelMin;
        private ItemDrop.ItemData item;
        private Attack attack;
        private float attackDrawPercentage;
        private float currentMaxDrawPercentage;

        public static BowLocalManager instance;
        public static Vector3 spawnPoint;
        public static Vector3 aimDir;

        public static bool isPulling;
        public static bool startedPulling;
        public static bool aborting;

        private GameObject arrowAttach;


        private void Start() {
            instance = this;
            arrowAttach = new GameObject();
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

            var arrowHand = VHVRConfig.LeftHanded() ? SteamVR_Input_Sources.LeftHand : SteamVR_Input_Sources.RightHand;
            var bowHand = VHVRConfig.LeftHanded() ? SteamVR_Input_Sources.RightHand : SteamVR_Input_Sources.LeftHand;

            // Enable using the bow hand orientation alone for aiming if the bow hand is holding down both the grip and the trigger.
            oneHandedAiming = SteamVR_Actions.valheim_Grab.GetState(bowHand) && (SteamVR_Actions.valheim_Use.GetState(bowHand) || SteamVR_Actions.valheim_UseLeft.GetState(bowHand));

            if (SteamVR_Actions.valheim_Grab.GetState(arrowHand)) {
                handlePulling();
            }

            if (SteamVR_Actions.valheim_Grab.GetStateUp(arrowHand)) {
                releaseString();
            }

            if (predictionLine.enabled) {
                updatePredictionLine();
            }

            updateOutline();
            updateChargeIndicator();

            //bHaptics
            if (!BhapticsTactsuit.suitDisabled && !isPulling)
            {
                BhapticsTactsuit.StopThreadHaptic(VHVRConfig.LeftHanded() ? "BowStringLeft" : "BowStringRight");
            }
        }

        private void updateOutline() {
            if (outline == null) {
                return;
            }
            if (outline.enabled && Player.m_localPlayer.HaveStamina(getStaminaUsage() + 0.1f)) {
                outline.enabled = false;
            } else if (!outline.enabled && !Player.m_localPlayer.HaveStamina(getStaminaUsage() + 0.1f)) {
                outline.enabled = true;
            }
        }

        private void updateChargeIndicator() {
            var drawPercent = Player.m_localPlayer.GetAttackDrawPercentage();
            if (VHVRConfig.RestrictBowDrawSpeed() != "None" && pulling && drawPercent < 1 && drawPercent > 0) {
                chargeIndicator.transform.localScale = new Vector3(0.05f * (1 - drawPercent), 0.0001f, 0.05f * (1 - drawPercent));
                chargeIndicator.GetComponent<MeshRenderer>().material.color = new Vector4(1, drawPercent, 0, 1);
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

            Vector3 vel = aimDir * Mathf.Lerp(projectileVelMin, projectileVel, pullPercentage());

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

            VrikCreator.GetDominantHandConnector().position = pullObj.transform.position;
            arrowAttach.transform.rotation = pullObj.transform.rotation;
            arrowAttach.transform.position = pullObj.transform.position;
            spawnPoint = getArrowRestPosition();
            aimDir = getAimDir();
            if (arrow) {
                arrow.transform.position = pullObj.transform.position + aimDir * arrowLength;
                arrow.transform.rotation = Quaternion.LookRotation(aimDir, -bowOrientation.transform.up);
            }
            var currDrawPercentage = pullPercentage();
            currentMaxDrawPercentage = Math.Max(currDrawPercentage, currentMaxDrawPercentage);
            if (arrow != null && currentMaxDrawPercentage > attackDrawPercentage && VHVRConfig.RestrictBowDrawSpeed() == "None") {
                float additionalStaminaDrain = 15;
                Player.m_localPlayer.UseStamina((currentMaxDrawPercentage - attackDrawPercentage) * additionalStaminaDrain * VHVRConfig.GetBowStaminaScalar());
            }
            attackDrawPercentage = currentMaxDrawPercentage;
        }

        private void releaseString(bool withoutShoot = false) {
            if (!pulling) {
                return;
            }

            VrikCreator.ResetHandConnectors();

            predictionLine.enabled = false;
            pulling = isPulling = false;
            attackDrawPercentage = pullPercentage();
            currentMaxDrawPercentage = 0;
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
            return VHVRConfig.RestrictBowDrawSpeed() != "None" ? Math.Min(realLifePullPercentage, Player.m_localPlayer.GetAttackDrawPercentage()) : realLifePullPercentage;
        }

        private bool checkHandNearString() {
            if (Vector3.Distance(mainHand.position, bowOrientation.transform.TransformPoint(pullStart)) >
                attachRange) {
                return false;
            }

            if (arrow != null) {
                startedPulling = true;
                isPulling = true;
                predictionLine.enabled = VHVRConfig.UseArrowPredictionGraphic();
                attackDrawPercentage = 0;
            }

            return pulling = true;
        }

        public void toggleArrow() {
            if (arrow != null) {
                destroyArrow();
                //bHaptics
                if (!BhapticsTactsuit.suitDisabled)
                {
                    BhapticsTactsuit.PlaybackHaptics(VHVRConfig.LeftHanded() ?
                         "HolsterArrowLeftShoulder" : "HolsterArrowRightShoulder");
                }
                return;
            }
            
            var ammoItem = Player.m_localPlayer.GetAmmoItem();
            
            if (ammoItem == null || ammoItem.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Ammo) {
                // out of ammo
                if (!Attack.HaveAmmo(Player.m_localPlayer, item))
                {
                    return;
                }
                Attack.EquipAmmoItem(Player.m_localPlayer, item);
            }

            try {
                arrow = Instantiate(ammoItem.m_shared.m_attack.m_attackProjectile, arrowAttach.transform);
            } catch {
                return;
            }

            //bHaptics
            if (!BhapticsTactsuit.suitDisabled)
            {
                BhapticsTactsuit.PlaybackHaptics(VHVRConfig.LeftHanded() ?
                    "UnholsterArrowLeftShoulder" : "UnholsterArrowRightShoulder");
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

        private Vector3 getAimDir() {
            return (getArrowRestPosition() - pullObj.transform.position).normalized;
        }        

        public bool isHoldingArrow() {
            return arrow != null;
        }
        public float GetAttackPercentage()
        {
            return attackDrawPercentage;
        }
    }
}
