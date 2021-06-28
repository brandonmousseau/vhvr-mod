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
        public static Vector3 startAim; 
        public static bool isThrowing;

        private void Awake() {
            fixedSpear = new GameObject();
        }

        private void OnDestroy() {
            Destroy(fixedSpear);
        }

        private void OnRenderObject() {
            fixedSpear.transform.position = transform.position;

            if (SteamVR_Actions.valheim_Grab.GetStateDown(SteamVR_Input_Sources.RightHand)) {
                if (startAim == Vector3.zero) {
                    startAim = Player.m_localPlayer.transform.InverseTransformPoint(VRPlayer.rightHand.transform.position);
                }
            }

            if (!SteamVR_Actions.valheim_Grab.GetStateUp(SteamVR_Input_Sources.RightHand)) {
                return;
            }

            if (snapshots.Count < 3) {
                return;
            }

            if (isThrowing) {
                startAim = Vector3.zero;
                return;
            }

            spawnPoint = transform.position;
            var dist = 0.0f;
            Vector3 posEnd = Player.m_localPlayer.transform.InverseTransformPoint(VRPlayer.rightHand.transform.position);
            Vector3 posStart = Player.m_localPlayer.transform.InverseTransformPoint(VRPlayer.rightHand.transform.position);

            foreach (Vector3 snapshot in snapshots) {
                var curDist = Vector3.Distance(snapshot, posEnd);
                if (curDist > dist) {
                    dist = curDist;
                    posStart = snapshot;
                }
            }

            var speedModifier = 1.5f;
            if (dist > 0.65f) {
                speedModifier = 2f;
            }else if (dist > 0.9f) {
                speedModifier = 2.5f;
            }

            aimDir = Player.m_localPlayer.transform.TransformDirection(posEnd - startAim).normalized*Vector3.Distance(posEnd, posStart)* speedModifier;

            if (Vector3.Distance(posEnd,posStart) > 0.16f&& VRPlayer.justUnsheathed == false) {
                isThrowing = true;
            }

            if (SteamVR_Actions.valheim_Grab.GetStateUp(SteamVR_Input_Sources.RightHand)&& Vector3.Distance(posEnd, posStart) < 0.16f ) {
                startAim = Vector3.zero;
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
                snapshots.Add(Player.m_localPlayer.transform.InverseTransformPoint(VRPlayer.rightHand.transform.position));
            }

            if (snapshots.Count > MAX_SNAPSHOTS) {
                snapshots.RemoveAt(0);
            }

            tickCounter = 0;
        }
    }
}