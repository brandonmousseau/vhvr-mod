using System.Threading;
using UnityEngine;

namespace ValheimVRMod.Scripts {
    public class BowManager : MonoBehaviour {
        
        private const float minStringSize = 0.965f;
        private Vector3[] verts;
        private bool wasPulling;
        
        protected float maxPullLength;
        protected Vector3 stringTop;
        protected Vector3 stringBottom;
        protected Vector3 pullStart;
        protected GameObject pullObj;
        protected Quaternion originalRotation;
        protected bool initialized;
        protected bool wasInitialized;
        
        public bool pulling;
        public Transform rightHand;

        void Awake() {
            
            originalRotation = transform.localRotation;
            stringTop = new Vector3();
            stringBottom = new Vector3();

            Mesh mesh = GetComponent<MeshFilter>().mesh;
            verts = mesh.vertices;
            // we need to run this method in thread as it takes longer than a frame and freezes game for a moment
            Thread thread = new Thread(()=>removeOldString(mesh));
            thread.Start();
            
            pullObj = new GameObject();
            pullObj.transform.SetParent(transform, false);
            pullObj.transform.forward *= -1;
        }

        protected void OnDestroy() {
            Destroy(pullObj);
        }

        /**
     * Removing the old bow string, which is part of the bow mesh to later replace it with a linerenderer.
     * we are making use of the fact that string triangles are longer then all other triangles
     * so we simply iterate all triangles and compare their vertex distances to a certain minimum size
     * for the new triangle array, we just skip those with bigger vertex distance
     * but we save the top and bottom points of the deleted vertices so we can use them for our new string. 
     */
        private void removeOldString(Mesh mesh) {
            
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
                    if (stringTop == null || v.y > stringTop.y) {
                        stringTop = v;
                    }

                    if (stringBottom == null || v.y < stringBottom.y) {
                        stringBottom = v;
                    }
                }
            }
            
            pullStart = Vector3.Lerp(stringTop, stringBottom, 0.5f);
            maxPullLength = 0.6f - pullStart.z;
            initialized = true;
        }

        /**
     * now we create a new string out of a linerenderer with 3 points, using the saved top and bottom points
     * and a new third one in the middle.
     */
        private void createNewString() {
            var lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = false;
            lineRenderer.widthMultiplier = 0.005f;
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
        protected void OnRenderObject() {

            if (!initialized) {
                return;
            }

            if (!wasInitialized) {
                GetComponent<MeshFilter>().mesh.vertices = verts;
                createNewString();
                wasInitialized = true;
            }

            if (pulling) {
                if (!wasPulling) {
                    wasPulling = true;
                }

                pullString();
                gameObject.GetComponent<LineRenderer>().SetPosition(1, pullObj.transform.localPosition);
                transform.LookAt(rightHand, -transform.parent.forward);   
                
            } else if (wasPulling) {
                wasPulling = false;
                pullObj.transform.localPosition = pullStart;
                transform.localRotation = originalRotation;
                gameObject.GetComponent<LineRenderer>().SetPosition(1, stringTop);
            }
        }

        private void pullString() {
            if (Vector3.Distance(rightHand.position, transform.TransformPoint(pullStart)) <
                maxPullLength) {
                pullObj.transform.position = rightHand.position;

                if (pullObj.transform.localPosition.z - pullStart.z < 0) {
                    pullObj.transform.localPosition = new Vector3(pullObj.transform.localPosition.x, pullObj.transform.localPosition.y, pullStart.z);
                }
            } 
        }
    }
}