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
        private const float legacyArrowCenterToTailDistance = 1.25f;
        private const float shortArrowCenterToTailDistance = 0.78f;

        private GameObject arrow;
        private GameObject pausedCosmeticArrow; // An arrow shown on the bow when player movment is paused even after the actual arrow is shot for cosmetic purposes.
        private GameObject chargeIndicator;
        private GameObject drawIndicator;
        private LineRenderer predictionLine;
        private float fullDrawLength { get { return Mathf.Max(VHVRConfig.GetBowFullDrawLength(), GetBraceHeight() + 0.1f); } }
        private float projectileVel;
        private float projectileVelMin;
        private ItemDrop.ItemData item;
        private Attack attack;
        private float attackDrawPercentage;
        private float currentMaxDrawPercentage;
        private float centerToTailDistance = legacyArrowCenterToTailDistance;

        public static BowLocalManager instance;
        public static Vector3 spawnPoint;
        public static Vector3 aimDir;
        public static float realLifePullPercentage { get; private set; }

        public static bool isPulling;
        public static bool startedPulling;
        public static bool aborting;
        public static bool finishedPulling;

        private GameObject arrowAttach;


        private void Start() {
            instance = this;
            arrowAttach = new GameObject();
            mainHand = VRPlayer.dominantHand.transform;
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
            drawIndicator.transform.localPosition -= Vector3.forward * 0.001f;

            arrowAttach.transform.SetParent(mainHand, false);

            item = Player.m_localPlayer.GetLeftItem();
            if (item != null) {
                attack = item.m_shared.m_attack.Clone();
            }

        }

        protected new void OnDestroy() {
            base.OnDestroy();
            destroyArrow();
            destroyPausedCosmeticArrow();
            Destroy(predictionLine);
            Destroy(arrowAttach);
            Destroy(chargeIndicator);
            Destroy(drawIndicator);
        }

        private void destroyArrow() {
            if (arrow != null) {
                arrow.GetComponent<ZNetView>().Destroy();
            }
        }

        private void destroyPausedCosmeticArrow()
        {
            if (pausedCosmeticArrow != null)
            {
                Destroy(pausedCosmeticArrow);
            }
            pausedCosmeticArrow = null;
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
            if (VRPlayer.ShouldPauseMovement)
            {
                if (arrow != null && pausedCosmeticArrow == null)
                {
                    // Show an arrow on the bow when player movement is paused.
                    // This arrow will persist after the actual arrow is shown until player movement is unpaused.
                    // It is purely cosmetic and has no effect on arrow shooting and attacks in actual gameplay.
                    pausedCosmeticArrow = Instantiate(arrow, bowOrientation);
                    pausedCosmeticArrow.transform.SetPositionAndRotation(arrow.transform.position, arrow.transform.rotation);
                }
            }
            else
            {
                destroyPausedCosmeticArrow();
            }


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

        protected override float getPullLenghtRestriction()
        {
            // If RestrictBowDrawSpeed is enabled, limit the vr pull length by the square root of the current attack draw percentage to simulate the resistance.
            return
                VHVRConfig.RestrictBowDrawSpeed() == "Full" ?
                    Mathf.Lerp(GetBraceHeight(), fullDrawLength, Math.Max(Mathf.Sqrt(Player.m_localPlayer.GetAttackDrawPercentage()), 0.01f)) :
                    fullDrawLength + 0.05f;
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
            if (pulling && realLifePullPercentage < 1 && realLifePullPercentage > 0)
            {
                drawIndicator.transform.localScale = new Vector3(0.05f * (1 - realLifePullPercentage), 0.0001f, 0.05f * (1 - realLifePullPercentage));
                drawIndicator.GetComponent<MeshRenderer>().material.color = new Vector4(0f, realLifePullPercentage, 1, 1);
                drawIndicator.SetActive(true);
            }
            else
            {
                drawIndicator.SetActive(false);
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
            var isBowDrawable = Player.m_localPlayer.GetLeftItem().m_shared.m_attack.m_bowDraw;
            Vector3 vel = aimDir * Mathf.Lerp(projectileVelMin, projectileVel, isBowDrawable ? pullPercentage():1);

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

            realLifePullPercentage = oneHandedAiming ? 1 : Mathf.Pow(Math.Min(Math.Max(pullStart.localPosition.z - pullObj.transform.localPosition.z, 0) / (fullDrawLength - GetBraceHeight()), 1), 2);
         
            //bHaptics
            if (!BhapticsTactsuit.suitDisabled && realLifePullPercentage != 0)
            {
                BhapticsTactsuit.StartThreadHaptic(VHVRConfig.LeftHanded() ? "BowStringLeft" : "BowStringRight",
                    realLifePullPercentage * 1.5f, true);
                // ARMS TACTOSY
                BhapticsTactsuit.StartThreadHaptic(VHVRConfig.LeftHanded() ? "Recoil_L" : "Recoil_R",
                    realLifePullPercentage * 1.5f, true);
            }

            VrikCreator.GetLocalPlayerDominantHandConnector().position = pullObj.transform.position;
            arrowAttach.transform.SetPositionAndRotation(pullObj.transform.position, pushObj.transform.rotation);
            spawnPoint = getArrowRestPosition();
            aimDir = getAimDir();
            if (arrow) {
                arrow.transform.position = pullObj.transform.position + aimDir * centerToTailDistance;
                arrow.transform.rotation = Quaternion.LookRotation(aimDir, bowOrientation.transform.up);
            }
            var currDrawPercentage = pullPercentage();
            currentMaxDrawPercentage = Math.Max(currDrawPercentage, currentMaxDrawPercentage);
            if (arrow != null && currentMaxDrawPercentage > attackDrawPercentage && VHVRConfig.RestrictBowDrawSpeed() == "None") {
                float additionalStaminaDrain = 15;
                Player.m_localPlayer.UseStamina((currentMaxDrawPercentage - attackDrawPercentage) * additionalStaminaDrain * VHVRConfig.GetBowStaminaScalar());
            }
            attackDrawPercentage = currentMaxDrawPercentage;
            if (attackDrawPercentage == 1 && !finishedPulling) 
            {
                finishedPulling = true;
                VRPlayer.dominantHand.otherHand.hapticAction.Execute(0, 0.2f, 100, 0.3f, VRPlayer.nonDominantHandInputSource);
            }
        }

        private void releaseString(bool withoutShoot = false) {
            if (!pulling) {
                return;
            }

            VrikCreator.ResetHandConnectors();

            predictionLine.enabled = false;
            pulling = isPulling = false;
            finishedPulling = false;
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
            VRPlayer.dominantHand.hapticAction.Execute(0, 0.1f, 75, 0.9f, VRPlayer.dominantHandInputSource);
            VRPlayer.dominantHand.otherHand.hapticAction.Execute(0, 0.2f, 100, 0.3f, VRPlayer.nonDominantHandInputSource);
            destroyArrow();
        }

        private float pullPercentage() {
            return VHVRConfig.RestrictBowDrawSpeed() != "None" ? Math.Min(realLifePullPercentage, Player.m_localPlayer.GetAttackDrawPercentage()) : realLifePullPercentage;
        }

        private bool checkHandNearString() {
            if (Vector3.Distance(mainHand.position, pullStart.position) > attachRange) {
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

            ItemDrop.ItemData ammoItem = EquipScript.equipAmmo();
            if (ammoItem == null)
            {
                // Out of ammo
                return;
            }

            switch (ammoItem.m_shared.m_name)
            {
                case "$item_arrow_needle":
                case "$item_arrow_carapace":
                    centerToTailDistance = shortArrowCenterToTailDistance;
                    break;
                default:
                    centerToTailDistance = legacyArrowCenterToTailDistance;
                    break;
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
            arrow.transform.localPosition = new Vector3(0, 0, centerToTailDistance);
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
            chargeIndicator.transform.SetParent(bowOrientation.transform);
            chargeIndicator.transform.localPosition = new Vector3(0, VHVRConfig.ArrowRestElevation() * 0.75f, 0);
            chargeIndicator.transform.localRotation = Quaternion.Euler(90, 0, 0);
            chargeIndicator.layer = LayerUtils.getWorldspaceUiLayer();
            chargeIndicator.SetActive(false);
            chargeIndicator.GetComponent<MeshRenderer>().material.color = new Vector4(0.5f, 0.5f, 0, 0.5f);
            chargeIndicator.GetComponent<MeshRenderer>().receiveShadows = false;
            chargeIndicator.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            chargeIndicator.GetComponent<MeshRenderer>().lightProbeUsage = LightProbeUsage.Off;
            chargeIndicator.GetComponent<MeshRenderer>().reflectionProbeUsage = ReflectionProbeUsage.Off;

            Destroy(chargeIndicator.GetComponent<Collider>());

            drawIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            drawIndicator.transform.SetParent(bowOrientation.transform);
            drawIndicator.transform.localPosition = new Vector3(0, VHVRConfig.ArrowRestElevation() * 0.75f, 0);
            drawIndicator.transform.localRotation = Quaternion.Euler(90, 0, 0);
            drawIndicator.layer = LayerUtils.getWorldspaceUiLayer();
            drawIndicator.SetActive(false);
            drawIndicator.GetComponent<MeshRenderer>().material.color = new Vector4(0.5f, 0.5f, 0, 0.5f);
            drawIndicator.GetComponent<MeshRenderer>().receiveShadows = false;
            drawIndicator.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            drawIndicator.GetComponent<MeshRenderer>().lightProbeUsage = LightProbeUsage.Off;
            drawIndicator.GetComponent<MeshRenderer>().reflectionProbeUsage = ReflectionProbeUsage.Off;

            Destroy(drawIndicator.GetComponent<Collider>());
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
