using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VR_IK_Creator : MonoBehaviour
{

    private bool initialized;

    public Transform leftController;
    public Transform rightController;
    public Transform camera;


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
        
        RootMotion.FinalIK.VRIK vrik = this.gameObject.AddComponent<RootMotion.FinalIK.VRIK>();
        vrik.AutoDetectReferences();


        Transform leftHand = (new GameObject()).transform;
        leftHand.parent = leftController;
        leftHand.transform.localPosition = new Vector3(0.03f, 0.02f, -0.17f);
        leftHand.transform.localRotation = Quaternion.Euler(140.56f, -94.25f, -80.0f);
        vrik.solver.leftArm.target = leftHand;


        Transform rightHand = (new GameObject()).transform;
        rightHand.parent = rightController;
        rightHand.transform.localPosition = new Vector3(-0.03f, 0.02f, -0.17f);
        rightHand.transform.localRotation = Quaternion.Euler(115.88f, 88.74f, 80.0f);
        vrik.solver.rightArm.target = rightHand;

        Transform head = (new GameObject()).transform;
        head.parent = camera;
        head.localPosition = new Vector3(0, -0.175f, -0.151f);
        head.localRotation = Quaternion.Euler(0, 90, 0);
        vrik.solver.spine.headTarget = head;

    }
    
}
