namespace ValheimVRMod.Utilities
{
    internal static class VRPosePacket
    {
        // Existing wire format: transform + deprecated velocity, then 40 finger quaternions
        // and four control bytes. Older senders omit the weapon and foot sections.
        internal const int FingerRotationsPerHand = 20;
        internal const int CameraBytes = 40;
        internal const int BodyBytes = 4 * CameraBytes + 2 * FingerRotationsPerHand * 16 + 4;
        internal const int WeaponBytes = BodyBytes + 28;
        internal const int FeetBytes = WeaponBytes + 56;

        internal static bool HasValidLength(int length)
        {
            return length == CameraBytes || length == BodyBytes || length == WeaponBytes || length >= FeetBytes;
        }
    }
}
