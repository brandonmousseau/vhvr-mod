using System.Collections.Generic;
using UnityEngine;
using Valve.VR;
using ValheimVRMod.VRCore;

using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts {
    public class SpearManager : MonoBehaviour {
        private const int MAX_SNAPSHOTS = 7;
        private const int MIN_SNAPSHOTSCHECK = 3;
        private int tickCounter;
        private List<Vector3> snapshots = new List<Vector3>();
        private GameObject fixedSpear;

        private const float minDist = 0.16f;
        private const float slowThrowModifier = 1.5f;
        private const float mediumThrowModifier = 2f;
        private const float fastThrowModifier = 2.5f;
        private const float mediumThrowMinDist = 0.65f;
        private const float fastThrowMinDist = 0.9f;

        public static Vector3 spawnPoint;
        public static Vector3 aimDir;
        public static Vector3 startAim; 
        public static bool isThrowing;
        private GameObject rotSave;
        private static bool isPressed;

        private void Awake() {
            fixedSpear = new GameObject();
            rotSave = new GameObject();
            rotSave.transform.SetParent(transform.parent);
            rotSave.transform.position = transform.position;
            rotSave.transform.localRotation = transform.localRotation;
        }

        private void OnDestroy() {
            Destroy(fixedSpear);
            Destroy(rotSave);
        }

        private void OnRenderObject() {
            fixedSpear.transform.position = transform.position;
            if (SteamVR_Actions.valheim_Grab.GetStateDown(SteamVR_Input_Sources.RightHand)) {
                if (startAim == Vector3.zero) {
                    startAim = Player.m_localPlayer.transform.InverseTransformPoint(VRPlayer.rightHand.transform.position);
                }
                isPressed = true;
            }

            if (isPressed) {
                transform.position = VRPlayer.rightHand.transform.position;
                transform.LookAt(VRPlayer.rightHand.transform.position + Player.m_localPlayer.transform.TransformDirection(Player.m_localPlayer.transform.InverseTransformPoint(VRPlayer.rightHand.transform.position) - startAim).normalized);
                if (EquipScript.getRight() == EquipType.SpearChitin) {
                    transform.rotation = transform.rotation * Quaternion.AngleAxis(90, Vector3.right);
                }
            }

            if (!SteamVR_Actions.valheim_Grab.GetStateUp(SteamVR_Input_Sources.RightHand)) {
                return;
            }

            if (snapshots.Count < MIN_SNAPSHOTSCHECK) {
                return;
            }

            if (isThrowing) {
                ResetSpearOffset();
                startAim = Vector3.zero;
                return;
            }

            spawnPoint = VRPlayer.rightHand.transform.position;
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

            var speedModifier = slowThrowModifier;
            if (dist > fastThrowMinDist) {
                speedModifier = fastThrowModifier;
            }else if (dist > mediumThrowMinDist) {
                speedModifier = mediumThrowModifier;
            }

            aimDir = Player.m_localPlayer.transform.TransformDirection(posEnd - startAim).normalized*Vector3.Distance(posEnd, posStart)* speedModifier;

            if (Vector3.Distance(posEnd,posStart) > minDist && VRPlayer.justUnsheathed == false) {
                isThrowing = true;
            }
            
            if (SteamVR_Actions.valheim_Grab.GetStateUp(SteamVR_Input_Sources.RightHand)&& Vector3.Distance(posEnd, posStart) <= minDist) {
                startAim = Vector3.zero;
                ResetSpearOffset();
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

        private void ResetSpearOffset()
        {
            transform.position = rotSave.transform.position;
            transform.localRotation = rotSave.transform.localRotation;
            isPressed = false;
        }
    }
}