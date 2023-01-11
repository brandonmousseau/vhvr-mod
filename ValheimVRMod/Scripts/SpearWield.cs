using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts {
    class ThrowableWeaponWield : WeaponWield
    {
        private SpearManager spearManager;
        void Awake()
        {
            MeshFilter meshFilter = gameObject.GetComponentInChildren<MeshFilter>();
            if (IsUlnarForwardSpear())
            {
                meshFilter.gameObject.transform.localRotation *= Quaternion.AngleAxis(180, Vector3.right);
                LogUtils.LogWarning("Spear item: " + itemName);
                switch (itemName) // TODO: is this the right property to check?
                {
                    case "SpearChitin":
                        meshFilter.gameObject.transform.localPosition = new Vector3(0, 0, -0.2f);
                        break;
                    case "SpearElderbark":
                    case "SpearBronze":
                    case "SpearCarapace":
                        meshFilter.gameObject.transform.localPosition = new Vector3(0, 0, -1.15f);
                        break;
                }
            }

            // TODO: consider renaming this ThrowableManager
            spearManager = meshFilter.gameObject.AddComponent<SpearManager>();
            spearManager.weaponWield = this;
        }

        public static bool IsRadialForwardSpear()
        {
            return IsSpear() && !VHVRConfig.SpearInverseWield();
        }

        public static bool IsUlnarForwardSpear()
        {
            return IsSpear() && VHVRConfig.SpearInverseWield();
        }

        protected bool InExclusiveOneHandedMode()
        {
            // TODO: add a base method in WeaponWield and override.
            return IsSpear() && (SpearManager.IsAiming() || SpearManager.isThrowing);
        }

        private static bool IsSpear()
        {
            // TODO: move this to EquipScript.
            return EquipScript.getRight() == EquipType.Spear || EquipScript.getRight() == EquipType.SpearChitin;
        }
    }
}
