using UnityEngine;
using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.VRCore.Backends
{
    // The joints of the player's hands from the OpenXR runtime (see VhvrHandTrackingFeature), turned into Unity's
    // convention and into world space through the tracking origin the hand's pose is relative to. Read from the
    // runtime at most once a frame per hand.
    static class OpenXRHandJoints
    {
        private const int Count = (int)VRHandJoint.Count;

        private static readonly float[][] raw = { new float[Count * 4], new float[Count * 4] };
        private static readonly bool[][] rawValid = { new bool[Count], new bool[Count] };
        private static readonly Quaternion[][] world = { new Quaternion[Count], new Quaternion[Count] };
        private static readonly int[] readFrame = { -1, -1 };
        private static readonly bool[] tracked = new bool[2];
        private static readonly int[] source = new int[2];
        // What was last logged per hand: -1 not tracked, otherwise the source.
        private static readonly int[] loggedState = { int.MinValue, int.MinValue };

        // origin: the transform the runtime's app space is placed by, the parent of the hand's pose.
        public static bool TryGet(XRHand hand, Transform origin, out Quaternion[] rotations, out bool[] valid)
        {
            int h = (int)hand;
            if (readFrame[h] != Time.frameCount)
            {
                readFrame[h] = Time.frameCount;
                tracked[h] = read(h);
            }
            rotations = null;
            valid = null;
            if (!tracked[h])
            {
                return false;
            }
            // The origin may have moved since the joints were read, so this is done on each call.
            Quaternion toWorld = origin != null ? origin.rotation : Quaternion.identity;
            for (int j = 0; j < Count; j++)
            {
                world[h][j] = rawValid[h][j] ? toWorld * toUnity(raw[h], j) : Quaternion.identity;
            }
            rotations = world[h];
            valid = rawValid[h];
            return true;
        }

        // Whether the hand's joints, as last read, were made up by the runtime from its controller.
        public static bool IsFromController(XRHand hand) =>
            tracked[(int)hand] && source[(int)hand] == VHVR.OpenXR.VhvrHandTrackingFeature.SourceController;

        private static bool read(int h)
        {
            bool located = OpenXRLoaderSetup.LocateHandJoints(h, raw[h], rawValid[h], out int source);
            OpenXRHandJoints.source[h] = located ? source : 0;
            bool ok = located && rawValid[h][(int)VRHandJoint.Palm];
            int state = ok ? source : -1;
            if (state != loggedState[h])
            {
                loggedState[h] = state;
                LogInfo("OpenXR " + (h == 0 ? "left" : "right") + " hand joints: " +
                    (!ok ? "not tracked" : source == 2 ? "from the controller" : source == 1 ? "from hand tracking" : "tracked") + ".");
            }
            return ok;
        }

        // OpenXR is right-handed with -Z along a joint's bone; mirroring Z gives Unity's left-handed space, where the
        // bone runs along +Z and +Y stays out of the back of the hand.
        private static Quaternion toUnity(float[] q, int joint) =>
            new Quaternion(-q[joint * 4], -q[joint * 4 + 1], q[joint * 4 + 2], q[joint * 4 + 3]);
    }
}
