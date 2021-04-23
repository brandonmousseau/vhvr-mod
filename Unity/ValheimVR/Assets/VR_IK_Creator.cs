using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VR_IK_Creator : MonoBehaviour
{

    public Transform leftController;
    public Transform rightController;
    public Transform camera;

    void Awake()
    {
        
        RootMotion.FinalIK.VRIK vrik = this.gameObject.AddComponent<RootMotion.FinalIK.VRIK>();
        foreach (Transform child in gameObject.GetComponentsInChildren<Transform>()) {
            
            switch (child.name) {
                case "Visual":
                    vrik.references.root = child;
                    break;
                case "Hips":
                    vrik.references.pelvis = child;
                    break;
                case "Spine1":
                    vrik.references.spine = child;
                    break;
                case "Spine2":
                    vrik.references.chest = child;
                    break;
                case "Neck":
                    vrik.references.neck = child;
                    break;
                case "Head":
                    vrik.references.head = child;
                    break;
                case "LeftShoulder":
                    vrik.references.leftShoulder = child;
                    break;
                case "LeftArm":
                    vrik.references.leftUpperArm = child;
                    break;
                case "LeftForeArm":
                    vrik.references.leftForearm = child;
                    break;
                case "LeftHand":
                    vrik.references.leftHand = child;
                    break;
                case "RightShoulder":
                    vrik.references.rightShoulder = child;
                    break;
                case "RightArm":
                    vrik.references.rightUpperArm = child;
                    break;
                case "RightForeArm":
                    vrik.references.rightForearm = child;
                    break;
                case "RightHand":
                    vrik.references.rightHand = child;
                    break;

            }

        }


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


    // Update is called once per frame
    void Update()
    {
        
    }
}
