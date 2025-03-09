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
        private static readonly Vector3 handAimOffset = new Vector3(0, -0.15f, -0.85f);
        private const float minDist = 0.0625f;
        private const float TOTAL_DIRECTION_LINE_COOL_DOWN = 2;

        public LocalWeaponWield weaponWield { private get; set; }
        public static Vector3 spawnPoint { get; private set; }
        public static Vector3 aimDir { get; private set; }
        public static float throwSpeed { get; private set; }
        public static Vector3 startAim { get; private set; }
        public static bool isThrowing;
        public static bool isAiming { get; private set; }
        public static bool preAimingInTwoStagedThrow { get { return VHVRConfig.SpearThrowType() == "TwoStagedThrowing" && !isAiming && SteamVR_Actions.valheim_Grab.GetState(VRPlayer.dominantHandInputSource); } }

        private GameObject rotSave;
        private LineRenderer directionLine;
        private SteamVR_Action_Boolean useAction { get { return VHVRConfig.LeftHanded() ? SteamVR_Actions.valheim_UseLeft : SteamVR_Actions.valheim_Use; } }

        private float directionCooldown;
        private float aimingDuration = 0;
        private int tickCounter;
        private PhysicsEstimator handPhysicsEstimator { get { return VHVRConfig.LeftHanded() ? VRPlayer.leftHandPhysicsEstimator : VRPlayer.rightHandPhysicsEstimator; } }

        private void Awake()
        {
            directionLine = new GameObject().AddComponent<LineRenderer>();
            directionLine.widthMultiplier = 0.03f;
            directionLine.positionCount = 2;
            directionLine.material = Instantiate(VRAssetManager.GetAsset<Material>("Unlit"));
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

            if (directionLine.enabled)
            {
                directionLine.material.color =
                    isAiming ? Color.white : new Color(1, 1, 1, directionCooldown / TOTAL_DIRECTION_LINE_COOL_DOWN);
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
            var vrTransform = VRPlayer.instance.transform;
            var direction = vrTransform.TransformDirection(startAim);
            var lineDirection = VRPlayer.dominantHand.transform.TransformDirection(handAimOffset);
            var pStartAim = vrTransform.InverseTransformDirection(lineDirection.normalized);
            UpdateThrowCalculation(direction, lineDirection, pStartAim);
        }

        private void UpdateDartSpearThrowCalculation()
        {
            var vrTransform = VRPlayer.instance.transform;
            var direction = vrTransform.TransformDirection(
                vrTransform.InverseTransformPoint(VRPlayer.dominantHand.transform.position) - startAim);
            var lineDirection = direction;
            var pStartAim = vrTransform.InverseTransformPoint(VRPlayer.dominantHand.transform.position);
            UpdateThrowCalculation(direction, lineDirection, pStartAim);
        }

        private void UpdateClassicThrowCalculation()
        {
            var avgDir = handPhysicsEstimator.GetAverageVelocityInSnapshots();
            var lineDirection = avgDir;
            var pStartAim = VRPlayer.instance.transform.InverseTransformPoint(VRPlayer.dominantHand.transform.position);
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
                aimDir = direction;
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
            aimDir = direction;
            throwSpeed = throwing.ThrowSpeed;
            if (throwing.Distance > minDist)
            {
                throwSpeed = throwing.ThrowSpeed;
                if (MountedAttackUtils.StartAttackIfRiding(isSecondaryAttack: EquipScript.getRight() == EquipType.Spear))
                {
                    ResetSpearOffset();
                }
                else 
                {
                    // Let control patches and vanilla game handle attack if the player is not riding.
                    isThrowing = true;
                }
                if (EquipScript.getRight() == EquipType.SpearChitin)
                {
                    GetComponentInParent<SpearWield>().HideHarpoon();
                }
            }

            if (useAction.GetStateUp(VRPlayer.dominantHandInputSource) && throwing.Distance <= minDist)
            {
                startAim = Vector3.zero;
                ResetSpearOffset();
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
            direction = direction.normalized;
            var handTipOffset = (VHVRConfig.LeftHanded() ? VRPlayer.leftHandBone.up : VRPlayer.rightHandBone.up) * 0.125f;
            var angularVelocity = handPhysicsEstimator.GetAngularVelocity();

            var throwSpeed =
                Mathf.Max(
                    Vector3.Dot(
                        direction, WeaponUtils.GetWeaponVelocity(handPhysicsEstimator.GetVelocity(), angularVelocity, handTipOffset)),
                    Vector3.Dot(
                        direction, WeaponUtils.GetWeaponVelocity(handPhysicsEstimator.GetAverageVelocityInSnapshots(), angularVelocity, handTipOffset)));

            if (throwSpeed < VHVRConfig.FullThrowSpeed())
            {
                throwSpeed /= VHVRConfig.FullThrowSpeed();
            }
            else
            {
                var normalizer = Mathf.Max(VHVRConfig.FullThrowSpeed(), 2);
                throwSpeed = throwSpeed > normalizer ? throwSpeed / normalizer : 1;
            }

            return new ThrowCalculate(throwSpeed, handPhysicsEstimator.GetLongestLocomotion(Mathf.Min(0.4f, aimingDuration)).magnitude);
        }
    }
}
