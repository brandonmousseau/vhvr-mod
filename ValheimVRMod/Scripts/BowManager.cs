using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.VRCore;
using Valve.VR;

public class BowManager : MonoBehaviour {

    private const float minStringSize = 0.965f;
    private const float maxPullLength = 0.5f;
    private const float attachRange = 0.2f;
    private Vector3 stringTop;
    private Vector3 stringBottom;
    private Vector3 pullStart;
    private GameObject pullObj;
    private bool lineRendererExists;
    private Quaternion originalRotation;
    private GameObject arrow;
    private bool isPulling;
    
    public static BowManager instance;
    public static float attackDrawPercentage;
    public static Vector3 spawnPoint;
    public static Vector3 aimDir;

    public static bool c_isPulling;
    public static bool c_startedPulling;
    public static bool c_aborting;
    

    void Awake() {

        instance = this;
        originalRotation = transform.localRotation;
        stringTop = new Vector3();
        stringBottom = new Vector3();
        removeOldString();
        pullStart = Vector3.Lerp(stringTop, stringBottom, 0.5f);
        pullObj = new GameObject();
        pullObj.transform.SetParent(transform, false);
        pullObj.transform.forward *= -1;
    }

    private void removeOldString() {
        
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
        
        mesh.triangles = trilist.ToArray();
        
    }
    
    private void createNewString() {
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
            createNewString();
            lineRendererExists = true;
        }

        if (SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand)) {
            handlePulling();
        }
        
        if (SteamVR_Actions.valheim_Grab.GetStateUp(SteamVR_Input_Sources.RightHand)) {
            releaseString();
        }
        
    }

    private void handlePulling() {
        
        if (!isPulling && !checkHandNearString()) {
            return;
        }

        if (Player.m_localPlayer.GetStamina() <= 0) {
            releaseString(true);
            return;
        }
        
        if (Vector3.Distance(VRPlayer.rightHand.transform.position, transform.TransformPoint(pullStart)) < maxPullLength) {

            Vector3 previous = pullObj.transform.localPosition;
            pullObj.transform.position = VRPlayer.rightHand.transform.position;
            Vector3 next = pullObj.transform.localPosition;
            
            if (next.z - pullStart.z < 0) {
                next = pullStart;
            }
            else {
                Player.m_localPlayer.UseStamina(((next.z - previous.z)) / maxPullLength * 10);
            }
            gameObject.GetComponent<LineRenderer>().SetPosition(1, next);

        } // in case of low framerate and the string is pulled lightning fast and released instantly afterwards, we might not have 100% pullLength
          // ... but lets ignore this edgecase
          
        transform.LookAt(VRPlayer.rightHand.transform, -transform.parent.forward);

    }

    private void releaseString(bool withoutShoot = false) {

        if (!isPulling) {
            return;
        }
        
        isPulling = c_isPulling = false;
        attackDrawPercentage = pullPercentage();
        spawnPoint = transform.position;
        aimDir = -transform.forward;
        
        pullObj.transform.localPosition = pullStart;
        transform.localRotation = originalRotation;
        gameObject.GetComponent<LineRenderer>().SetPosition(1, stringTop);
        
        if (withoutShoot || arrow == null || attackDrawPercentage <= 0.0f) {
            
            if (arrow) {
                arrow.transform.SetParent(VRPlayer.rightHand.transform, false);
                if (attackDrawPercentage <= 0.0f) {
                    c_aborting = true;
                }
            }
            
            return;
        }

        Destroy(arrow);

    }
    private float pullPercentage() {
        return (pullObj.transform.localPosition.z - pullStart.z) / maxPullLength;
    }

    private bool checkHandNearString() {
        
        if (Vector3.Distance(VRPlayer.rightHand.transform.position, transform.TransformPoint(pullStart)) > attachRange) {
            return false;
        }

        if (arrow != null) {
            arrow.transform.SetParent(pullObj.transform, false);
            c_startedPulling = true;
            c_isPulling = true;
        }
        
        return isPulling = true;

    }

    public void toggleArrow() {

        if (arrow != null) {
            Destroy(arrow);
            return;
        }

        ItemDrop.ItemData ammoItem = Player.m_localPlayer.GetInventory().GetAmmoItem(Player.m_localPlayer.GetLeftItem().m_shared.m_ammoType);
        if (ammoItem.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Ammo) {
            return;
        }
        
        arrow = Instantiate(ammoItem.m_shared.m_attack.m_attackProjectile);
        // we need to disable the Projectile Component, else the arrow will shoot out of the hands like a New Year rocket
        arrow.GetComponent<Projectile>().enabled = false;
        arrow.transform.SetParent(VRPlayer.rightHand.transform, false);
        arrow.transform.localRotation = Quaternion.identity;
        arrow.transform.localPosition = new Vector3(0, 0, 1.3f);
        
    }
}