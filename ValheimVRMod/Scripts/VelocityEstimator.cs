using System.Collections.Generic;
using UnityEngine;

namespace ValheimVRMod.Scripts
{
    public class VelocityEstimator : MonoBehaviour
    {
        private const int MAX_SNAPSHOTS_BASE = 20;

        private List<Vector3> snapshots = new List<Vector3>();
        private List<Quaternion> rotationSnapshots = new List<Quaternion>();
        public List<Vector3> velocitySnapshots = new List<Vector3>();

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

        private void Awake()
        {
            if (refTransform == null)
            {
                refTransform = transform.parent;
            }
        }

        private void FixedUpdate()
        {
            if (refTransform == null)
            {
                refTransform = transform.parent;
            }
            snapshots.Add(refTransform.InverseTransformPoint(transform.position));
            rotationSnapshots.Add(Quaternion.Inverse(transform.rotation) * transform.rotation);
            if (snapshots.Count >= 2) {
                // TODO: consider using least square fit or a smoonthening function over all snapshots, but should balance with performance too.
                velocitySnapshots.Add((snapshots[snapshots.Count - 1] - snapshots[0]) / Time.fixedDeltaTime / (snapshots.Count - 1));
            }
            if (snapshots.Count > MAX_SNAPSHOTS_BASE)
            {
                snapshots.RemoveAt(0);
            }
            if (rotationSnapshots.Count > MAX_SNAPSHOTS_BASE)
            {
                rotationSnapshots.RemoveAt(0);
            }
            if (velocitySnapshots.Count > MAX_SNAPSHOTS_BASE)
            {
                velocitySnapshots.RemoveAt(0);
            }
        }

        public Vector3 GetVelocity()
        {
            return velocitySnapshots.Count > 0 ? refTransform.TransformVector(velocitySnapshots[0]) : new Vector3(0, 0, 0);
        }
    }
}
