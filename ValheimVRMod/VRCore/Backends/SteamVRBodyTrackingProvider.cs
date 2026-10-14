using System.Collections.Generic;
using UnityEngine;
using Valve.VR;
using ValheimVRMod.VRCore.Backends;

namespace ValheimVRMod.VRCore.BodyTracking
{
    // SteamVR-backed body tracking using a single role-bound pose action read across
    // the waist/foot input sources. This replaces the old approach of iterating raw
    // device indices (capped at 16 by SteamVR_TrackedObject.EIndex) and guessing roles
    // by position. SteamVR resolves each source to whichever tracker the user assigned
    // that role to in the SteamVR settings, so there is no device-count ceiling and no
    // heuristic role detection.
    // The pose action is declared in the action catalog (VRInputActions) like every other action.
    public class SteamVRBodyTrackingProvider : MonoBehaviour, IBodyTrackingProvider
    {
        // Lives in the Valheim action set, which is already activated during gameplay so its
        // poses update each frame.
        public const string ActionPath = "/actions/Valheim/in/BodyPose";

        // Null until the actions have been created.
        private static SteamVR_Action_Pose cachedBodyPose;
        private static SteamVR_Action_Pose bodyPose => cachedBodyPose ??
            (SteamVRActionCatalog.IsBuilt ? cachedBodyPose = SteamVRActionCatalog.Action<SteamVR_Action_Pose>(ActionPath) : null);

        // Each joint maps to one or more SteamVR input sources in priority order (highest
        // priority first). Feet prefer the Foot tracker role but fall back to the Ankle
        // role, so a user who assigned either role in SteamVR gets working tracking.
        // SteamVR resolves each role to exactly one device, so the fallback (picking the
        // first source with a valid pose) has to happen here rather than in the bindings.
        private static readonly Dictionary<BodyJoint, SteamVR_Input_Sources[]> JointSources =
            new Dictionary<BodyJoint, SteamVR_Input_Sources[]>
            {
                { BodyJoint.Waist, new[] { SteamVR_Input_Sources.Waist } },
                { BodyJoint.LeftFoot, new[] { SteamVR_Input_Sources.LeftFoot, SteamVR_Input_Sources.LeftAnkle } },
                { BodyJoint.RightFoot, new[] { SteamVR_Input_Sources.RightFoot, SteamVR_Input_Sources.RightAnkle } },
            };

        private readonly Dictionary<BodyJoint, Transform> jointTransforms = new Dictionary<BodyJoint, Transform>();

        // Attaches role-following child transforms under the given camera rig. Pose values
        // from the action are expressed relative to the tracking origin (the rig), matching
        // how the legacy SteamVR_TrackedObject transforms were parented.
        public void Initialize(Transform cameraRig)
        {
            foreach (var joint in JointSources.Keys)
            {
                if (jointTransforms.ContainsKey(joint))
                {
                    continue;
                }
                var t = new GameObject("VHVR_BodyJoint_" + joint).transform;
                t.SetParent(cameraRig, worldPositionStays: false);
                jointTransforms[joint] = t;
            }
        }

        public bool IsAvailable
        {
            get
            {
                if (bodyPose == null)
                {
                    return false;
                }
                foreach (var joint in JointSources.Keys)
                {
                    if (IsJointActive(joint))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public bool IsJointActive(BodyJoint joint)
        {
            return GetActiveSource(joint).HasValue;
        }

        // Returns the highest-priority source for the joint that currently has a connected
        // tracker reporting a valid pose, or null if none of the joint's sources do.
        private SteamVR_Input_Sources? GetActiveSource(BodyJoint joint)
        {
            if (bodyPose == null || !JointSources.TryGetValue(joint, out var sources))
            {
                return null;
            }
            foreach (var source in sources)
            {
                if (bodyPose.GetDeviceIsConnected(source) && bodyPose.GetPoseIsValid(source))
                {
                    return source;
                }
            }
            return null;
        }

        public Transform GetJointTransform(BodyJoint joint)
        {
            return IsJointActive(joint) && jointTransforms.TryGetValue(joint, out var t) ? t : null;
        }

        private void LateUpdate()
        {
            if (bodyPose == null)
            {
                return;
            }
            foreach (var joint in JointSources.Keys)
            {
                if (!jointTransforms.TryGetValue(joint, out var t))
                {
                    continue;
                }
                var source = GetActiveSource(joint);
                if (!source.HasValue)
                {
                    continue;
                }
                t.localPosition = bodyPose.GetLocalPosition(source.Value);
                t.localRotation = bodyPose.GetLocalRotation(source.Value);
            }
        }
    }
}
