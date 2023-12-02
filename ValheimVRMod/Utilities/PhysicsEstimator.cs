using System.Collections.Generic;
using UnityEngine;
using Valve.VR.InteractionSystem;

namespace ValheimVRMod.Utilities
{
    // Utility component for estimating the physics of the game object such as its velocity.
    public class PhysicsEstimator : MonoBehaviour
    {
        private const int MAX_SNAPSHOTS = 8;
        private const int MAX_SPARSE_SNAPSHOTS = 16;
        private const int SPARSE_SNAPSHOT_INTERVAL = 4;

        private List<Vector3> snapshots = new List<Vector3>();
        private List<Quaternion> rotationSnapshots = new List<Quaternion>();
        private List<Vector3> velocitySnapshots = new List<Vector3>();
        private List<Vector3> sparseSnapshots = new List<Vector3>(); // Sparsely snapshotted positions for estimating longest locomotion in a time span.
        private Vector3? cachedAverageVelocityInSnapshots = null;
        private LineRenderer debugVelocityLine;
        private Hand hand = null;

        private int sparseSnapshotTicker = 0;

        // The transform used as the frames of reference for velocity calculation.
        private Transform _refTransform;
        public Transform refTransform {
            get
            {
                return _refTransform;
            }
            set
            {
                if (_refTransform == value)
                {
                    return;
                }
                snapshots.Clear();
                rotationSnapshots.Clear();
                velocitySnapshots.Clear();
                sparseSnapshots.Clear();
                cachedAverageVelocityInSnapshots = null;
                _refTransform = value;
            }
        }

        public bool renderDebugVelocityLine = false;

        public void UseVrHandControllerPhysics(Hand hand)
        {
            this.hand = hand;
        }

        private void Awake()
        {
            CreateDebugVelocityLine();
        }

        void FixedUpdate()
        {
            snapshots.Add(refTransform == null ? transform.position : refTransform.InverseTransformPoint(transform.position));
            rotationSnapshots.Add(refTransform == null ? transform.rotation : Quaternion.Inverse(refTransform.rotation) * transform.rotation);
            if (snapshots.Count >= 2) {
                // TODO: consider using least square fit or a smoonthening function over all snapshots, but should balance with performance too.
                velocitySnapshots.Add((snapshots[snapshots.Count - 1] - snapshots[0]) / Time.fixedDeltaTime / (snapshots.Count - 1));
            }
            if ((++sparseSnapshotTicker) >= SPARSE_SNAPSHOT_INTERVAL)
            {
                sparseSnapshots.Add(snapshots[0]);
                sparseSnapshotTicker = 0;
            }
            cachedAverageVelocityInSnapshots = null;

            if (snapshots.Count > MAX_SNAPSHOTS)
            {
                snapshots.RemoveAt(0);
            }
            if (rotationSnapshots.Count > MAX_SNAPSHOTS)
            {
                rotationSnapshots.RemoveAt(0);
            }
            if (velocitySnapshots.Count > MAX_SNAPSHOTS)
            {
                velocitySnapshots.RemoveAt(0);
            }
            if (sparseSnapshots.Count > MAX_SPARSE_SNAPSHOTS)
            {
                sparseSnapshots.RemoveAt(0);
            }
        }

        void OnRenderObject()
        {
            debugVelocityLine.enabled = renderDebugVelocityLine;
            debugVelocityLine.SetPosition(0, transform.position);
            debugVelocityLine.SetPosition(1, transform.position + GetVelocity());
        }

        void Destroy()
        {
            Destroy(debugVelocityLine.gameObject);
        }

        public Vector3 GetVelocity(Vector3? position = null)
        {
            if (hand)
            {
                if (position == null)
                {
                    return hand.GetTrackedObjectVelocity();
                }
                return hand.GetTrackedObjectVelocity() + Vector3.Cross(hand.GetTrackedObjectAngularVelocity(), (Vector3)position - transform.position);
            }
          
            if (velocitySnapshots.Count == 0)
            {
                return Vector3.zero;
            }

            Vector3 v = velocitySnapshots[velocitySnapshots.Count - 1];
            if (position != null && rotationSnapshots.Count > 1)
            {
                Vector3 localPosition = transform.InverseTransformPoint((Vector3) position);
                Vector3 shiftDueToRotation = rotationSnapshots[rotationSnapshots.Count - 1] * localPosition - rotationSnapshots[0] * localPosition;
                v += shiftDueToRotation / Time.fixedDeltaTime / (rotationSnapshots.Count - 1);
            }
            return refTransform == null ? v : refTransform.TransformVector(v);
        }

        public Quaternion GetAngularVelocity()
        {
            if (hand)
            {
                Vector3 angularVelocity = hand.GetTrackedObjectAngularVelocity();
                return Quaternion.AngleAxis(angularVelocity.magnitude * 180 / Mathf.PI, angularVelocity);
            }
            if (rotationSnapshots.Count <= 1)
            {
                return Quaternion.identity;
            }
            Quaternion deltaRotation = Quaternion.Inverse(rotationSnapshots[0]) * rotationSnapshots[rotationSnapshots.Count - 1];
            float deltaT = (rotationSnapshots.Count - 1) * Time.fixedDeltaTime;
            Quaternion w = Quaternion.SlerpUnclamped(Quaternion.identity, deltaRotation, 1 / deltaT);
            return refTransform == null ? w : refTransform.rotation * w * Quaternion.Inverse(refTransform.rotation);
        }
            
        public Vector3 GetAcceleration() {
            if (velocitySnapshots.Count <= 1) {
                return Vector3.zero;
            }
            Vector3 a = (velocitySnapshots[velocitySnapshots.Count - 1] - velocitySnapshots[0]) / Time.fixedDeltaTime / (velocitySnapshots.Count - 1);
            return refTransform == null ? a : refTransform.TransformVector(a);
        }

        public Vector3 GetVelocityOfPoint(Vector3 pos)
        {
            // TODO: remove GetVelocityOfPoint()
            return GetVelocity(pos);
        }

        public Vector3 GetAverageVelocityInSnapshots()
        {
            if (velocitySnapshots.Count == 0)
            {
                return Vector3.zero;
            }

            if (cachedAverageVelocityInSnapshots == null)
            {
                Vector3 vSum = Vector3.zero;
                foreach (Vector3 v in velocitySnapshots)
                {
                    vSum += v;
                }
                cachedAverageVelocityInSnapshots = vSum / velocitySnapshots.Count;
            }

            return refTransform == null ? ((Vector3) cachedAverageVelocityInSnapshots) : refTransform.TransformVector((Vector3) cachedAverageVelocityInSnapshots);
        }

        // Returns the farthest locomtion in the past deltaT seconds relative to the current position.
        public Vector3 GetLongestLocomotion(float deltaT)
        {
            Vector3 longestLocomotion = Vector3.zero;
            float longestDist = 0;
            float currentDeltaT = 0;
            for (int i = sparseSnapshots.Count - 1; i >= 0 && currentDeltaT <= deltaT; i--, currentDeltaT += Time.fixedDeltaTime * SPARSE_SNAPSHOT_INTERVAL)
            {
                Vector3 locomotion = snapshots[snapshots.Count - 1] - sparseSnapshots[i];
                float dist = locomotion.magnitude;
                if (dist > longestDist)
                {
                    longestLocomotion = locomotion;
                    longestDist = dist;
                }
            }
            return refTransform == null ? longestLocomotion : refTransform.TransformVector(longestLocomotion);
        }

        private void CreateDebugVelocityLine()
        {
            debugVelocityLine = new GameObject().AddComponent<LineRenderer>();
            debugVelocityLine.useWorldSpace = true;
            debugVelocityLine.widthMultiplier = 0.006f;
            debugVelocityLine.positionCount = 2;
            debugVelocityLine.material.color = new Color(0.9f, 0.33f, 0.31f);
            debugVelocityLine.enabled = false;
        }
    }
}

