using UnityEngine;
using ValheimVRMod.Scripts.Block;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;
using Valve.VR.InteractionSystem;

namespace ValheimVRMod.Scripts
{
    public abstract class WeaponWield : MonoBehaviour
    {
        // TODO: move non-local-player logic from LocalWeaponWield to this class.
        protected abstract bool IsPlayerLeftHanded();
        protected abstract bool ShouldUseTwoHandedWield();
    }
}
