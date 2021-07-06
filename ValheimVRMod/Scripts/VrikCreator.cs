using RootMotion.FinalIK;
using UnityEngine;

namespace ValheimVRMod.Scripts {
    public class VrikCreator {

        private static readonly Vector3 leftUnequippedPosition = new Vector3(-0.027f, 0.05f, -0.18f);
        private static readonly Quaternion leftUnequippedRotation = Quaternion.Euler(0, 90f, 135f);
        private static readonly Vector3 leftUnequippedEllbow = new Vector3(1, 0, 0);
        private static readonly Vector3 rightUnequippedPosition = new Vector3(0.027f, 0.05f, -0.18f);
        private static readonly Quaternion rightUnequippedRotation = Quaternion.Euler(0, -90f, -135f);
        private static readonly Vector3 rightUnequippedEllbow = new Vector3(-1, 0, 0);
        
        private static readonly Vector3 leftEquippedPosition = new Vector3(-0.02f, 0.09f, -0.1f);
        private static readonly Quaternion leftEquippedRotation = Quaternion.Euler(0, 90, -170);
        private static readonly Vector3 leftEquippedEllbow = new Vector3(1, -3f, 0);
        private static readonly Vector3 rightEquippedPosition = new Vector3(0.02f, 0.09f, -0.1f);
        private static readonly Quaternion rightEquippedRotation = Quaternion.Euler(0, -90, -170);
        private static readonly Vector3 rightEquippedEllbow = new Vector3(-1, -3f, 0);
        
        public static VRIK initialize(GameObject target, Transform leftController, Transform rightController, Transform camera) {
            VRIK vrik = target.AddComponent<VRIK>();
            vrik.AutoDetectReferences();
            vrik.references.leftThigh = null;
            vrik.references.leftCalf = null;
            vrik.references.leftFoot = null;
            vrik.references.leftToes = null;
            vrik.references.rightThigh = null;
            vrik.references.rightCalf = null;
            vrik.references.rightFoot = null;
            vrik.references.rightToes = null;

            vrik.solver.leftArm.target = new GameObject().transform;
            vrik.solver.leftArm.target.parent = leftController;
            vrik.solver.rightArm.target = new GameObject().transform;
            vrik.solver.rightArm.target.parent = rightController;

            Transform head = new GameObject().transform;
            head.parent = camera;
            head.localPosition = new Vector3(0, -0.165f, -0.09f);
            head.localRotation = Quaternion.Euler(0, 90, 0);
            vrik.solver.spine.headTarget = head;
            vrik.solver.spine.maxRootAngle = 180;
            

            //Avoid akward movements
            vrik.solver.spine.maintainPelvisPosition = 0f;
            vrik.solver.spine.pelvisPositionWeight = 0f;
            vrik.solver.spine.pelvisRotationWeight = 0f;
            vrik.solver.spine.bodyPosStiffness = 0f;
            vrik.solver.spine.bodyRotStiffness = 0f;
            
            return vrik;
        }
        
        public static void resetVrikHandTransform(Humanoid player) {
            
            VRIK vrik = player.GetComponent<VRIK>();   
            
            if (vrik == null) {
                return;
            }
            
            if (player.GetComponent<VRPlayerSync>()?.currentLeftWeapon != null) {
                vrik.solver.leftArm.target.localPosition = leftEquippedPosition;
                vrik.solver.leftArm.target.localRotation = leftEquippedRotation;
                vrik.solver.leftArm.palmToThumbAxis = leftEquippedEllbow;

            }
            else {
                vrik.solver.leftArm.target.localPosition = leftUnequippedPosition;
                vrik.solver.leftArm.target.localRotation = leftUnequippedRotation;
                vrik.solver.leftArm.palmToThumbAxis = leftUnequippedEllbow;
            }
            
            if (player.GetComponent<VRPlayerSync>()?.currentRightWeapon != null) {
                vrik.solver.rightArm.target.localPosition = rightEquippedPosition;
                vrik.solver.rightArm.target.localRotation = rightEquippedRotation;
                vrik.solver.rightArm.palmToThumbAxis = rightEquippedEllbow;
            }
            else {
                vrik.solver.rightArm.target.localPosition = rightUnequippedPosition;
                vrik.solver.rightArm.target.localRotation = rightUnequippedRotation;
                vrik.solver.rightArm.palmToThumbAxis = rightUnequippedEllbow;
            }
        }
    }
}