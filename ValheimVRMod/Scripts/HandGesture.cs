using UnityEngine;
using ValheimVRMod.VRCore;
using Valve.VR.InteractionSystem;

namespace ValheimVRMod.Scripts {
    public class HandGesture : MonoBehaviour {
        
        private bool isRightHand;
        private Quaternion handFixedRotation;
        private Transform sourceTransform;
        public Hand sourceHand;

        private void Start() {
            isRightHand = sourceHand == VRPlayer.rightHand;
            foreach (var t in sourceHand.GetComponentsInChildren<Transform>()) {
                if (t.name == "wrist_r") {
                    sourceTransform = t;
                }
            }
        }

        void OnRenderObject() {
            handFixedRotation = transform.rotation;
        }

        public bool isUnequiped() {
            if (isRightHand && (Player.m_localPlayer.GetRightItem() != null 
                                || BowLocalManager.instance != null && BowLocalManager.instance.isHoldingArrow())) {
                return false;
            }
            
            if (!isRightHand && Player.m_localPlayer.GetLeftItem() != null) {
                return false;
            }

            return true;
        }
        
        private void Update() {

            if (!isUnequiped()) {
                return;
            }
            
            transform.rotation = handFixedRotation ;
            updateFingerRotations();
        }

        private void updateFingerRotations() {

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

        private void updateFingerPart(Transform source, Transform target) {
            target.rotation = Quaternion.LookRotation(-source.up, isRightHand ? source.right : -source.right);

            if (source.childCount > 0 && target.childCount > 0) {
                updateFingerPart(source.GetChild(0), target.GetChild(0));
            }
        }
    }
}