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
        private Vector3 handpoint = new Vector3(0, -0.45f, 0.55f);
        private float stickyTimer;
        private bool isReferenceActive;
        private RaycastHit lastRefCast;
        private GameObject buildRefBox;
        private GameObject buildRefPointer;
        private GameObject buildRefPointer2;
        private int currRefType;
        private bool refWasChanged;
        public static BuildingManager instance;
        private bool holdPlacePressed;

        //Snapping stuff
        private bool isSnapping = false;
        private Transform firstSnapTransform;
        private Vector3 firstNormal;
        private Vector3 firstPoint;
        private Transform lastSnapTransform;
        private Quaternion lastSnapDirection;
        private GameObject pieceOnHand;
        private List<GameObject> snapPointsCollider;
        private GameObject snapPointer;
        private LineRenderer snapLine;
        private float snapTimer;

        //Precision Mode
        private bool isFreeMode = false;
        private Vector3 handTriggerPoint = new Vector3(0, -0.1f, -0.1f);
        private Vector3 handCenter = new Vector3(0, 0f, -0.1f);
        private GameObject freeModeAxisParent;
        private GameObject freeModeAxis;
        private float triggerFreeModeAreaTimer;
        private bool justChangedFreeMode;
        private bool inTriggerArea;
        private bool isExitFreeMode;
        private bool isMoving;
        private GameObject checkDir;
        private GameObject freeModePosRef;
        private Transform freeModeSnapSave1;
        private Transform freeModeSnapSave2;
        //gizmos 
        private GameObject translateAxisParent;
        private GameObject translateAxisX;
        private GameObject translateAxisY;
        private GameObject translateAxisZ;
        private GameObject grabbedAxis1;
        private GameObject translatePos;
        private bool isPrecisionMoving;
        private float triggerRotationModeTimer;

        //gizmos rotation
        private GameObject rotationAxisParent;
        private GameObject rotationAxisX;
        private GameObject rotationAxisY;
        private GameObject rotationAxisZ;
        private GameObject grabbedAxis2;
        private GameObject rotateReference;
        private bool isRotatingAdv;
        private LineRenderer rotationLine;
        private int lastRotationDist;
        private Vector3 startRotation;
        private Quaternion advRotationGhost;
        private int lastAdvRot;
        private float copyRotationTimer;
        private GameObject rotationChangeAxisIndicator;
        private GameObject rotationChangeAxisColor;
        private bool isRotationWorldAxis;
        private bool justChangedRotationMode;
        private bool justRotatedAnalogLongPress;

        public Piece currentComponent;
        public Transform originalRayTraceTransform;
        public Vector3 originalRayTracePos;
        public Vector3 originalRayTraceDir;
        public Heightmap originalHeightMap;

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
        private LayerMask waterpiecelayer = LayerMask.GetMask(new string[]
        {
            "Default",
            "static_solid",
            "Default_small",
            "piece",
            "piece_nonsolid",
            "terrain",
            "Water",
            "vehicle"
        });
        private LayerMask piecelayer2 = LayerMask.GetMask(new string[]
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
            createFreeModeRing();
            createCheckDir();
            createPrecisionModeAxis();
            createRotationAxis();
            snapPointsCollider = new List<GameObject>();
            lastAdvRot = Player.m_localPlayer.m_placeRotation+1;
            for (var i = 0; i <= 20; i++)
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
            Destroy(freeModeAxisParent);
            Destroy(checkDir);
            Destroy(freeModePosRef);
            Destroy(translateAxisParent);
            Destroy(translatePos);
            Destroy(rotationAxisParent);
            Destroy(rotateReference);
            isFreeMode = false;
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

            if (VHVRConfig.AdvancedBuildingMode())
            {
                UpdateRotateAnalog();
                FreeMode();
                RotationModeChange();
            }

            UpdateLine();
        }

        private void UpdateLine()
        {
            var doLine = false;
            if (isReferenceActive)
            {
                snapLine.material.color = Color.green * 0.4f;
                doLine = true;
            }
            else if (isSnapping)
            {
                snapLine.positionCount = 2;
                snapLine.material.color = new Color(0.5f, 0.4f, 0.005f);
            }
            else if (isFreeMode)
            {
                if(copyRotationTimer>0)
                    doLine = true;
                snapLine.enabled = false;
            }
            else
            {
                snapLine.material.color = Color.red * 0.8f;
                doLine = true;
            }

            if (doLine)
            {
                RaycastHit pieceRaycast;

                var layerCheck = piecelayer;
                if (currentComponent)
                {
                    if (currentComponent.m_waterPiece || currentComponent.m_noInWater)
                        layerCheck = waterpiecelayer;
                }
                if (Physics.Raycast(PlaceModeRayVectorProvider.startingPosition, PlaceModeRayVectorProvider.rayDirection, out pieceRaycast, 50f, layerCheck))
                {
                    snapLine.enabled = true;
                    snapLine.positionCount = 2;
                    snapLine.SetPosition(0, PlaceModeRayVectorProvider.startingPosition);
                    snapLine.SetPosition(1, pieceRaycast.point);
                    originalRayTraceTransform = pieceRaycast.transform;
                    originalRayTracePos = pieceRaycast.point;
                    originalRayTraceDir = pieceRaycast.normal;
                    originalHeightMap = pieceRaycast.collider.GetComponent<Heightmap>();
                }
                else
                {
                    snapLine.enabled = true;
                    snapLine.positionCount = 2;
                    originalRayTraceTransform = null;
                    snapLine.SetPosition(0, PlaceModeRayVectorProvider.startingPosition);
                    snapLine.SetPosition(1, PlaceModeRayVectorProvider.startingPosition + (PlaceModeRayVectorProvider.rayDirection * 50));
                }
            }
        }

        //Validate Building piece
        public void ValidateBuildingPiece(GameObject piece)
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
            if (component.m_noClipping && Player.m_localPlayer.TestGhostClipping(piece, 0.2f))
            {
                Player.m_localPlayer.m_placementStatus = Player.PlacementStatus.Invalid;
            }
            if (!piece.activeSelf)
            {
                Player.m_localPlayer.m_placementStatus = Player.PlacementStatus.Invalid;
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
            buildRefPointer = Instantiate(VRAssetManager.GetAsset<GameObject>("RuneStakeCy"));
            buildRefPointer.layer = 16;

            var lightbox = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lightbox.transform.localScale = new Vector3(0.12f, 0.13f, 0.12f);
            lightbox.transform.SetParent(buildRefPointer.transform);
            Material newMaterial = new Material(Shader.Find("Unlit/Color"));
            lightbox.GetComponent<MeshRenderer>().material = newMaterial;
            lightbox.GetComponent<MeshRenderer>().material.color = Color.green * 0.6f;
            lightbox.transform.localPosition = Vector3.up * 0.6f;
            Destroy(lightbox.GetComponent<Collider>());
        }
        private void createRefPointer2()
        {
            buildRefPointer2 = Instantiate(VRAssetManager.GetAsset<GameObject>("GizmoRing"));
            var child = buildRefPointer2.transform.GetChild(0);
            child.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Unlit/Color"));
            child.GetComponent<MeshRenderer>().material.color = Color.green * 0.6f;
            buildRefPointer2.transform.SetParent(buildRefPointer.transform);
            buildRefPointer2.transform.localPosition = Vector3.up * 0.6f;
        }
        private void BuildReferencePoint()
        {
            RaycastHit pieceRaycast;
            if (inTriggerArea || isFreeMode || isSnapping)
            {
                EnableRefPoint(false);
                currRefType = 0;
                stickyTimer = 0;
                isReferenceActive = false;
                return;
            }
            if (SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand) && !isRotatingAdv)
            {
                if (!Physics.Raycast(PlaceModeRayVectorProvider.startingPositionLeft, PlaceModeRayVectorProvider.rayDirectionLeft, out pieceRaycast, 50f, piecelayer2))
                {
                    return;
                }
                EnableRefPoint(true);
                UpdateRefType();
                UpdateRefPosition(pieceRaycast, PlaceModeRayVectorProvider.rayDirectionLeft);
                UpdateRefRotation(GetRefDirection(PlaceModeRayVectorProvider.rayDirectionLeft));
                buildRefPointer.transform.rotation = Quaternion.FromToRotation(buildRefPointer.transform.up, pieceRaycast.normal) * buildRefPointer.transform.rotation;
                lastRefCast = pieceRaycast;
                isReferenceActive = true;
            }
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
                buildRefPointer2.SetActive(false);
            }
            else
            {
                buildRefBox.transform.position = pieceRaycast.point - (pieceRaycast.normal * 0.25f);
                buildRefPointer2.SetActive(true);
            }
            
            buildRefPointer.transform.position = pieceRaycast.point;
            //buildRefPointer2.transform.position = pieceRaycast.point;
        }

        private void UpdateRefRotation(Vector3 refDirection)
        {
            buildRefBox.transform.rotation = Quaternion.FromToRotation(buildRefBox.transform.up, refDirection) * buildRefBox.transform.rotation;
            buildRefPointer2.transform.rotation = Quaternion.FromToRotation(buildRefPointer2.transform.up, refDirection) * buildRefPointer2.transform.rotation;
        }

        public int TranslateRotation()
        {
            var dir = PlaceModeRayVectorProvider.rayDirection;
            var angle = Vector3.SignedAngle(Vector3.forward, new Vector3(dir.x,0,dir.z).normalized, Vector3.up);
            angle = angle < 0 ? angle + 360 : angle;
            var snapAngle = Mathf.RoundToInt(angle * 16 / 360);
            return snapAngle;
        }

        public bool IsReferenceMode()
        {
            return isReferenceActive;
        }

        //snap Stuff
        private void createSnapPointer()
        {
            snapPointer = Instantiate(VRAssetManager.GetAsset<GameObject>("RuneStakeCy"));
            snapPointer.layer = 16;

            var lightbox = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lightbox.transform.localScale = new Vector3(0.12f, 0.13f, 0.12f);
            lightbox.transform.SetParent(snapPointer.transform);
            Material newMaterial = new Material(Shader.Find("Unlit/Color"));
            lightbox.GetComponent<MeshRenderer>().material = newMaterial;
            lightbox.GetComponent<MeshRenderer>().material.color = Color.yellow * 0.6f;
            lightbox.transform.localPosition = Vector3.up * 0.6f;
            Destroy(lightbox.GetComponent<Collider>());
        }
        public bool isSnapMode()
        {
            if (isReferenceActive || !isSnapping || isFreeMode)
            {
                snapLine.enabled = false;
                snapPointer.SetActive(false);
            }
            return !isReferenceActive && isSnapping && !isFreeMode;
        }
        public Vector3 UpdateSelectedSnapPoints(GameObject onHand)
        {
            pieceOnHand = onHand;
            if(VHVRConfig.AdvancedBuildingMode())
                onHand.transform.rotation = advRotationGhost;
            if (lastSnapTransform && pieceOnHand && lastSnapDirection!= pieceOnHand.transform.rotation )
            {
                snapPointer.SetActive(true);
                lastSnapDirection = pieceOnHand.transform.rotation;
                UpdateSnapPointCollider(pieceOnHand, lastSnapTransform);
            }

            //Multiple Raycast 
            RaycastHit[] snapPointsCast = new RaycastHit[10];
            int hits = Physics.RaycastNonAlloc(PlaceModeRayVectorProvider.startingPosition, PlaceModeRayVectorProvider.rayDirection, snapPointsCast, 12f, LayerMask.GetMask("piece_nonsolid"));
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

            snapLine.SetPosition(0, PlaceModeRayVectorProvider.startingPosition);
            snapLine.SetPosition(1, nearestTransform.position);
            snapLine.enabled = true;
            onHand.SetActive(true);
            return nearestTransform.position;
        }

        private void BuildSnapPoint()
        {
            RaycastHit pieceRaycast;
            if(isSnapping && !lastSnapTransform)
            {
                snapPointer.SetActive(false);
                EnableAllSnapPoints(false);
                firstSnapTransform = null;
                firstNormal = Vector3.zero;
                firstPoint = Vector3.zero;
                lastSnapTransform = null;
                if (pieceOnHand)
                    lastSnapDirection = pieceOnHand.transform.rotation * Quaternion.Euler(0, 90, 0);
                pieceOnHand = null;
                snapTimer = 0;
                isSnapping = false;
            }
            if (SteamVR_Actions.laserPointers_LeftClick.GetState(SteamVR_Input_Sources.RightHand) && !isReferenceActive && !isFreeMode && !CheckMenuIsOpen())
            {
                if (Physics.Raycast(PlaceModeRayVectorProvider.startingPosition, PlaceModeRayVectorProvider.rayDirection, out pieceRaycast, 50f, LayerMask.GetMask("piece")))
                {
                    if (!firstSnapTransform)
                    {
                        firstSnapTransform = pieceRaycast.transform;
                        firstNormal = pieceRaycast.normal;
                        firstPoint = pieceRaycast.point;
                    }
                }
                if (!firstSnapTransform)
                {
                    return;
                }
                if (snapTimer >= 3)
                {
                    //EnableRefPoint(true);
                    if (!isSnapping || !lastSnapTransform)
                    {
                        lastSnapTransform = firstSnapTransform;
                        snapPointer.transform.position = firstPoint;
                        snapPointer.transform.rotation = Quaternion.FromToRotation(snapPointer.transform.up, firstNormal) * snapPointer.transform.rotation;
                        isSnapping = true;
                        if (pieceOnHand)
                            lastSnapDirection = pieceOnHand.transform.rotation * Quaternion.Euler(0, 90, 0);
                    }
                }
                else
                {
                    snapTimer += Time.unscaledDeltaTime;
                }
            }
            else
            {
                if (isSnapping)
                {
                    snapTimer += Time.unscaledDeltaTime;
                }
                if (snapTimer <= 3 || snapTimer >= 4)
                {
                    snapPointer.SetActive(false);
                    EnableAllSnapPoints(false);
                    firstSnapTransform = null;
                    firstNormal = Vector3.zero;
                    firstPoint = Vector3.zero;
                    lastSnapTransform = null;
                    if (pieceOnHand)
                        lastSnapDirection = pieceOnHand.transform.rotation * Quaternion.Euler(0, 90, 0);
                    pieceOnHand = null;
                    snapTimer = 0;
                    isSnapping = false;
                }
            }
        }
        public bool CheckMenuIsOpen()
        {
            return Hud.IsPieceSelectionVisible() || StoreGui.IsVisible() || InventoryGui.IsVisible() || Menu.IsVisible() || (TextViewer.instance && TextViewer.instance.IsVisible()) || Minimap.IsOpen();
        }

        private List<Transform> GetSelectedSnapPoints(Transform piece)
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
        private GameObject CreateSnapPointCollider()
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
        private bool CheckSamePieceSamePlace(Vector3 pos,GameObject ghost, Piece onHandPiece)
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
        private void EnableAllSnapPoints(bool enabled)
        {
            for (var i = 0; i < snapPointsCollider.Count; i++)
            {
                if (snapPointsCollider[i] && snapPointsCollider[i].activeSelf != enabled)
                {
                    snapPointsCollider[i].SetActive(enabled);
                }
            }
        }
        private void ResetAllSnapPoints()
        {
            foreach(var points in snapPointsCollider)
            {
                points.transform.position = Vector3.zero;
            }
        }


        ////// Advanced stuff
        // Precision stuff
        private void createPrecisionModeAxis()
        {
            translateAxisParent = new GameObject();

            translateAxisX = GameObject.CreatePrimitive(PrimitiveType.Cube);
            translateAxisX.transform.localScale = new Vector3(0.005f, 0.1f, 0.005f);
            translateAxisX.GetComponent<MeshRenderer>().material.color = Color.red;
            Destroy(translateAxisX.GetComponent<Collider>());
            translateAxisX.transform.SetParent(translateAxisParent.transform);
            translateAxisX.transform.Rotate(0, 0, 90);

            translateAxisY = GameObject.CreatePrimitive(PrimitiveType.Cube);
            translateAxisY.transform.localScale = new Vector3(0.005f, 0.1f, 0.005f);
            translateAxisY.GetComponent<MeshRenderer>().material.color = Color.green;
            Destroy(translateAxisY.GetComponent<Collider>());
            translateAxisY.transform.SetParent(translateAxisParent.transform);
            
            translateAxisZ = GameObject.CreatePrimitive(PrimitiveType.Cube);
            translateAxisZ.transform.localScale = new Vector3(0.005f, 0.1f, 0.005f);
            translateAxisZ.GetComponent<MeshRenderer>().material.color = Color.blue;
            Destroy(translateAxisZ.GetComponent<Collider>());
            translateAxisZ.transform.SetParent(translateAxisParent.transform);
            translateAxisZ.transform.Rotate(90, 0, 0);

            translatePos = new GameObject();
        }
        private void createFreeModeRing()
        {
            freeModeAxisParent = Instantiate(VRAssetManager.GetAsset<GameObject>("GizmoRing"));
            //freeModeAxisParent.transform.SetParent(rotationAxisParent.transform);
            //freeModeAxisParent.transform.localPosition = Vector3.zero;
            freeModeAxis = freeModeAxisParent.transform.GetChild(0).gameObject;
            freeModeAxis.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Standard"));
            freeModeAxis.GetComponent<MeshRenderer>().material.color = Color.blue;
            freeModeAxis.transform.localScale = new Vector3(1, 5, 1);
            freeModeAxis.transform.localScale *= 0.1f;

            //freeModeAxis = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            //freeModeAxis.transform.localScale = new Vector3(0.055f, 0.01f, 0.055f);
            //freeModeAxis.layer = 16;
            //freeModeAxis.GetComponent<MeshRenderer>().material.color = Color.blue;
            freeModeAxisParent.transform.SetParent(this.gameObject.transform);
            freeModeAxisParent.transform.localPosition = Vector3.up * 0.45f;
            freeModeAxisParent.transform.rotation = this.gameObject.transform.rotation;
            if (!VHVRConfig.AdvancedBuildingMode())
            {
                freeModeAxisParent.SetActive(false);
            }
            //Destroy(freeModeAxis.GetComponent<Collider>());
        }

        private void createCheckDir()
        {
            checkDir = new GameObject();
            freeModePosRef = new GameObject();
            freeModePosRef.transform.SetParent(checkDir.transform);
        }
        private void FreeMode()
        {
            var leftHandCenter = VRPlayer.leftHand.transform.TransformPoint(handCenter);
            var dist = Vector3.Distance(leftHandCenter, freeModeAxisParent.transform.position);
            if (isExitFreeMode)
            {
                triggerFreeModeAreaTimer -= Time.deltaTime;
                if (triggerFreeModeAreaTimer < 0)
                {
                    isExitFreeMode = false;
                    isFreeMode = false;
                    triggerFreeModeAreaTimer = 0;
                    return;
                }
                return;
            }
            
            if (!justChangedFreeMode)
            {
                if (dist < 0.08f)
                {
                    inTriggerArea = true;
                    if (SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand))
                    {
                        triggerFreeModeAreaTimer += Time.deltaTime;
                        if (triggerFreeModeAreaTimer > 5)
                        {
                            isFreeMode = !isFreeMode;
                            justChangedFreeMode = true;
                            if (isFreeMode)
                            {
                                translateAxisParent.SetActive(true);
                                freeModeAxis.GetComponent<MeshRenderer>().material.color = Color.green;
                            }
                            else
                            {
                                translateAxisParent.SetActive(false);
                                freeModeAxis.GetComponent<MeshRenderer>().material.color = Color.blue;
                            }
                        }
                    }
                    else
                    {
                        triggerFreeModeAreaTimer = 0;
                    }
                }
                else
                {
                    inTriggerArea = false;
                    triggerFreeModeAreaTimer = 0;
                }
            }
            else
            {
                if (SteamVR_Actions.valheim_Grab.GetStateUp(SteamVR_Input_Sources.LeftHand))
                {
                    justChangedFreeMode = false;
                }
            }
        }
        public bool isCurrentlyFreeMode()
        {
            if (!VHVRConfig.AdvancedBuildingMode())
                return false;

            return isFreeMode;
        }
        public bool isCurrentlyPreciseMoving()
        {
            if (!VHVRConfig.AdvancedBuildingMode())
                return false;

            return isPrecisionMoving;
        }
        public bool isCurrentlyMoving()
        {
            return isMoving;
        }
        public bool isHoldingPlace()
        {
            if (!VHVRConfig.BuildOnRelease())
                return false;

            if (!SteamVR_Actions.laserPointers_LeftClick.GetState(SteamVR_Input_Sources.RightHand) && !SteamVR_Actions.valheim_Jump.GetState(SteamVR_Input_Sources.Any)) 
                holdPlacePressed = false;
            else if (SteamVR_Actions.laserPointers_LeftClick.GetState(SteamVR_Input_Sources.RightHand))
                holdPlacePressed = true;

            return holdPlacePressed && !freeModeSnapSave1;
        }
        public bool isHoldingJump()
        {
            return SteamVR_Actions.valheim_Jump.GetState(SteamVR_Input_Sources.Any) && !freeModeSnapSave1;
        }
        public void ExitPreciseMode()
        {
            isExitFreeMode = true;
            triggerFreeModeAreaTimer = 1;
        }

        public void PrecisionUpdate(GameObject ghost)
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

            if (SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand) &&
                SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand) &&
                !(grabbedAxis2 || grabbedAxis1)) 
            {

                if (!isMoving)
                {
                    isMoving = true;
                    freeModePosRef.transform.position = ghost.transform.position;
                    freeModePosRef.transform.rotation = ghost.transform.rotation;
                    translateAxisParent.SetActive(false);
                    rotationAxisParent.SetActive(false);
                }
                //ghost.transform.position = lastPos + (avgPos - lastAvgPos);
                //ghost.transform.rotation = avgRot * (Quaternion.Inverse(lastAvgRot) * lastRot);

                freeModePosRef.transform.position += (checkDir.transform.forward * 1.2f * Time.unscaledDeltaTime * VRControls.instance.getDirectRightYAxis());
                freeModePosRef.transform.RotateAround(freeModePosRef.transform.position, checkDir.transform.up, -VRControls.instance.getDirectRightXAxis()*2);
                
                ghost.transform.position = freeModePosRef.transform.position ;
                ghost.transform.rotation = freeModePosRef.transform.rotation ;
                advRotationGhost = freeModePosRef.transform.rotation;

                if (SteamVR_Actions.valheim_Jump.GetState(SteamVR_Input_Sources.Any))
                {
                    if (freeModeSnapSave1)
                    {
                        Vector3 vector3 = freeModeSnapSave2.position - (freeModeSnapSave1.position - freeModePosRef.transform.position);
                        freeModePosRef.transform.position = vector3;
                        ghost.transform.position = freeModePosRef.transform.position;
                    }
                    else
                    {
                        Player.m_localPlayer.FindClosestSnapPoints(ghost.transform, 0.5f, out freeModeSnapSave1, out freeModeSnapSave2, new List<Piece>());
                    }
                }
                else
                {
                    freeModeSnapSave1 = null;
                    freeModeSnapSave2 = null;
                }
                //ghost.transform.rotation = avgRot * (Quaternion.Inverse(lastAvgRot) * lastRot);
            }
            else
            {
                if (isMoving)
                {
                    translateAxisParent.SetActive(true);
                    rotationAxisParent.SetActive(true);
                }
                isMoving = false;
            }

            //gizmo stuff
            var rotPlacement = VRPlayer.leftHand.transform.TransformPoint(handCenter) - (VRPlayer.leftHand.transform.right * -0.2f) + (PlaceModeRayVectorProvider.rayDirectionLeft * 0.1f);
            var rotationOffset = ghost.transform.forward * 10;
            rotationOffset = new Vector3(rotationOffset.x, 0, rotationOffset.z).normalized;
            if (rotationOffset == Vector3.zero)
            {
                var dirCheckRight = new Vector3(ghost.transform.right.x, 0, ghost.transform.right.z);
                rotationOffset = Vector3.Cross(dirCheckRight, Vector3.up);
            }

            if (grabbedAxis1)
            {
                if (!isPrecisionMoving)
                {
                    isPrecisionMoving = true;
                    translatePos.transform.position = ghost.transform.position;
                    translatePos.transform.rotation = ghost.transform.rotation;
                }
                ghost.transform.position = translatePos.transform.position;
                if (grabbedAxis1 == translateAxisX)
                {
                    grabbedAxis1.transform.localPosition = Vector3.Project(translateAxisParent.transform.InverseTransformPoint(rightHandCenter), Vector3.right);
                    ghost.transform.position += grabbedAxis1.transform.position - translateAxisParent.transform.position;
                }
                else if (grabbedAxis1 == translateAxisY)
                {
                    grabbedAxis1.transform.localPosition = Vector3.Project(translateAxisParent.transform.InverseTransformPoint(rightHandCenter), Vector3.up);
                    ghost.transform.position += grabbedAxis1.transform.position - translateAxisParent.transform.position;
                }
                else if (grabbedAxis1 == translateAxisZ)
                {
                    grabbedAxis1.transform.localPosition = Vector3.Project(translateAxisParent.transform.InverseTransformPoint(rightHandCenter), Vector3.forward);
                    ghost.transform.position += grabbedAxis1.transform.position - translateAxisParent.transform.position;
                }
                if (!SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand))
                {
                    translatePos.transform.position += grabbedAxis1.transform.position - translateAxisParent.transform.position;
                    ghost.transform.position = translatePos.transform.position;
                    grabbedAxis1 = null;
                    translateAxisX.transform.localPosition = Vector3.zero;
                    translateAxisY.transform.localPosition = Vector3.zero;
                    translateAxisZ.transform.localPosition = Vector3.zero;
                    isPrecisionMoving = false;
                }
            }
            else
            {
                if (SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand) && !isMoving)
                {
                    if (Vector3.Distance(rightHandCenter, translateAxisParent.transform.position) < 0.1f)
                    {
                        var handUp = VRPlayer.rightHand.transform.TransformDirection(0, -0.3f, -0.7f);
                        if (Mathf.Abs(Vector3.Dot(handUp, translateAxisParent.transform.right)) > 0.6f)
                        {
                            grabbedAxis1 = translateAxisX;
                        }
                        else if (Mathf.Abs(Vector3.Dot(handUp, translateAxisParent.transform.up)) > 0.6f)
                        {
                            grabbedAxis1 = translateAxisY;
                        }
                        else if (Mathf.Abs(Vector3.Dot(handUp, translateAxisParent.transform.forward)) > 0.6f)
                        {
                            grabbedAxis1 = translateAxisZ;
                        }
                    }
                }
                translateAxisParent.transform.position = rotPlacement ;
                translateAxisParent.transform.rotation = Quaternion.LookRotation(rotationOffset);
            }
        }


        //Advanced Rotation
        private void createRotationAxis()
        {
            rotationAxisParent = new GameObject();

            var axisX = Instantiate(VRAssetManager.GetAsset<GameObject>("GizmoRing"));
            axisX.transform.SetParent(rotationAxisParent.transform);
            axisX.transform.localPosition = Vector3.zero;
            var childX = axisX.transform.GetChild(0);
            childX.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Standard"));
            childX.GetComponent<MeshRenderer>().material.color = Color.red;
            childX.transform.localScale *= 0.196f;
            axisX.transform.Rotate(0, 0, 90);
            rotationAxisX = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rotationAxisX.transform.localScale = new Vector3(0.005f, 0.1f, 0.005f);
            rotationAxisX.GetComponent<MeshRenderer>().material.color = Color.red;
            Destroy(rotationAxisX.GetComponent<Collider>());
            rotationAxisX.transform.SetParent(rotationAxisParent.transform);
            rotationAxisX.transform.Rotate(0, 0, 90);

            var axisY = Instantiate(VRAssetManager.GetAsset<GameObject>("GizmoRing"));
            axisY.transform.SetParent(rotationAxisParent.transform);
            axisY.transform.localPosition = Vector3.zero;
            var childY = axisY.transform.GetChild(0);
            childY.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Standard"));
            childY.GetComponent<MeshRenderer>().material.color = Color.green;
            childY.transform.localScale *= 0.20f;
            rotationAxisY = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rotationAxisY.transform.localScale = new Vector3(0.005f, 0.1f, 0.005f);
            rotationAxisY.GetComponent<MeshRenderer>().material.color = Color.green;
            Destroy(rotationAxisY.GetComponent<Collider>());
            rotationAxisY.transform.SetParent(rotationAxisParent.transform);

            var axisZ = Instantiate(VRAssetManager.GetAsset<GameObject>("GizmoRing"));
            axisZ.transform.SetParent(rotationAxisParent.transform);
            axisZ.transform.localPosition = Vector3.zero;
            var childZ = axisZ.transform.GetChild(0);
            childZ.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Standard"));
            childZ.GetComponent<MeshRenderer>().material.color = Color.blue;
            childZ.transform.localScale *= 0.198f;
            axisZ.transform.Rotate(90, 0, 0);
            rotationAxisZ = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rotationAxisZ.transform.localScale = new Vector3(0.005f, 0.1f, 0.005f);
            rotationAxisZ.GetComponent<MeshRenderer>().material.color = Color.blue;
            Destroy(rotationAxisZ.GetComponent<Collider>());
            rotationAxisZ.transform.SetParent(rotationAxisParent.transform);
            rotationAxisZ.transform.Rotate(90, 0, 0);

            rotateReference = new GameObject();

            rotationLine = new GameObject().AddComponent<LineRenderer>();
            rotationLine.widthMultiplier = 0.005f;
            rotationLine.positionCount = 2;
            Material newMaterial = new Material(Shader.Find("Unlit/Color"));
            rotationLine.material = newMaterial;
            rotationLine.enabled = false;
            rotationLine.receiveShadows = false;
            rotationLine.shadowCastingMode = ShadowCastingMode.Off;
            rotationLine.lightProbeUsage = LightProbeUsage.Off;
            rotationLine.reflectionProbeUsage = ReflectionProbeUsage.Off;

            rotationChangeAxisIndicator = Instantiate(VRAssetManager.GetAsset<GameObject>("GizmoRing"));
            rotationChangeAxisColor = rotationChangeAxisIndicator.transform.GetChild(0).gameObject;
            rotationChangeAxisColor.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Standard"));
            rotationChangeAxisColor.GetComponent<MeshRenderer>().material.color = Color.yellow;
            rotationChangeAxisColor.transform.localScale = new Vector3(1.2f, 5, 1.2f);
            rotationChangeAxisColor.transform.localScale *= 0.1f;

            rotationChangeAxisIndicator.transform.SetParent(this.gameObject.transform);
            rotationChangeAxisIndicator.transform.localPosition = Vector3.up * -0.3f;
            rotationChangeAxisIndicator.transform.rotation = this.gameObject.transform.rotation;
            if (!VHVRConfig.AdvancedBuildingMode())
            {
                rotationChangeAxisIndicator.SetActive(false);
            }
        }

        private void RotationModeChange()
        {
            var leftHandCenter = VRPlayer.leftHand.transform.TransformPoint(handCenter);
            var dist = Vector3.Distance(leftHandCenter, rotationChangeAxisIndicator.transform.position);

            if (!justChangedRotationMode)
            {
                if (dist < 0.08f)
                {
                    inTriggerArea = true;
                    if (SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand))
                    {
                        triggerRotationModeTimer += Time.deltaTime;
                        if (triggerRotationModeTimer > 5)
                        {
                            isRotationWorldAxis = !isRotationWorldAxis;
                            justChangedRotationMode = true;
                            if (isRotationWorldAxis)
                            {
                                rotationChangeAxisColor.GetComponent<MeshRenderer>().material.color = Color.cyan;
                            }
                            else
                            {
                                rotationChangeAxisColor.GetComponent<MeshRenderer>().material.color = Color.yellow;
                            }
                        }
                    }
                    else
                    {
                        triggerRotationModeTimer = 0;
                    }
                }
                else
                {
                    inTriggerArea = false;
                    triggerRotationModeTimer = 0;
                }
            }
            else
            {
                if (SteamVR_Actions.valheim_Grab.GetStateUp(SteamVR_Input_Sources.LeftHand))
                {
                    justChangedRotationMode = false;
                }
            }
        }

        public void UpdateRotateAnalog()
        {
            if (VRControls.instance.getDirectRightXAxis() != 0 && SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand))
            {
                if(lastAdvRot!= Player.m_localPlayer.m_placeRotation)
                {
                    if (isRotationWorldAxis||VHVRConfig.AdvancedRotationUpWorld())
                    {
                        var ghost = Player.m_localPlayer.m_placementGhost;
                        ghost.transform.rotation = advRotationGhost;
                        ghost.transform.RotateAround(ghost.transform.position, Vector3.up, 22.5f * -VRControls.instance.getDirectRightXAxis());
                        advRotationGhost = ghost.transform.rotation;
                    }
                    else
                    {
                        advRotationGhost *= Quaternion.Euler(0, 22.5f * -VRControls.instance.getDirectRightXAxis(), 0);
                    }
                    
                    lastAdvRot = Player.m_localPlayer.m_placeRotation;
                }
            }
            if (VRControls.instance.getDirectRightYAxis() != 0 && SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand)) 
            {
                if (copyRotationTimer <= 4)
                {
                    advRotationGhost = Quaternion.Euler(0f, 22.5f * (float)Player.m_localPlayer.m_placeRotation, 0f);
                }
                else
                {
                    if (originalRayTraceTransform && !justRotatedAnalogLongPress) 
                    {
                        Piece pieceParent = originalRayTraceTransform.GetComponentInParent(typeof(Piece)) as Piece;
                        var transformCopy = pieceParent ? pieceParent.transform : originalRayTraceTransform;
                        switch (VRControls.instance.getDirectRightYAxis())
                        {
                            case 1:
                                advRotationGhost = transformCopy.rotation * Quaternion.Euler(0, 180, 0);
                                break;
                            case -1:
                                advRotationGhost = transformCopy.rotation;
                                break;
                        }

                        if (isFreeMode && copyRotationTimer >= 8) 
                        {
                            Player.m_localPlayer.m_placementGhost.transform.position = transformCopy.position;
                            justRotatedAnalogLongPress = true;
                        }
                        else if (!isFreeMode)
                        {
                            justRotatedAnalogLongPress = true;
                        }
                    }
                }
                copyRotationTimer += Time.deltaTime;
            }
            else
            {
                justRotatedAnalogLongPress = false;
                copyRotationTimer = 0;
            }
        }
        public void UpdateRotationAdvanced(GameObject ghost)
        {
            if (!VHVRConfig.AdvancedBuildingMode())
            {
                return;
            }

            var leftHandCenter = VRPlayer.leftHand.transform.TransformPoint(handCenter);
            var rightHandCenter = VRPlayer.rightHand.transform.TransformPoint(handCenter);
            var rotPlacement = VRPlayer.rightHand.transform.TransformPoint(handCenter) - (VRPlayer.rightHand.transform.right * 0.2f) + (PlaceModeRayVectorProvider.rayDirection * 0.1f);
            ghost.transform.rotation = advRotationGhost;
            if (grabbedAxis2)
            {
                if (!isRotatingAdv)
                {
                    isRotatingAdv = true;
                    rotationLine.enabled = true;
                    rotateReference.transform.position = ghost.transform.position;
                    rotateReference.transform.rotation = ghost.transform.rotation;
                    advRotationGhost = ghost.transform.rotation;
                    rotationLine.SetPosition(0, rotationAxisParent.transform.position);
                }
                ghost.transform.rotation = rotateReference.transform.rotation;
                var localHandPos = rotationAxisParent.transform.InverseTransformPoint(leftHandCenter);
                var localPosDir = ((grabbedAxis2.transform.position - rotationAxisParent.transform.position) * 10).normalized;
                var distance = Vector3.Distance(rotationAxisParent.transform.position, grabbedAxis2.transform.position);
                var rotate = false;
                var snapAngleMultiplier = 22.5f ;
                if (distance > 0.05f)
                {
                    if (lastRotationDist == 0)
                    {
                        startRotation = grabbedAxis2.transform.localPosition;
                        lastRotationDist = 1;
                    }
                    snapAngleMultiplier = 22.5f / Mathf.Max(1,Mathf.Floor(distance * 10));
                    rotate = true;
                }else 
                {
                    lastRotationDist = 0;
                    ghost.transform.rotation = advRotationGhost;
                }
                float rotateAngle = 0 ;
                Quaternion rotationTotal;
                var rotationHelper = Vector3.zero;
                if (grabbedAxis2 == rotationAxisX)
                {
                    grabbedAxis2.transform.localPosition = new Vector3(0, localHandPos.y, localHandPos.z);
                    rotationLine.material.color = Color.red * 0.5f;
                    rotateAngle = Vector3.SignedAngle(grabbedAxis2.transform.localPosition, startRotation, -Vector3.right);
                    rotateAngle = Mathf.Round(rotateAngle / snapAngleMultiplier) * snapAngleMultiplier;
                    rotationHelper.x = rotateAngle;
                }
                else if (grabbedAxis2 == rotationAxisY)
                {
                    grabbedAxis2.transform.localPosition = new Vector3(localHandPos.x, 0, localHandPos.z);
                    rotationLine.material.color = Color.green * 0.5f;
                    rotateAngle = Vector3.SignedAngle(grabbedAxis2.transform.localPosition, startRotation, -Vector3.up);
                    rotateAngle = Mathf.Round(rotateAngle / snapAngleMultiplier) * snapAngleMultiplier;
                    rotationHelper.y = rotateAngle;
                }
                else if (grabbedAxis2 == rotationAxisZ)
                {
                    grabbedAxis2.transform.localPosition = new Vector3(localHandPos.x, localHandPos.y, 0);
                    rotationLine.material.color = Color.blue * 0.5f;
                    rotateAngle = Vector3.SignedAngle(grabbedAxis2.transform.localPosition, startRotation, -Vector3.forward);
                    rotateAngle = Mathf.Round(rotateAngle / snapAngleMultiplier) * snapAngleMultiplier;
                    rotationHelper.z = rotateAngle;
                }

                if (isRotationWorldAxis)
                {
                    ghost.transform.RotateAround(ghost.transform.position, rotationAxisParent.transform.right, rotationHelper.x);
                    ghost.transform.RotateAround(ghost.transform.position, Vector3.up, rotationHelper.y);
                    ghost.transform.RotateAround(ghost.transform.position, rotationAxisParent.transform.forward, rotationHelper.z);
                }
                else
                {
                    rotationTotal = rotateReference.transform.rotation * Quaternion.Euler(rotationHelper);
                    ghost.transform.rotation = rotationTotal;
                }

                if (rotate && ghost.transform.rotation != advRotationGhost)
                {
                    VRPlayer.leftHand.hapticAction.Execute(0, 0.01f, 10, 0.01f, SteamVR_Input_Sources.LeftHand);
                    advRotationGhost = ghost.transform.rotation;
                }

                rotationLine.SetPosition(0, rotationAxisParent.transform.position + ((grabbedAxis2.transform.position - rotationAxisParent.transform.position)*10).normalized * 0.05f);
                rotationLine.SetPosition(1, grabbedAxis2.transform.position);
                if (!SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand))
                {
                    grabbedAxis2 = null;
                    lastRotationDist = 0;
                    rotationAxisX.transform.localPosition = Vector3.zero;
                    rotationAxisY.transform.localPosition = Vector3.zero;
                    rotationAxisZ.transform.localPosition = Vector3.zero;
                    isRotatingAdv = false;
                    rotationLine.enabled = false;
                    advRotationGhost = ghost.transform.rotation;
                }
            }
            else
            {
                if (SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand) && !(isMoving || isReferenceActive)) 
                {
                    if (Vector3.Distance(leftHandCenter, rotationAxisParent.transform.position) < 0.1f)
                    {
                        var handUp = VRPlayer.leftHand.transform.TransformDirection(0, -0.3f, -0.7f);
                        if (Mathf.Abs(Vector3.Dot(handUp, rotationAxisParent.transform.right)) > 0.6f)
                        {
                            grabbedAxis2 = rotationAxisX;
                        }
                        else if (Mathf.Abs(Vector3.Dot(handUp, rotationAxisParent.transform.up)) > 0.6f)
                        {
                            grabbedAxis2 = rotationAxisY;
                        }
                        else if (Mathf.Abs(Vector3.Dot(handUp, rotationAxisParent.transform.forward)) > 0.6f)
                        {
                            grabbedAxis2 = rotationAxisZ;
                        }
                    }
                }
                rotationAxisParent.transform.position = rotPlacement;
                if (isRotationWorldAxis)
                {
                    var dirCheckUp = new Vector3(ghost.transform.forward.x, 0, ghost.transform.forward.z);
                    if(dirCheckUp == Vector3.zero)
                    {
                        var dirCheckRight = new Vector3(ghost.transform.right.x, 0, ghost.transform.right.z);
                        dirCheckUp = Vector3.Cross(dirCheckRight, Vector3.up);
                    }
                    rotationAxisParent.transform.rotation = Quaternion.LookRotation(dirCheckUp, Vector3.up);
                }
                else
                {
                    rotationAxisParent.transform.rotation = ghost.transform.rotation;
                }
            }

            var marker = Player.m_localPlayer.m_placementMarkerInstance;
            if (marker)
            {
                marker.transform.LookAt(marker.transform.position + originalRayTraceDir,  ghost.transform.forward);
            }
            //update position after changing
            if (isSnapping || isFreeMode)
            {
                return;
            }
            currentComponent = ghost.GetComponent<Piece>();
            Collider[] componentsInChildren = ghost.GetComponentsInChildren<Collider>();
            if (componentsInChildren.Length != 0)
            {
                ghost.transform.position = originalRayTracePos + originalRayTraceDir * 50f;
                ghost.transform.rotation = advRotationGhost;
                Vector3 b = Vector3.zero;
                float num = 999999f;
                foreach (Collider collider in componentsInChildren)
                {
                    if (!collider.isTrigger && collider.enabled)
                    {
                        MeshCollider meshCollider = collider as MeshCollider;
                        if (!(meshCollider != null) || meshCollider.convex)
                        {
                            Vector3 vector2 = collider.ClosestPoint(originalRayTracePos);
                            float num2 = Vector3.Distance(vector2, originalRayTracePos);
                            if (num2 < num)
                            {
                                b = vector2;
                                num = num2;
                            }
                        }
                    }
                }
                Vector3 b2 = ghost.transform.position - b;
                if (currentComponent.m_waterPiece)
                {
                    b2.y = 3f;
                }
                ghost.transform.position = originalRayTracePos + b2;
                ghost.transform.rotation = advRotationGhost;
            }
            var getPiece = originalRayTraceTransform ? originalRayTraceTransform.GetComponentInParent(typeof(Piece)) as Piece : null;
            if (originalHeightMap||!getPiece)
            {
                ghost.transform.position = new Vector3(originalRayTracePos.x, ghost.transform.position.y, originalRayTracePos.z);
            }

            Transform transform;
            Transform transform2;
            var flag = ZInput.GetButton("AltPlace") || ZInput.GetButton("JoyAltPlace");
            if (Player.m_localPlayer.FindClosestSnapPoints(ghost.transform, 0.5f, out transform, out transform2, new List<Piece>()) && !flag ) 
            {
                Vector3 vector3 = transform2.position - (transform.position - ghost.transform.position);
                if(!CheckSamePieceSamePlace(vector3, ghost, currentComponent))
                {
                    ghost.transform.position = vector3;
                }
            }
        }
    }
}
