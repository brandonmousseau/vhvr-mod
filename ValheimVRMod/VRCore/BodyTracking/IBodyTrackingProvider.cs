using UnityEngine;

namespace ValheimVRMod.VRCore.BodyTracking
{
    // Body joints that can be driven by dedicated trackers (full body tracking).
    // Kept runtime-agnostic on purpose: the SteamVR implementation maps these to
    // SteamVR tracker roles today, and a future OpenXR implementation can map them
    // to OpenXR body-tracking/role paths without callers changing.
    public enum BodyJoint
    {
        Waist,
        LeftFoot,
        RightFoot,
    }

    // Abstraction over the source of full-body tracker poses.
    // Replaces the old approach of iterating raw SteamVR device indices and guessing
    // which device is the waist/feet by position. Implementations are responsible for
    // resolving a joint to the correct tracker (by role) and keeping a Transform in
    // sync with that tracker's pose each frame.
    public interface IBodyTrackingProvider
    {
        // True if any body tracker is currently usable. Cheap to poll.
        bool IsAvailable { get; }

        // True if the given joint currently has a connected tracker reporting a valid pose.
        bool IsJointActive(BodyJoint joint);

        // Returns a Transform that follows the tracker assigned to the given joint.
        // The Transform is parented under the VR camera rig and updated every frame.
        // Returns null if the joint is not currently tracked. The returned Transform's
        // validity should be re-checked via <see cref="IsJointActive"/> before use.
        Transform GetJointTransform(BodyJoint joint);
    }
}
