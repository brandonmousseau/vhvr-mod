using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts
{
    class SpearWield : LocalWeaponWield
    {
        private float harpoonHidingTimer = 0;
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

            if (EquipScript.getRight() == EquipType.SpearChitin)
            {
                MeshRenderer spearRenderer = throwableManager.GetComponent<MeshRenderer>();
                spearRenderer.shadowCastingMode = (harpoonHidingTimer > 0 && !ThrowableManager.isAiming) ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
            }
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
