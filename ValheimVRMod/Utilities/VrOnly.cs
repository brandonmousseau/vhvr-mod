using System.Runtime.CompilerServices;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Utilities
{
    /// <summary>
    /// The single boundary between flat screen safe code and code that touches the VR assemblies.
    ///
    /// The type layout must stay empty: with no fields of its own, this class loads even when SteamVR.dll
    /// and the other VR assemblies are missing, so a flat screen method may reference it. Each member is
    /// NoInlining and has a signature free of VR types, so calling code can be compiled without resolving
    /// anything from those assemblies; only the member's own body needs them, and that body is only ever
    /// compiled once it is actually called, which flat screen mode never does.
    ///
    /// Every caller must guard the call with a cheap non-VR test placed FIRST, so that short circuiting
    /// keeps the call from executing in a flat screen session.
    /// </summary>
    [VrOnly]
    internal static class VrOnly
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool IsLocalPlayerLeftHanded()
        {
            return !VRPlayer.isRightHandMainWeaponHand;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool LocalPlayerShouldPauseMovement()
        {
            return VRPlayer.ShouldPauseMovement;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool ShouldTrackFeetForLocalPlayer()
        {
            return VRPlayer.vrPlayerInstance != null && VRPlayer.vrPlayerInstance.shouldTrackFeet();
        }
    }
}
