using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR.InteractionSystem;

namespace ValheimVRMod.Scripts {
    public class HandGesture : MonoBehaviour {
        
        private bool isRightHand;
        private bool isMainHand { get { return isRightHand ^ VHVRConfig.LeftHanded(); } }
        private Quaternion handFixedRotation;
        private Hand _sourceHand;
        private Transform sourceTransform;
        
        public Hand sourceHand {
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

            switch (EquipScript.getRight())
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
            if (EquipScript.getLeft() == EquipType.Crossbow)
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

            if (!areFingersFree() || Game.IsPaused() || VRPlayer.ShouldPauseMovement) {
                return;
            }

            transform.rotation = handFixedRotation ;
            updateFingerRotations();
        }

        private bool ensureSourceTransform()
        {
            if (sourceTransform == null) {
                foreach (var t in sourceHand.GetComponentsInChildren<Transform>())
                {
                    if (t.name == "wrist_r")
                    {
                        sourceTransform = t;
                    }
                }
            }
            return sourceTransform != null;
        }

        private void updateFingerRotations()
        {
            if (!ensureSourceTransform())
            {
                return;
            }

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
            target.rotation = Quaternion.LookRotation(-source.up, isRightHand ? source.right : -source.right);

            if (source.childCount > 0 && target.childCount > 0) {
                updateFingerPart(source.GetChild(0), target.GetChild(0));
            }
        }
    }
}
