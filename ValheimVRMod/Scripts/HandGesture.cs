using ValheimVRMod.VRCore.Backends;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts {
    public class HandGesture : MonoBehaviour {
        
        private bool isRightHand;
        private bool isMainHand { get { return isRightHand ^ !VRPlayer.isRightHandMainWeaponHand; } }
        private Quaternion handFixedRotation;
        private VRHand _sourceHand;
        private Transform sourceTransform;
        // The wrist of the other hand, which is the one that drives this hand in the barber mirror.
        private Transform mirrorSourceTransform;
        private bool mirroringFingers;

        public VRHand sourceHand {
            get
            {
                return _sourceHand;
            }
            set {
                _sourceHand = value;
                isRightHand = (_sourceHand == VRPlayer.rightHand);
                ensureSourceTransform();
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

        private bool ensureSourceTransform()
        {
            if (sourceTransform == null) {
                sourceTransform = findWrist(sourceHand);
            }
            return sourceTransform != null;
        }

        private bool ensureMirrorSourceTransform()
        {
            if (mirrorSourceTransform == null && sourceHand != null) {
                mirrorSourceTransform = findWrist(sourceHand.otherHand);
            }
            return mirrorSourceTransform != null;
        }

        private static Transform findWrist(VRHand hand)
        {
            if (hand == null)
            {
                return null;
            }
            Transform wrist = null;
            foreach (var t in hand.GetComponentsInChildren<Transform>())
            {
                if (t.name == "wrist_r")
                {
                    wrist = t;
                }
            }
            return wrist;
        }

        // When mirrored, the fingers follow those of the user's opposite hand reflected in the barber mirror, like
        // the hand itself does.
        private void updateFingerRotations(bool mirrored)
        {
            if (mirrored ? !ensureMirrorSourceTransform() : !ensureSourceTransform())
            {
                return;
            }
            Transform sourceTransform = mirrored ? mirrorSourceTransform : this.sourceTransform;
            mirroringFingers = mirrored;

            for (int i = 0; i < transform.childCount; i++) {

                var child = transform.GetChild(i);
                switch (child.name) {
                    
                    case ("LeftHandThumb1"):
                    case ("RightHandThumb1"):
                        updateFingerPart(sourceTransform.GetChild(0).GetChild(0), child);
                        break;
                    
                    case ("LeftHandIndex1"):
                    case ("RightHandIndex1"):
                        updateFingerPart(sourceTransform.GetChild(1).GetChild(0), child);
                        break;
                    
                    case ("LeftHandMiddle1"):
                    case ("RightHandMiddle1"):
                        updateFingerPart(sourceTransform.GetChild(2).GetChild(0), child);
                        break;

                    case ("LeftHandRing1"):
                    case ("RightHandRing1"):
                        updateFingerPart(sourceTransform.GetChild(3).GetChild(0), child);
                        break;
                    
                    case ("LeftHandPinky1"):
                    case ("RightHandPinky1"):
                        updateFingerPart(sourceTransform.GetChild(4).GetChild(0), child);
                        break;
                }
            }
        }

        private void updateFingerPart(Transform source, Transform target)
        {
            if (mirroringFingers)
            {
                // The source is the opposite hand, whose reflection is a hand of this side.
                target.rotation = Quaternion.LookRotation(
                    BarberMirror.ReflectDirection(-source.up),
                    BarberMirror.ReflectDirection(isRightHand ? -source.right : source.right));
            }
            else
            {
                target.rotation = Quaternion.LookRotation(-source.up, isRightHand ? source.right : -source.right);
            }

            if (source.childCount > 0 && target.childCount > 0) {
                updateFingerPart(source.GetChild(0), target.GetChild(0));
            }
        }
    }
}
