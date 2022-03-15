using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using ValheimVRMod.VRCore.UI;
using Valve.VR;
using System.Collections.Generic;

namespace ValheimVRMod.Scripts
{
    class BuildingManager : MonoBehaviour
    {
        private static Vector3 handpoint = new Vector3(0, -0.45f, 0.55f);
        private float stickyTimer;
        private bool isReferenced;
        private RaycastHit lastRefCast;
        private GameObject buildRefBox;
        private GameObject buildRefPointer;
        private GameObject buildRefPointer2;
        private int currRefType;
        private bool refWasChanged;
        public static BuildingManager instance ;

        //Snapping stuff
        private static bool isSnapping = false;
        private static Transform lastSnapCast;
        private static GameObject pieceOnHand;
        private static int totalSnapPointsCount;
        private static List<GameObject> snapPointsCollider;
        private static GameObject snapPointer;
        private static float snapTimer;

        private LayerMask piecelayer = LayerMask.GetMask(new string[]
        {
            "Default",
            "static_solid",
            "Default_small",
            "piece",
            "piece_nonsolid",
            "terrain",
            "vehicle"
        });
        private static LayerMask piecelayer2 = LayerMask.GetMask(new string[]
        {
            "Default",
            "static_solid",
            "Default_small",
            "piece",
            "terrain",
            "vehicle"
        });

        private void createRefBox()
        {
            buildRefBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            buildRefBox.transform.localScale = new Vector3(2, 0.0001f, 2);
            buildRefBox.transform.localScale *= 16f;
            buildRefBox.layer = 16;
            Destroy(buildRefBox.GetComponent<MeshRenderer>());
        }
        private void createRefPointer()
        {
            buildRefPointer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            buildRefPointer.transform.localScale = new Vector3(1, 2, 1);
            buildRefPointer.transform.localScale *= 0.2f;
            buildRefPointer.layer = 16;
            buildRefPointer.GetComponent<MeshRenderer>().material.color = Color.green;
            Destroy(buildRefPointer.GetComponent<Collider>());
            //Destroy(sphere.GetComponent<MeshRenderer>()); 
        }
        private void createRefPointer2()
        {
            buildRefPointer2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            buildRefPointer2.transform.localScale = new Vector3(3, 0.5f, 3);
            buildRefPointer2.transform.localScale *= 0.2f;
            buildRefPointer2.layer = 16;
            buildRefPointer2.GetComponent<MeshRenderer>().material.color = Color.green;
            Destroy(buildRefPointer2.GetComponent<Collider>());
            //Destroy(sphere.GetComponent<MeshRenderer>()); 
        }
        private void createSnapPointer()
        {
            snapPointer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            snapPointer.transform.localScale = new Vector3(1, 2, 1);
            snapPointer.transform.localScale *= 0.2f;
            snapPointer.layer = 16;
            snapPointer.GetComponent<MeshRenderer>().material.color = Color.yellow;
            Destroy(snapPointer.GetComponent<Collider>());
            //Destroy(sphere.GetComponent<MeshRenderer>()); 
        }
        private void Awake()
        {
            createRefBox();
            createRefPointer();
            createRefPointer2();
            createSnapPointer();
            snapPointsCollider = new List<GameObject>();
            instance = this;
        }
        private void OnDestroy()
        {
            Destroy(buildRefBox);
            Destroy(buildRefPointer);
            Destroy(buildRefPointer2);
            Destroy(snapPointer);
            foreach(GameObject collider in snapPointsCollider)
            {
                Destroy(collider);
            }
            snapPointsCollider = null;
        }
        private void OnRenderObject()
        {
            BuildReferencePoint();
            BuildSnapPoint();
        }

        private void BuildReferencePoint()
        {
            RaycastHit pieceRaycast;
            if (SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand))
            {
                if (!Physics.Raycast(VRPlayer.leftHand.transform.position, VRPlayer.leftHand.transform.TransformDirection(handpoint), out pieceRaycast, 50f, piecelayer2))
                {
                    return;
                }
                EnableRefPoint(true);
                UpdateRefType();
                UpdateRefPosition(pieceRaycast, VRPlayer.leftHand.transform.TransformDirection(handpoint));
                UpdateRefRotation(GetRefDirection(VRPlayer.leftHand.transform.TransformDirection(handpoint)));
                lastRefCast = pieceRaycast;
                isReferenced = true;
            }
            else if (SteamVR_Actions.laserPointers_LeftClick.GetState(SteamVR_Input_Sources.RightHand))
            {
                //if (!Physics.Raycast(VRPlayer.rightHand.transform.position, VRPlayer.rightHand.transform.TransformDirection(handpoint), out pieceRaycast, 50f, piecelayer))
                //{
                //    return;
                //}
                //if (stickyTimer >= 2)
                //{
                //    EnableRefPoint(true);
                //    UpdateRefType();
                //    if (!isReferenced)
                //    {
                //        lastRefCast = pieceRaycast;
                //        isReferenced = true;
                //    }
                //    //UpdateRefPosition(lastRefCast, VRPlayer.rightHand.transform.TransformDirection(handpoint));
                //    //UpdateRefRotation(GetRefDirection(VRPlayer.rightHand.transform.TransformDirection(handpoint)));
                //}
                //else
                //{
                //    stickyTimer += Time.unscaledDeltaTime;
                //}
            }
            else
            {
                stickyTimer += Time.unscaledDeltaTime;
                if (stickyTimer <= 2 || stickyTimer >= 3)
                {
                    EnableRefPoint(false);
                    currRefType = 0;
                    stickyTimer = 0;
                    isReferenced = false;
                }
            }
        }
        private void BuildSnapPoint()
        {
            RaycastHit pieceRaycast;
            if (SteamVR_Actions.laserPointers_LeftClick.GetState(SteamVR_Input_Sources.RightHand))
            {
                if (!Physics.Raycast(VRPlayer.rightHand.transform.position, VRPlayer.rightHand.transform.TransformDirection(handpoint), out pieceRaycast, 50f, piecelayer2))
                {
                    return;
                }
                if (snapTimer >= 2)
                {
                    //EnableRefPoint(true);
                    if (!isSnapping)
                    {
                        lastSnapCast = pieceRaycast.transform;
                        isSnapping = true;
                    }
                    UpdateSnapPointColliders(pieceOnHand, lastSnapCast);
                }
                else
                {
                    snapTimer += Time.unscaledDeltaTime;
                }
            }
            else
            {
                snapTimer += Time.unscaledDeltaTime;
                if (snapTimer <= 2 || snapTimer >= 3)
                {
                    EnableAllSnapPoints(false);
                    snapTimer = 0;
                    isSnapping = false;
                }
            }
        }

        private void UpdateRefType()
        {
            switch (VRControls.instance.getPieceRefModifier())
            {
                case -1:
                    if (!refWasChanged && currRefType > -1)
                        currRefType -= 1;
                    refWasChanged = true;
                    break;
                case 0:
                    refWasChanged = false;
                    break;
                case 1:
                    if (!refWasChanged && currRefType < 1)
                        currRefType += 1;
                    refWasChanged = true;
                    break;
            }
        }
        private Vector3 GetRefDirection(Vector3 refHandDir)
        {
            var refDirection = lastRefCast.normal;
            switch (currRefType)
            {
                case -1:
                    return new Vector3(0, 1, 0);
                case 1:
                    refDirection = new Vector3(lastRefCast.normal.x, 0, lastRefCast.normal.z).normalized;
                    if (refDirection == Vector3.zero)
                    {
                        refDirection = new Vector3(refHandDir.x, 0, refHandDir.z).normalized;
                    }
                    return refDirection;
            }
            return refDirection;
        }

        private void EnableRefPoint(bool enabled)
        {
            buildRefBox.SetActive(enabled);
            buildRefPointer.SetActive(enabled);
            buildRefPointer2.SetActive(enabled);
        }
        private void UpdateRefPosition(RaycastHit pieceRaycast, Vector3 direction)
        {
            if (pieceRaycast.transform && buildRefBox.transform.parent != pieceRaycast.transform)
            {
                buildRefBox.transform.SetParent(pieceRaycast.transform);
            }
            else if (buildRefBox.transform.parent != null)
            {
                buildRefBox.transform.SetParent(null);
            }
            buildRefBox.transform.position = pieceRaycast.point + (VRPlayer.leftHand.transform.TransformDirection(handpoint) * 0.3f);
            buildRefPointer.transform.position = pieceRaycast.point;
            buildRefPointer2.transform.position = pieceRaycast.point;
        }

        private void UpdateRefRotation(Vector3 refDirection)
        {
            buildRefBox.transform.rotation = Quaternion.FromToRotation(buildRefBox.transform.up, refDirection) * buildRefBox.transform.rotation;
            buildRefPointer.transform.rotation = Quaternion.FromToRotation(buildRefPointer.transform.up, refDirection) * buildRefPointer.transform.rotation;
            buildRefPointer2.transform.rotation = Quaternion.FromToRotation(buildRefPointer2.transform.up, refDirection) * buildRefPointer2.transform.rotation;
        }

        public static int TranslateRotation()
        {
            var dir = VRPlayer.rightHand.transform.TransformDirection(handpoint);
            var angle = Vector3.SignedAngle(Vector3.forward, new Vector3(dir.x,0,dir.z).normalized, Vector3.up);
            angle = angle < 0 ? angle + 360 : angle;
            var snapAngle = Mathf.RoundToInt(angle * 16 / 360);
            return snapAngle;
        }


        //snap Stuff
        public Vector3 UpdateSelectedSnapPoints(GameObject onHand)
        {
            pieceOnHand = onHand;
            RaycastHit raySnap;
            if (Physics.Raycast(VRPlayer.rightHand.transform.position, VRPlayer.rightHand.transform.TransformDirection(handpoint), out raySnap, 20f, LayerMask.GetMask("piece_nonsolid")))
            {
                return raySnap.transform.position;
            }
            return onHand.transform.position;
        }

        private static List<Transform> GetSelectedSnapPoints(Transform piece)
        {

            List<Transform> snapPoints = new List<Transform>();
            if (!piece)
            {
                return snapPoints;
            }
            foreach (Transform child in piece)
            {
                if (child.CompareTag("snappoint"))
                {
                    snapPoints.Add(child);
                }
            }
            return snapPoints;
        }
        private static GameObject CreateSnapPointCollider()
        {
            var newCollider = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            newCollider.transform.localScale *= 0.8f;
            newCollider.layer = 16;
            newCollider.GetComponent<MeshRenderer>().material.color = Color.blue;
            //Destroy(buildRefBox.GetComponent<MeshRenderer>());
            return newCollider;
        }
        
        private static void EnableAllSnapPoints(bool enabled)
        {
            for(var i = 0; i< snapPointsCollider.Count; i++)
            {
                if (snapPointsCollider[i] && snapPointsCollider[i].activeSelf != enabled)
                {
                    snapPointsCollider[i].SetActive(enabled);
                }
            }
        }
        private void UpdateSnapPointColliders(GameObject onHand, Transform pieceRaycast)
        {
            var onHandPoints = onHand.transform;
            if (!onHandPoints)
            {
                return;
            }
            if (!pieceRaycast.transform)
            {
                return;
            }
            var aimedPoints = pieceRaycast.parent.transform ? pieceRaycast.parent.transform : pieceRaycast;
            if (!aimedPoints)
            {
                return;
            }
            if (!aimedPoints.GetComponent<Piece>())
            {
                return;
            }
            var onHandSnapPoints = GetSelectedSnapPoints(onHandPoints);
            if (onHandSnapPoints.Count == 0)
            {
                return;
            }
            var aimedSnapPoints = GetSelectedSnapPoints(aimedPoints);
            if (aimedSnapPoints.Count == 0)
            {
                return;
            }
            totalSnapPointsCount = onHandSnapPoints.Count * aimedSnapPoints.Count;
            var snapcount = 0;

            snapPointer.transform.position = aimedPoints.transform.position;
            EnableAllSnapPoints(false);
            for (var i = 0; i < aimedSnapPoints.Count; i++)
            {
                for (var j = 0; j < onHandSnapPoints.Count; j++)
                {
                    if (snapPointsCollider.Count > snapcount)
                    {
                        snapPointsCollider[snapcount].transform.position = aimedSnapPoints[i].position - (onHandSnapPoints[j].position - onHand.transform.position);
                    }
                    else
                    {
                        snapPointsCollider.Add(CreateSnapPointCollider());
                        snapPointsCollider[snapcount].transform.position = aimedSnapPoints[i].position - (onHandSnapPoints[j].position - onHand.transform.position);
                    }
                    snapPointsCollider[snapcount].SetActive(true);
                    snapcount++;
                }
            }
        }
    }
}
