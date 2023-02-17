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
        private static readonly Vector3 handAimOffset = new Vector3(0, -0.45f, -0.55f);
        private static readonly Vector3 handAimOffsetInverse = new Vector3(0, -0.15f, -0.85f);
        private const float minDist = 0.16f;
        private const float TOTAL_DIRECTION_LINE_COOL_DOWN = 2;

        public LocalWeaponWield weaponWield { private get; set; }
        public static Vector3 spawnPoint { get; private set; }
        public static Vector3 aimDir { get; private set; }
        public static Vector3 startAim { get; private set; }
        public static bool isThrowing;
        public static bool isAiming { get; private set; }

        private GameObject rotSave;
        private LineRenderer directionLine;
        private SteamVR_Action_Boolean useAction { get { return VHVRConfig.LeftHanded() ? SteamVR_Actions.valheim_UseLeft : SteamVR_Actions.valheim_Use; } }

        private float directionCooldown;
        private float aimingDuration = 0;
        private int tickCounter;
        private PhysicsEstimator handPhysicsEstimator { get { return VHVRConfig.LeftHanded() ? VRPlayer.leftHandPhysicsEstimator : VRPlayer.rightHandPhysicsEstimator; } }

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

            if (!SteamVR_Actions.valheim_Grab.GetState(VRPlayer.dominantHandInputSource) || LocalWeaponWield.isCurrentlyTwoHanded())
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
            if (isAiming)
            {
                aimingDuration += Time.fixedDeltaTime;
            }

            tickCounter++;
            if (tickCounter < 5)
            {
                return;
            }

            tickCounter = 0;
            if (!VHVRConfig.UseSpearDirectionGraphic())
            {
                return;
            }

            if (directionCooldown <= 0 || LocalWeaponWield.isCurrentlyTwoHanded())
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
            var avgDir = handPhysicsEstimator.GetAverageVelocityInSnapshots();
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

                if (VHVRConfig.SpearThrowType() == "Classic")
                {
                    // Since classic throw type calculates spear pointing solely based on hand velocity,
                    // avoid responding to it too much at low velocity so that the spear direction does not flicker.
                    Vector3 defaultDirection =
                        Quaternion.AngleAxis(-30, VRPlayer.dominantHand.transform.right) * (VHVRConfig.SpearInverseWield() ? -LocalWeaponWield.weaponForward : LocalWeaponWield.weaponForward);
                    float aligning = Vector3.Dot(defaultDirection, direction.normalized) * handPhysicsEstimator.GetVelocity().magnitude;
                    Vector3 aligningDirection = aligning > 0 ? direction.normalized : -direction.normalized;
                    Vector3 spearPointing = Vector3.RotateTowards(defaultDirection, aligningDirection, Mathf.Atan(Mathf.Abs(aligning) * 0.5f), Mathf.Infinity);
                    UpdateSpearThrowModel(spearPointing);
                }
                else {
                    UpdateSpearThrowModel(direction.normalized);
                }
                UpdateDirectionLine(
                    VRPlayer.dominantHand.transform.position - direction.normalized,
                    VRPlayer.dominantHand.transform.position + direction.normalized * 50);
            }

            if (!useAction.GetStateUp(VRPlayer.dominantHandInputSource))
            {
                return;
            }

            aimingDuration = 0;

            if (isThrowing)
            {
                ResetSpearOffset();
                startAim = Vector3.zero;
                return;
            }

            spawnPoint = VRPlayer.dominantHand.transform.position;
            var throwing = CalculateThrowAndDistance(direction);
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
            if (!VHVRConfig.UseSpearDirectionGraphic() || LocalWeaponWield.isCurrentlyTwoHanded())
            {
                return;
            }
            List<Vector3> pointList = new List<Vector3>();
            pointList.Add(pos1);
            pointList.Add(pos2);
            directionLine.SetPositions(pointList.ToArray());
            directionLine.enabled = true;
            directionCooldown = TOTAL_DIRECTION_LINE_COOL_DOWN;
        }

        class ThrowCalculate
        {
            public float ThrowSpeed { get; set; }
            public float Distance { get; set; }
            public ThrowCalculate(float throwSpeed, float distance)
            {
                ThrowSpeed = throwSpeed;
                Distance = distance;
            }
        }

        private ThrowCalculate CalculateThrowAndDistance(Vector3 direction)
        {
            var throwSpeed = 1f;
            if (VHVRConfig.SpearThrowSpeedDynamic())
            {
                throwSpeed = Vector3.Dot(handPhysicsEstimator.GetAverageVelocityInSnapshots(), direction.normalized);
                // Apply some non-linear damping otherwise the spear flies too fast even if thrown at low speed.
                throwSpeed *= Mathf.Clamp(throwSpeed  * 0.25f, 0.25f, 0.5f);
            }
            return new ThrowCalculate(throwSpeed, handPhysicsEstimator.GetLongestLocomotion(Mathf.Min(0.4f, aimingDuration)).magnitude);
        }
    }
}
