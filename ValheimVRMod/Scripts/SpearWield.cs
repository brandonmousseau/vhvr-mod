using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts {
    class SpearWield : WeaponWield
    {
    private SpearManager spearManager;
    void Awake()
        {
            spearManager = gameObject.GetComponentInChildren<MeshFilter>().addComponent<SpearManager>();
        }
    }
}
