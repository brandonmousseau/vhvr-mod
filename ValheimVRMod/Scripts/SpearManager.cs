using System.Collections.Generic;
using UnityEngine;
using Valve.VR;

namespace ValheimVRMod.Scripts {
    public class SpearManager : MonoBehaviour {
        private const int MAX_SNAPSHOTS = 7;
        private int tickCounter;
        private List<Vector3> snapshots = new List<Vector3>();
        private GameObject fixedSpear;
        
        public static Vector3 spawnPoint;
        public static Vector3 aimDir;
        public static bool isThrowing;

        private void Awake() {
            fixedSpear = new GameObject();
        }

        private void OnDestroy() {
            Destroy(fixedSpear);
        }

        private void OnRenderObject() {
            fixedSpear.transform.position = transform.position;

            if (!SteamVR_Actions.valheim_Grab.GetStateUp(SteamVR_Input_Sources.RightHand)) {
                return;
            }

            if (isThrowing) {
                return;
            }

            spawnPoint = transform.position;
            var dist = 0.0f;
            Vector3 posEnd = spawnPoint;
            Vector3 posStart = spawnPoint;

            foreach (Vector3 snapshot in snapshots) {
                var curDist = Vector3.Distance(snapshot, posEnd);
                if (curDist > dist) {
                    dist = curDist;
                    posStart = snapshot;
                }
            }

            aimDir = (posEnd - posStart).normalized;
            isThrowing = true;
        }

        private void FixedUpdate() {
            tickCounter++;
            if (tickCounter < 5) {
                return;
            }

            snapshots.Add(fixedSpear.transform.position);

            if (snapshots.Count > MAX_SNAPSHOTS) {
                snapshots.RemoveAt(0);
            }

            tickCounter = 0;
        }
    }
}