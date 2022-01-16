using RootMotion.FinalIK;
using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts {
    public class VrikCreator
    {

        private static ArmConf leftUnequipped = ArmConf.create(new Vector3(-0.027f, 0.05f, -0.18f),
            Quaternion.Euler(0, 90f, 135f),
            new Vector3(1, 0, 0));
        private static ArmConf rightUnequipped = ArmConf.create(new Vector3(0.027f, 0.05f, -0.18f),
            Quaternion.Euler(0, -90f, -135f),
            new Vector3(-1, 0, 0));
        private static ArmConf leftEquipped = ArmConf.create(new Vector3(-0.02f, 0.09f, -0.1f),
            Quaternion.Euler(0, 90, -170),
            new Vector3(1, -3f, 0));
        private static ArmConf rightEquipped = ArmConf.create(new Vector3(0.02f, 0.09f, -0.1f),
            Quaternion.Euler(0, -90, 170),
            new Vector3(-1, -3f, 0));
        private static ArmConf leftSpear = ArmConf.create(new Vector3(-0.02f, 0.06f, -0.15f),
            Quaternion.Euler(0, 90, 140),
            new Vector3(1, -3f, 0));
        private static ArmConf rightSpear = ArmConf.create(new Vector3(0.02f, 0.06f, -0.15f),
            Quaternion.Euler(0, -90, -140),
            new Vector3(-1, -3f, 0));
        
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

        private static void setHandTransform(IKSolverVR.Arm arm, ArmConf conf)
        {
            arm.target.localPosition = conf.position;
            arm.target.localRotation = conf.rotation;
            arm.palmToThumbAxis = conf.ellbow;
        }
        
        public static void resetVrikHandTransform(Humanoid player) {
            
            VRIK vrik = player.GetComponent<VRIK>();   
            
            if (vrik == null) {
                return;
            }
            
            if (player.GetComponent<VRPlayerSync>()?.currentLeftWeapon != null) {
                if (VHVRConfig.LeftHanded() && player.GetComponent<VRPlayerSync>().currentLeftWeapon.name.StartsWith("Spear")) {
                    Debug.Log("D1");
                    setHandTransform(vrik.solver.leftArm, leftSpear);
                } else {
                    Debug.Log("D2");
                    setHandTransform(vrik.solver.leftArm, leftEquipped);   
                }
            }
            else {
                Debug.Log("D3");
                setHandTransform(vrik.solver.leftArm, leftUnequipped);
            }
            
            if (player.GetComponent<VRPlayerSync>()?.currentRightWeapon != null) {
                if (! VHVRConfig.LeftHanded() && player.GetComponent<VRPlayerSync>().currentRightWeapon.name.StartsWith("Spear")) {
                    Debug.Log("D4");
                    setHandTransform(vrik.solver.rightArm, rightSpear);
                } else {
                    Debug.Log("D5");
                    setHandTransform(vrik.solver.rightArm, rightEquipped);
                }
            } else {
                Debug.Log("D6");
                setHandTransform(vrik.solver.rightArm, rightUnequipped);   
            }
        }
    }

    class ArmConf
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 ellbow;

        public static ArmConf create(Vector3 position, Quaternion rotation, Vector3 ellbow)
        {
            var armConf = new ArmConf();
            armConf.position = position;
            armConf.rotation = rotation;
            armConf.ellbow = ellbow;
            return armConf;
        }
    }
}