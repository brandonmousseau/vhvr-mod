using ValheimVRMod.VRCore.Backends;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts {
    public class HandGesture : MonoBehaviour {

        // The character's finger bones, by the names it may have, in order from the thumb, with the joints that drive
        // the first of them and the last one a finger can go to. The bones after the first follow on the next joints.
        private static readonly string[][] fingerBoneNames =
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
        private static readonly VRHandJoint[] fingerTips =
        {
            VRHandJoint.ThumbTip, VRHandJoint.IndexTip, VRHandJoint.MiddleTip, VRHandJoint.RingTip, VRHandJoint.LittleTip,
        };

        // Per finger, per bone down it from the first: the bone's rotation relative to an anatomical frame on it (+Z
        // along the bone, +Y out of the back of the hand), for sources in VRHandJointFrame.Anatomical. Null until
        // worked out, empty when the hand has no knuckles to work it out from.
        private Quaternion[][] anatomicalToBone;

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
            bool anatomical = source.handJointFrame == VRHandJointFrame.Anatomical;
            if (anatomical && !ensureAnatomicalToBone())
            {
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
                    Quaternion rotation = joints[joint];
                    if (mirrored)
                    {
                        // The source is the opposite hand, whose reflection is a hand of this side.
                        rotation = Quaternion.LookRotation(
                            BarberMirror.ReflectDirection(rotation * Vector3.forward),
                            BarberMirror.ReflectDirection(rotation * Vector3.up));
                    }
                    if (anatomical)
                    {
                        Quaternion[] toBone = anatomicalToBone[finger];
                        int segment = joint - (int)fingerFirstJoints[finger];
                        if (segment >= toBone.Length)
                        {
                            break;
                        }
                        rotation *= toBone[segment];
                    }
                    bone.rotation = rotation;
                    if (bone.childCount == 0)
                    {
                        break;
                    }
                    bone = bone.GetChild(0);
                }
            }
        }

        // Works out which way each finger bone of the character points and which side of it is the back of the hand,
        // from the bones themselves: along is towards the next bone (or as the previous one for the last), and the back
        // of the hand is across that from the line of the knuckles, which stays put as the fingers curl.
        private bool ensureAnatomicalToBone()
        {
            if (anatomicalToBone != null)
            {
                return anatomicalToBone.Length > 0;
            }
            anatomicalToBone = new Quaternion[0][];
            Transform[] firstBones = new Transform[fingerBoneNames.Length];
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                int finger = fingerOf(child.name);
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

            var result = new Quaternion[fingerBoneNames.Length][];
            for (int finger = 0; finger < fingerBoneNames.Length; finger++)
            {
                var toBone = new System.Collections.Generic.List<Quaternion>();
                Transform bone = firstBones[finger];
                Vector3 along = Vector3.zero;
                int count = (int)fingerTips[finger] - (int)fingerFirstJoints[finger] + 1;
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
                    Vector3 back = bone.InverseTransformDirection(worldBack);
                    toBone.Add(Quaternion.Inverse(Quaternion.LookRotation(along, back)));
                    bone = bone.childCount > 0 ? bone.GetChild(0) : null;
                }
                result[finger] = toBone.ToArray();
            }
            anatomicalToBone = result;
            return true;
        }

        private static int fingerOf(string boneName)
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
