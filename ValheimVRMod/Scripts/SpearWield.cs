using ValheimVRMod.VRCore.Backends;
using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using ValheimVRMod.VRCore.UI;

namespace ValheimVRMod.Scripts
{
    class SpearWield : LocalWeaponWield
    {
        private float harpoonHidingTimer = 0;
        // How long other clients are told to release what is harpooned at least, so that it gets through to them.
        private const float HARPOON_RELEASE_SIGNAL_MIN_DURATION = 0.5f;
        private float harpoonReleaseSignalCountdown = 0;
        public static bool isSignallingHarpoonRelease { get; private set; }
        public static Vector3 lastFixedUpdatedAimDir { get; private set; }


        void FixedUpdate()
        {
            // Record the aiming direction here instead of querying it whenever so that when OnRenderObject() is called
            // for two eyes in the same frame the value would not be different.
            lastFixedUpdatedAimDir = ThrowableManager.aimDir;

            var throwableManager = GetComponentInChildren<ThrowableManager>();

            if (harpoonHidingTimer > 0)
            {
                harpoonHidingTimer -= Time.fixedDeltaTime;
            }

            if (EquipScript.CurrentMainHandEquipType() == EquipType.SpearChitin)
            {
                MeshRenderer spearRenderer = throwableManager.GetComponent<MeshRenderer>();
                spearRenderer.shadowCastingMode = (harpoonHidingTimer > 0 && !ThrowableManager.isAiming) ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        void Update()
        {
            // The trigger of the hand that is not holding the harpoon lets go of what is harpooned.
            var freeHand = VRPlayer.secondaryWeaponHandInputSource;
            if (EquipScript.CurrentMainHandEquipType() == EquipType.SpearChitin &&
                !isCurrentlyTwoHanded() &&
                !LaserPointerChords.IsLaserActiveFor(freeHand) &&
                VRInputActions.valheim_Use.GetState(freeHand))
            {
                harpoonReleaseSignalCountdown = HARPOON_RELEASE_SIGNAL_MIN_DURATION;
            }
            else if (harpoonReleaseSignalCountdown > 0)
            {
                harpoonReleaseSignalCountdown -= Time.deltaTime;
            }

            UpdateHarpoonReleaseSignal(harpoonReleaseSignalCountdown > 0);
        }

        protected override void OnDestroy()
        {
            UpdateHarpoonReleaseSignal(false);
            base.OnDestroy();
        }

        // Vanilla lets go of a harpooned character when the player who harpooned it is blocking. Blocking for real
        // is not something that can be done at will in VR, so while the signal is on the player is only reported as
        // blocking to what is harpooned, without anything being blocked: to the characters that are owned by this
        // client by PatchIsBlocking and to the ones that are owned by other clients by the synced blocking state.
        private void UpdateHarpoonReleaseSignal(bool signalling)
        {
            if (!signalling && !isSignallingHarpoonRelease)
            {
                return;
            }
            isSignallingHarpoonRelease = signalling;

            var player = Player.m_localPlayer;
            if (player == null || player.m_nview == null || !player.m_nview.IsValid())
            {
                return;
            }
            // Vanilla only writes this when the player starts or stops blocking, so it has to be written back when
            // the signal ends and kept up while it lasts in case vanilla writes it in between.
            player.m_nview.GetZDO().Set(ZDOVars.s_isBlockingHash, signalling || player.m_internalBlockingState);
        }

        public void HideHarpoon()
        {
            harpoonHidingTimer = 1;
        }

        protected override bool TemporaryDisableTwoHandedWield()
        {
            return ThrowableManager.isAiming || ThrowableManager.isThrowing;
        }
    }
}
