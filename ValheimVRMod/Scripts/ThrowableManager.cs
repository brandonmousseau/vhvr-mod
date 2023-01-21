using System.Collections.Generic;
using UnityEngine;
using Valve.VR;
using ValheimVRMod.VRCore;
using UnityEngine.Rendering;
using ValheimVRMod.Scripts.Block;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts
{
    public class ThrowableManager : MonoBehaviour
    {
        private const int MAX_SNAPSHOTS = 7;
        private const int MIN_SNAPSHOTSCHECK = 3;
        private static readonly Vector3 handAimOffset = new Vector3(0, -0.45f, -0.55f);
        private static readonly Vector3 handAimOffsetInverse = new Vector3(0, -0.15f, -0.85f);
        private const float minDist = 0.16f;
        private const float slowThrowModifier = 1.5f;
        private const float mediumThrowModifier = 2f;
        private const float fastThrowModifier = 2.5f;
        private const float mediumThrowMinDist = 0.65f;
        private const float fastThrowMinDist = 0.9f;
        private const float totalCooldown = 2;

        public WeaponWield weaponWield { private get; set; }



        public static Vector3 spawnPoint { get; private set; }
        public static Vector3 aimDir { get; private set; }
        public static Vector3 startAim { get; private set; }
        public static bool isThrowing;
        public static bool isAiming { get; private set; }

        private GameObject rotSave;
        private LineRenderer directionLine;
        private SteamVR_Action_Boolean useAction { get { return VHVRConfig.LeftHanded() ? SteamVR_Actions.valheim_UseLeft : SteamVR_Actions.valheim_Use; } }

        private float directionCooldown;
        private int tickCounter;
        private List<Vector3> snapshots = new List<Vector3>();
        private List<Vector3> throwDirSnapshot = new List<Vector3>();

        private void Awake()
        {
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

        private void OnDestroy()
        {
            ResetSpearOffset();
            Destroy(rotSave);
            Destroy(directionLine, directionCooldown);
        }

        private void OnRenderObject()
        {
            if (SteamVR_Actions.valheim_Grab.GetStateUp(VRPlayer.dominantHandInputSource))
            {
                startAim = Vector3.zero;
                ResetSpearOffset();
                return;
            }

            if (!SteamVR_Actions.valheim_Grab.GetState(VRPlayer.dominantHandInputSource) || WeaponWield.isCurrentlyTwoHanded())
            {
                return;
            }

            switch (VHVRConfig.SpearThrowType())
            {
                case "DartType":
                    UpdateDartSpearThrowCalculation();
                    return;
                case "TwoStagedThrowing":
                    UpdateTwoStagedThrowCalculation();
                    return;
                case "SecondHandAiming":
                    UpdateSecondHandAimCalculation();
                    return;
                case "Classic":
                    UpdateClassicThrowCalculation();
                    return;
                default:
                    Debug.LogError("Wrong SpearThrowType");
                    return;
            }
        }

        private void FixedUpdate()
        {
            tickCounter++;
            if (tickCounter < 5)
            {
                return;
            }

            snapshots.Add(Player.m_localPlayer.transform.InverseTransformPoint(VRPlayer.dominantHand.transform.position));

            if (snapshots.Count > MAX_SNAPSHOTS)
            {
                snapshots.RemoveAt(0);
            }

            tickCounter = 0;
            if (!VHVRConfig.UseSpearDirectionGraphic())
            {
                return;
            }

            if (directionCooldown <= 0 || weaponWield.allowBlocking())
            {
                directionCooldown = 0;
                directionLine.enabled = false;
            }
            else if (!isAiming)
            {
                directionCooldown -= Time.unscaledDeltaTime * 5;
            }
        }
        private void UpdateSecondHandAimCalculation()
        {

            ShieldBlock.instance?.ScaleShieldSize(0.4f);
            var direction = VRPlayer.dominantHand.otherHand.transform.position - CameraUtils.getCamera(CameraUtils.VR_CAMERA).transform.position;
            var lineDirection = direction;
            var pStartAim = direction.normalized;
            UpdateThrowCalculation(direction, lineDirection, pStartAim);
        }

        private void UpdateTwoStagedThrowCalculation()
        {

            var direction = startAim;
            var lineDirection = VRPlayer.dominantHand.transform.TransformDirection(VHVRConfig.SpearInverseWield() ? handAimOffsetInverse : handAimOffset);
            var pStartAim = lineDirection.normalized;
            UpdateThrowCalculation(direction, lineDirection, pStartAim);
        }
        private void UpdateDartSpearThrowCalculation()
        {

            var direction = Player.m_localPlayer.transform.TransformDirection(Player.m_localPlayer.transform.InverseTransformPoint(VRPlayer.dominantHand.transform.position) - startAim);
            var lineDirection = direction;
            var pStartAim = Player.m_localPlayer.transform.InverseTransformPoint(VRPlayer.dominantHand.transform.position);
            UpdateThrowCalculation(direction, lineDirection, pStartAim);
        }

        private void UpdateClassicThrowCalculation()
        {
            var dist = 0.0f;
            Vector3 posStart = VRPlayer.dominantHand.transform.position;
            Vector3 posEnd = VRPlayer.dominantHand.transform.position;

            foreach (Vector3 snapshot in snapshots)
            {
                var curDist = Vector3.Distance(snapshot, posEnd);
                if (curDist > dist)
                {
                    dist = curDist;
                    posStart = Player.m_localPlayer.transform.TransformPoint(snapshot);
                }
            }
            var direction = posEnd - posStart;
            if (throwDirSnapshot.Count == 0 || direction != throwDirSnapshot[throwDirSnapshot.Count - 1])
            {
                throwDirSnapshot.Add(direction);
            }
            var avgDir = Vector3.zero;
            var totalCount = 0;
            for (var i = 0; i < throwDirSnapshot.Count; i++)
            {
                if (Vector3.Angle(throwDirSnapshot[i], direction) < 5)
                {
                    avgDir += throwDirSnapshot[i];
                    totalCount++;
                }
                else if (Vector3.Angle(throwDirSnapshot[i], direction) > 40)
                {
                    throwDirSnapshot.RemoveAt(i);
                }
            }
            if (throwDirSnapshot.Count > 8)
            {
                throwDirSnapshot.RemoveAt(0);
            }
            avgDir = avgDir / (totalCount);


            var lineDirection = avgDir;
            var pStartAim = Player.m_localPlayer.transform.InverseTransformPoint(VRPlayer.dominantHand.transform.position);
            UpdateThrowCalculation(avgDir, lineDirection, pStartAim);
        }

        private void UpdateThrowCalculation(Vector3 direction, Vector3 lineDirection, Vector3 pStartAim)
        {
            if (!isAiming && !isThrowing)
            {
                switch (VHVRConfig.SpearThrowType())
                {
                    case "DartType":
                    case "Classic":
                        break;
                    default:
                        UpdateDirectionLine(
                            VRPlayer.dominantHand.transform.position,
                            VRPlayer.dominantHand.transform.position + lineDirection.normalized * 50);
                        break;
                }
            }

            if (useAction.GetStateDown(VRPlayer.dominantHandInputSource))
            {
                if (startAim == Vector3.zero)
                {
                    startAim = pStartAim;
                }

                isAiming = true;
            }

            if (isAiming)
            {
                UpdateSpearThrowModel(direction.normalized);
                UpdateDirectionLine(
                    VRPlayer.dominantHand.transform.position - direction.normalized,
                    VRPlayer.dominantHand.transform.position + direction.normalized * 50);
            }

            if (!useAction.GetStateUp(VRPlayer.dominantHandInputSource))
            {
                return;
            }

            if (snapshots.Count < MIN_SNAPSHOTSCHECK)
            {
                return;
            }

            if (isThrowing)
            {
                ResetSpearOffset();
                startAim = Vector3.zero;
                return;
            }

            spawnPoint = VRPlayer.dominantHand.transform.position;
            var throwing = CalculateThrowAndDistance();
            aimDir = direction.normalized * throwing.ThrowSpeed;

            if (throwing.Distance > minDist)
            {
                isThrowing = true;
            }

            if (useAction.GetStateUp(VRPlayer.dominantHandInputSource) && throwing.Distance <= minDist)
            {
                startAim = Vector3.zero;
                ResetSpearOffset();
            }
        }

        private void UpdateSpearThrowModel(Vector3 inversePosition)
        {
            if (!EquipScript.isSpearEquipped())
            {
                return;
            }

            var offsetPos = Vector3.Distance(VRPlayer.dominantHand.transform.position, rotSave.transform.position);
            transform.position = VRPlayer.dominantHand.transform.position - Vector3.ClampMagnitude(inversePosition, offsetPos);
            transform.LookAt(VRPlayer.dominantHand.transform.position + inversePosition);
            transform.localRotation *= rotSave.transform.localRotation;

            if (!VHVRConfig.SpearInverseWield())
            {
                transform.localRotation *= Quaternion.AngleAxis(180, Vector3.right);
            }
        }

        private void ResetSpearOffset()
        {
            isAiming = false;
            ShieldBlock.instance?.ScaleShieldSize(1f);

            if (!EquipScript.isSpearEquipped())
            {
                return;
            }

            transform.position = rotSave.transform.position;
            transform.localRotation = rotSave.transform.localRotation;
        }

        private void UpdateDirectionLine(Vector3 pos1, Vector3 pos2)
        {
            if (!VHVRConfig.UseSpearDirectionGraphic() || weaponWield.allowBlocking())
            {
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
            public ThrowCalculate(Vector3 posStart, Vector3 posEnd, float throwSpeed)
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
            Vector3 posStart = Player.m_localPlayer.transform.InverseTransformPoint(VRPlayer.dominantHand.transform.position);
            Vector3 posEnd = posStart;

            foreach (Vector3 snapshot in snapshots)
            {
                var curDist = Vector3.Distance(snapshot, posEnd);
                if (curDist > dist)
                {
                    dist = curDist;
                    posStart = snapshot;
                }
            }

            var throwSpeed = 1f;
            if (VHVRConfig.SpearThrowSpeedDynamic())
            {
                var speedModifier = slowThrowModifier;
                if (dist > fastThrowMinDist)
                {
                    speedModifier = fastThrowModifier;
                }
                else if (dist > mediumThrowMinDist)
                {
                    speedModifier = mediumThrowModifier;
                }
                throwSpeed = Vector3.Distance(posEnd, posStart) * speedModifier;
            }
            return new ThrowCalculate(posStart, posEnd, throwSpeed);
        }
    }
}
