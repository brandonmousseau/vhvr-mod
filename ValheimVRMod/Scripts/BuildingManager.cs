using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using ValheimVRMod.VRCore.UI;
using Valve.VR;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace ValheimVRMod.Scripts
{
    class BuildingManager : MonoBehaviour
    {
        private static Vector3 handpoint = new Vector3(0, -0.45f, 0.55f);
        private float stickyTimer;
        private static bool isReferenceActive;
        private RaycastHit lastRefCast;
        private GameObject buildRefBox;
        private GameObject buildRefPointer;
        private GameObject buildRefPointer2;
        private int currRefType;
        private bool refWasChanged;
        public static BuildingManager instance;

        //Snapping stuff
        private static bool isSnapping = false;
        private static Transform lastSnapTransform;
        private static Vector3 lastSnapDirection;
        private static Transform lastNearestTransform;
        private static GameObject pieceOnHand;
        private static int totalSnapPointsCount;
        private static List<GameObject> snapPointsCollider;
        private static GameObject snapPointer;
        private static LineRenderer snapLine;
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
            snapPointer.transform.localScale = new Vector3(1, 3, 1);
            snapPointer.transform.localScale *= 0.2f;
            snapPointer.layer = 16;
            snapPointer.GetComponent<MeshRenderer>().material.color = Color.yellow;
            Destroy(snapPointer.GetComponent<Collider>());
            //Destroy(sphere.GetComponent<MeshRenderer>()); 
        }
        private void createSnapLine()
        {
            snapLine = new GameObject().AddComponent<LineRenderer>();
            snapLine.gameObject.layer = LayerMask.GetMask("WORLDSPACE_UI_LAYER");
            snapLine.widthMultiplier = 0.05f;
            snapLine.positionCount = 2;
            snapLine.material.color = Color.yellow;
            snapLine.enabled = false;
            snapLine.receiveShadows = false;
            snapLine.shadowCastingMode = ShadowCastingMode.Off;
            snapLine.lightProbeUsage = LightProbeUsage.Off;
            snapLine.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
        private void Awake()
        {
            createRefBox();
            createRefPointer();
            createRefPointer2();
            createSnapPointer();
            createSnapLine();
            snapPointsCollider = new List<GameObject>();
            for(var i = 0; i <= 20; i++)
            {
                snapPointsCollider.Add(CreateSnapPointCollider());
            }
            instance = this;
        }
        private void OnDestroy()
        {
            Destroy(buildRefBox);
            Destroy(buildRefPointer);
            Destroy(buildRefPointer2);
            Destroy(snapPointer);
            Destroy(snapLine);
            foreach (GameObject collider in snapPointsCollider)
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
                if (!Physics.Raycast(PlaceModeRayVectorProvider.startingPositionLeft, PlaceModeRayVectorProvider.rayDirectionLeft, out pieceRaycast, 50f, piecelayer2))
                {
                    return;
                }
                EnableRefPoint(true);
                UpdateRefType();
                UpdateRefPosition(pieceRaycast, PlaceModeRayVectorProvider.rayDirectionLeft);
                UpdateRefRotation(GetRefDirection(PlaceModeRayVectorProvider.rayDirectionLeft));
                lastRefCast = pieceRaycast;
                isReferenceActive = true;
            }
            //else if (SteamVR_Actions.laserPointers_LeftClick.GetState(SteamVR_Input_Sources.RightHand))
            //{
            //    if (!Physics.Raycast(PlaceModeRayVectorProvider.startingPosition, PlaceModeRayVectorProvider.rayDirection, out pieceRaycast, 50f, piecelayer))
            //    {
            //        return;
            //    }
            //    if (stickyTimer >= 2)
            //    {
            //        EnableRefPoint(true);
            //        UpdateRefType();
            //        if (!isReferenced)
            //        {
            //            lastRefCast = pieceRaycast;
            //            isReferenced = true;
            //        }
            //        UpdateRefPosition(lastRefCast, PlaceModeRayVectorProvider.rayDirection);
            //        UpdateRefRotation(GetRefDirection(PlaceModeRayVectorProvider.rayDirection));
            //    }
            //    else
            //    {
            //        stickyTimer += Time.unscaledDeltaTime;
            //    }
            //}
            else
            {
                stickyTimer += Time.unscaledDeltaTime;
                if (stickyTimer <= 2 || stickyTimer >= 3)
                {
                    EnableRefPoint(false);
                    currRefType = 0;
                    stickyTimer = 0;
                    isReferenceActive = false;
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
            
            if (currRefType == 0)
            {
                buildRefBox.transform.position = pieceRaycast.point - (pieceRaycast.normal * 0.2f) + Vector3.Project(pieceRaycast.transform.position - pieceRaycast.point, -pieceRaycast.normal);
            }
            else
            {
                buildRefBox.transform.position = pieceRaycast.point - (pieceRaycast.normal * 0.25f);
            }
            
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
            var dir = PlaceModeRayVectorProvider.rayDirection;
            var angle = Vector3.SignedAngle(Vector3.forward, new Vector3(dir.x,0,dir.z).normalized, Vector3.up);
            angle = angle < 0 ? angle + 360 : angle;
            var snapAngle = Mathf.RoundToInt(angle * 16 / 360);
            return snapAngle;
        }

        public static bool IsReferenceMode()
        {
            return isReferenceActive;
        }

        //snap Stuff

        public static bool isSnapMode()
        {
            if (isReferenceActive || !isSnapping)
            {
                snapLine.enabled = false;
                snapPointer.SetActive(false);
            }
            return !isReferenceActive && isSnapping;
        }
        public Vector3 UpdateSelectedSnapPoints(GameObject onHand)
        {
            pieceOnHand = onHand;
            if (lastSnapTransform && pieceOnHand && lastSnapDirection!= pieceOnHand.transform.forward )
            {
                snapPointer.SetActive(true);
                lastSnapDirection = pieceOnHand.transform.forward;
                UpdateSnapPointCollider(pieceOnHand, lastSnapTransform);
            }

            //Multiple Raycast 
            RaycastHit[] snapPointsCast = new RaycastHit[10];
            int hits = Physics.RaycastNonAlloc(PlaceModeRayVectorProvider.startingPosition, PlaceModeRayVectorProvider.rayDirection, snapPointsCast, 20f, LayerMask.GetMask("piece_nonsolid"));
            if (hits == 0)
            {
                snapLine.enabled = false;
                //if (lastNearestTransform)
                //{
                //    return lastNearestTransform.transform.position;
                //}
                return onHand.transform.position;
            }

            Transform nearestTransform = snapPointsCast[0].transform;

            for (int i = 1; i < hits; i++)
            {
                var dir = PlaceModeRayVectorProvider.rayDirection;
                var nearestPosRef = nearestTransform.position - PlaceModeRayVectorProvider.startingPosition;
                var currPosRef = snapPointsCast[i].transform.position - PlaceModeRayVectorProvider.startingPosition;
                if (Vector3.Dot(dir, nearestPosRef) < Vector3.Dot(dir, currPosRef))
                {
                    nearestTransform = snapPointsCast[i].transform;
                }
            }

            //Check all nearest
            //Transform nearestTransform = snapPointsCollider[0].transform;

            //for (int i = 1; i < snapPointsCollider.Count; i++)
            //{
            //    var dir = PlaceModeRayVectorProvider.rayDirection;
            //    var nearestPosRef = nearestTransform.position - PlaceModeRayVectorProvider.startingPosition;
            //    var currPosRef = snapPointsCollider[i].transform.position - PlaceModeRayVectorProvider.startingPosition;
            //    if (Vector3.Dot(dir, currPosRef) > 0.5f)
            //    {
            //        if (Vector3.Dot(dir, nearestPosRef) < Vector3.Dot(dir, currPosRef))
            //        {
            //            nearestTransform = snapPointsCollider[i].transform;
            //        }
            //    }

            //}
            snapLine.SetPosition(0, PlaceModeRayVectorProvider.startingPosition);
            snapLine.SetPosition(1, nearestTransform.position);
            snapLine.enabled = true;
            lastNearestTransform = nearestTransform;
            return nearestTransform.position;
        }

        private void BuildSnapPoint()
        {
            RaycastHit pieceRaycast;
            if (SteamVR_Actions.laserPointers_LeftClick.GetState(SteamVR_Input_Sources.RightHand) && !isReferenceActive)
            {
                if (!Physics.Raycast(PlaceModeRayVectorProvider.startingPosition, PlaceModeRayVectorProvider.rayDirection, out pieceRaycast, 50f, LayerMask.GetMask("piece")))
                {
                    return;
                }
                if (snapTimer >= 2)
                {
                    //EnableRefPoint(true);
                    if (!isSnapping || !lastSnapTransform)
                    {
                        lastSnapTransform = pieceRaycast.transform;
                        snapPointer.transform.position = pieceRaycast.transform.position;
                        snapPointer.transform.rotation = Quaternion.FromToRotation(snapPointer.transform.up, pieceRaycast.normal) * snapPointer.transform.rotation;
                        isSnapping = true;
                    }
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
                    snapPointer.SetActive(false);
                    EnableAllSnapPoints(false);
                    lastSnapTransform = null;
                    pieceOnHand = null;
                    lastSnapDirection = Vector3.zero;
                    snapTimer = 0;
                    isSnapping = false;
                }
            }
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
            newCollider.SetActive(false);
            newCollider.transform.localScale *= 1.5f;
            newCollider.layer = 16;
            //newCollider.GetComponent<MeshRenderer>().material.color = Color.blue;
            Destroy(newCollider.GetComponent<MeshRenderer>());

            var newIndicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            newIndicator.GetComponent<MeshRenderer>().material.color = Color.yellow;
            newIndicator.transform.localScale *= 0.2f;
            Destroy(newIndicator.GetComponent<Collider>());
            newIndicator.transform.SetParent(newCollider.transform);

            return newCollider;
        }
        private void UpdateSnapPointCollider(GameObject onHand, Transform pieceRaycast)
        {
            var onHandPoints = onHand.transform;
            if (!onHandPoints)
            {
                return;
            }
            if (!pieceRaycast || !pieceRaycast.transform)
            {
                return;
            }
            Piece onHandPiece = onHand.GetComponent<Piece>();
            Piece pieceParent = pieceRaycast.GetComponentInParent(typeof(Piece)) as Piece;
            if (!pieceParent)
            {
                return;
            }
            var aimedPoints = pieceParent.transform;
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

            EnableAllSnapPoints(false);
            ResetAllSnapPoints();
            for (var i = 0; i < aimedSnapPoints.Count; i++)
            {
                for (var j = 0; j < onHandSnapPoints.Count; j++)
                {
                    var currPos = aimedSnapPoints[i].position - (onHandSnapPoints[j].position - onHand.transform.position);

                    //Snap point check 
                    //check if its the same position as its reference
                    if (currPos == aimedPoints.transform.position)
                    {
                        continue;
                    }
                    //check if there's already same piece on that snapping point 
                    if (CheckSamePieceSamePlace(currPos, onHand, onHandPiece))
                    {
                        continue;
                    }
                    //check if its a duplicate of exsisting point
                    foreach (var points in snapPointsCollider)
                    {
                        if (points.transform.position == currPos)
                        {
                            continue;
                        }
                    }

                    //actually make snapping point
                    if (snapPointsCollider.Count < snapcount + 1)
                    {
                        snapPointsCollider.Add(CreateSnapPointCollider());
                    }
                    snapPointsCollider[snapcount].transform.position = currPos;
                    snapPointsCollider[snapcount].transform.rotation = onHandPoints.rotation;
                    snapPointsCollider[snapcount].SetActive(true);
                    snapcount++;
                }
            }
        }
        private static bool CheckSamePieceSamePlace(Vector3 pos,GameObject ghost, Piece onHandPiece)
        {
            Collider[] piecesInPlace = Physics.OverlapSphere(pos, 1f, LayerMask.GetMask("piece"));
            var name = ghost.name;
            var rotation = ghost.transform.rotation;
            var allowRotatedOverlap = onHandPiece.m_allowRotatedOverlap;
            foreach(var piece in piecesInPlace)
            {
                Piece pieceParent = piece.GetComponentInParent(typeof(Piece)) as Piece;

                //same function as IsOverlapingOtherPiece
                if (Vector3.Distance(pos, pieceParent.transform.position) < 0.05f && 
                    (!allowRotatedOverlap || Quaternion.Angle(piece.transform.rotation, rotation) <= 10f) && 
                    pieceParent.gameObject.name.StartsWith(name))
                {
                    return true;
                }
            }
            return false;
        }
        private static void EnableAllSnapPoints(bool enabled)
        {
            for (var i = 0; i < snapPointsCollider.Count; i++)
            {
                if (snapPointsCollider[i] && snapPointsCollider[i].activeSelf != enabled)
                {
                    snapPointsCollider[i].SetActive(enabled);
                }
            }
        }
        private static void ResetAllSnapPoints()
        {
            foreach(var points in snapPointsCollider)
            {
                points.transform.position = Vector3.zero;
            }
        }
    }
}
