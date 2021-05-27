using UnityEngine;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts {
    public class HandGestures : MonoBehaviour {
        
        private bool isRightHand;
        private Quaternion handFixedRotation;
        private Transform sourceHand;
        public  Transform targetHand;

        private void Awake() {
            isRightHand = gameObject == VRPlayer.rightHand.gameObject;
            foreach (var t in GetComponentsInChildren<Transform>()) {
                if (t.name == "wrist_r") {
                    sourceHand = t;
                }
            }
        }

        void OnRenderObject() {
            handFixedRotation = targetHand.rotation;
        }

        private void Update() {
            if (isRightHand && (Player.m_localPlayer.GetRightItem() != null || BowManager.instance.isHoldingArrow())) {
                return;
            }
            
            if (!isRightHand && Player.m_localPlayer.GetLeftItem() != null) {
                return;
            }
            
            targetHand.rotation = handFixedRotation ;
            updateFingerRotations();
        }

        private void updateFingerRotations() {

            for (int i = 0; i < targetHand.childCount; i++) {

                var child = targetHand.GetChild(i);
                switch (child.name) {
                    
                    case ("LeftHandThumb1"):
                    case ("RightHandThumb1"):
                        updateFingerPart(sourceHand.GetChild(0).GetChild(0), targetHand.GetChild(i));
                        break;
                    
                    case ("LeftHandIndex1"):
                    case ("RightHandIndex1"):
                        updateFingerPart(sourceHand.GetChild(1).GetChild(0), targetHand.GetChild(i));
                        break;
                    
                    case ("LeftHandMiddle1"):
                    case ("RightHandMiddle1"):
                        updateFingerPart(sourceHand.GetChild(2).GetChild(0), targetHand.GetChild(i));
                        break;

                    case ("LeftHandRing1"):
                    case ("RightHandRing1"):
                        updateFingerPart(sourceHand.GetChild(3).GetChild(0), targetHand.GetChild(i));
                        break;
                    
                    case ("LeftHandPinky1"):
                    case ("RightHandPinky1"):
                        updateFingerPart(sourceHand.GetChild(4).GetChild(0), targetHand.GetChild(i));
                        break;
                }
            }
        }

        private void updateFingerPart(Transform source, Transform target) {
            target.rotation = Quaternion.LookRotation(source.forward, isRightHand ? source.right : -source.right);

            if (source.childCount > 0 && target.childCount > 0) {
                updateFingerPart(source.GetChild(0), target.GetChild(0));
            }
        }
    }
}