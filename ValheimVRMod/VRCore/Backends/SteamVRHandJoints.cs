using UnityEngine;
using Valve.VR;
using Valve.VR.InteractionSystem;

namespace ValheimVRMod.VRCore.Backends
{
    // The joints of one of the player's hands from SteamVR's glove model, which the skeleton poser animates under the
    // Hand, as rotations the character's finger bones take directly (VRHandJointFrame.CharacterBone). This is what
    // HandGesture used to read from the glove itself.
    //
    // The glove's wrist_r has a chain of bones per finger, in order from the thumb: the thumb's from its metacarpal
    // (thumb_0, thumb_1, thumb_2, end), the others' from their metacarpal (meta, 0, 1, 2, end). Its bones point along
    // -up, with right out of the back of a right hand and -right out of the back of a left one.
    sealed class SteamVRHandJoints
    {
        private const int Count = (int)VRHandJoint.Count;
        private static readonly VRHandJoint[] fingerFirstJoints =
        {
            VRHandJoint.ThumbMetacarpal, VRHandJoint.IndexMetacarpal, VRHandJoint.MiddleMetacarpal,
            VRHandJoint.RingMetacarpal, VRHandJoint.LittleMetacarpal,
        };
        private static readonly VRHandJoint[] fingerTips =
        {
            VRHandJoint.ThumbTip, VRHandJoint.IndexTip, VRHandJoint.MiddleTip, VRHandJoint.RingTip, VRHandJoint.LittleTip,
        };

        private readonly Hand hand;
        private readonly Quaternion[] rotations = new Quaternion[Count];
        private readonly bool[] valid = new bool[Count];
        private readonly Transform[] bones = new Transform[Count];
        private Transform wrist;

        public SteamVRHandJoints(Hand hand) { this.hand = hand; }

        public bool TryGet(out Quaternion[] rotations, out bool[] valid)
        {
            rotations = null;
            valid = null;
            if (!ensureWrist())
            {
                return false;
            }
            Vector3 backOfHand = hand.handType == SteamVR_Input_Sources.RightHand ? Vector3.right : Vector3.left;
            for (int j = 0; j < Count; j++)
            {
                Transform bone = bones[j];
                this.valid[j] = bone != null;
                if (this.valid[j])
                {
                    this.rotations[j] = Quaternion.LookRotation(-bone.up, bone.rotation * backOfHand);
                }
            }
            rotations = this.rotations;
            valid = this.valid;
            return true;
        }

        // Finds the glove's bones once its render model is there.
        private bool ensureWrist()
        {
            if (wrist != null)
            {
                return true;
            }
            System.Array.Clear(bones, 0, Count);
            foreach (Transform t in hand.GetComponentsInChildren<Transform>())
            {
                if (t.name == "wrist_r")
                {
                    wrist = t;
                }
            }
            if (wrist == null)
            {
                return false;
            }
            for (int finger = 0; finger < fingerFirstJoints.Length && finger < wrist.childCount; finger++)
            {
                Transform bone = wrist.GetChild(finger);
                for (int joint = (int)fingerFirstJoints[finger]; joint <= (int)fingerTips[finger] && bone != null; joint++)
                {
                    bones[joint] = bone;
                    bone = bone.childCount > 0 ? bone.GetChild(0) : null;
                }
            }
            return true;
        }
    }
}
