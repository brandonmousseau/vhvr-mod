using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts {
    private SpearManager spearManager;
    class SpearWield : WeaponWield
    {
        void Awake()
        {
            spearManager = gameObject.GetComponentInChildren<MeshFilter>().addComponent<SpearManager>();
        }
    }
}
