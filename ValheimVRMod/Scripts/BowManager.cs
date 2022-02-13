using System;
using System.Threading;
using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts {
    public class BowManager : MonoBehaviour {
        
        private const float minStringSize = 0.965f;
        private Vector3[] verts;
        private bool wasPulling;
        private Transform upperLimbBone;
        private Transform lowerLimbBone;
        private Transform stringTop;
        private Transform stringBottom;

        public static float realLifePullPercentage;

        protected const float maxPullLength = 0.6f;
        protected Vector3 pullStart;
        protected GameObject pullObj;
        protected GameObject pushObj;
        protected Quaternion originalRotation;
        protected bool initialized;
        protected bool wasInitialized;

        public bool pulling;
        public Transform mainHand;

        void Awake() {
            
            originalRotation = transform.localRotation;

            Mesh mesh = GetComponent<MeshFilter>().mesh;
            verts = mesh.vertices;
            // we need to run this method in thread as it takes longer than a frame and freezes game for a moment
            Thread thread = new Thread(()=>removeOldString(mesh));
            thread.Start();

            pullObj = new GameObject();
            pullObj.transform.SetParent(transform, false);
            pullObj.transform.forward *= -1;

            pushObj = new GameObject();
            pushObj.transform.SetParent(transform, false);
        }

        protected void OnDestroy() {
            Destroy(pullObj);
            Destroy(pushObj);
        }

        /**
     * Removing the old bow string, which is part of the bow mesh to later replace it with a linerenderer.
     * we are making use of the fact that string triangles are longer then all other triangles
     * so we simply iterate all triangles and compare their vertex distances to a certain minimum size
     * for the new triangle array, we just skip those with bigger vertex distance
     * but we save the top and bottom points of the deleted vertices so we can use them for our new string. 
     */
        private void removeOldString(Mesh mesh) {

            Vector3 localStringTopPos = new Vector3(0, 0, 0);
            Vector3 localStringBottomPos = new Vector3(0, 0, 0);
            for (int i = 0; i < mesh.triangles.Length / 3; i++) {
                Vector3 v1 = verts[mesh.triangles[i * 3]];
                Vector3 v2 = verts[mesh.triangles[i * 3 + 1]];
                Vector3 v3 = verts[mesh.triangles[i * 3 + 2]];

                if (Vector3.Distance(v1, v2) < minStringSize &&
                    Vector3.Distance(v2, v3) < minStringSize &&
                    Vector3.Distance(v3, v1) < minStringSize) {
                    continue;
                }
                
                verts[mesh.triangles[i * 3 + 1]] = verts[mesh.triangles[i * 3]];
                verts[mesh.triangles[i * 3 + 2]] = verts[mesh.triangles[i * 3]];

                foreach (Vector3 v in new[] {v1, v2, v3}) {
                    if (v.y > localStringTopPos.y) {
                        localStringTopPos = v;
                    }

                    if (v.y < localStringBottomPos.y) {
                        localStringBottomPos = v;
                    }
                }
            }

            const float handleHeight = 0.52f;
            Vector3 handleTop = new Vector3(0, 0, 0);
            Vector3 handleBottom = new Vector3(0, 0, 0);
            for (int i = 0; i < verts.Length; i++) {
                Vector3 v = verts[i];

                if (v.y <= handleHeight / 2f && v.y > handleTop.y) {
                    handleTop = v;
                }

                if (v.y >= -handleHeight / 2f && v.y < handleBottom.y) {
                    handleBottom = v;
                }
            }

            upperLimbBone = new GameObject("BowUpperLimbBone").transform;
            lowerLimbBone = new GameObject("BowLowerLimbBone").transform;
            upperLimbBone.parent = lowerLimbBone.parent = transform;
            upperLimbBone.localPosition = new Vector3(0, handleHeight / 2, handleTop.z);
            lowerLimbBone.localPosition = new Vector3(0, -handleHeight / 2, handleBottom.z);
            upperLimbBone.localRotation = Quaternion.identity;
            lowerLimbBone.localRotation = Quaternion.identity;

            stringTop = new GameObject().transform;
            stringBottom = new GameObject().transform;
            stringTop.SetParent(upperLimbBone, false);
            stringBottom.SetParent(lowerLimbBone, false);
            stringTop.position = transform.TransformPoint(localStringTopPos);
            stringBottom.position = transform.TransformPoint(localStringBottomPos);

            pullStart = Vector3.Lerp(localStringTopPos, localStringBottomPos, 0.5f);

            initialized = true;
        }

        /**
     * now we create a new string out of a linerenderer with 3 points, using the saved top and bottom points
     * and a new third one in the middle.
     */
        private void createNewString() {
            var lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.widthMultiplier = 0.005f;
            lineRenderer.positionCount = 3;
            updateStringRenderer();
            lineRenderer.material.color = new Color(0.703125f, 0.48828125f, 0.28515625f); // just a random brown color
        }

        private void skinBones() {
            Transform handleBone = new GameObject("BowHandleBone").transform;
            handleBone.parent = transform;
            handleBone.localRotation = Quaternion.identity;
            handleBone.localPosition = Vector3.Lerp(upperLimbBone.localPosition, lowerLimbBone.localPosition, 0);

            Transform[] bones = new Transform[3] { upperLimbBone, handleBone, lowerLimbBone};

            Matrix4x4[] bindPoses = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) {
                bindPoses[i] = bones[i].worldToLocalMatrix * transform.localToWorldMatrix;
            }

            BoneWeight[] weights = new BoneWeight[verts.Length];
            for (int i = 0; i < verts.Length; i++) {
                if (verts[i].y > upperLimbBone.localPosition.y) {
                    weights[i].boneIndex0 = 0;
                } else if (verts[i].y >= lowerLimbBone.localPosition.y) {
                    weights[i].boneIndex0 = 1;
                } else {
                    weights[i].boneIndex0 = 2;
                }
                weights[i].weight0 = 1;
            }

            gameObject.AddComponent<SkinnedMeshRenderer>();
            SkinnedMeshRenderer skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
            Mesh mesh = GetComponent<MeshFilter>().mesh;
            mesh.boneWeights = weights;
            mesh.bindposes = bindPoses;
            skinnedMeshRenderer.bones = bones;
            skinnedMeshRenderer.sharedMesh = mesh;
            skinnedMeshRenderer.material = GetComponent<MeshRenderer>().material;
            skinnedMeshRenderer.forceMatrixRecalculationPerRender = true;

            // Remove the original renderer since we will be using SkinnedMeshRenderer only.
            Destroy(GetComponent<MeshRenderer>());
        }


        /**
     * Need to use OnRenderObject instead of Update or LateUpdate,
     * because of VRIK Bone Updates happening in LateUpdate 
     */
        protected void OnRenderObject() {

            if (!initialized) {
                return;
            }

            if (!wasInitialized) {
                GetComponent<MeshFilter>().mesh.vertices = verts;
                skinBones();
                createNewString();
                wasInitialized = true;
            }

            if (pulling) {
                if (!wasPulling) {
                    wasPulling = true;
                }

                rotateBowOnPulling();
                pullString();
            } else if (wasPulling) {
                wasPulling = false;
                pullObj.transform.localPosition = pullStart;
                transform.localRotation = originalRotation;
            }

            morphBow();
        }

        private void rotateBowOnPulling() {
            float realLifeHandDistance = (mainHand.position - transform.position).magnitude;

            // The angle between the push direction and the arrow direction.
            double pushOffsetAngle = Math.Asin(VHVRConfig.ArrowRestElevation() / realLifeHandDistance);

            // Align the z-axis of the pushObj with the direction of the draw force and determine its y-axis using the orientation of the bow hand.
            pushObj.transform.LookAt(mainHand, worldUp: -transform.parent.forward);

            // Assuming that the bow is perpendicular to the arrow, the angle between the y-axis of the bow and the y-axis of the pushObj should also be pushOffsetAngle.
            transform.rotation = pushObj.transform.rotation * Quaternion.AngleAxis((float) (-pushOffsetAngle * (180.0 / Math.PI)), Vector3.right);
         }

        private void morphBow() {
            float pullDelta = pullObj.transform.localPosition.z - pullStart.z;

            // Just a heuristic and simplified approximation for the bend angle.
            float bendAngleDegrees = pullDelta <= 0 ? 0 : Mathf.Asin(Math.Min(1, pullDelta)) * 180 / Mathf.PI;
            upperLimbBone.localRotation = Quaternion.Euler(bendAngleDegrees, 0, 0);
            lowerLimbBone.localRotation = Quaternion.Euler(-bendAngleDegrees, 0, 0);

            updateStringRenderer();
        }

        private void updateStringRenderer() {
            gameObject.GetComponent<LineRenderer>().SetPosition(0, stringTop.position);
            gameObject.GetComponent<LineRenderer>().SetPosition(1, pulling ? pullObj.transform.position : stringTop.position);
            gameObject.GetComponent<LineRenderer>().SetPosition(2, stringBottom.position);
        }

        private void pullString() {

            pullObj.transform.position = mainHand.position;
            var pullPos = pullObj.transform.localPosition;
            realLifePullPercentage = Mathf.Pow(Math.Min(Math.Max(pullPos.z - pullStart.z, 0) / (maxPullLength - pullStart.z), 1), 2);

            // If RestrictBowDrawSpeed is enabled, limit the vr pull length by the square root of the current attack draw percentage to simulate the resistance.
            float pullLengthRestriction = VHVRConfig.RestrictBowDrawSpeed() ? Mathf.Lerp(pullStart.z, maxPullLength, Math.Max(Mathf.Sqrt(Player.m_localPlayer.GetAttackDrawPercentage()), 0.01f)) : maxPullLength;

            if (pullPos.z > pullLengthRestriction) {
                pullObj.transform.localPosition = new Vector3(pullPos.x, pullPos.y, pullLengthRestriction);
            }

            if (pullPos.z < pullStart.z) {
                pullObj.transform.localPosition = new Vector3(pullPos.x, pullPos.y, pullStart.z);
            }
        }
    }
}
