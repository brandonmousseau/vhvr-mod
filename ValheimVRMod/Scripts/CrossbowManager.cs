using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts {
    class CrossbowManager : WeaponWield
    {
        private static CrossbowManager instance;

        private Quaternion originalLocalRotation;

        void Start()
        {
            instance = this;
        }

        void Awake()
        {
            originalLocalRotation = transform.localRotation;
        }

        protected override void OnRenderObject()
        {
            if (!isCurrentlyTwoHanded() && VHVRConfig.LeftHanded())
            {
                // Make sure the top of the bow is facing the up when holding it one-handed.
                transform.localRotation = originalLocalRotation * Quaternion.AngleAxis(180, Vector3.forward);
            } else {
                transform.locationRotation = originalLocalRotation;
            }
            base.OnRenderObject();
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
