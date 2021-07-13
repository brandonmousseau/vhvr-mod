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
        private readonly Vector3 handAimOffset = new Vector3(0, -0.45f, -0.55f);

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
            UpdateTwoHandedWield();
            if (VHVRConfig.SpearThrowType() == "DartType") {
                UpdateDartSpearThrowCalculation();
            }
            else if (VHVRConfig.SpearThrowType() == "TwoStagedThrowing") {
                UpdateTwoStagedThrowCalculation();
            }
            else if (VHVRConfig.SpearThrowType() == "SecondHandAiming") {
                UpdateSecondHandAimCalculation();
            }
            else {
                UpdateDartSpearThrowCalculation();
            }
        }

        private void FixedUpdate() {
            tickCounter++;
            if (tickCounter < 5) {
                return;
            }

            snapshots.Add(Player.m_localPlayer.transform.InverseTransformPoint(VRPlayer.rightHand.transform.position));

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
        private void UpdateSecondHandAimCalculation()
        {
            Transform cameraHead = CameraUtils.getCamera(CameraUtils.VR_CAMERA).transform;
            var direction = VRPlayer.leftHand.transform.position - cameraHead.transform.position;
            if (!SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand)) {
                startAim = Vector3.zero;
                ResetSpearOffset();
                return;
            }
            if (!isThrowingStance && !isThrowing) {
                UpdateDirectionLine(
                    VRPlayer.rightHand.transform.position,
                    VRPlayer.rightHand.transform.position + (direction).normalized * 50);
            }
            if (SteamVR_Actions.valheim_Use.GetStateDown(SteamVR_Input_Sources.RightHand)) {
                if (startAim == Vector3.zero) {
                    startAim = direction.normalized;
                }
                isThrowingStance = true;
            }

            if (isThrowingStance) {
                ShieldManager.ScaleShieldSize(0.4f);
                UpdateSpearThrowModel(direction.normalized);
                UpdateDirectionLine(
                    VRPlayer.rightHand.transform.position - direction.normalized,
                    VRPlayer.rightHand.transform.position + direction.normalized * 50);
            }

            if (!SteamVR_Actions.valheim_Use.GetStateUp(SteamVR_Input_Sources.RightHand)) {
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
            var throwing = CalculateThrowAndDistance();
            aimDir = direction.normalized * throwing.ThrowSpeed;

            if (throwing.Distance > minDist) {
                isThrowing = true;
            }

            if (SteamVR_Actions.valheim_Use.GetStateUp(SteamVR_Input_Sources.RightHand) && throwing.Distance <= minDist) {
                startAim = Vector3.zero;
                ResetSpearOffset();
            }
        }
        private void UpdateTwoStagedThrowCalculation()
        {
            if (!SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand)) {
                startAim = Vector3.zero;
                ResetSpearOffset();
                return;
            }
            if (!isThrowingStance&&!isThrowing) {
                UpdateDirectionLine(
                    VRPlayer.rightHand.transform.position,
                    VRPlayer.rightHand.transform.position + (VRPlayer.rightHand.transform.TransformDirection(handAimOffset).normalized * 50));
            }
            if (SteamVR_Actions.valheim_Use.GetStateDown(SteamVR_Input_Sources.RightHand)) {
                if (startAim == Vector3.zero) {
                    startAim = VRPlayer.rightHand.transform.TransformDirection(handAimOffset).normalized;
                }
                isThrowingStance = true;
            }

            if (isThrowingStance) {
                UpdateSpearThrowModel(startAim.normalized);
                UpdateDirectionLine(
                    VRPlayer.rightHand.transform.position - startAim,
                    VRPlayer.rightHand.transform.position + startAim * 50);
            }

            if (!SteamVR_Actions.valheim_Use.GetStateUp(SteamVR_Input_Sources.RightHand)) {
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
            var throwing = CalculateThrowAndDistance();
            aimDir = startAim.normalized * throwing.ThrowSpeed;

            if (throwing.Distance > minDist) {
                isThrowing = true;
            }

            if (SteamVR_Actions.valheim_Use.GetStateUp(SteamVR_Input_Sources.RightHand) && throwing.Distance <= minDist) {
                startAim = Vector3.zero;
                ResetSpearOffset();
            }
        }
        private void UpdateDartSpearThrowCalculation()
        {
            if (!SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand)) {
                startAim = Vector3.zero;
                ResetSpearOffset();
                return;
            }
            if (SteamVR_Actions.valheim_Use.GetStateDown(SteamVR_Input_Sources.RightHand)) {
                if (startAim == Vector3.zero) {
                    startAim = Player.m_localPlayer.transform.InverseTransformPoint(VRPlayer.rightHand.transform.position);
                }
                isThrowingStance = true;
            }

            if (isThrowingStance) {
                var inversePosition = Player.m_localPlayer.transform.TransformDirection(Player.m_localPlayer.transform.InverseTransformPoint(VRPlayer.rightHand.transform.position) - startAim).normalized;
                UpdateSpearThrowModel(inversePosition);
                UpdateDirectionLine(
                    VRPlayer.rightHand.transform.position - inversePosition, 
                    VRPlayer.rightHand.transform.position + inversePosition.normalized * 50);
            }

            if (!SteamVR_Actions.valheim_Use.GetStateUp(SteamVR_Input_Sources.RightHand)) {
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
            var throwing = CalculateThrowAndDistance();
            aimDir = Player.m_localPlayer.transform.TransformDirection(throwing.PosEnd - startAim).normalized * throwing.ThrowSpeed;

            if (throwing.Distance > minDist) {
                isThrowing = true;
            }

            if (SteamVR_Actions.valheim_Use.GetStateUp(SteamVR_Input_Sources.RightHand) && throwing.Distance <= minDist) {
                startAim = Vector3.zero;
                ResetSpearOffset();
            }
        }

        private void UpdateSpearThrowModel(Vector3 inversePosition)
        {
            var offsetPos = Vector3.Distance(VRPlayer.rightHand.transform.position, rotSave.transform.position);
            transform.position = VRPlayer.rightHand.transform.position - Vector3.ClampMagnitude(inversePosition, offsetPos);
            transform.LookAt(VRPlayer.rightHand.transform.position + inversePosition);
            transform.localRotation = transform.localRotation * (rotSave.transform.localRotation) * Quaternion.AngleAxis(180, Vector3.right);
        }
        private void UpdateTwoHandedWield()
        {
            if (VHVRConfig.SpearTwoHanded()) {
                if (SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand)) {
                    var offsetPos = Vector3.Distance(VRPlayer.rightHand.transform.position, rotSave.transform.position);
                    var handDist = Vector3.Distance(VRPlayer.rightHand.transform.position, VRPlayer.leftHand.transform.position);
                    var inversePosition = VRPlayer.rightHand.transform.position - VRPlayer.leftHand.transform.position;
                    var calculateSpearDistance = (inversePosition.normalized * 0.08f / handDist)-inversePosition.normalized*0.1f;
                    transform.position = VRPlayer.rightHand.transform.position - Vector3.ClampMagnitude(inversePosition.normalized, offsetPos) + (calculateSpearDistance);
                    LogUtils.LogDebug("handDist = " + handDist + " - math : "+ calculateSpearDistance);
                    transform.LookAt(VRPlayer.rightHand.transform.position + inversePosition.normalized*5);
                    transform.localRotation = transform.localRotation * (rotSave.transform.localRotation) * Quaternion.AngleAxis(180, Vector3.right);
                    return;
                }
                else if (SteamVR_Actions.valheim_Grab.GetStateUp(SteamVR_Input_Sources.LeftHand)) {
                    ResetSpearOffset();
                }
            }
        }
        private void ResetSpearOffset()
        {
            transform.position = rotSave.transform.position;
            transform.localRotation = rotSave.transform.localRotation;
            isThrowingStance = false;
            ShieldManager.ScaleShieldSize(1f);
        }
        private void UpdateDirectionLine(Vector3 pos1 ,Vector3 pos2)
        {
            if (!VHVRConfig.UseSpearDirectionGraphic()) {
                return;
            }
            List<Vector3> pointList = new List<Vector3>();
            pointList.Add(pos1);
            pointList.Add(pos2);
            directionLine.SetPositions(pointList.ToArray());
            directionLine.enabled = true;
            directionCooldown = totalCooldown;
        }

        class ThrowCalculate
        {
            public Vector3 PosStart { get; set; }
            public Vector3 PosEnd { get; set; }
            public float ThrowSpeed { get; set; }
            public float Distance { get; set; }
            public ThrowCalculate(Vector3 posStart,Vector3 posEnd,float throwSpeed)
            {
                PosStart = posStart;
                PosEnd = posEnd;
                ThrowSpeed = throwSpeed;
                Distance = Vector3.Distance(posEnd, posStart);
            }
        }
        private ThrowCalculate CalculateThrowAndDistance()
        {
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

            var throwSpeed = 1f;
            if (VHVRConfig.SpearThrowSpeedDynamic()) {
                var speedModifier = slowThrowModifier;
                if (dist > fastThrowMinDist) {
                    speedModifier = fastThrowModifier;
                }
                else if (dist > mediumThrowMinDist) {
                    speedModifier = mediumThrowModifier;
                }
                throwSpeed = Vector3.Distance(posEnd, posStart) * speedModifier;
            }
            return new ThrowCalculate(posStart, posEnd, throwSpeed);
        }
    }
}