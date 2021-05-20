using UnityEngine;

namespace ValheimVRMod.Scripts {
    public class VrikCreator : MonoBehaviour {

        private bool initialized;

        public Transform leftController;
        public Transform rightController;
        public Transform camera;
        public static Transform leftHand;
        public static Transform rightHand;


        // Update is called once per frame
        void Update() {
            if (initialized) {
                return;
            }

            if (leftController != null && rightController != null && camera != null) {
                initialize();
                initialized = true;
            }

        }

        private void initialize() {

            RootMotion.FinalIK.VRIK vrik = gameObject.AddComponent<RootMotion.FinalIK.VRIK>();
            vrik.AutoDetectReferences();
            vrik.references.leftThigh = null;
            vrik.references.leftCalf = null;
            vrik.references.leftFoot = null;
            vrik.references.leftToes = null;
            vrik.references.rightThigh = null;
            vrik.references.rightCalf = null;
            vrik.references.rightFoot = null;
            vrik.references.rightToes = null;

            leftHand = (new GameObject()).transform;
            leftHand.parent = leftController;
            leftHand.transform.localPosition = new Vector3(0.0107f, 0.0469f, -0.1774f);
            leftHand.transform.localRotation = Quaternion.Euler(181.978f, -90.343f, -45.733f);
            vrik.solver.leftArm.target = leftHand;

            rightHand = (new GameObject()).transform;
            rightHand.parent = rightController;
            rightHand.transform.localPosition = new Vector3(0.0107f, 0.0469f, -0.1774f);
            rightHand.transform.localRotation = Quaternion.Euler(181.978f, 90.343f, 45.733f);
            vrik.solver.rightArm.target = rightHand;

            Transform head = (new GameObject()).transform;
            head.parent = camera;
            head.localPosition = new Vector3(0, -0.133f, -0.089f);
            head.localRotation = Quaternion.Euler(0, 90, 0);
            vrik.solver.spine.headTarget = head;
            vrik.solver.spine.maxRootAngle = 180;

        }
    }
}