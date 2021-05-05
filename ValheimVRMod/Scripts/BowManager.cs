using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.VRCore;
using Valve.VR;

public class BowManager : MonoBehaviour {

    float y1 = -0.0053254f;
    float y2 = 0.00497f;
    private GameObject goT;
    private GameObject goS;
    private GameObject goB;
    private GameObject pullObj;
    
    private bool isPulling;
    
    private Quaternion originalRotation;

    

    void Awake() {
        
        originalRotation = transform.localRotation;

        Vector3 stringTop = new Vector3();
        Vector3 stringBottom = new Vector3();
        Vector3 pullStart = new Vector3();
        
        Mesh mesh = GetComponent<MeshFilter>().mesh;
        var trilist = new List<int>();

        for (int i = 0; i < mesh.triangles.Length / 3; i++) {
            bool drawTriangle = false;
            Vector3 top = Vector3.zero;
            Vector3 bottom = Vector3.zero;

            for (int j = 0; j < 3; j++) {
                var v = mesh.vertices[mesh.triangles[i * 3 + j]];
                float y = v.y;

                if (y >= y1 && y <= y2) {
                    drawTriangle = true;
                    break;
                }

                if (y > y2) {
                    top = v;
                }

                if (y < y1) {
                    bottom = v;
                }
            }

            if (top == Vector3.zero || bottom == Vector3.zero) {
                drawTriangle = true;
            }


            if (!drawTriangle) {
                
                if (top.y > stringTop.y) {
                    stringTop = top;    
                }

                if (bottom.y < stringBottom.y) {
                    stringBottom = bottom;
                }
                
                pullStart = Vector3.Lerp(stringTop, stringBottom, 0.5f);
            }

            for (int j = 0; j < 3; j++) {
                if (drawTriangle) {
                    trilist.Add(mesh.triangles[i * 3 + j]);
                } else {
                    trilist.Add(0);
                }
            }
        }

        goT = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        goT.transform.parent = transform;
        goT.transform.localScale *= 0.1f;
        goT.transform.localPosition = stringTop;
        goS = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        goS.transform.parent = transform;
        goS.transform.localScale *= 0.1f;
        goS.transform.localPosition = pullStart;
        goS.transform.localRotation = Quaternion.identity;
        goB = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        goB.transform.parent = transform;
        goB.transform.localScale *= 0.1f;
        goB.transform.localPosition = stringBottom;
        pullObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pullObj.transform.parent = transform;
        pullObj.transform.localScale *= 0.1f;
        pullObj.transform.localPosition = pullStart;

        GetComponent<MeshFilter>().mesh.triangles = trilist.ToArray();
    }

    private void OnRenderObject() {
        
        if (SteamVR_Actions.valheim_Hide.GetState(SteamVR_Input_Sources.RightHand)) {
            Debug.Log("blub..");
            checkPullStuff();
        }
        
        if (SteamVR_Actions.valheim_Hide.GetStateUp(SteamVR_Input_Sources.RightHand)) {
            Debug.Log("blab..");
            checkReleasetuff();
        }
        
    }

    private void checkPullStuff() {
        
        if (!isPulling) {
            checkHandNearString();
            return;
        }
        
        if (Vector3.Distance(VRPlayer.rightHand.transform.position, goS.transform.position) < 0.5f) {
            pullObj.transform.position = VRPlayer.rightHand.transform.position;
        }

        transform.LookAt(VRPlayer.rightHand.transform);

    }

    private void checkReleasetuff() {
        isPulling = false;
        pullObj.transform.position = goS.transform.position;
        goS.GetComponent<MeshRenderer>().material.color = Color.white;
        transform.localRotation = originalRotation;
    }

    private void checkHandNearString() {

        
        Debug.Log(Vector3.Distance(VRPlayer.rightHand.transform.position, goS.transform.position));
        
        if (Vector3.Distance(VRPlayer.rightHand.transform.position, goS.transform.position) > 0.1f) {
            return;
        }
        
        isPulling = true;
        goS.GetComponent<MeshRenderer>().material.color = Color.red;

    }
}