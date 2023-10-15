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

        protected bool isRedDotVisible { set { redDotRenderer.enabled = value; } }

        private const float RED_DOT_DISTANCE = 256;
        private const float RED_DOT_SIZE_RADIANS = 1f / 256f;
        private static Material RedDotMaterial = null;
        private MeshRenderer redDotRenderer; // Red dot for aiming

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

        private bool knifeReverseHold;
        private float shieldSize = 1f;

        protected virtual void Awake()
        {
            lastRenderedTransform = new GameObject().transform;
            physicsEstimator = lastRenderedTransform.gameObject.AddComponent<PhysicsEstimator>();
            physicsEstimator.refTransform = CameraUtils.getCamera(CameraUtils.VR_CAMERA)?.transform.parent;
        }

        protected override void OnDestroy()
        {
            VrikCreator.ResetHandConnectors();
            Destroy(lastRenderedTransform.gameObject);
            Destroy(redDotRenderer.gameObject);
            base.OnDestroy();
        }

        protected override Vector3 UpdateTwoHandedWield()
        {
            if (VRPlayer.ShouldPauseMovement)
            {
                return weaponForward;
            }

            bool wasTwoHanded = (LocalPlayerTwoHandedState != TwoHandedState.SingleHanded);
            weaponForward = base.UpdateTwoHandedWield();
            LocalPlayerTwoHandedState = twoHandedState;

            if (attackAnimation == "knife_stab")
            {
                KnifeWield();
                weaponForward = GetWeaponPointingDir();
            }

            if (!redDotRenderer)
            {
                InitializeRedDot();
            }

            updateCrosshair();

            if (twoHandedState != TwoHandedState.SingleHanded)
            {
                //VRIK Hand rotation
                RotateHandsForTwoHandedWield(weaponForward);
                // Adjust the positions so that they are rotated around the hand centers which are slightly off from their local origins.
                Vector3 frontHandCenter = getHandCenter(frontHandTransform);
                Vector3 rearHandCenter = getHandCenter(rearHandTransform);
                frontHandConnector.position = frontHandConnector.parent.position + frontHandConnector.forward * HAND_CENTER_OFFSET + (frontHandCenter - frontHandTransform.position);
                rearHandConnector.position = rearHandConnector.parent.position + rearHandConnector.forward * HAND_CENTER_OFFSET + (rearHandCenter - rearHandTransform.position);

                shieldSize = 0.4f;
            }
            else if (wasTwoHanded)
            {
                VrikCreator.ResetHandConnectors();
                shieldSize = 1f;
            }

            if (!EquipScript.isSpearEquipped() && VHVRConfig.TwoHandedWithShield())
            {
                ShieldBlock.instance?.ScaleShieldSize(shieldSize);
            }

            // The transform outside OnRenderObject() might be invalid or discontinuous, therefore we need to record its state within this method for physics calculation later.
            lastRenderedTransform.parent = transform;
            lastRenderedTransform.SetPositionAndRotation(transform.position, transform.rotation);
            lastRenderedTransform.localScale = Vector3.one;
            lastRenderedTransform.SetParent(null, true);

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
                case "Hoe":
                case "Hammer":
                case "Cultivator":
                    return TwoHandedState.SingleHanded;
                case "FishingRod":
                    if (FishingManager.instance && FishingManager.instance.reelGrabbed)
                        return TwoHandedState.SingleHanded;
                    break;
            }

            if (attackAnimation == "knife_stab") {
                return TwoHandedState.SingleHanded;
            }
            
            if (isLeftHandWeapon() && EquipScript.getLeft() != EquipType.Crossbow)
            {
                return TwoHandedState.SingleHanded;
            }

            if (!SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand) ||
                !SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand) ||
                TemporaryDisableTwoHandedWield())
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
            float wieldingAngle = Vector3.Angle(rightHandToLeftHand, GetWeaponPointingDir());
            if (wieldingAngle < 60)
            {
                return TwoHandedState.RightHandBehind;
            }
            else if (wieldingAngle > 60f)
            {
                return TwoHandedState.LeftHandBehind;
            }

            return TwoHandedState.SingleHanded;
        }

        protected virtual void RotateHandsForTwoHandedWield(Vector3 weaponPointingDir)
        {
            Vector3 desiredFrontHandForward = Vector3.Project(frontHandTransform.forward, weaponPointingDir);
            Vector3 desiredRearHandForward = Vector3.Project(rearHandTransform.forward, Quaternion.AngleAxis(10, rearHandTransform.right) * weaponPointingDir);
            frontHandConnector.rotation = Quaternion.LookRotation(desiredFrontHandForward, frontHandTransform.up);
            rearHandConnector.rotation = Quaternion.LookRotation(desiredRearHandForward, rearHandTransform.up);
        }

        private void KnifeWield()
        {
            if (SteamVR_Actions.valheim_Grab.GetState(VRPlayer.dominantHandInputSource))
            {
                // Reverse grip
                transform.rotation = GetOriginalRotation() * Quaternion.AngleAxis(180, Vector3.right);
                knifeReverseHold = true;
            }
            else if (knifeReverseHold)
            {
                transform.rotation = GetOriginalRotation();
                knifeReverseHold = false;
            }
        }

        public static bool isCurrentlyTwoHanded()
        {
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
                    return VHVRConfig.BlockingType() == "Gesture" ? isCurrentlyTwoHanded() : SteamVR_Actions.valheim_Grab.GetState(VRPlayer.dominantHandInputSource);
            }
        }

        public bool isLeftHandWeapon()
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
            bool isAiming = (EquipScript.getLeft() == EquipType.Crossbow || EquipScript.getRight() == EquipType.Magic) && isCurrentlyTwoHanded();
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
                // Since the red dot is rendered at a far distance and could be subject to strong fog effect,
                // we need a fog-free material so that its color does not fade.
                // TODO: consider writing a custom shader instead of borrowing the VR pointer material.
                RedDotMaterial = new Material(VRPlayer.leftPointer.gameObject.GetComponentInChildren<Renderer>().material);
                RedDotMaterial.color = Color.black;
                RedDotMaterial.EnableKeyword("_EMISSION");
                RedDotMaterial.SetColor("_EmissionColor", Color.red);
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
