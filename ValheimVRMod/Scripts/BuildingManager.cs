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
        private static GameObject pieceOnHand;
        private static List<GameObject> snapPointsCollider;
        private static GameObject snapPointer;
        private static LineRenderer snapLine;
        private static float snapTimer;

        //Precision Mode
        private static bool isPrecise = false;
        private static Vector3 handTriggerPoint = new Vector3(0, 0.1f, 0.1f);
        private static Vector3 handCenter = new Vector3(0, 0f, -0.1f);
        private static GameObject precisionStatus;
        private static float preciseTimer;
        private static bool justChangedPreciseMode;
        private static bool inPreciseArea;
        private static bool isExitPrecise;
        private static bool isMoving;
        private static GameObject checkDir;
        private static GameObject precisionPosRef;
        private static Transform precisionSnapSave1;
        private static Transform precisionSnapSave2;
        //gizmos 
        private static GameObject translateAxisParent;
        private static GameObject translateAxisX;
        private static GameObject translateAxisY;
        private static GameObject translateAxisZ;
        private static GameObject grabbedAxis1;
        private static GameObject translatePos;
        private static bool isPrecisionMoving;

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

        private void createSnapLine()
        {
            snapLine = new GameObject().AddComponent<LineRenderer>();
            snapLine.gameObject.layer = LayerMask.GetMask("WORLDSPACE_UI_LAYER");
            snapLine.widthMultiplier = 0.005f;
            snapLine.positionCount = 3;
            Material newMaterial = new Material(Shader.Find("Unlit/Color"));
            snapLine.material = newMaterial;
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
            createPrecisionBall();
            createCheckDir();
            createPrecisionAxis();
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
            Destroy(precisionStatus);
            Destroy(checkDir);
            Destroy(precisionPosRef);
            Destroy(translateAxisParent);
            Destroy(translatePos);
            isPrecise = false;
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
            PrecisionMode();
            UpdateLine();
        }

        private void UpdateLine()
        {
            var doLine = false;
            if (isReferenceActive)
            {
                snapLine.material.color = new Color(0,0.4f,0);
                doLine = true;
            }
            else if (isSnapping)
            {
                snapLine.positionCount = 2;
                snapLine.material.color = new Color(0.5f, 0.4f, 0.005f);
            }
            else if (isPrecise)
            {
                doLine = false;
                //if (precisionSnapSave1)
                //{
                //    snapLine.material.color = new Color(0f, 0.2f, 0.5f);
                //}
                //else if (isMoving)
                //{
                //    snapLine.material.color = new Color(0, 0.4f, 0);
                //}
                //else
                //{
                //    snapLine.material.color = new Color(0.5f, 0.4f, 0.005f);
                //}
                //snapLine.positionCount = 3;
                //snapLine.SetPosition(0, checkDir.transform.position + (checkDir.transform.right * -0.2f));
                //snapLine.SetPosition(1, checkDir.transform.position + (checkDir.transform.forward * 0.2f));
                //snapLine.SetPosition(2, checkDir.transform.position + (checkDir.transform.right * 0.2f));

                //var handUp = VRPlayer.leftHand.transform.TransformDirection(0, -0.3f, -0.7f);
                //var lefthandcenter = VRPlayer.leftHand.transform.TransformPoint(handCenter);
                //snapLine.SetPosition(0, lefthandcenter + PlaceModeRayVectorProvider.rayDirectionLeft*4);
                //snapLine.SetPosition(1, lefthandcenter);
                //snapLine.SetPosition(2, lefthandcenter + handUp);
                snapLine.enabled = false;
            }
            else
            {
                snapLine.material.color = new Color(0.8f, 0f, 0f);
                doLine = true;
            }

            if (doLine)
            {
                RaycastHit pieceRaycast;
                if (Physics.Raycast(PlaceModeRayVectorProvider.startingPosition, PlaceModeRayVectorProvider.rayDirection, out pieceRaycast, 50f, piecelayer))
                {
                    snapLine.enabled = true;
                    snapLine.positionCount = 2;
                    snapLine.SetPosition(0, PlaceModeRayVectorProvider.startingPosition);
                    snapLine.SetPosition(1, pieceRaycast.point);
                }
                else
                {
                    snapLine.enabled = true;
                    snapLine.positionCount = 2;
                    snapLine.SetPosition(0, PlaceModeRayVectorProvider.startingPosition);
                    snapLine.SetPosition(1, PlaceModeRayVectorProvider.startingPosition + (PlaceModeRayVectorProvider.rayDirection * 50));
                }
            }
        }

        //Validate Building piece
        public static void ValidateBuildingPiece(GameObject piece)
        {
            Player.m_localPlayer.m_placementStatus = Player.PlacementStatus.Valid;
            Piece component = piece.GetComponent<Piece>();
            
            StationExtension component2 = component.GetComponent<StationExtension>();
            if (!piece.activeSelf)
            {
                Player.m_localPlayer.m_placementStatus = Player.PlacementStatus.Invalid;
            }
            if (component2 != null)
            {
                CraftingStation craftingStation = component2.FindClosestStationInRange(component.transform.position);
                if (craftingStation)
                {
                    component2.StartConnectionEffect(craftingStation);
                }
                else
                {
                    component2.StopConnectionEffect();
                    Player.m_localPlayer.m_placementStatus = Player.PlacementStatus.ExtensionMissingStation;
                }
                if (component2.OtherExtensionInRange(component.m_spaceRequirement))
                {
                    Player.m_localPlayer.m_placementStatus = Player.PlacementStatus.MoreSpace;
                }
            }
            if (component.m_onlyInTeleportArea && !EffectArea.IsPointInsideArea(component.transform.position, EffectArea.Type.Teleport, 0f))
            {
                Player.m_localPlayer.m_placementStatus = Player.PlacementStatus.NoTeleportArea;
            }
            if (!component.m_allowedInDungeons && component.transform.position.y > 3000f && !EnvMan.instance.CheckInteriorBuildingOverride())
            {
                Player.m_localPlayer.m_placementStatus = Player.PlacementStatus.NotInDungeon;
            }

            if (Location.IsInsideNoBuildLocation(component.transform.position))
            {
                Player.m_localPlayer.m_placementStatus = Player.PlacementStatus.NoBuildZone;
            }

            PrivateArea component5 = component.GetComponent<PrivateArea>();
            float radius = component5 ? component5.m_radius : 0f;
            if (!PrivateArea.CheckAccess(component.transform.position, radius))
            {
                Player.m_localPlayer.m_placementStatus = Player.PlacementStatus.PrivateZone;
            }

            component.SetInvalidPlacementHeightlight(Player.m_localPlayer.m_placementStatus != Player.PlacementStatus.Valid);
        }

        //Reference mode 
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
        private void BuildReferencePoint()
        {
            RaycastHit pieceRaycast;
            if (inPreciseArea || isPrecise)
            {
                EnableRefPoint(false);
                currRefType = 0;
                stickyTimer = 0;
                isReferenceActive = false;
                return;
            }
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
        public static bool isSnapMode()
        {
            if (isReferenceActive || !isSnapping || isPrecise)
            {
                snapLine.enabled = false;
                snapPointer.SetActive(false);
            }
            return !isReferenceActive && isSnapping && !isPrecise;
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
            return nearestTransform.position;
        }

        private void BuildSnapPoint()
        {
            RaycastHit pieceRaycast;
            if (SteamVR_Actions.laserPointers_LeftClick.GetState(SteamVR_Input_Sources.RightHand) && !isReferenceActive && !isPrecise)
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


        // Precision stuff

        private void createPrecisionAxis()
        {
            translateAxisParent = new GameObject();

            translateAxisX = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            translateAxisX.transform.localScale = new Vector3(0.02f, 0.1f, 0.02f);
            translateAxisX.GetComponent<MeshRenderer>().material.color = Color.red;
            Destroy(translateAxisX.GetComponent<Collider>());
            translateAxisX.transform.SetParent(translateAxisParent.transform);
            translateAxisX.transform.Rotate(0, 0, 90);

            translateAxisY = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            translateAxisY.transform.localScale = new Vector3(0.02f, 0.1f, 0.02f);
            translateAxisY.GetComponent<MeshRenderer>().material.color = Color.green;
            Destroy(translateAxisY.GetComponent<Collider>());
            translateAxisY.transform.SetParent(translateAxisParent.transform);
            
            translateAxisZ = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            translateAxisZ.transform.localScale = new Vector3(0.02f, 0.1f, 0.02f);
            translateAxisZ.GetComponent<MeshRenderer>().material.color = Color.blue;
            Destroy(translateAxisZ.GetComponent<Collider>());
            translateAxisZ.transform.SetParent(translateAxisParent.transform);
            translateAxisZ.transform.Rotate(90, 0, 0);

            translatePos = new GameObject();
        }
        private void createPrecisionBall()
        {
            precisionStatus = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            precisionStatus.transform.localScale *= 0.05f;
            precisionStatus.layer = 16;
            precisionStatus.GetComponent<MeshRenderer>().material.color = Color.blue;
            Destroy(precisionStatus.GetComponent<Collider>());
        }

        private void createCheckDir()
        {
            checkDir = new GameObject();
            precisionPosRef = new GameObject();
            precisionPosRef.transform.SetParent(checkDir.transform);
        }
        private static void PrecisionMode()
        {
            var triggerPoint = VRPlayer.rightHand.transform.TransformPoint(handTriggerPoint);
            var leftHandCenter = VRPlayer.leftHand.transform.TransformPoint(handCenter);
            var dist = Vector3.Distance(leftHandCenter, triggerPoint);
            //LogUtils.LogDebug("Distance : " + Vector3.Distance(leftHandCenter, triggerPoint));
            precisionStatus.transform.position = triggerPoint;
            if (isExitPrecise)
            {
                preciseTimer -= Time.deltaTime;
                if (preciseTimer < 0)
                {
                    isExitPrecise = false;
                    isPrecise = false;
                    preciseTimer = 0;
                    return;
                }
                return;
            }
            if (isPrecise)
            {
                precisionStatus.GetComponent<MeshRenderer>().material.color = Color.green;
                translateAxisParent.SetActive(true);
            }
            else
            {
                precisionStatus.GetComponent<MeshRenderer>().material.color = Color.blue;
                translateAxisParent.SetActive(false);
            }
            if (!justChangedPreciseMode)
            {
                if (dist < 0.08f)
                {
                    inPreciseArea = true;
                    if (SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand))
                    {
                        preciseTimer += Time.deltaTime;
                        if (preciseTimer > 5)
                        {
                            isPrecise = !isPrecise;
                            justChangedPreciseMode = true;
                        }
                    }
                    else
                    {
                        preciseTimer = 0;
                    }
                }
                else
                {
                    inPreciseArea = false;
                    preciseTimer = 0;
                }
            }
            else
            {
                if (SteamVR_Actions.valheim_Grab.GetStateUp(SteamVR_Input_Sources.LeftHand))
                {
                    justChangedPreciseMode = false;
                }
            }
        }
        public static bool isCurrentlyPreciseMode()
        {
            return isPrecise;
        }
        public static bool isCurrentlyMoving()
        {
            return isMoving;
        }
        public static void ExitPreciseMode()
        {
            isExitPrecise = true;
            preciseTimer = 1;
        }

        public static void PrecisionUpdate(GameObject ghost)
        {
            var leftHandCenter = VRPlayer.leftHand.transform.TransformPoint(handCenter);
            var rightHandCenter = VRPlayer.rightHand.transform.TransformPoint(handCenter);
            var avgPos = (leftHandCenter + rightHandCenter) / 2;
            var distanceHand = Vector3.Distance(leftHandCenter, rightHandCenter);
            var forwardAvg = (PlaceModeRayVectorProvider.rayDirection + PlaceModeRayVectorProvider.rayDirectionLeft) / 2;
            var cross = Vector3.Cross(forwardAvg, (rightHandCenter - avgPos).normalized);
            var avgRot = Quaternion.identity;
            avgRot.SetLookRotation(forwardAvg, cross);
            checkDir.transform.position = avgPos;
            checkDir.transform.rotation = avgRot;

            if (SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand)&& 
                SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand))
            {

                if (!isMoving)
                {
                    isMoving = true;
                    precisionPosRef.transform.position = ghost.transform.position;
                    precisionPosRef.transform.rotation = ghost.transform.rotation;
                }
                //ghost.transform.position = lastPos + (avgPos - lastAvgPos);
                //ghost.transform.rotation = avgRot * (Quaternion.Inverse(lastAvgRot) * lastRot);

                precisionPosRef.transform.position += (checkDir.transform.forward * 1.2f * Time.unscaledDeltaTime * VRControls.instance.getDirectRightYAxis());
                precisionPosRef.transform.RotateAround(precisionPosRef.transform.position, checkDir.transform.up, -VRControls.instance.getDirectRightXAxis()*2);
                
                ghost.transform.position = precisionPosRef.transform.position ;
                ghost.transform.rotation = precisionPosRef.transform.rotation ;

                if (SteamVR_Actions.valheim_Jump.GetState(SteamVR_Input_Sources.Any))
                {
                    if (precisionSnapSave1)
                    {
                        Vector3 vector3 = precisionSnapSave2.position - (precisionSnapSave1.position - precisionPosRef.transform.position);
                        precisionPosRef.transform.position = vector3;
                        ghost.transform.position = precisionPosRef.transform.position;
                    }
                    else
                    {
                        Player.m_localPlayer.FindClosestSnapPoints(ghost.transform, 0.5f, out precisionSnapSave1, out precisionSnapSave2, new List<Piece>());
                    }
                }
                else
                {
                    precisionSnapSave1 = null;
                    precisionSnapSave2 = null;
                }
                //ghost.transform.rotation = avgRot * (Quaternion.Inverse(lastAvgRot) * lastRot);
            }
            else
            {
                isMoving = false;
            }

            //gizmo stuff
            //var rotPlacement = (ghost.transform.position - Player.m_localPlayer.transform.position) * 10;
            //rotPlacement = new Vector3(rotPlacement.x, 0, rotPlacement.z).normalized / 4f;
            //var height = Vector3.up * 1.4f;
            //var rotationOffset = ghost.transform.forward * 10;
            //rotationOffset = new Vector3(rotationOffset.x, 0, rotationOffset.z).normalized;

            //if (grabbedAxis1)
            //{
            //    if (!isPrecisionMoving)
            //    {
            //        isPrecisionMoving = true;
            //        translatePos.transform.position = ghost.transform.position;
            //        translatePos.transform.rotation = ghost.transform.rotation;
            //    }
            //    ghost.transform.position = translatePos.transform.position;
            //    if (grabbedAxis1 == translateAxisX)
            //    {
            //        grabbedAxis1.transform.localPosition = Vector3.Project(translateAxisParent.transform.InverseTransformPoint(leftHandCenter), Vector3.right);
            //        ghost.transform.position += grabbedAxis1.transform.position - translateAxisParent.transform.position;
            //    }
            //    else if (grabbedAxis1 == translateAxisY)
            //    {
            //        grabbedAxis1.transform.localPosition = Vector3.Project(translateAxisParent.transform.InverseTransformPoint(leftHandCenter), Vector3.up);
            //        ghost.transform.position += grabbedAxis1.transform.position - translateAxisParent.transform.position;
            //    }
            //    else if (grabbedAxis1 == translateAxisZ)
            //    {
            //        grabbedAxis1.transform.localPosition = Vector3.Project(translateAxisParent.transform.InverseTransformPoint(leftHandCenter), Vector3.forward);
            //        ghost.transform.position += grabbedAxis1.transform.position - translateAxisParent.transform.position;
            //    }
            //    if (!SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand))
            //    {
            //        translatePos.transform.position += grabbedAxis1.transform.position - translateAxisParent.transform.position;
            //        ghost.transform.position = translatePos.transform.position;
            //        grabbedAxis1 = null;
            //        translateAxisX.transform.localPosition = Vector3.zero;
            //        translateAxisY.transform.localPosition = Vector3.zero;
            //        translateAxisZ.transform.localPosition = Vector3.zero;
            //        isPrecisionMoving = false;
            //    }
            //}
            //else
            //{
            //    if (SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand))
            //    {
            //        if (Vector3.Distance(leftHandCenter, translateAxisParent.transform.position) < 0.1f)
            //        {
            //            var handUp = VRPlayer.leftHand.transform.TransformDirection(0, -0.3f, -0.7f);
            //            if (Mathf.Abs(Vector3.Dot(handUp, translateAxisParent.transform.right)) > 0.6f)
            //            {
            //                grabbedAxis1 = translateAxisX;
            //            }
            //            else if (Mathf.Abs(Vector3.Dot(handUp, translateAxisParent.transform.up)) > 0.6f)
            //            {
            //                grabbedAxis1 = translateAxisY;
            //            }
            //            else if (Mathf.Abs(Vector3.Dot(handUp, translateAxisParent.transform.forward)) > 0.6f)
            //            {
            //                grabbedAxis1 = translateAxisZ;
            //            }
            //        }
            //    }
            //    translateAxisParent.transform.position = Player.m_localPlayer.transform.position + rotPlacement + height;
            //    translateAxisParent.transform.rotation = Quaternion.LookRotation(rotationOffset);
            //}
        }
    }
}
