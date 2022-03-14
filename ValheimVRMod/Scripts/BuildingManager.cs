using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using ValheimVRMod.VRCore.UI;
using Valve.VR;

namespace ValheimVRMod.Scripts
{
    class BuildingManager : MonoBehaviour
    {
        private readonly Vector3 handpoint = new Vector3(0, -0.45f, 0.55f);
        private float stickyTimer;
        private bool isReferenced;
        private RaycastHit lastRefCast;
        private GameObject buildRefBox;
        private GameObject buildRefPointer;
        private GameObject buildRefPointer2;
        private int currRefType;
        private bool refWasChanged;
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
        private LayerMask piecelayer2 = LayerMask.GetMask(new string[]
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
        private void Awake()
        {
            createRefBox();
            createRefPointer();
            createRefPointer2();
        }
        private void OnDestroy()
        {
            Destroy(buildRefBox);
            Destroy(buildRefPointer);
            Destroy(buildRefPointer2);
        }
        private void OnRenderObject()
        {
            BuildReferencePoint();
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
                if (!Physics.Raycast(VRPlayer.rightHand.transform.position, VRPlayer.rightHand.transform.TransformDirection(handpoint), out pieceRaycast, 50f, piecelayer))
                {
                    return;
                }
                if (stickyTimer >= 2)
                {
                    EnableRefPoint(true);
                    UpdateRefType();
                    if (!isReferenced)
                    {
                        lastRefCast = pieceRaycast;
                        isReferenced = true;
                    }
                    UpdateRefPosition(lastRefCast, VRPlayer.rightHand.transform.TransformDirection(handpoint));
                    UpdateRefRotation(GetRefDirection(VRPlayer.rightHand.transform.TransformDirection(handpoint)));
                }
                else
                {
                    stickyTimer += Time.unscaledDeltaTime;
                }
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
    }
}
