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
        private GameObject arrow;
        private float gravity;
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

        public static BowLocalManager instance;
        public static Vector3 spawnPoint;
        public static Vector3 aimDir;
        public static float realLifePullPercentage { get; private set; }

        public static bool isPullingArrow;
        public static bool startedPullingArrow;
        public static bool aborting;
        public static bool finishedPulling;

        private GameObject arrowAttach;

        private MeshRenderer hideableGlowMeshRenderer;

        private void Start() {
            instance = this;
            arrowAttach = new GameObject();
            mainHand = VRPlayer.dominantHand.transform;
            predictionLine = new GameObject().AddComponent<LineRenderer>();
            predictionLine.widthMultiplier = 0.03f;
            predictionLine.positionCount = 20;
            predictionLine.material = Instantiate(VRAssetManager.GetAsset<Material>("Unlit"));
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

            hideableGlowMeshRenderer = WeaponUtils.GetHideableBowGlowMeshRenderer(transform, item.m_shared.m_name);
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

        protected override bool OnlyUseDominantHand()
        {
            return VHVRConfig.OneHandedBow();
        }

        private void destroyArrow() {
            if (arrow == null) {
                return;
            }
            
            arrow.GetComponentInChildren<ZNetView>().Destroy();
            if (arrow != null)
            {
                Destroy(arrow);
                arrow = null;
            }
        }

        private void destroyPausedCosmeticArrow()
        {
            if (pausedCosmeticArrow == null)
            {
                return;
            }

            pausedCosmeticArrow.GetComponentInChildren<ZNetView>()?.Destroy();
            if (pausedCosmeticArrow != null)
            {
                Destroy(pausedCosmeticArrow);
                pausedCosmeticArrow = null;
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
            bowHandAiming = SteamVR_Actions.valheim_Grab.GetState(bowHand) && (SteamVR_Actions.valheim_Use.GetState(bowHand) || SteamVR_Actions.valheim_UseLeft.GetState(bowHand));

            if (SteamVR_Actions.valheim_Use.GetState(arrowHand) ||
                SteamVR_Actions.valheim_UseLeft.GetState(arrowHand) ||
                SteamVR_Actions.valheim_Grab.GetState(arrowHand)) {
                handlePulling();
            }

            if (SteamVR_Actions.valheim_Use.GetStateUp(arrowHand) ||
                SteamVR_Actions.valheim_UseLeft.GetStateUp(arrowHand) ||
                SteamVR_Actions.valheim_Grab.GetStateUp(arrowHand)) {
                releaseString();
            }

            if (predictionLine.enabled) {
                updatePredictionLine();
            }

            updateOutline();
            updateChargeIndicator();

            //bHaptics
            if (!BhapticsTactsuit.suitDisabled && !pulling)
            {
                BhapticsTactsuit.StopThreadHaptic(VHVRConfig.LeftHanded() ? "BowStringLeft" : "BowStringRight");
            }

            if (hideableGlowMeshRenderer)
            {
                hideableGlowMeshRenderer.enabled = !pulling;
            }
        }

        void FixedUpdate()
        {
            if (!pulling || !MountedAttackUtils.IsRiding())
            {
                return;
            }

            // Vanilla game do no support attacking while riding so we need to explicitly apply stamina drain here.
            Player.m_localPlayer.UseStamina(Player.m_localPlayer.GetCurrentWeapon().GetDrawStaminaDrain() * Time.fixedDeltaTime);
        }


        protected override float getPullLengthRestriction(float? drawPercentage = null)
        {

            if (drawPercentage == null && RestrictBowDrawSpeed() && VHVRConfig.RestrictBowDrawSpeed() == "Full")
            {
                drawPercentage = Player.m_localPlayer.GetAttackDrawPercentage();
            }
            // If RestrictBowDrawSpeed is enabled, limit the vr pull length by the square root of the current attack draw percentage to simulate the resistance.
            return
                drawPercentage == null ?
                fullDrawLength + 0.05f :
                Mathf.Lerp(GetBraceHeight(), fullDrawLength, Math.Max(Mathf.Sqrt((float) drawPercentage), 0.01f));
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
            if (RestrictBowDrawSpeed() && pulling && drawPercent < 1 && drawPercent > 0) {
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
                vel += Vector3.down * gravity * stepLength;
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

            realLifePullPercentage = bowHandAiming || OnlyUseDominantHand() ? 1 : Mathf.Pow(Math.Min(Math.Max(pullStart.localPosition.z - pullObj.transform.localPosition.z, 0) / (fullDrawLength - GetBraceHeight()), 1), 2);
         
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
            if (OnlyUseDominantHand())
            {
                // TODO: Fix the bow hand connector position since it is slightly lower than where it should be.
                VrikCreator.GetLocalPlayerNonDominantHandConnector().position = pushObj.transform.position;
            }

            arrowAttach.transform.SetPositionAndRotation(pullObj.transform.position, pushObj.transform.rotation);
            spawnPoint = getArrowRestPosition();
            aimDir = getAimDir();
            if (arrow) {
                arrow.transform.position = pullObj.transform.position;
                arrow.transform.rotation = Quaternion.LookRotation(aimDir, bowOrientation.transform.up);
            }
            var currDrawPercentage = pullPercentage();
            currentMaxDrawPercentage = Math.Max(currDrawPercentage, currentMaxDrawPercentage);

            if (arrow != null && currentMaxDrawPercentage > attackDrawPercentage && !RestrictBowDrawSpeed()) {
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

            bowOrientation.transform.localPosition = Vector3.zero;

            predictionLine.enabled = false;
            pulling = isPullingArrow = false;
            finishedPulling = false;
            attackDrawPercentage = pullPercentage();
            currentMaxDrawPercentage = 0;
            spawnPoint = getArrowRestPosition();
            aimDir = getAimDir();

            if (!withoutShoot && arrow)
            {
                // Force starting attack here since vanilla game does not support attacking while riding.
                MountedAttackUtils.StartAttackIfRiding(isSecondaryAttack: false, realLifePullPercentage);
            }

            if (withoutShoot || arrow == null || attackDrawPercentage <= 0.0f) {
                if (arrow) {
                    resetArrowAttachToHand();
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
            return RestrictBowDrawSpeed() ? Math.Min(realLifePullPercentage, Player.m_localPlayer.GetAttackDrawPercentage()) : realLifePullPercentage;
        }

        private bool checkHandNearString() {
            var nock = pullStart.position + bowOrientation.TransformVector(Vector3.up * VHVRConfig.ArrowRestElevation());
            if (!OnlyUseDominantHand() && Vector3.Distance(mainHand.position, nock) > attachRange) {
                return false;
            }

            if (OnlyUseDominantHand() && arrow == null)
            {
                toggleArrow();
            }

            if (arrow != null) {
                startedPullingArrow = true;
                isPullingArrow = true;
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

            arrow = new GameObject();
            arrow.transform.parent = arrowAttach.transform;
            GameObject arrowModel;
            try
            {
                gravity = ammoItem.m_shared.m_attack.m_attackProjectile.GetComponent<Projectile>().m_gravity;

                switch (ammoItem.m_shared.m_name)
                {
                    case "$item_arrow_obsidian":
                    case "$item_arrow_charred":
                        // The projectile prefab of these arrows are incorrect. Use the drop prefab instead.
                        arrowModel = Instantiate(ammoItem.m_dropPrefab, arrow.transform);
                        arrowModel.GetComponent<ZNetView>().SetLocalScale(Vector3.one * 1.3f);
                        Destroy(arrowModel.GetComponent<ParticleSystemRenderer>()); // Do not display the particles indicating a pickable item
                        Destroy(arrowModel.GetComponent<ParticleSystem>());
                        Destroy(arrowModel.GetComponent<ItemDrop>()); // Do not let the object drop from hand
                        Destroy(arrowModel.GetComponent<Rigidbody>()); // Do not let the object drop from hand
                        break;
                    default:
                        arrowModel = Instantiate(ammoItem.m_shared.m_attack.m_attackProjectile, arrow.transform);
                        Destroy(arrowModel.GetComponent<Projectile>()); // Do not let the arrow automatically launch from hand
                        Destroy(findTrail(arrowModel.transform));
                        arrowModel.GetComponent<ZNetView>().SetLocalScale(Vector3.one * 0.78f);
                        break;
                }
            }
            catch (Exception e) {
                LogUtils.LogError(e.Message);
                return;
            }

            switch (ammoItem.m_shared.m_name)
            {
                case "$item_arrow_carapace":
                    var offset = VHVRConfig.ArrowRestHorizontalOffsetMultiplier();
                    arrowModel.transform.localRotation =  Quaternion.Euler(0, 0, offset > 0 ? 45 : (offset < 0 ? -45 : 0));
                    arrowModel.transform.localPosition = new Vector3(0, 0, 0.605f);
                    break;
                case "$item_arrow_charred":
                    arrowModel.transform.localPosition = new Vector3(0, 0, 0.49f);
                    break;
                case "$item_arrow_fire":
                case "$item_arrow_poison":
                    arrowModel.transform.localPosition = new Vector3(0, 0, 0.97f);
                    break;
                case "$item_arrow_frost":
                    arrowModel.transform.localPosition = new Vector3(0, 0, 1.02f);
                    break;
                case "$item_arrow_needle":
                    arrowModel.transform.localPosition = new Vector3(0, 0, 0.605f);
                    break;
                case "$item_arrow_obsidian":
                    arrowModel.transform.localRotation = Quaternion.Euler(0, 180, 0);
                    arrowModel.transform.localPosition = new Vector3(0, 0.03f, 0.5f);
                    break;
                default:
                    arrowModel.transform.localPosition = new Vector3(0, 0, 0.99f);
                    break;
            }

            //bHaptics
            if (!BhapticsTactsuit.suitDisabled)
            {
                BhapticsTactsuit.PlaybackHaptics(VHVRConfig.LeftHanded() ?
                    "UnholsterArrowLeftShoulder" : "UnholsterArrowRightShoulder");
            }

            var collider = arrow.GetComponentInChildren<Collider>();
            if (collider)
            {
                Destroy(collider);
            }

            resetArrowAttachToHand();
            arrow.transform.localPosition = Vector3.zero;
            arrow.transform.localRotation = Quaternion.identity;
            foreach (ParticleSystem particleSystem in arrow.GetComponentsInChildren<ParticleSystem>()) {
                particleSystem.transform.localScale *= VHVRConfig.ArrowParticleSize();
            }

            var currentAttack = Player.m_localPlayer.GetCurrentWeapon().m_shared.m_attack;
            projectileVel = currentAttack.m_projectileVel + ammoItem.m_shared.m_attack.m_projectileVel;
            projectileVelMin = currentAttack.m_projectileVelMin + ammoItem.m_shared.m_attack.m_projectileVelMin;
            
        }

        private void resetArrowAttachToHand()
        {
            arrowAttach.transform.localRotation = Quaternion.identity;
            arrowAttach.transform.localPosition = -0.05f * Vector3.forward;
        }

        private void createChargeIndicator() {
            chargeIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            chargeIndicator.transform.SetParent(bowOrientation.transform);
            chargeIndicator.transform.localPosition = new Vector3(0, VHVRConfig.ArrowRestElevation() * 0.75f, 0);
            chargeIndicator.transform.localRotation = Quaternion.Euler(90, 0, 0);
            chargeIndicator.layer = LayerUtils.getWorldspaceUiLayer();
            chargeIndicator.SetActive(false);
            var chargeIndicatorRendrer = chargeIndicator.GetComponent<MeshRenderer>();
            chargeIndicatorRendrer.material = Instantiate(VRAssetManager.GetAsset<Material>("Unlit"));
            chargeIndicatorRendrer.material.color = new Vector4(0.5f, 0.5f, 0, 0.5f);
            chargeIndicatorRendrer.receiveShadows = false;
            chargeIndicatorRendrer.shadowCastingMode = ShadowCastingMode.Off;
            chargeIndicatorRendrer.lightProbeUsage = LightProbeUsage.Off;
            chargeIndicatorRendrer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            Destroy(chargeIndicator.GetComponent<Collider>());

            drawIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            drawIndicator.transform.SetParent(bowOrientation.transform);
            drawIndicator.transform.localPosition = new Vector3(0, VHVRConfig.ArrowRestElevation() * 0.75f, 0);
            drawIndicator.transform.localRotation = Quaternion.Euler(90, 0, 0);
            drawIndicator.layer = LayerUtils.getWorldspaceUiLayer();
            drawIndicator.SetActive(false);
            var drawIndicatorRendrer = drawIndicator.GetComponent<MeshRenderer>();
            drawIndicatorRendrer.material = Instantiate(VRAssetManager.GetAsset<Material>("Unlit"));
            drawIndicatorRendrer.material.color = new Vector4(0.5f, 0.5f, 0, 0.5f);
            drawIndicatorRendrer.receiveShadows = false;
            drawIndicatorRendrer.shadowCastingMode = ShadowCastingMode.Off;
            drawIndicatorRendrer.lightProbeUsage = LightProbeUsage.Off;
            drawIndicatorRendrer.reflectionProbeUsage = ReflectionProbeUsage.Off;

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

        private bool RestrictBowDrawSpeed() 
        {
            // When riding, vanilla bow draw is restricted to zero so we have to force bypassing it.
            return VHVRConfig.RestrictBowDrawSpeed() != "None" && !MountedAttackUtils.IsRiding(); 
        }
    }
}
