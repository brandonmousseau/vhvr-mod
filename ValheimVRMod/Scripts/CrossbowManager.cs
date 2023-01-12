using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts {
    class CrossbowManager : WeaponWield
    {
        private static CrossbowManager instance;
        private static readonly Quaternion frontGripRotationForLeftHand = Quaternion.Euler(0, -15, 90);
        private static readonly Quaternion frontGripRotationForRightHand = Quaternion.Euler(0, 15, -90);

        private Quaternion originalLocalRotation;

        void Start()
        {
            instance = this;
        }

        void Awake()
        {
            originalLocalRotation = transform.localRotation;
        }

        protected override void RotateHandsForTwoHandedWield(Vector3 weaponHoldVector) {
            if (VHVRConfig.CrossbowSaggitalRotationSource() != "RearHand")
            {
                return;
            }

            Quaternion lookRotation = Quaternion.LookRotation(weaponHoldVector, rearHand.transform.up);
            if (frontHand == VRPlayer.leftHand)
            {
                VrikCreator.leftHandConnector.rotation = lookRotation * frontGripRotationForLeftHand;
            }
            else
            {
                VrikCreator.rightHandConnector.rotation = lookRotation * frontGripRotationForRightHand;
            }
        }

        protected override Quaternion GetSingleHandedRotation(Quaternion originalRotation)
        {
            // Make sure the top of the bow is facing up when holding it one-handed.
            return VHVRConfig.LeftHanded() ? originalRotation * Quaternion.AngleAxis(180, Vector3.forward) : originalRotation;
        }

        protected override bool TemporaryDisableTwoHandedWield()
        {
            return crossbowMorphManager.isPulling || crossbowMorphManager.IsHandClosePullStart();
        }

        public static bool IsPullingTrigger()
        {
            if (instance == null || !isCurrentlyTwoHanded())
            {
                return false;
            }
            return instance.rearHand == VRPlayer.leftHand ? SteamVR_Actions.valheim_UseLeft.stateDown : SteamVR_Actions.valheim_Use.stateDown;
        }

        public static Vector3 AimDir { get { return weaponForward; } }

        public static Vector3 GetBoltSpawnPoint(Attack attack)
        {
            // TODO: simplify logic?
            return VRPlayer.rightPointer.rayStartingPosition + weaponForward * (new Vector3(attack.m_attackOffset, attack.m_attackRange, attack.m_attackHeight)).magnitude * 0.4f;
        }
    }
}
