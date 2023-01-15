using System.Collections.Generic;
using UnityEngine;

namespace ValheimVRMod.Utilities
{
    // Utility component for estimating the physics of the game object such as its velocity.
    public class PhysicsEstimator : MonoBehaviour
    {
        private const int MAX_SNAPSHOTS = 8;

        private List<Vector3> snapshots = new List<Vector3>();
        private List<Quaternion> rotationSnapshots = new List<Quaternion>();
        private List<Vector3> velocitySnapshots = new List<Vector3>();
        private LineRenderer debugVelocityLine;

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
                _refTransform = value;
            }
        }

        public bool renderDebugVelocityLine = false;

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

        public Vector3 GetVelocity()
        {
            // TODO: migrate the calculation of swinging, stabbbing, throwing, and parrying to use this class.
            if (velocitySnapshots.Count == 0)
            {
                return Vector3.zero;
            }
            return refTransform == null ? velocitySnapshots[0] : refTransform.TransformVector(velocitySnapshots[0]);
        }

        public Vector3 GetAverageVelocityInSnapshots()
        {
            if (velocitySnapshots.Count == 0)
            {
                return Vector3.zero;
            }

            Vector3 vSum = Vector3.zero;
            foreach (Vector3 v in velocitySnapshots)
            {
                vSum += v;
            }
            Vector3 vAverage = vSum / velocitySnapshots.Count;

            return refTransform == null ? vAverage : refTransform.TransformVector(vAverage);
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
