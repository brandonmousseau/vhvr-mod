using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.VRCore;
using Valve.VR;

public class BowManager : MonoBehaviour {

    private static float minStringSize = 0.965f;
    private static float maxPullLength = 0.4f;
    private static float attachRange = 0.1f;
    private Vector3 stringTop;
    private Vector3 stringBottom;
    private Vector3 pullStart;
    private GameObject pullObj;
    private bool lineRendererExists;
    
    private bool isPulling;
    
    private Quaternion originalRotation;

    

    void Awake() {
        
        originalRotation = transform.localRotation;

        stringTop = new Vector3();
        stringBottom = new Vector3();
        pullStart = new Vector3();
        
        Mesh mesh = GetComponent<MeshFilter>().mesh;
        var trilist = new List<int>();

        for (int i = 0; i < mesh.triangles.Length / 3; i++) {
            
            bool drawTriangle = false;
            Vector3 v1 = mesh.vertices[mesh.triangles[i * 3]];
            Vector3 v2 = mesh.vertices[mesh.triangles[i * 3 + 1]];
            Vector3 v3 = mesh.vertices[mesh.triangles[i * 3 + 2]];

            if (Vector3.Distance(v1, v2) < minStringSize &&
                Vector3.Distance(v2, v3) < minStringSize &&
                Vector3.Distance(v3, v1) < minStringSize) {
                drawTriangle = true;
            }
            else {

                foreach (Vector3 v in new[] {v1, v2, v3}) {
                    if (stringTop == null || v.y > stringTop.y) {
                        stringTop = v;
                    }

                    if (stringBottom == null || v.y < stringBottom.y) {
                        stringBottom = v;
                    }
                }
            }

            if (drawTriangle) {
                for (int j = 0; j < 3; j++) {
                    trilist.Add(mesh.triangles[i * 3 + j]);
                }
            }
        }
        
        pullStart = Vector3.Lerp(stringTop, stringBottom, 0.5f);

        Debug.Log(stringTop + "__" + stringBottom);

        pullObj = new GameObject();
        pullObj.transform.parent = transform;
        pullObj.transform.localPosition = pullStart;

        GetComponent<MeshFilter>().mesh.triangles = trilist.ToArray();
    }
    
    private void createString() {
        var lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false;
        lineRenderer.widthMultiplier = 0.01f;
        lineRenderer.positionCount = 3;
        lineRenderer.SetPosition(0, stringTop);
        lineRenderer.SetPosition(1, stringTop);
        lineRenderer.SetPosition(2, stringBottom);
        lineRenderer.material.color = new Color(0.703125f,0.48828125f,0.28515625f);
    }
    
    /**
     * Need to use OnRenderObject instead of Update or LateUpdate,
     * because of VRIK Bone Updates happening in LateUpdate 
     */
    private void OnRenderObject() {
        
        if (!lineRendererExists) {
            createString();
            lineRendererExists = true;
        }

        if (SteamVR_Actions.valheim_Hide.GetState(SteamVR_Input_Sources.RightHand)) {
            handlePulling();
        }
        
        if (SteamVR_Actions.valheim_Hide.GetStateUp(SteamVR_Input_Sources.RightHand)) {
            handleReleasing();
        }
        
    }

    private void handlePulling() {
        
        if (!isPulling) {
            checkHandNearString();
            return;
        }
        
        if (Vector3.Distance(VRPlayer.rightHand.transform.position, transform.TransformPoint(pullStart)) < maxPullLength) {
            pullObj.transform.position = VRPlayer.rightHand.transform.position;
            gameObject.GetComponent<LineRenderer>().SetPosition(1, pullObj.transform.localPosition);
        }

        transform.LookAt(VRPlayer.rightHand.transform, -transform.parent.forward);

    }

    private void handleReleasing() {
        isPulling = false;
        pullObj.transform.localPosition = pullStart;
        transform.localRotation = originalRotation;
        gameObject.GetComponent<LineRenderer>().SetPosition(1, stringTop);
    }

    private void checkHandNearString() {
        
        if (Vector3.Distance(VRPlayer.rightHand.transform.position, transform.TransformPoint(pullStart)) > attachRange) {
            return;
        }
        
        isPulling = true;

    }
}