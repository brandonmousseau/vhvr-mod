using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;
using Valve.VR.InteractionSystem;

namespace ValheimVRMod.Scripts {
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
        private bool pulling;
        private LineRenderer predictionLine;

        public static BowManager instance;
        public static float attackDrawPercentage;
        public static Vector3 spawnPoint;
        public static Vector3 aimDir;

        public static bool isPulling;
        public static bool startedPulling;
        public static bool aborting;


        private void Start() {
            predictionLine = new GameObject().AddComponent<LineRenderer>();
            predictionLine.widthMultiplier = 0.01f;
            predictionLine.positionCount = 20;
            predictionLine.material.color = Color.white;
            predictionLine.enabled = false;
            predictionLine.receiveShadows = false;
            predictionLine.shadowCastingMode = ShadowCastingMode.Off;
        }

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

        private void OnDestroy() {
            Destroy(arrow);
            Destroy(pullObj);
            Destroy(predictionLine);
        }

        /**
     * Removing the old bow string, which is part of the bow mesh to later replace it with a linerenderer.
     * we are making use of the fact that string triangles are longer then all other triangles
     * so we simply iterate all triangles and compare their vertex distances to a certain minimum size
     * for the new triangle array, we just skip those with bigger vertex distance
     * but we save the top and bottom points of the deleted vertices so we can use them for our new string. 
     */
        private void removeOldString() {
            var mesh = GetComponent<MeshFilter>().mesh;
            var trilist = new List<int>();

            for (int i = 0; i < mesh.triangles.Length / 3; i++) {
                Vector3 v1 = mesh.vertices[mesh.triangles[i * 3]];
                Vector3 v2 = mesh.vertices[mesh.triangles[i * 3 + 1]];
                Vector3 v3 = mesh.vertices[mesh.triangles[i * 3 + 2]];

                if (Vector3.Distance(v1, v2) < minStringSize &&
                    Vector3.Distance(v2, v3) < minStringSize &&
                    Vector3.Distance(v3, v1) < minStringSize) {
                    for (int j = 0; j < 3; j++) {
                        trilist.Add(mesh.triangles[i * 3 + j]);
                    }
                    continue;
                }
                foreach (Vector3 v in new[] {v1, v2, v3}) {
                    if (stringTop == null || v.y > stringTop.y) {
                        stringTop = v;
                    }

                    if (stringBottom == null || v.y < stringBottom.y) {
                        stringBottom = v;
                    }
                }
            }

            mesh.triangles = trilist.ToArray();
        }

        /**
     * now we create a new string out of a linerenderer with 3 points, using the saved top and bottom points
     * and a new third one in the middle.
     */
        private void createNewString() {
            var lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = false;
            lineRenderer.widthMultiplier = 0.01f;
            lineRenderer.positionCount = 3;
            lineRenderer.SetPosition(0, stringTop);
            lineRenderer.SetPosition(1, stringTop);
            lineRenderer.SetPosition(2, stringBottom);
            lineRenderer.material.color = new Color(0.703125f, 0.48828125f, 0.28515625f); // just a random brown color
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

            updatePredictionLine();
        }

        /**
     * calculate predictionline of how the arrow will fly
     */
        private void updatePredictionLine() {
            if (!predictionLine.enabled) {
                return;
            }

            float stepLength = 0.1f;
            float stepSize = 20;
            float unknownVelFactor = 5;
            Vector3 pos = transform.position;
            Vector3 vel = -transform.forward * stepSize * unknownVelFactor * attackDrawPercentage;
            List<Vector3> pointList = new List<Vector3>();

            for (int i = 0; i < stepSize; i++) {
                pointList.Add(pos);
                vel += Vector3.down * 9.81f * stepLength;
                pos += vel * stepLength;
            }

            predictionLine.positionCount = 20;
            predictionLine.SetPositions(pointList.ToArray());
        }

        private void handlePulling() {
            if (!pulling && !checkHandNearString()) {
                return;
            }

            spawnPoint = transform.position;
            aimDir = -transform.forward;
            attackDrawPercentage = pullPercentage();

            if (Player.m_localPlayer.GetStamina() <= 0) {
                releaseString(true);
                return;
            }

            if (Vector3.Distance(VRPlayer.rightHand.transform.position, transform.TransformPoint(pullStart)) <
                maxPullLength) {
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
            if (!pulling) {
                return;
            }

            predictionLine.enabled = false;
            pulling = isPulling = false;
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
                        aborting = true;
                    }
                }

                return;
            }
            // SHOOTING
            VRPlayer.leftHand.hapticAction.Execute(0, 0.2f, 100, 0.3f, SteamVR_Input_Sources.LeftHand);
            Destroy(arrow);
        }

        private float pullPercentage() {
            return (pullObj.transform.localPosition.z - pullStart.z) / maxPullLength;
        }

        private bool checkHandNearString() {
            if (Vector3.Distance(VRPlayer.rightHand.transform.position, transform.TransformPoint(pullStart)) >
                attachRange) {
                return false;
            }

            if (arrow != null) {
                arrow.transform.SetParent(pullObj.transform, false);
                startedPulling = true;
                isPulling = true;
                predictionLine.enabled = VHVRConfig.UseArrowPredictionGraphic();
            }

            return pulling = true;
        }

        public void toggleArrow() {
            if (arrow != null) {
                Destroy(arrow);
                return;
            }

            var ammoType = Player.m_localPlayer.GetLeftItem().m_shared.m_ammoType;
            
            ItemDrop.ItemData ammoItem = Player.m_localPlayer.GetInventory().GetAmmoItem(ammoType);

            if (ammoItem == null) {
                // out of ammo
                return;
            }
            
            if (ammoItem.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Ammo) {
                return;
            }

            arrow = Instantiate(ammoItem.m_shared.m_attack.m_attackProjectile, VRPlayer.rightHand.transform);
            // we need to disable the Projectile Component, else the arrow will shoot out of the hands like a New Year rocket
            arrow.GetComponent<Projectile>().enabled = false;
            arrow.transform.localRotation = Quaternion.identity;
            arrow.transform.localPosition = new Vector3(0, 0, 1.25f);
            ParticleFix.maybeFix(arrow);
        }
        
        public bool isHoldingArrow() {
            return arrow != null;
        }
    }
}