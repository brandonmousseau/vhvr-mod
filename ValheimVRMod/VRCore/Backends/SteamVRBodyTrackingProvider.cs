using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Valve.VR;
using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.VRCore.BodyTracking
{
    // SteamVR-backed body tracking using a single role-bound pose action read across
    // the waist/foot input sources. This replaces the old approach of iterating raw
    // device indices (capped at 16 by SteamVR_TrackedObject.EIndex) and guessing roles
    // by position. SteamVR resolves each source to whichever tracker the user assigned
    // that role to in the SteamVR settings, so there is no device-count ceiling and no
    // heuristic role detection.
    // The pose action is created and registered at runtime (see <see cref="EnsureActionRegistered"/>)
    // so this needs no changes to the prebuilt SteamVR_Actions.dll / Unity project --
    // only an entry in actions.json (shipped via StreamingAssets).
    public class SteamVRBodyTrackingProvider : MonoBehaviour, IBodyTrackingProvider
    {
        // Must match the action name added to actions.json. Lives in the Valheim action
        // set, which is already activated during gameplay so its poses update each frame.
        public const string ActionPath = "/actions/Valheim/in/BodyPose";

        private static SteamVR_Action_Pose bodyPose;

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

        // Creates the body pose action and registers it with SteamVR_Input. Must be called
        // after SteamVR_Actions.PreInitialize() (which populates the action arrays) and
        // before SteamVR_Input.Initialize() (which initializes every action in those arrays
        // and which UpdatePoseActions() later iterates). Idempotent.
        public static void EnsureActionRegistered()
        {
            if (bodyPose != null)
            {
                return;
            }

            if (SteamVR_Input.actions == null || SteamVR_Input.actionsPose == null)
            {
                LogError("Cannot register body pose action before SteamVR action arrays are initialized.");
                return;
            }

            bodyPose = SteamVR_Action.Create<SteamVR_Action_Pose>(ActionPath);

            // Append to the arrays that SteamVR_Input.Initialize() and UpdatePoseActions()
            // iterate, so the action gets a handle and is updated every frame.
            SteamVR_Input.actions = SteamVR_Input.actions.Concat(new SteamVR_Action[] { bodyPose }).ToArray();
            SteamVR_Input.actionsPose = SteamVR_Input.actionsPose.Concat(new SteamVR_Action_Pose[] { bodyPose }).ToArray();
            SteamVR_Input.actionsIn = SteamVR_Input.actionsIn.Concat(new ISteamVR_Action_In[] { bodyPose }).ToArray();

            LogInfo("Registered body tracking pose action: " + ActionPath);
        }

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
