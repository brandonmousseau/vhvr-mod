using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts {
    public class FishingManager : MonoBehaviour {
        private const int MAX_SNAPSHOTS = 7;
        private const int MIN_SNAPSHOTSCHECK = 3;
        private const float MIN_DISTANCE = 0.2f;
        private static float maxDist = 1.0f;
        private Transform rodTop;
        private int tickCounter;
        private List<Vector3> snapshots = new List<Vector3>();
        private bool preparingThrow;
        private FishingFloat fishingFloat;

        public static float attackDrawPercentage;
        public static Vector3 spawnPoint;
        public static Vector3 aimDir;
        public static bool isThrowing;
        public static bool isFishing;
        public static bool isPulling;
        public static GameObject fixedRodTop;

        private void Awake() {
            rodTop = transform.parent.Find("_RodTop");
            fixedRodTop = new GameObject();
        }

        private void OnDestroy() {
            Destroy(fixedRodTop);
        }

        private void Update() {
            foreach (FishingFloat instance in FishingFloat.GetAllInstances()) {
                if (instance.GetOwner() == Player.m_localPlayer) {
                    fishingFloat = instance;
                    isFishing = true;
                    return;
                }
            }

            isFishing = false;
        }

        private void OnRenderObject() {

            var inputSource = VHVRConfig.LeftHanded()
                ? SteamVR_Input_Sources.LeftHand
                : SteamVR_Input_Sources.RightHand;
            
            fixedRodTop.transform.position = rodTop.position;
            isPulling = isFishing && SteamVR_Actions.valheim_Grab.GetState(inputSource);
            if (!isFishing && SteamVR_Actions.valheim_Grab.GetStateDown(inputSource)) {
                preparingThrow = true;
            }

            if (!SteamVR_Actions.valheim_Grab.GetStateUp(inputSource)) {
                return;
            }

            if (isFishing || isThrowing || !preparingThrow) {
                return;
            }

            if (snapshots.Count < MIN_SNAPSHOTSCHECK) {
                return;
            }

            spawnPoint = rodTop.position;
            var dist = 0.0f;
            Vector3 posEnd = fixedRodTop.transform.position;
            Vector3 posStart = fixedRodTop.transform.position;

            foreach (Vector3 snapshot in snapshots) {
                var curDist = Vector3.Distance(snapshot, posEnd);
                if (curDist > dist) {
                    dist = curDist;
                    posStart = snapshot;
                }
            }

            aimDir = (posEnd - posStart).normalized;
            aimDir = Quaternion.AngleAxis(-30, Vector3.Cross(Vector3.up, aimDir)) * aimDir;
            attackDrawPercentage = Vector3.Distance(snapshots[snapshots.Count - 1], snapshots[snapshots.Count - 2]) /
                                   maxDist;

            if (Vector3.Distance(posEnd, posStart)> MIN_DISTANCE) {
                isThrowing = true;
                preparingThrow = false;
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
                snapshots.Add(fixedRodTop.transform.position);
            }
            

            if (snapshots.Count > MAX_SNAPSHOTS) {
                snapshots.RemoveAt(0);
            }

            tickCounter = 0;

            var inputSource = VHVRConfig.LeftHanded()
                ? SteamVR_Input_Sources.LeftHand
                : SteamVR_Input_Sources.RightHand;
            
            if (isFishing && fishingFloat.GetCatch()  && (int) (Time.fixedTime * 10) % 2 >= 1) {
                VRPlayer.rightHand.hapticAction.Execute(0, 0.001f, 150, 0.7f, inputSource);
            }
        }
    }
}