using UnityEngine;
using ValheimVRMod.Scripts.Block;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
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
            ReturnToSingleHanded();
            Destroy(lastRenderedTransform.gameObject);
            base.OnDestroy();
        }

        protected override void OnRenderObject()
        {
            if (VRPlayer.ShouldPauseMovement)
            {
                return;
            }

            // TODO: find out whether on of these two weaponForward updates can be skipped.
            weaponForward = GetWeaponPointingDir();
            base.OnRenderObject();
            weaponForward = GetWeaponPointingDir();

            LocalPlayerTwoHandedState = twoHandedState;

            if (twoHandedState == TwoHandedState.SingleHanded)
            {
                shieldSize = 1f;
            }
            else
            {
                //VRIK Hand rotation
                RotateHandsForTwoHandedWield(GetWeaponPointingDir());
                // Adjust the positions so that they are rotated around the hand centers which are slightly off from their local origins.
                Vector3 frontHandCenter = getHandCenter(frontHandTransform);
                Vector3 rearHandCenter = getHandCenter(rearHandTransform);
                frontHandConnector.position = frontHandConnector.parent.position + frontHandConnector.forward * HAND_CENTER_OFFSET + (frontHandCenter - frontHandTransform.position);
                rearHandConnector.position = rearHandConnector.parent.position + rearHandConnector.forward * HAND_CENTER_OFFSET + (rearHandCenter - rearHandTransform.position);

                shieldSize = 0.4f;
            }

            // The transform outside OnRenderObject() might be invalid or discontinuous, therefore we need to record its state within this method for physics calculation later.
            lastRenderedTransform.parent = transform;
            lastRenderedTransform.SetPositionAndRotation(transform.position, transform.rotation);
            lastRenderedTransform.localScale = Vector3.one;
            lastRenderedTransform.SetParent(null, true);

            if (attack.m_attackAnimation == "knife_stab")
            {
                KnifeWield();
            }
            if (!EquipScript.isSpearEquipped() && VHVRConfig.TwoHandedWithShield())
            {
                ShieldBlock.instance?.ScaleShieldSize(shieldSize);
            }
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

            if (attack.m_attackAnimation == "knife_stab") {
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
                ReturnToSingleHanded();
                // Reverse grip
                transform.localRotation *= Quaternion.AngleAxis(180, Vector3.right);
                knifeReverseHold = true;
            }
            else if (knifeReverseHold)
            {
                ReturnToSingleHanded();
                knifeReverseHold = false;
            }
        }

        protected override void ReturnToSingleHanded()
        {
            VrikCreator.ResetHandConnectors();
            base.ReturnToSingleHanded();
        }

        public static bool isCurrentlyTwoHanded()
        {
            return LocalPlayerTwoHandedState != TwoHandedState.SingleHanded;
        }

        public bool allowBlocking()
        {
            switch (attack.m_attackAnimation)
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
    }
}
