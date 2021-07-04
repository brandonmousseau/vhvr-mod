using System.Collections.Generic;
using UnityEngine;
using Valve.VR;
using ValheimVRMod.VRCore;
using UnityEngine.Rendering;
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
        private static bool isThrowingStance;
        private LineRenderer directionLine;
        private float directionCooldown;
        private float totalCooldown = 2;

        private void Awake() {
            fixedSpear = new GameObject();
            rotSave = new GameObject();
            rotSave.transform.SetParent(transform.parent);
            rotSave.transform.position = transform.position;
            rotSave.transform.localRotation = transform.localRotation;

            directionLine = new GameObject().AddComponent<LineRenderer>();
            directionLine.widthMultiplier = 0.03f;
            directionLine.positionCount = 2;
            directionLine.material.color = Color.white;
            directionLine.enabled = false;
            directionLine.receiveShadows = false;
            directionLine.shadowCastingMode = ShadowCastingMode.Off;
            directionLine.lightProbeUsage = LightProbeUsage.Off;
            directionLine.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private void OnDestroy() {
            Destroy(fixedSpear);
            Destroy(rotSave);
            Destroy(directionLine,directionCooldown);
        }

        private void OnRenderObject() {
            fixedSpear.transform.position = transform.position;
            if (SteamVR_Actions.valheim_Grab.GetStateDown(SteamVR_Input_Sources.RightHand)) {
                if (startAim == Vector3.zero) {
                    startAim = Player.m_localPlayer.transform.InverseTransformPoint(VRPlayer.rightHand.transform.position);
                }
                isThrowingStance = true;
            }

            if (isThrowingStance) {
                var inversePosition = Player.m_localPlayer.transform.TransformDirection(Player.m_localPlayer.transform.InverseTransformPoint(VRPlayer.rightHand.transform.position) - startAim).normalized;
                var offsetPos = Vector3.Distance(VRPlayer.rightHand.transform.position, rotSave.transform.position);
                transform.position = VRPlayer.rightHand.transform.position - Vector3.ClampMagnitude(inversePosition, offsetPos);
                transform.LookAt(VRPlayer.rightHand.transform.position + inversePosition);
                transform.localRotation = transform.localRotation * (rotSave.transform.localRotation) * Quaternion.AngleAxis(180, Vector3.right);
                UpdateDirectionLine(inversePosition);
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
            if (!VHVRConfig.UseSpearDirectionGraphic()) {
                return;
            }

            if (directionCooldown <= 0) {
                directionCooldown = 0;
                directionLine.enabled = false;
            }
            else if (!isThrowingStance) {
                directionCooldown -= Time.deltaTime*5;
            }
        }

        private void ResetSpearOffset()
        {
            transform.position = rotSave.transform.position;
            transform.localRotation = rotSave.transform.localRotation;
            isThrowingStance = false;
        }
        private void UpdateDirectionLine(Vector3 inversePosition)
        {
            if (!VHVRConfig.UseSpearDirectionGraphic()) {
                return;
            }
            List<Vector3> pointList = new List<Vector3>();
            pointList.Add(VRPlayer.rightHand.transform.position - inversePosition);
            pointList.Add(VRPlayer.rightHand.transform.position + inversePosition.normalized * 50);
            directionLine.SetPositions(pointList.ToArray());
            directionLine.enabled = VHVRConfig.UseSpearDirectionGraphic();
            directionCooldown = totalCooldown;
        }
    }
}