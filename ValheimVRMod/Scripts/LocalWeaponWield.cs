using UnityEngine;
using UnityEngine.Rendering;
using ValheimVRMod.Scripts.Block;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using ValheimVRMod.VRCore.UI;
using Valve.VR;
using Valve.VR.InteractionSystem;

namespace ValheimVRMod.Scripts
{
    // Manages weapon wield of the local player.
    public class LocalWeaponWield : WeaponWield
    {
        public static Vector3 weaponForward;
        public static TwoHandedState LocalPlayerTwoHandedState { get; private set; }
        public static bool IsDominantHandBehind { get { return isCurrentlyTwoHanded() && (LocalPlayerTwoHandedState == TwoHandedState.RightHandBehind ^ VHVRConfig.LeftHanded()); } }
        public static bool isAiming { get; private set; }  
        public static Vector3 localWeaponTip { get; private set; }
        public static bool CurrentTwoHandedWieldStartedWithLongGrip { get; private set; }
        public static bool IsWeaponPointingUlnar { get; private set; }
        public static bool IsDominantHandHoldInversed { get; private set; }

        protected bool isRedDotVisible { set { redDotRenderer.enabled = value; } }

        private const float RED_DOT_DISTANCE = 256;
        private const float RED_DOT_SIZE_RADIANS = 1f / 256f;
        private static Material RedDotMaterial = null;
        private MeshRenderer redDotRenderer; // Red dot for aiming
        private bool preparingToUnstickTwoHandedWield = false;
        private bool rotatingHandConnectors;

        public Hand mainHand {
            get {
                switch (twoHandedState)
                {
                    case TwoHandedState.RightHandBehind:
                        return VRPlayer.rightHand;
                    case TwoHandedState.LeftHandBehind:
                        return VRPlayer.leftHand;
                    default:
                        return VRPlayer.dominantHand;
                }

            }
        }

        public PhysicsEstimator physicsEstimator { get; private set; }
        private Transform frontHandConnector { get { return twoHandedState == TwoHandedState.LeftHandBehind ? VrikCreator.localPlayerRightHandConnector : VrikCreator.localPlayerLeftHandConnector; } }
        private Transform rearHandConnector { get { return twoHandedState == TwoHandedState.LeftHandBehind ? VrikCreator.localPlayerLeftHandConnector : VrikCreator.localPlayerRightHandConnector; } }
        private Transform lastRenderedTransform;

        private float shieldSize = 1f;

        protected virtual void Awake()
        {
            IsWeaponPointingUlnar = EquipScript.isSpearEquipped() && !VHVRConfig.SpearInverseWield();
            lastRenderedTransform = new GameObject().transform;
            physicsEstimator = lastRenderedTransform.gameObject.AddComponent<PhysicsEstimator>();
            physicsEstimator.refTransform = CameraUtils.getCamera(CameraUtils.VR_CAMERA)?.transform.parent;
        }

        protected override void OnDestroy()
        {
            VrikCreator.ResetHandConnectors();
            LocalPlayerTwoHandedState = TwoHandedState.SingleHanded;
            IsWeaponPointingUlnar = false;
            Destroy(lastRenderedTransform.gameObject);
            Destroy(redDotRenderer.gameObject);
            base.OnDestroy();
        }

        protected void OnDisable()
        {
            LocalPlayerTwoHandedState = TwoHandedState.SingleHanded;
            IsWeaponPointingUlnar = false;
        }

        protected override Vector3 UpdateTwoHandedWield()
        {
            if (VRPlayer.ShouldPauseMovement)
            {
                return weaponForward;
            }

            bool wasTwoHanded = (LocalPlayerTwoHandedState != TwoHandedState.SingleHanded);
            if (wasTwoHanded)
            {
                IsWeaponPointingUlnar = Vector3.Dot(VRPlayer.dominantHand.transform.forward, weaponForward) < 0;
                weaponForward = base.UpdateTwoHandedWield();
                if (twoHandedState == TwoHandedState.SingleHanded)
                {
                    IsWeaponPointingUlnar = Vector3.Dot(VRPlayer.dominantHand.transform.forward, weaponForward) < 0;
                }
            }
            else
            {
                weaponForward = base.UpdateTwoHandedWield();
                CurrentTwoHandedWieldStartedWithLongGrip = ShouldUseLongGrip();
            }

            LocalPlayerTwoHandedState = twoHandedState;
            IsDominantHandHoldInversed = geometryProvider.InverseHoldForDominantHand();

            if (!redDotRenderer)
            {
                InitializeRedDot();
            }

            if (EquipScript.getLeft() == EquipType.Crossbow && VHVRConfig.OneHandedBow())
            {
                isAiming = true;
            }
            else if (IsDundr())
            {
                isAiming = true;
            }
            else if (EquipScript.getLeft() == EquipType.Crossbow || EquipScript.getRight() == EquipType.Magic)
            {
                isAiming = isCurrentlyTwoHanded();
            }
            else
            {
                isAiming = false;
            }

            updateCrosshair();

            if (twoHandedState != TwoHandedState.SingleHanded)
            {
                //VRIK Hand rotation
                RotateHandsForTwoHandedWield(weaponForward);
                rotatingHandConnectors = true;
                // Adjust the positions so that they are rotated around the hand centers which are slightly off from their local origins.
                Vector3 frontHandCenter = getHandCenter(frontHandTransform);
                Vector3 rearHandCenter = getHandCenter(rearHandTransform);
                frontHandConnector.position = frontHandConnector.parent.position + frontHandConnector.forward * HAND_CENTER_OFFSET + (frontHandCenter - frontHandTransform.position);
                rearHandConnector.position = rearHandConnector.parent.position + rearHandConnector.forward * HAND_CENTER_OFFSET + (rearHandCenter - rearHandTransform.position);

                shieldSize = 0.4f;
            }
            else
            {
                if (rotatingHandConnectors)
                {
                    VrikCreator.ResetHandConnectors();
                    shieldSize = 1f;
                }
                if (geometryProvider.ShouldRotateHandForOneHandedWield())
                {
                    RotateHandForOneHandedWield(weaponForward);
                    rotatingHandConnectors = true;
                }
                else
                {
                    rotatingHandConnectors = false;
                }
            }

            if (!EquipScript.isSpearEquipped() && EquipScript.getRight() != EquipType.Knife && VHVRConfig.TwoHandedWithShield())
            {
                ShieldBlock.instance?.ScaleShieldSize(shieldSize);
            }

            // The transform outside OnRenderObject() might be invalid or discontinuous, therefore we need to record its state within this method for physics calculation later.
            lastRenderedTransform.parent = transform;
            lastRenderedTransform.SetPositionAndRotation(transform.position, transform.rotation);
            lastRenderedTransform.localScale = Vector3.one;
            lastRenderedTransform.SetParent(null, true);
            localWeaponTip = transform.position + (weaponLength - distanceBetweenGripAndRearEnd) * weaponForward;

            return weaponForward;
        }

        protected override bool IsPlayerLeftHanded() {
            return VHVRConfig.LeftHanded();
        }

        protected override Transform GetLeftHandTransform()
        {
            return VRPlayer.leftHand.transform;
        }

        protected override Transform GetRightHandTransform()
        {
            return VRPlayer.rightHand.transform;
        }

        protected virtual bool TemporaryDisableTwoHandedWield()
        {
            return false;
        }

        protected override TwoHandedState GetDesiredTwoHandedState(bool wasTwoHanded)
        {
            if (!VHVRConfig.TwoHandedWield())
            {
                return TwoHandedState.SingleHanded;
            }

            if (!VHVRConfig.TwoHandedWithShield() && EquipScript.getLeft() == EquipType.Shield)
            {
                return TwoHandedState.SingleHanded;
            }
            
            switch (itemName)
            {
                case "Hammer":
                    return TwoHandedState.SingleHanded;
                case "FishingRod":
                    if (FishingManager.instance && FishingManager.instance.reelGrabbed)
                        return TwoHandedState.SingleHanded;
                    break;
            }

            if (nonDominantHandHasWeapon() && EquipScript.getLeft() != EquipType.Crossbow)
            {
                return TwoHandedState.SingleHanded;
            }

            if (TemporaryDisableTwoHandedWield())
            {
                return TwoHandedState.SingleHanded;
            }

            if (wasTwoHanded && VHVRConfig.StickyTwoHandedWield() && EquipScript.getLeft() != EquipType.Crossbow)
            {
                if (SteamVR_Actions.valheim_Grab.GetStateDown(VRPlayer.dominantHandInputSource))
                {
                    preparingToUnstickTwoHandedWield = true;
                }
                if (SteamVR_Actions.valheim_Grab.GetState(VRPlayer.nonDominantHandInputSource))
                {
                    preparingToUnstickTwoHandedWield = false;
                }
                if (SteamVR_Actions.valheim_Grab.GetStateUp(VRPlayer.dominantHandInputSource))
                {
                    if (preparingToUnstickTwoHandedWield)
                    {
                        preparingToUnstickTwoHandedWield = false;
                        return TwoHandedState.SingleHanded;
                    }
                    if (Mathf.Abs(Vector3.Dot(VRPlayer.dominantHand.transform.forward, weaponForward)) < 0.5f)
                    {
                        return TwoHandedState.SingleHanded;
                    }
                }
            }
            else if (!SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand) ||
                    !SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand))
            {
                return TwoHandedState.SingleHanded;
            }

            if (wasTwoHanded)
            {
                // Stay in current two-handed mode since both hands are grabbing.
                return twoHandedState;
            }

            // Enter two-handed wield as needed.
            Vector3 rightHandToLeftHand = getHandCenter(GetLeftHandTransform()) - getHandCenter(GetRightHandTransform());
            if (Vector3.Project(rightHandToLeftHand, GetWeaponPointingDirection()).magnitude > weaponLength * 0.5f)
            {
                return TwoHandedState.SingleHanded;
            }
            float wieldingAngle = Vector3.Angle(rightHandToLeftHand, GetWeaponPointingDirection());
            if (wieldingAngle < 60)
            {
                preparingToUnstickTwoHandedWield = false;
                return TwoHandedState.RightHandBehind;
            }
            else if (wieldingAngle > 120f)
            {
                preparingToUnstickTwoHandedWield = false;
                return TwoHandedState.LeftHandBehind;
            }

            return TwoHandedState.SingleHanded;
        }

        protected virtual void RotateHandsForTwoHandedWield(Vector3 weaponPointingDir)
        {
            Vector3 desiredFrontHandForward =
                Vector3.Project(
                    frontHandTransform.forward,
                    EquipScript.getRight() == EquipType.Scythe ? Vector3.Cross(weaponPointingDir, frontHandTransform.up) : weaponPointingDir);
            Vector3 desiredRearHandForward = Vector3.Project(rearHandTransform.forward, Quaternion.AngleAxis(10, rearHandTransform.right) * weaponPointingDir);
            frontHandConnector.rotation = Quaternion.LookRotation(desiredFrontHandForward, frontHandTransform.up);
            rearHandConnector.rotation = Quaternion.LookRotation(desiredRearHandForward, rearHandTransform.up);
        }

        private void RotateHandForOneHandedWield(Vector3 weaponPointingDir)
        {
            VrikCreator.GetLocalPlayerDominantHandConnector().rotation =
                Quaternion.LookRotation(
                    Quaternion.AngleAxis(10, mainHand.transform.right) * weaponPointingDir,
                    mainHand.transform.up);
        }

        public static bool isCurrentlyTwoHanded()
        {
            if (EquipScript.getLeft() == EquipType.None && EquipScript.getRight() == EquipType.None)
            {
                return false;
            }
            return LocalPlayerTwoHandedState != TwoHandedState.SingleHanded;
        }

        public bool allowBlocking()
        {
            switch (attackAnimation)
            {
                case "knife_stab":
                    if (EquipScript.getLeft() == EquipType.Shield)
                        return false;
                    else
                        return SteamVR_Actions.valheim_Grab.GetState(VRPlayer.dominantHandInputSource);
                default:
                    if (!SteamVR_Actions.valheim_Grab.GetState(VRPlayer.dominantHandInputSource))
                    {
                        return false;
                    }
                    return !VHVRConfig.UseGestureBlock() || isCurrentlyTwoHanded();
            }
        }

        public static bool nonDominantHandHasWeapon()
        {
            var player = Player.m_localPlayer;
            var leftHandItem = player?.m_leftItem?.m_shared.m_itemType;

            return !(leftHandItem is null) && leftHandItem != ItemDrop.ItemData.ItemType.Shield;
        }

        private void updateCrosshair()
        {
            GameObject crosshair = CrosshairManager.instance.weaponCrosshair;
            if (crosshair == null)
            {
                LogUtils.LogWarning("Crosshair not found for weapon");
                return;
            }

            crosshair.SetActive(VHVRConfig.ShowStaticCrosshair());
            crosshair.transform.SetParent(transform, false);
            crosshair.transform.position = transform.position + CrosshairManager.WEAPON_CROSSHAIR_DISTANCE * weaponForward;
            crosshair.transform.localRotation = Quaternion.identity;
            crosshair.SetActive(isAiming);
        }

        private void InitializeRedDot()
        {
            redDotRenderer = GameObject.CreatePrimitive(PrimitiveType.Sphere).GetComponent<MeshRenderer>();
            redDotRenderer.transform.parent = transform;
            redDotRenderer.transform.position = transform.position + weaponForward * RED_DOT_DISTANCE;
            redDotRenderer.transform.localScale = Vector3.one * RED_DOT_DISTANCE * RED_DOT_SIZE_RADIANS;
            GameObject.Destroy(redDotRenderer.gameObject.GetComponent<Collider>());
            if (RedDotMaterial == null)
            {
                RedDotMaterial = Object.Instantiate(VRAssetManager.GetAsset<Material>("Unlit"));
                RedDotMaterial.color = Color.red;
            }

            redDotRenderer.sharedMaterial = RedDotMaterial;
            redDotRenderer.gameObject.layer = LayerUtils.getWorldspaceUiLayer();
            redDotRenderer.receiveShadows = false;
            redDotRenderer.shadowCastingMode = ShadowCastingMode.Off;
            redDotRenderer.lightProbeUsage = LightProbeUsage.Off;
            redDotRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            redDotRenderer.enabled = false;
        }
    }
}
