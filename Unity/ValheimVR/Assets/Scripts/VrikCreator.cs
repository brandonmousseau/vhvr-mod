using UnityEngine;

public class VrikCreator : MonoBehaviour {
    private bool initialized;

    public Transform leftController;
    public Transform rightController;
    public Transform camera;

    private RootMotion.FinalIK.VRIK vrik;
    private Transform leftHandWrist;
    private Transform rightHandWrist;
    
    private Quaternion leftHandFixedRotation;
    private Quaternion rightHandFixedRotation;

    void OnRenderObject() {
        if (!initialized && leftController != null && rightController != null && camera != null) {
            initialize();
            initialized = true;
        }

        if (initialized) {
            leftHandFixedRotation = vrik.references.leftHand.rotation;
            rightHandFixedRotation = vrik.references.rightHand.rotation;
            updateFingerRotations(leftHandWrist, vrik.references.leftHand, false);
            updateFingerRotations(rightHandWrist, vrik.references.rightHand, true);
        }
    }

    private void Update() {
        if (initialized && findHandWrists()) {
            vrik.references.leftHand.rotation = leftHandFixedRotation ;
            vrik.references.rightHand.rotation = rightHandFixedRotation;
            updateFingerRotations(leftHandWrist, vrik.references.leftHand, false);
            updateFingerRotations(rightHandWrist, vrik.references.rightHand, true);
        }
    }

    private bool findHandWrists() {
        if (leftHandWrist != null && rightHandWrist != null) {
            return true;
        }

        foreach (var t in leftController.GetComponentsInChildren<Transform>()) {
            if (t.name == "wrist_r") {
                leftHandWrist = t;
            }
        }

        foreach (var t in rightController.GetComponentsInChildren<Transform>()) {
            if (t.name == "wrist_r") {
                rightHandWrist = t;
            }
        }

        return leftHandWrist != null && rightHandWrist != null;
    }

    private void updateFingerRotations(Transform source, Transform target, bool isRightHand) {
        for (int i = 0; i < target.childCount; i++) {
            var child = target.GetChild(i);
            switch (child.name) {
                case ("LeftHandThumb1"):
                case ("RightHandThumb1"):
                    updateFinger(source.GetChild(0).GetChild(0), target.GetChild(i), isRightHand);
                    break;

                case ("LeftHandIndex1"):
                case ("RightHandIndex1"):
                    updateFinger(source.GetChild(1).GetChild(0), target.GetChild(i), isRightHand);
                    break;

                case ("LeftHandMiddle1"):
                case ("RightHandMiddle1"):
                    updateFinger(source.GetChild(2).GetChild(0), target.GetChild(i), isRightHand);
                    break;

                case ("LeftHandRing1"):
                case ("RightHandRing1"):
                    updateFinger(source.GetChild(3).GetChild(0), target.GetChild(i), isRightHand);
                    break;

                case ("LeftHandPinky1"):
                case ("RightHandPinky1"):
                    updateFinger(source.GetChild(4).GetChild(0), target.GetChild(i), isRightHand);
                    break;
            }
        }
    }

    private void updateFinger(Transform source, Transform target, bool isRightHand) {
        target.rotation = Quaternion.LookRotation(-source.up, isRightHand ? source.right : -source.right);

        if (source.childCount > 0 && target.childCount > 0) {
            updateFinger(source.GetChild(0), target.GetChild(0), isRightHand);
        }
    }

    private void initialize() {
        vrik = gameObject.AddComponent<RootMotion.FinalIK.VRIK>();
        vrik.AutoDetectReferences();
        vrik.references.leftThigh = null;
        vrik.references.leftCalf = null;
        vrik.references.leftFoot = null;
        vrik.references.leftToes = null;
        vrik.references.rightThigh = null;
        vrik.references.rightCalf = null;
        vrik.references.rightFoot = null;
        vrik.references.rightToes = null;

        Transform leftHand = (new GameObject()).transform;
        leftHand.parent = leftController;
        leftHand.transform.localPosition = new Vector3(-0.0153f, 0.0662f, -0.1731f);
        leftHand.transform.localRotation = Quaternion.Euler(-8.222f, 79.485f, 142.351f);
        vrik.solver.leftArm.target = leftHand;

        Transform rightHand = (new GameObject()).transform;
        rightHand.parent = rightController;
        rightHand.transform.localPosition = new Vector3(0.02300016f, 0.07700036f, -0.1700005f);
        rightHand.transform.localRotation = Quaternion.Euler(-4.75f, -85.497f, -145.387f);
        vrik.solver.rightArm.target = rightHand;

        Transform head = (new GameObject()).transform;
        head.parent = camera;
        head.localPosition = new Vector3(0, -0.165f, -0.085f);
        head.localRotation = Quaternion.Euler(0, 90, 0);
        vrik.solver.spine.headTarget = head;
        vrik.solver.spine.maxRootAngle = 180;

    }
}