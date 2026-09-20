using System.Collections.Generic;
using UnityEngine;
using Valve.VR;
using ValheimVRMod.VRCore;
using ValheimVRMod.VRCore.UI;
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
        // Hand speed along the throw direction in m/s, which WeaponUtils.GetThrowLaunchSpeed() maps to the launch speed.
        public static float handSpeed { get; private set; }
        public static Vector3 startAim { get; private set; }
        public static bool isThrowing;
        public static bool isAiming { get; private set; }
        public static bool preAimingInTwoStagedThrow { get { return VHVRConfig.SpearThrowType() == "TwoStagedThrowing" && !isAiming && SteamVR_Actions.valheim_Grab.GetState(VRPlayer.mainWeaponHandInputSource); } }

        private GameObject rotSave;
        private LineRenderer directionLine;
        private SteamVR_Action_Boolean useAction { get { return VRPlayer.isRightHandMainWeaponHand ? SteamVR_Actions.valheim_Use : SteamVR_Actions.valheim_UseLeft; } }

        private float directionCooldown;
        private float aimingDuration = 0;
        private int tickCounter;
        private PhysicsEstimator handPhysicsEstimator { get { return VRPlayer.isRightHandMainWeaponHand ? VRPlayer.rightHandPhysicsEstimator : VRPlayer.leftHandPhysicsEstimator; } }

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
            EndThrowPreparation();
            Destroy(rotSave);
            Destroy(directionLine, directionCooldown);
        }

        private void OnRenderObject()
        {
            // Letting go of the grip, wielding the weapon with both hands, or handing the controls over to a
            // laser pointer all cancel an ongoing preparation rather than throw. The laser pointer action set
            // masks the Valheim one while it is up, so without cancelling here the trigger would read as
            // released and throw a spear that the player is merely holding while clicking on a GUI.
            if (!SteamVR_Actions.valheim_Grab.GetState(VRPlayer.mainWeaponHandInputSource) ||
                LocalWeaponWield.isCurrentlyTwoHanded() ||
                VRControls.laserControlsActive)
            {
                if (isAiming)
                {
                    EndThrowPreparation();
                }
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
            if (!(VHVRConfig.UseSpearDirectionGraphicOnGrip() || (VHVRConfig.UseSpearDirectionGraphicOnTriggerGrip() && useAction.GetState(VRPlayer.mainWeaponHandInputSource))))
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
            if (VHVRConfig.UseSpearDirectionGraphicOnTriggerGrip() && !useAction.GetState(VRPlayer.mainWeaponHandInputSource))
            {
                ShieldBlock.instance?.AdaptScaleShieldSize(1f);
            }
            else
            {
                ShieldBlock.instance?.AdaptScaleShieldSize(0.4f);
            }
            var direction = VRPlayer.mainWeaponHand.otherHand.transform.position - CameraUtils.getCamera(CameraUtils.VR_CAMERA).transform.position;
            var lineDirection = direction;
            var pStartAim = direction.normalized;
            UpdateThrowCalculation(direction, lineDirection, pStartAim);
        }

        private void UpdateTwoStagedThrowCalculation()
        {
            var vrTransform = VRPlayer.instance.transform;
            var direction = vrTransform.TransformDirection(startAim);
            var lineDirection = VRPlayer.mainWeaponHand.transform.TransformDirection(handAimOffset);
            var pStartAim = vrTransform.InverseTransformDirection(lineDirection.normalized);
            UpdateThrowCalculation(direction, lineDirection, pStartAim);
        }

        private void UpdateDartSpearThrowCalculation()
        {
            var vrTransform = VRPlayer.instance.transform;
            var direction = vrTransform.TransformDirection(
                vrTransform.InverseTransformPoint(VRPlayer.mainWeaponHand.transform.position) - startAim);
            var lineDirection = direction;
            var pStartAim = vrTransform.InverseTransformPoint(VRPlayer.mainWeaponHand.transform.position);
            UpdateThrowCalculation(direction, lineDirection, pStartAim);
        }

        private void UpdateClassicThrowCalculation()
        {
            var avgDir = handPhysicsEstimator.GetAverageVelocityInSnapshots();
            var lineDirection = avgDir;
            var pStartAim = VRPlayer.instance.transform.InverseTransformPoint(VRPlayer.mainWeaponHand.transform.position);
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
                            VRPlayer.mainWeaponHand.transform.position,
                            VRPlayer.mainWeaponHand.transform.position + lineDirection.normalized * 50);
                        break;
                }
            }

            // The preparation phase lasts as long as the trigger is held on top of the grip, and is read from
            // the trigger's current state rather than from its edges: an edge can belong to the laser pointer
            // action set taking the trigger away or handing it back instead of to the player.
            if (useAction.GetState(VRPlayer.mainWeaponHandInputSource))
            {
                if (!isAiming)
                {
                    isAiming = true;
                    startAim = pStartAim;
                    aimingDuration = 0;
                }

                aimDir = direction;
                UpdateDirectionLine(
                    VRPlayer.mainWeaponHand.transform.position - direction.normalized,
                    VRPlayer.mainWeaponHand.transform.position + direction.normalized * 50);
                return;
            }

            if (!isAiming)
            {
                return;
            }

            // The trigger has been released, which ends the preparation whether or not it throws. Ending it
            // before the throw is evaluated is what keeps the throw single: OnRenderObject() runs once per
            // rendering camera, and the later passes of the same frame find no preparation left to release.
            // The aiming duration is measured over the window the player spent aiming, so it has to be read
            // before the preparation ends and resets it.
            float completedAimingDuration = aimingDuration;
            EndThrowPreparation();

            if (isThrowing)
            {
                // The previous throw is still waiting to be handed over to vanilla, see ControlPatches.
                return;
            }

            spawnPoint = VRPlayer.mainWeaponHand.transform.position;
            var throwing = CalculateThrowAndDistance(direction, completedAimingDuration);
            aimDir = direction;
            handSpeed = throwing.HandSpeed;
            if (throwing.Distance <= minDist)
            {
                return;
            }

            if (!MountedAttackUtils.StartAttackIfRiding(isSecondaryAttack: EquipScript.CurrentMainHandEquipType() == EquipType.Spear))
            {
                // Let control patches and vanilla game handle attack if the player is not riding.
                isThrowing = true;
            }

            if (EquipScript.CurrentMainHandEquipType() == EquipType.SpearChitin)
            {
                GetComponentInParent<SpearWield>().HideHarpoon();
            }
        }

        // Ends the throw preparation phase without throwing: whoever ends it decides whether a throw comes out
        // of it.
        private void EndThrowPreparation()
        {
            isAiming = false;
            startAim = Vector3.zero;
            aimingDuration = 0;
            ShieldBlock.instance?.AdaptScaleShieldSize(1f);
        }

        private void UpdateDirectionLine(Vector3 pos1, Vector3 pos2)
        {
            if (!(VHVRConfig.UseSpearDirectionGraphicOnGrip() || (VHVRConfig.UseSpearDirectionGraphicOnTriggerGrip() && useAction.GetState(VRPlayer.mainWeaponHandInputSource))) || LocalWeaponWield.isCurrentlyTwoHanded())
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
            public float HandSpeed { get; set; }
            public float Distance { get; set; }
            public ThrowCalculate(float handSpeed, float distance)
            {
                HandSpeed = handSpeed;
                Distance = distance;
            }
        }

        private ThrowCalculate CalculateThrowAndDistance(Vector3 direction, float completedAimingDuration)
        {
            direction = direction.normalized;
            var handTipOffset =
                (VRPlayer.isRightHandMainWeaponHand ? VRPlayer.rightHandBone.up : VRPlayer.leftHandBone.up) * 0.125f;
            var angularVelocity = handPhysicsEstimator.GetAngularVelocity();

            var speedAlongThrow =
                Mathf.Max(
                    Vector3.Dot(
                        direction, WeaponUtils.GetWeaponVelocity(handPhysicsEstimator.GetVelocity(), angularVelocity, handTipOffset)),
                    Vector3.Dot(
                        direction, WeaponUtils.GetWeaponVelocity(handPhysicsEstimator.GetAverageVelocityInSnapshots(), angularVelocity, handTipOffset)));

            return new ThrowCalculate(
                Mathf.Max(speedAlongThrow, 0),
                handPhysicsEstimator.GetLongestLocomotion(Mathf.Min(0.4f, completedAimingDuration)).magnitude);
        }
    }
}
