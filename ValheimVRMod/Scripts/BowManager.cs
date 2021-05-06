using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.VRCore;
using Valve.VR;

public class BowManager : MonoBehaviour {

    private static float yT = -0.0053254f;
    private static float yB = 0.00497f;
    private static float pullLength = 0.4f;
    private static float attachDistance = 0.1f;
    private Vector3 topPosition;
    private Vector3 bottomPosition;
    private GameObject goS;
    private GameObject pullObj;
    private bool lineRendererExists;
    
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

                if (y >= yT && y <= yB) {
                    drawTriangle = true;
                    break;
                }

                if (y > yB) {
                    top = v;
                }

                if (y < yT) {
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

        topPosition = stringTop;
        bottomPosition = stringBottom;
        
        goS = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        goS.transform.parent = transform;
        goS.transform.localScale *= 0.1f;
        goS.transform.localPosition = pullStart;
        goS.transform.localRotation = Quaternion.identity;

        pullObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pullObj.transform.parent = transform;
        pullObj.transform.localScale *= 0.1f;
        pullObj.transform.localPosition = pullStart;

        GetComponent<MeshFilter>().mesh.triangles = trilist.ToArray();
    }
    
    private void createString() {
        var lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false;
        lineRenderer.widthMultiplier = 0.01f;
        lineRenderer.positionCount = 3;
        lineRenderer.SetPosition(0, topPosition);
        lineRenderer.SetPosition(1, topPosition);
        lineRenderer.SetPosition(2, bottomPosition);
        lineRenderer.material.color = new Color(0.703125f,0.48828125f,0.28515625f);
    }

    private void OnRenderObject() {
        
        if (!lineRendererExists) {
            createString();
            lineRendererExists = true;
        }

        
        if (SteamVR_Actions.valheim_Hide.GetState(SteamVR_Input_Sources.RightHand)) {
            checkPullStuff();
        }
        
        if (SteamVR_Actions.valheim_Hide.GetStateUp(SteamVR_Input_Sources.RightHand)) {
            checkReleasetuff();
        }
        
    }

    private void checkPullStuff() {
        
        if (!isPulling) {
            checkHandNearString();
            return;
        }
        
        if (Vector3.Distance(VRPlayer.rightHand.transform.position, goS.transform.position) < pullLength) {
            pullObj.transform.position = VRPlayer.rightHand.transform.position;
            gameObject.GetComponent<LineRenderer>().SetPosition(1, pullObj.transform.localPosition);
        }

        transform.LookAt(VRPlayer.rightHand.transform,  transform.parent.forward);
        transform.Rotate(new Vector3(0,0, 1), 180);

    }

    private void checkReleasetuff() {
        isPulling = false;
        pullObj.transform.position = goS.transform.position;
        goS.GetComponent<MeshRenderer>().material.color = Color.white;
        transform.localRotation = originalRotation;
        gameObject.GetComponent<LineRenderer>().SetPosition(1, topPosition);
    }

    private void checkHandNearString() {

        
        Debug.Log(Vector3.Distance(VRPlayer.rightHand.transform.position, goS.transform.position));
        
        if (Vector3.Distance(VRPlayer.rightHand.transform.position, goS.transform.position) > attachDistance) {
            return;
        }
        
        isPulling = true;
        goS.GetComponent<MeshRenderer>().material.color = Color.red;

    }
}