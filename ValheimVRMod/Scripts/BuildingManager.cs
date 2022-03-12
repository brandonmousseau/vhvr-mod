using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts
{
    class BuildingManager : MonoBehaviour
    {
        private float stickyTimer;
        private bool isReferenced;
        private GameObject buildRefBox;
        private GameObject buildRefPointer;
        private GameObject buildRefPointer2;
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
            buildRefBox.transform.localScale *= 8f;
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
            var handpoint = new Vector3(0, -0.45f, 0.55f);
            if (Physics.Raycast(VRPlayer.leftHand.transform.position, VRPlayer.leftHand.transform.TransformDirection(handpoint), out pieceRaycast, 50f, piecelayer2) && SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand))
            {
                buildRefBox.SetActive(true);
                buildRefPointer.SetActive(true);
                buildRefPointer2.SetActive(true);
                buildRefBox.transform.position = pieceRaycast.point + (VRPlayer.leftHand.transform.TransformDirection(handpoint) * 0.3f);
                buildRefPointer.transform.position = pieceRaycast.point;
                buildRefPointer2.transform.position = pieceRaycast.point;
                buildRefBox.transform.rotation = Quaternion.FromToRotation(buildRefBox.transform.up, pieceRaycast.normal) * buildRefBox.transform.rotation;
                buildRefPointer.transform.rotation = Quaternion.FromToRotation(buildRefPointer.transform.up, pieceRaycast.normal) * buildRefPointer.transform.rotation;
                buildRefPointer2.transform.rotation = Quaternion.FromToRotation(buildRefPointer2.transform.up, pieceRaycast.normal) * buildRefPointer2.transform.rotation;
                isReferenced = true;
            }
            else if (Physics.Raycast(VRPlayer.rightHand.transform.position, VRPlayer.rightHand.transform.TransformDirection(handpoint), out pieceRaycast, 50f, piecelayer) && SteamVR_Actions.laserPointers_LeftClick.GetState(SteamVR_Input_Sources.RightHand))
            {
                if (stickyTimer >= 2)
                {
                    buildRefBox.SetActive(true);
                    buildRefPointer.SetActive(true);
                    buildRefPointer2.SetActive(true);
                    if (!isReferenced)
                    {
                        buildRefBox.transform.position = pieceRaycast.point + (VRPlayer.rightHand.transform.TransformDirection(handpoint) * 0.3f);
                        buildRefPointer.transform.position = pieceRaycast.point ;
                        buildRefPointer2.transform.position = pieceRaycast.point ;
                        buildRefBox.transform.rotation = Quaternion.FromToRotation(buildRefBox.transform.up, pieceRaycast.normal) * buildRefBox.transform.rotation;
                        buildRefPointer.transform.rotation = Quaternion.FromToRotation(buildRefPointer.transform.up, pieceRaycast.normal) * buildRefPointer.transform.rotation;
                        buildRefPointer2.transform.rotation = Quaternion.FromToRotation(buildRefPointer2.transform.up, pieceRaycast.normal) * buildRefPointer2.transform.rotation;
                        isReferenced = true;
                    }

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
                    buildRefBox.SetActive(false);
                    buildRefPointer.SetActive(false);
                    buildRefPointer2.SetActive(false);
                    stickyTimer = 0;
                    isReferenced = false;
                }
            }
        }
    }
}
