using UnityEngine;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts {
    public class VrikCreator {

        // private static GameObject camSynchObj;
        // private static GameObject rHandSynchObj;
        // private static GameObject lHandSynchObj;
        
        public static RootMotion.FinalIK.VRIK initialize(GameObject target, Transform leftController, Transform rightController, Transform camera) {
            RootMotion.FinalIK.VRIK vrik = target.AddComponent<RootMotion.FinalIK.VRIK>();
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
            head.localPosition = new Vector3(0, -0.133f, -0.089f);
            head.localRotation = Quaternion.Euler(0, 90, 0);
            vrik.solver.spine.headTarget = head;
            vrik.solver.spine.maxRootAngle = 180;

            vrik.references.leftHand.gameObject.AddComponent<ZNetView>();
            vrik.references.leftHand.gameObject.AddComponent<ZSyncTransform>();
            
            vrik.references.rightHand.gameObject.AddComponent<ZNetView>();
            vrik.references.rightHand.gameObject.AddComponent<ZSyncTransform>();
            
            vrik.references.head.gameObject.AddComponent<ZNetView>();
            vrik.references.head.gameObject.AddComponent<ZSyncTransform>();
            
            //initLocalSynchObjects();
            
            return vrik;
        }
        
        // private static void initLocalSynchObjects() {
        //
        //     camSynchObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        //     camSynchObj.transform.parent = Player.m_localPlayer.transform;
        //     camSynchObj.GetComponent<MeshRenderer>().material.color = Color.blue;
        //     camSynchObj.AddComponent<ZNetView>();
        //     var zst = camSynchObj.AddComponent<ZSyncTransform>();
        //     zst.m_syncPosition = true;
        //     zst.m_syncRotation = true;
        //     zst.m_syncScale = true;
        //     rHandSynchObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        //     rHandSynchObj.transform.parent = Player.m_localPlayer.transform;
        //     rHandSynchObj.transform.localScale *= .1f;
        //     rHandSynchObj.GetComponent<MeshRenderer>().material.color = Color.green;
        //     rHandSynchObj.AddComponent<ZNetView>();
        //     zst = rHandSynchObj.AddComponent<ZSyncTransform>();
        //     zst.m_syncPosition = true;
        //     zst.m_syncRotation = true;
        //     zst.m_syncScale = true;
        //     lHandSynchObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        //     lHandSynchObj.transform.parent = Player.m_localPlayer.transform;
        //     lHandSynchObj.transform.localScale *= .1f;
        //     lHandSynchObj.GetComponent<MeshRenderer>().material.color = Color.red;
        //     lHandSynchObj.AddComponent<ZNetView>();
        //     zst = lHandSynchObj.AddComponent<ZSyncTransform>();
        //     zst.m_syncPosition = true;
        //     zst.m_syncRotation = true;
        //     zst.m_syncScale = true;
        //
        // }
        //
        // public void parentLocalSynchObjects(Transform camera, Transform leftController, Transform rightController) {
        //
        //     camSynchObj.transform.SetParent(camera, false);
        //     rHandSynchObj.transform.SetParent(rightController, false);
        //     lHandSynchObj.transform.SetParent(leftController, false);
        //
        // }
    }
}