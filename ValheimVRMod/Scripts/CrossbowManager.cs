using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;
using Valve.VR.InteractionSystem;

namespace ValheimVRMod.Scripts {
    class CrossbowManager : LocalWeaponWield
    {
        public const float INTERGRIP_DISTANCE = 0.35f;
        private static CrossbowManager instance;
        private static readonly Quaternion frontGripRotationForLeftHand = Quaternion.Euler(0, -15, 90);
        private static readonly Quaternion frontGripRotationForRightHand = Quaternion.Euler(0, 0, -120);

        private CrossbowMorphManager crossbowMorphManager;

        void Start()
        {
            instance = this;
        }

        protected override void Awake()
        {
            base.Awake();

            // Vanilla swaps the loaded and unloaded meshes through WeaponLoadState, so prefer its references and only
            // fall back to looking the children up by name: not every crossbow-like weapon (e.g. the grappling hook)
            // is guaranteed to name them "Loaded" and "Unloaded", or to have them at all.
            var weaponLoadState = GetComponentInChildren<WeaponLoadState>(true);
            GameObject loaded = weaponLoadState != null ? weaponLoadState.m_loaded : null;
            GameObject unloaded = weaponLoadState != null ? weaponLoadState.m_unloaded : null;
            if (loaded == null)
            {
                loaded = transform.Find("Loaded")?.gameObject;
            }
            if (unloaded == null)
            {
                unloaded = transform.Find("Unloaded")?.gameObject;
            }

            if (unloaded == null)
            {
                LogUtils.LogWarning("Crossbow " + name + " has no unloaded mesh; bending and manual reload are unavailable for it.");
                // Still attach the morph manager (inert without anatomy data) since the rest of the crossbow logic,
                // including two-handed wield, relies on it being present.
                crossbowMorphManager = gameObject.AddComponent<CrossbowMorphManager>();
                return;
            }

            // The mesh for the unloaded bow and and the mesh for the loaded bow are in two different child game objects.
            // We only need to use our custom bending animation on the unloaded one.
            crossbowMorphManager = unloaded.AddComponent<CrossbowMorphManager>();

            // Some crossbows' vanilla loaded model is not completely aligned with the vanilla unloaded model,
            // Fix it here so that the crossbow stays in place when loading.
            if (loaded != null)
            {
                WeaponUtils.AlignLoadedMeshToUnloadedMesh(loaded, unloaded);
            }
        }

        protected override void OnRenderObject()
        {
            base.OnRenderObject();
            isRedDotVisible = VHVRConfig.UseArrowPredictionGraphic() && twoHandedState != TwoHandedState.SingleHanded;
            crossbowMorphManager.loadBoltIfBoltInHandIsNearAnchor();
            if (twoHandedState == TwoHandedState.SingleHanded && VHVRConfig.OneHandedBow())
            {
                UpdateOneHandedAiming();
            }
            UpdateGrapplingChainAttachPoint();
        }

        protected override void OnDestroy()
        {
            grapplingChainAttachPoint = null;
            base.OnDestroy();
        }

        // Where a deployed grappling hook's chain attaches: the front of the crossbow as last rendered. Vanilla attaches
        // it to the left hand bone, which outside rendering may still be in its animated pose rather than where the hand
        // appears, so this is recorded at render time along with the crossbow transform.
        public static Vector3? grapplingChainAttachPoint { get; private set; }

        private void UpdateGrapplingChainAttachPoint()
        {
            if (!EquipScript.IsGrapplingHook(Player.m_localPlayer.GetLeftItem()))
            {
                grapplingChainAttachPoint = null;
                return;
            }

            // Same as localWeaponTip, but also after any one-handed aiming adjustment.
            grapplingChainAttachPoint =
                transform.position + GetWeaponPointingDirection() * (weaponLength - distanceBetweenGripAndRearEnd);

            // Also refresh the chain right away so it stays in sync with the crossbow for this render.
            var chain = GrapplingPoint.m_localGrappler != null ? GrapplingPoint.m_localGrappler.GetComponent<LineRenderer>() : null;
            if (chain != null)
            {
                chain.SetPosition(1, grapplingChainAttachPoint.Value);
            }
        }

        private void UpdateOneHandedAiming()
        { 
            bool isAiming =
                VHVRConfig.LeftHanded() ?
                SteamVR_Actions.valheim_UseLeft.state :
                SteamVR_Actions.valheim_Use.state;
            if (!isAiming)
            {
                transform.position = geometryProvider.GetDesiredSingleHandedPosition(this);
                transform.rotation = geometryProvider.GetDesiredSingleHandedRotation(this);
                VrikCreator.ResetHandConnectors();
                return;
            }

            Vector3 aimingDirection = VRPlayer.dominantHandRayDirection;

            if (VRPlayer.offHandWield)
            {
                // Bow hand is dominant hand, use bow hand to determine weapon transform;
                transform.position = VRPlayer.bowHand.transform.position;
                transform.rotation = Quaternion.LookRotation(aimingDirection, VRPlayer.bowHand.transform.up);
                VrikCreator.GetLocalPlayerArrowHandConnector().position =
                    VRPlayer.bowHand.transform.position - aimingDirection * INTERGRIP_DISTANCE;
            }
            else
            {
                // Arrow hand is dominant hand, use bow hand to determine weapon transform;
                transform.position =
                    VRPlayer.arrowHand.transform.position + aimingDirection * INTERGRIP_DISTANCE;
                transform.rotation = Quaternion.LookRotation(aimingDirection, VRPlayer.arrowHand.transform.up);
            }

            Quaternion frontHandRotation =
                VRPlayer.isLeftHandMainWeaponHand ?
                frontGripRotationForRightHand :
                frontGripRotationForLeftHand;
            VrikCreator.GetLocalPlayerBowHandConnector().SetPositionAndRotation(
                transform.position, transform.rotation * frontHandRotation);
        }

        // The trigger of the hand that doesn't fire: the front hand when wielding two-handed, otherwise the hand
        // holding the crossbow (the other hand's trigger fires it when wielding one-handed).
        private SteamVR_Action_Boolean OtherHandTriggerAction
        {
            get
            {
                switch (twoHandedState)
                {
                    case TwoHandedState.LeftHandBehind:
                        return SteamVR_Actions.valheim_Use;
                    case TwoHandedState.RightHandBehind:
                        return SteamVR_Actions.valheim_UseLeft;
                    default:
                        return VRPlayer.isRightHandMainWeaponHand ? SteamVR_Actions.valheim_UseLeft : SteamVR_Actions.valheim_Use;
                }
            }
        }

        void Update()
        {
            // Pressing the other hand's trigger releases a deployed grappling hook. This runs in Update rather than
            // OnRenderObject, which may run several times per frame while the hook is only destroyed at its end.
            if (GrapplingPoint.m_localGrappler != null &&
                OtherHandTriggerAction.stateDown &&
                EquipScript.IsGrapplingHook(Player.m_localPlayer.GetLeftItem()))
            {
                GrapplingPoint.m_localGrappler.Break(early: true);
            }
        }

        public static bool CanQueueReloadAction() {
            if (instance?.crossbowMorphManager != null)
            {
                return instance.crossbowMorphManager.shouldAutoReload || instance.crossbowMorphManager.isPulling;
            }

            return CrossbowAnatomy.getAnatomy(Player.m_localPlayer.GetLeftItem().m_shared.m_name) == null;
        }

        protected override void RotateHandsForTwoHandedWield(Vector3 weaponPointingDir) {
            Quaternion lookRotation = Quaternion.LookRotation(weaponPointingDir, geometryProvider.GetPreferredTwoHandedWeaponUp(this));
            switch (twoHandedState)
            {
                case TwoHandedState.LeftHandBehind:
                    VrikCreator.localPlayerRightHandConnector.rotation = lookRotation * frontGripRotationForRightHand;
                    break;
                case TwoHandedState.RightHandBehind:
                    VrikCreator.localPlayerLeftHandConnector.rotation = lookRotation * frontGripRotationForLeftHand;
                    break;
            }
        }

        protected override bool TemporaryDisableTwoHandedWield()
        {
            return crossbowMorphManager.isPulling || crossbowMorphManager.IsHandClosePullStart();
        }

        // useSecondaryAttack reports whether the pull should fire the weapon's secondary attack instead of its primary one.
        public static bool IsPullingTrigger(out bool useSecondaryAttack)
        {
            useSecondaryAttack = false;
            if (instance == null)
            {
                return false;
            }

            // Vanilla only ever reports a weapon as loaded if its attack requires reloading, and only checks the loaded
            // state for such weapons when starting the attack, so a crossbow-like weapon without reloading must not be
            // held to it.
            var weapon = Player.m_localPlayer.GetLeftItem();
            if (weapon != null && weapon.m_shared.m_attack.m_requiresReload && !Player.m_localPlayer.IsWeaponLoaded())
            {
                return false;
            }

            bool isPullingTrigger = false;
            switch (instance.twoHandedState)
            {
                case TwoHandedState.LeftHandBehind:
                    isPullingTrigger = SteamVR_Actions.valheim_UseLeft.stateDown;
                    break;
                case TwoHandedState.RightHandBehind:
                    isPullingTrigger = SteamVR_Actions.valheim_Use.stateDown;
                    break;
                default:
                    if (VHVRConfig.OneHandedBow())
                    {
                        isPullingTrigger =
                            VRPlayer.isRightHandMainWeaponHand ?
                            SteamVR_Actions.valheim_Use.stateUp :
                            SteamVR_Actions.valheim_UseLeft.stateUp;
                    }
                    break;
            }

            if (isPullingTrigger && !instance.crossbowMorphManager.isBoltLoaded)
            {
                Player.m_localPlayer.ResetLoadedWeapon();
                return false;
            }

            if (isPullingTrigger && EquipScript.IsGrapplingHook(weapon) && !string.IsNullOrEmpty(weapon.m_shared.m_secondaryAttack?.m_attackAnimation))
            {
                // In VR the hook stays deployed by default (vanilla's secondary attack); holding the other hand's
                // trigger while firing shoots and retracts instead (vanilla's primary attack).
                useSecondaryAttack = !instance.OtherHandTriggerAction.state;
            }
            
            return isPullingTrigger;
        }

        public static Vector3 AimDir {
            get { 
                if (VHVRConfig.OneHandedBow() && !isCurrentlyTwoHanded())
                {
                    return VRPlayer.dominantHandRayDirection;
                }
                return weaponForward;
            }
        }

        public static Vector3 GetBoltSpawnPoint(Attack attack)
        {
            if (VHVRConfig.OneHandedBow() && !isCurrentlyTwoHanded()) {
                return VRPlayer.arrowHand.transform.position + INTERGRIP_DISTANCE * AimDir;
            }

            return VRPlayer.bowHand.transform.position;
        }
    }
}
