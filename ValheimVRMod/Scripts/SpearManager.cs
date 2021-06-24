using System.Collections.Generic;
using UnityEngine;
using Valve.VR;
using ValheimVRMod.VRCore;

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

            if (snapshots.Count < 3) {
                return;
            }

            if (isThrowing) {
                return;
            }

            
            Vector3 posEnd = snapshots[snapshots.Count-1];
            Vector3 posStart = snapshots[0];
            spawnPoint = transform.position;

            aimDir = (posEnd - posStart)*2;


            if (Vector3.Distance(posEnd,posStart) > 0.16f&& VRPlayer.justUnsheathed == false) {
                isThrowing = true;
            }
        }
        private void FixedUpdate() {
            tickCounter++;
            if (tickCounter < 5) {
                return;
            }

            if (VRPlayer.justUnsheathed == true) {
                snapshots.Clear();
            }
            else {
                snapshots.Add(fixedSpear.transform.localPosition - Player.m_localPlayer.transform.position);
            }
            

            if (snapshots.Count > MAX_SNAPSHOTS) {
                snapshots.RemoveAt(0);
            }

            tickCounter = 0;
        }
    }
}