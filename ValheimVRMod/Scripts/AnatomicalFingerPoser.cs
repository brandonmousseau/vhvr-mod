using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore.Backends;

namespace ValheimVRMod.Scripts
{
    // Poses the character's fingers from hand joints in VRHandJointFrame.Anatomical (+Z along the bone, +Y out of the
    // back of the hand, the thumbnail for the thumb), as OpenXR gives them. Valheim's finger bones are not laid out that
    // way, so each bone is fitted once from the character's own geometry. Sources in VRHandJointFrame.CharacterBone
    // (SteamVR) never come here; HandGesture poses those itself.
    sealed class AnatomicalFingerPoser
    {
        // The joints that drive each finger's first bone, from the thumb. The character's Thumb1 is the thumb's
        // metacarpal, while Index1 and the others are the proximal phalanges.
        private static readonly VRHandJoint[] firstJoints =
        {
            VRHandJoint.ThumbMetacarpal, VRHandJoint.IndexProximal, VRHandJoint.MiddleProximal,
            VRHandJoint.RingProximal, VRHandJoint.LittleProximal,
        };

        // How far the character's thumbnail is turned from the back of the hand towards the thumb side, about the
        // thumb, in degrees: 0 faces it out of the back of the hand, 90 away from the fingers.
        private const float THUMBNAIL_ROLL = 60f;

        private readonly Transform hand;
        private readonly bool isRightHand;

        // Per finger, per bone down it from the first: the bone's rotation relative to its anatomical frame. Null until
        // worked out, empty when the hand has no knuckles to work it out from.
        private Quaternion[][] toBone;
        private readonly Transform[] firstBones = new Transform[HandGesture.fingerBoneNames.Length];

        // For joints made up from a controller, whose thumb does not bend in a way that suits the character: the
        // metacarpal is left as it is, and each bone after it that has one after it in turn (a phalanx) bends from
        // its resting local rotation (thumbPhalanxRests) about an axis across it (thumbPhalanxAxes, in its parent's
        // space) that takes it towards the root of the metacarpal. It bends as far as the source's joint at its root
        // has bent beyond the least it has been (thumbOpenBends), up to THUMB_PHALANX_MAX_BEND.
        private const float THUMB_PHALANX_MAX_BEND = 45f;
        private readonly List<Quaternion> thumbPhalanxRests = new List<Quaternion>();
        private readonly List<Vector3> thumbPhalanxAxes = new List<Vector3>();
        private readonly float[] thumbOpenBends = { float.MaxValue, float.MaxValue, float.MaxValue };

        public AnatomicalFingerPoser(Transform hand, bool isRightHand)
        {
            this.hand = hand;
            this.isRightHand = isRightHand;
        }

        public void Pose(Quaternion[] joints, bool[] valid, bool mirrored, bool fromController)
        {
            if (!ensureFitted())
            {
                return;
            }
            for (int finger = 0; finger < firstBones.Length; finger++)
            {
                Transform bone = firstBones[finger];
                if (bone == null)
                {
                    continue;
                }
                if (finger == 0 && fromController && thumbPhalanxAxes.Count > 0)
                {
                    poseControllerThumb(bone, joints, valid);
                    continue;
                }
                Quaternion[] fit = toBone[finger];
                int first = (int)firstJoints[finger];
                for (int joint = first; joint <= (int)HandGesture.fingerTips[finger] && valid[joint]; joint++)
                {
                    int segment = joint - first;
                    if (segment >= fit.Length)
                    {
                        break;
                    }
                    Quaternion rotation = mirrored ? HandGesture.Mirror(joints[joint]) : joints[joint];
                    bone.rotation = rotation * fit[segment];
                    if (bone.childCount == 0)
                    {
                        break;
                    }
                    bone = bone.GetChild(0);
                }
            }
        }

        // See thumbPhalanxAxes. The phalanges are the bones from the thumb's proximal joint on, and the bends there do
        // not change in a mirror.
        private void poseControllerThumb(Transform metacarpal, Quaternion[] joints, bool[] valid)
        {
            Transform bone = metacarpal;
            for (int i = 0; i < thumbPhalanxAxes.Count; i++)
            {
                bone = bone.GetChild(0);
                int joint = (int)VRHandJoint.ThumbProximal + i;
                float bend = 0;
                if (i < thumbOpenBends.Length && joint <= (int)VRHandJoint.ThumbTip && valid[joint] && valid[joint - 1])
                {
                    float angle = Vector3.Angle(joints[joint - 1] * Vector3.forward, joints[joint] * Vector3.forward);
                    thumbOpenBends[i] = Mathf.Min(thumbOpenBends[i], angle);
                    bend = Mathf.Min(angle - thumbOpenBends[i], THUMB_PHALANX_MAX_BEND);
                }
                bone.localRotation = Quaternion.AngleAxis(bend, thumbPhalanxAxes[i]) * thumbPhalanxRests[i];
            }
        }

        // Works out which way each finger bone of the character points and which side of it is the back of the hand,
        // from the bones themselves: along is towards the next bone (or as the previous one for the last), and the back
        // of the hand is across that from the line of the knuckles, which stays put as the fingers curl.
        private bool ensureFitted()
        {
            if (toBone != null)
            {
                return toBone.Length > 0;
            }
            toBone = new Quaternion[0][];
            for (int i = 0; i < hand.childCount; i++)
            {
                Transform child = hand.GetChild(i);
                int finger = HandGesture.fingerOf(child.name);
                if (finger >= 0)
                {
                    firstBones[finger] = child;
                }
            }
            Transform index = firstBones[1];
            Transform little = firstBones[4];
            if (index == null || little == null)
            {
                LogUtils.LogWarning("The character's hand has no knuckles to fit the hand joints to.");
                return false;
            }
            Vector3 across = index.position - little.position;

            var result = new Quaternion[firstBones.Length][];
            for (int finger = 0; finger < firstBones.Length; finger++)
            {
                var fit = new List<Quaternion>();
                Transform bone = firstBones[finger];
                Vector3 along = Vector3.zero;
                int count = (int)HandGesture.fingerTips[finger] - (int)firstJoints[finger] + 1;
                for (int segment = 0; segment < count && bone != null; segment++)
                {
                    // In the bone's own space; the last bone, without a child, points the way its parent does.
                    along = bone.childCount > 0 ? bone.GetChild(0).localPosition : Quaternion.Inverse(bone.localRotation) * along;
                    Vector3 worldAlong = bone.TransformDirection(along);
                    Vector3 worldBack = isRightHand ? Vector3.Cross(across, worldAlong) : Vector3.Cross(worldAlong, across);
                    // Along the line of the knuckles there is no telling the back of the hand from across it.
                    if (along.sqrMagnitude < 1e-10f ||
                        worldBack.sqrMagnitude < 0.01f * across.sqrMagnitude * worldAlong.sqrMagnitude)
                    {
                        break;
                    }
                    if (finger == 0)
                    {
                        // The back of a thumb bone, as OpenXR has it, is the side of the thumbnail, which faces away
                        // from the fingers more than out of the back of the hand: turned about the thumb that way.
                        float roll = THUMBNAIL_ROLL * Mathf.Deg2Rad;
                        Vector3 side = Vector3.ProjectOnPlane(Vector3.ProjectOnPlane(across, worldAlong), worldBack);
                        if (side.sqrMagnitude > 1e-10f)
                        {
                            worldBack = worldBack.normalized * Mathf.Cos(roll) + side.normalized * Mathf.Sin(roll);
                        }
                    }
                    Vector3 back = bone.InverseTransformDirection(worldBack);
                    fit.Add(Quaternion.Inverse(Quaternion.LookRotation(along, back)));
                    bone = bone.childCount > 0 ? bone.GetChild(0) : null;
                }
                result[finger] = fit.ToArray();
            }
            toBone = result;
            fitControllerThumb();
            return true;
        }

        // See thumbPhalanxAxes: across each phalanx, from where it points towards the root of the metacarpal.
        private void fitControllerThumb()
        {
            Transform metacarpal = firstBones[0];
            if (metacarpal == null)
            {
                return;
            }
            for (Transform bone = metacarpal; bone.childCount > 0 && bone.GetChild(0).childCount > 0; )
            {
                bone = bone.GetChild(0);
                Vector3 along = bone.GetChild(0).position - bone.position;
                Vector3 axis = Vector3.Cross(along, metacarpal.position - bone.position);
                if (axis.sqrMagnitude < 1e-12f)
                {
                    break;
                }
                thumbPhalanxRests.Add(bone.localRotation);
                thumbPhalanxAxes.Add(bone.parent.InverseTransformDirection(axis).normalized);
            }
        }
    }
}
