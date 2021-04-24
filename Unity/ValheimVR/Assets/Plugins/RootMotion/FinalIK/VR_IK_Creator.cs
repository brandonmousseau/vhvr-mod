using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VR_IK_Creator : MonoBehaviour
{

    private bool initialized;

    public Transform leftController;
    public Transform rightController;
    public Transform camera;
    public RootMotion.FinalIK.VRIK vrik;


    // Update is called once per frame
    void Update()
    {
        if (initialized)
        {
            return;
        }

        if (leftController != null && rightController != null && camera != null)
        {
            initialize();
            initialized = true;
        }
        
    }

    private void initialize()
    {
        
        vrik = this.gameObject.AddComponent<RootMotion.FinalIK.VRIK>();
        vrik.AutoDetectReferences();
        vrik.references.leftThigh = null;
        vrik.references.leftCalf = null;
        vrik.references.leftFoot = null;
        vrik.references.leftToes = null;
        vrik.references.rightThigh = null;
        vrik.references.rightCalf = null;
        vrik.references.rightFoot = null;
        vrik.references.rightToes = null;


        Transform leftHand = Instantiate(vrik.references.leftHand.gameObject).transform;
        leftHand.parent = leftController;
        leftHand.transform.localPosition = new Vector3(0.03f, 0.02f, -0.17f);
        leftHand.transform.localRotation = Quaternion.Euler(140.56f, -94.25f, -80.0f);
        vrik.solver.leftArm.target = leftHand;

        Transform rightHand =  Instantiate(vrik.references.rightHand.gameObject).transform;
        rightHand.parent = rightController;
        rightHand.transform.localPosition = new Vector3(-0.03f, 0.02f, -0.17f);
        rightHand.transform.localRotation = Quaternion.Euler(115.88f, 88.74f, 80.0f);
        vrik.solver.rightArm.target = rightHand;

        Transform head = Instantiate(vrik.references.head.gameObject).transform;
        head.parent = camera;
        head.localPosition = new Vector3(0, -0.133f, -0.089f);
        head.localRotation = Quaternion.Euler(0, 90, 0);
        vrik.solver.spine.headTarget = head;

    }
    
}
