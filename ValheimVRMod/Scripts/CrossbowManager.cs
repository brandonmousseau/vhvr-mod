using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

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

            var loaded = transform.Find("Loaded").gameObject;
            var unloaded = transform.Find("Unloaded").gameObject;

            // The mesh for the unloaded bow and and the mesh for the loaded bow are in two different child game objects.
            // We only need to use our custom bending animation on the unloaded one.
            crossbowMorphManager = unloaded.AddComponent<CrossbowMorphManager>();

            // Some crossbows' vanilla loaded model is not completely aligned with the vanilla unloaded model,
            // Fix it here so that the crossbow stays in place when loading.
            WeaponUtils.AlignLoadedMeshToUnloadedMesh(loaded, unloaded);
        }

        protected override void OnRenderObject()
        {
            base.OnRenderObject();
            isRedDotVisible = VHVRConfig.UseArrowPredictionGraphic() && twoHandedState != TwoHandedState.SingleHanded;
            crossbowMorphManager.loadBoltIfBoltInHandIsNearAnchor();
            if (twoHandedState == TwoHandedState.SingleHanded && VHVRConfig.OneHandedBow())
            {
                UpdateDominantHandAiming();
            }
        }

        private void UpdateDominantHandAiming()
        { 
            bool isAiming = VHVRConfig.LeftHanded() ? SteamVR_Actions.valheim_UseLeft.state : SteamVR_Actions.valheim_Use.state;
            if (!isAiming)
            {
                transform.localPosition = geometryProvider.GetDesiredSingleHandedPosition(this);
                transform.rotation = geometryProvider.GetDesiredSingleHandedRotation(this);
                VrikCreator.ResetHandConnectors();
                return;
            }

            Vector3 aimingDirection = VRPlayer.dominantHandRayDirection;
            transform.rotation = Quaternion.LookRotation(aimingDirection, VRPlayer.arrowHand.transform.up);
            transform.position = VRPlayer.arrowHand.transform.position + aimingDirection * INTERGRIP_DISTANCE;

            Quaternion frontHandRotation =
                VHVRConfig.LeftHanded() ? frontGripRotationForRightHand : frontGripRotationForLeftHand;
            VrikCreator.GetLocalPlayerBowHandConnector().SetPositionAndRotation(
                transform.position, transform.rotation * frontHandRotation);
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

        public static bool IsPullingTrigger()
        {
            if (instance == null)
            {
                return false;
            }

            if (!Player.m_localPlayer.IsWeaponLoaded())
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
