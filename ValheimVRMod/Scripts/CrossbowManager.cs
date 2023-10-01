using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts {
    class CrossbowManager : LocalWeaponWield
    {
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
            // The mesh for the unloaded bow and and the mesh for the loaded bow are in two different child game objects.
            // We only need to use our custom bending animation on the unloaded one.
            crossbowMorphManager = transform.Find("Unloaded").gameObject.AddComponent<CrossbowMorphManager>();
        }

        protected override void OnRenderObject()
        {
            base.OnRenderObject();
            isRedDotVisible = VHVRConfig.UseArrowPredictionGraphic() && twoHandedState != TwoHandedState.SingleHanded;
            CrossbowMorphManager.instance.loadBoltIfBoltinHandIsNearAnchor();
        }

        public static bool CanQueueReloadAction() {
            if (instance?.crossbowMorphManager != null)
            {
                return instance.crossbowMorphManager.shouldAutoReload || instance.crossbowMorphManager.isPulling;
            }

            return CrossbowAnatomy.getAnatomy(Player.m_localPlayer.GetLeftItem().m_shared.m_name) == null;
        }

        protected override void RotateHandsForTwoHandedWield(Vector3 weaponPointingDir) {
            Quaternion lookRotation = Quaternion.LookRotation(weaponPointingDir, GetPreferredTwoHandedWeaponUp());
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
        
        protected override Vector3 GetWeaponPointingDir()
        {
            return transform.forward;
        }        

        protected override Quaternion GetSingleHandedRotation(Quaternion originalRotation)
        {
            // Make sure the top of the bow is facing up when holding it one-handed.
            return VHVRConfig.LeftHanded() ? originalRotation * Quaternion.AngleAxis(180, Vector3.forward) : originalRotation;
        }

        protected override Vector3 GetPreferredTwoHandedWeaponUp()
        {
            Vector3 rearHandleUp = Vector3.Cross(frontHandTransform.position - rearHandTransform.position, rearHandTransform.right).normalized;
            switch (VHVRConfig.CrossbowSaggitalRotationSource())
            {
                case "RearHand":
                    return rearHandleUp;
                case "BothHands":
                    Vector3 frontHandPalmar = twoHandedState == TwoHandedState.LeftHandBehind ? -frontHandTransform.right : frontHandTransform.right;
                    Vector3 frontHandRadial = frontHandTransform.up;
                    Vector3 frontHandleUp = (frontHandPalmar * 1.73f + frontHandRadial).normalized;
                    return frontHandleUp + rearHandleUp;
                default:
                    LogUtils.LogWarning("WeaponWield: unknown CrossbowSaggitalRotationSource");
                    return rearHandleUp;
            }
        }

        protected override float GetPreferredOffsetFromRearHand(float handDist)
        {
            return 0.35f;
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

            bool isPullingTrigger = false;
            switch (instance.twoHandedState)
            {
                case TwoHandedState.LeftHandBehind:
                    isPullingTrigger = SteamVR_Actions.valheim_UseLeft.stateDown;
                    break;
                case TwoHandedState.RightHandBehind:
                    isPullingTrigger = SteamVR_Actions.valheim_Use.stateDown;
                    break;
            }

            if (isPullingTrigger && !CrossbowMorphManager.instance.isBoltLoaded)
            {
                Player.m_localPlayer.ResetLoadedWeapon();
                return false;
            }
            
            return isPullingTrigger;
        }

        public static Vector3 AimDir { get { return weaponForward; } }

        public static Vector3 GetBoltSpawnPoint(Attack attack)
        {
            // TODO: simplify logic?
            return VRPlayer.rightPointer.rayStartingPosition + weaponForward * (new Vector3(attack.m_attackOffset, attack.m_attackRange, attack.m_attackHeight)).magnitude * 0.4f;
        }
    }
}
