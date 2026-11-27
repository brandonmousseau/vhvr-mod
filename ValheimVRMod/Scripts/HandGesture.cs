using ValheimVRMod.VRCore.Backends;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts {
    public class HandGesture : MonoBehaviour {

        // The character's finger bones, by the names it may have, in order from the thumb, with the joints that drive
        // the first of them and the last one a finger can go to. The bones after the first follow on the next joints.
        internal static readonly string[][] fingerBoneNames =
        {
            new[] { "LeftHandThumb1", "RightHandThumb1" }, new[] { "LeftHandIndex1", "RightHandIndex1" },
            new[] { "LeftHandMiddle1", "RightHandMiddle1" }, new[] { "LeftHandRing1", "RightHandRing1" },
            new[] { "LeftHandPinky1", "RightHandPinky1" },
        };
        private static readonly VRHandJoint[] fingerFirstJoints =
        {
            VRHandJoint.ThumbProximal, VRHandJoint.IndexProximal, VRHandJoint.MiddleProximal,
            VRHandJoint.RingProximal, VRHandJoint.LittleProximal,
        };
        internal static readonly VRHandJoint[] fingerTips =
        {
            VRHandJoint.ThumbTip, VRHandJoint.IndexTip, VRHandJoint.MiddleTip, VRHandJoint.RingTip, VRHandJoint.LittleTip,
        };

        // Fits the joints of sources in VRHandJointFrame.Anatomical to this hand, made when first needed.
        private AnatomicalFingerPoser anatomicalPoser;

        private bool isRightHand;
        private bool isMainHand { get { return isRightHand ^ !VRPlayer.isRightHandMainWeaponHand; } }
        private Quaternion handFixedRotation;
        private VRHand _sourceHand;

        public VRHand sourceHand {
            get
            {
                return _sourceHand;
            }
            set {
                _sourceHand = value;
                isRightHand = (_sourceHand == VRPlayer.rightHand);
            }
        }

        void OnRenderObject() {
            handFixedRotation = transform.rotation;
        }

        public bool isHandFree() {
            if (LocalWeaponWield.isCurrentlyTwoHanded())
            {
                return false;
            }

            switch (EquipScript.CurrentMainHandEquipType())
            {
                case EquipType.Bow:
                    if (BowLocalManager.instance != null && BowLocalManager.instance.isHoldingArrow())
                    {
                        return false;
                    }
                    break;
                case EquipType.Claws:
                    return true;
                case EquipType.Crossbow:
                    if (CrossbowMorphManager.instance != null)
                    {
                        if (CrossbowMorphManager.instance.isHoldingBolt() || CrossbowMorphManager.instance.isPulling)
                        {
                            return false;
                        }
                    }
                    break;
                case EquipType.DualAxes:
                case EquipType.DualKnives:
                    return false;
            }

            return isMainHand ?
                Player.m_localPlayer?.GetRightItem() == null :
                Player.m_localPlayer?.GetLeftItem() == null;
        }

        private bool areFingersFree()
        {
            if (EquipScript.CurrentOffHandEquipType() == EquipType.Crossbow)
            {
                if (CrossbowMorphManager.instance != null)
                {
                    if (CrossbowMorphManager.instance.isHoldingBolt() || CrossbowMorphManager.instance.isPulling)
                    {
                        return false;
                    }
                }

                switch (LocalWeaponWield.LocalPlayerTwoHandedState)
                {
                    case WeaponWield.TwoHandedState.LeftHandBehind:
                        return !isRightHand;
                    case WeaponWield.TwoHandedState.RightHandBehind:
                        return isRightHand;
                    case WeaponWield.TwoHandedState.SingleHanded:
                        return isMainHand;
                }
            }

            return isHandFree();
        }

        private void Update() {

            // The barber mirror keeps the hands moving while movement is paused, see BarberMirror.
            bool mirrored = BarberMirror.IsActive;
            if (!areFingersFree() || Game.IsPaused() || (VRPlayer.ShouldPauseMovement && !mirrored)) {
                return;
            }

            transform.rotation = handFixedRotation ;
            updateFingerRotations(mirrored);
        }

        // Poses the fingers from the backend's hand joints (see VRHand.TryGetHandJoints): each finger bone takes the
        // rotation of its joint, down the finger for as long as both go. When mirrored, the fingers follow those of the
        // user's opposite hand reflected in the barber mirror, like the hand itself does.
        private void updateFingerRotations(bool mirrored)
        {
            VRHand source = mirrored ? sourceHand?.otherHand : sourceHand;
            if (source == null || !source.TryGetHandJoints(out Quaternion[] joints, out bool[] valid))
            {
                return;
            }
            if (source.handJointFrame == VRHandJointFrame.Anatomical)
            {
                if (anatomicalPoser == null)
                {
                    anatomicalPoser = new AnatomicalFingerPoser(transform, isRightHand);
                }
                anatomicalPoser.Pose(joints, valid, mirrored, source.handJointsFromController);
                return;
            }

            for (int i = 0; i < transform.childCount; i++) {

                var child = transform.GetChild(i);
                int finger = fingerOf(child.name);
                if (finger < 0)
                {
                    continue;
                }
                Transform bone = child;
                for (int joint = (int)fingerFirstJoints[finger]; joint <= (int)fingerTips[finger] && valid[joint]; joint++)
                {
                    bone.rotation = mirrored ? Mirror(joints[joint]) : joints[joint];
                    if (bone.childCount == 0)
                    {
                        break;
                    }
                    bone = bone.GetChild(0);
                }
            }
        }

        // A joint rotation of the user's opposite hand as its reflection in the barber mirror, a hand of this side.
        internal static Quaternion Mirror(Quaternion rotation) =>
            Quaternion.LookRotation(
                BarberMirror.ReflectDirection(rotation * Vector3.forward),
                BarberMirror.ReflectDirection(rotation * Vector3.up));

        internal static int fingerOf(string boneName)
        {
            for (int finger = 0; finger < fingerBoneNames.Length; finger++)
            {
                if (boneName == fingerBoneNames[finger][0] || boneName == fingerBoneNames[finger][1])
                {
                    return finger;
                }
            }
            return -1;
        }
    }
}
