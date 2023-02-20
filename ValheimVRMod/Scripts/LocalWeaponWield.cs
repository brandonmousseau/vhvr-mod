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
        private const float HAND_CENTER_OFFSET = 0.08f;

        private Attack attack;
        private bool knifeReverseHold;

        public static Vector3 weaponForward;
        private string itemName;

        public Transform rearHandTransform { get; private set; }
        public Transform frontHandTransform { get; private set; }

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

        private ItemDrop.ItemData item;
        private Transform singleHandedTransform;
        private Transform originalTransform;
        private Quaternion offsetFromPointingDir; // The rotation offset of this transform relative to the direction the weapon is pointing at.

        public static TwoHandedState LocalPlayerTwoHandedState { get; private set; }
        public TwoHandedState twoHandedState { get; private set; }
        private float shieldSize = 1f;
        private Transform frontHandConnector { get { return twoHandedState == TwoHandedState.LeftHandBehind ? VrikCreator.localPlayerRightHandConnector : VrikCreator.localPlayerLeftHandConnector; } }
        private Transform rearHandConnector { get { return twoHandedState == TwoHandedState.LeftHandBehind ? VrikCreator.localPlayerLeftHandConnector : VrikCreator.localPlayerRightHandConnector; } }
        private Vector3 estimatedLocalWeaponPointingDir = Vector3.forward;
        private Transform lastRenderedTransform;
        public PhysicsEstimator physicsEstimator { get; private set; }
        public static bool IsDominantHandBehind { get { return isCurrentlyTwoHanded() && (LocalPlayerTwoHandedState == TwoHandedState.RightHandBehind ^ VHVRConfig.LeftHanded()); } }

        private bool wasTwoHanded = false;

        ParticleSystem particleSystem;
        Transform particleSystemTransformUpdater;

        protected virtual void Awake()
        {
            lastRenderedTransform = new GameObject().transform;
            physicsEstimator = lastRenderedTransform.gameObject.AddComponent<PhysicsEstimator>();
            physicsEstimator.refTransform = CameraUtils.getCamera(CameraUtils.VR_CAMERA)?.transform.parent;
        }

        public LocalWeaponWield Initialize(ItemDrop.ItemData item, string itemName)
        {
            this.item = item;
            this.itemName = itemName;

            particleSystem = gameObject.GetComponentInChildren<ParticleSystem>();
            if (particleSystem != null)
            {
                particleSystemTransformUpdater = new GameObject().transform;
                particleSystemTransformUpdater.parent = transform;
                particleSystemTransformUpdater.SetPositionAndRotation(particleSystem.transform.position, particleSystem.transform.rotation);
            }

            attack = item.m_shared.m_attack.Clone();

            originalTransform = new GameObject().transform;
            singleHandedTransform = new GameObject().transform;
            originalTransform.parent = singleHandedTransform.parent = transform.parent;
            originalTransform.position = singleHandedTransform.position = transform.position;
            originalTransform.rotation = transform.rotation;
            transform.rotation = singleHandedTransform.rotation = GetSingleHandedRotation(originalTransform.rotation);

            MeshFilter weaponMeshFilter = gameObject.GetComponentInChildren<MeshFilter>();
            if (weaponMeshFilter != null)
            {
                estimatedLocalWeaponPointingDir = transform.InverseTransformDirection(WeaponUtils.EstimateWeaponPointingDirection(weaponMeshFilter, transform.parent.position));
            }

            offsetFromPointingDir = Quaternion.Inverse(Quaternion.LookRotation(GetWeaponPointingDir(), transform.up)) * transform.rotation;

            LocalPlayerTwoHandedState = twoHandedState = TwoHandedState.SingleHanded;

            return this;
        }

        private void OnDestroy()
        {
            ReturnToSingleHanded();
            Destroy(originalTransform.gameObject);
            Destroy(singleHandedTransform.gameObject);
            Destroy(lastRenderedTransform.gameObject);
            if (particleSystemTransformUpdater != null)
            {
                Destroy(particleSystemTransformUpdater.gameObject);
            }
        }

        protected virtual void OnRenderObject()
        {
            if (VRPlayer.ShouldPauseMovement)
            {
                return;
            }

            WieldHandle();
            if (particleSystem != null)
            {
                // The particle system on Mistwalker (as well as some modded weapons) for some reason needs it rotation updated explicitly in order to follow the sword in VR.
                particleSystem.transform.rotation = particleSystemTransformUpdater.transform.rotation;
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

        // Returns the direction the weapon is pointing.
        protected virtual Vector3 GetWeaponPointingDir()
        {
            return transform.TransformDirection(estimatedLocalWeaponPointingDir);
        }

        // Calculates the correct rotation of this game object for single-handed mode using the original rotation.
        // This should be the same as the original rotation in most cases but there are exceptions.
        protected virtual Quaternion GetSingleHandedRotation(Quaternion originalRotation)
        {
            switch (attack.m_attackAnimation)
            {
                case "atgeir_attack":
                    // Atgeir wield rotation fix: the tip of the atgeir is pointing at (0.328, -0.145, 0.934) in local coordinates.
                    return originalRotation * Quaternion.AngleAxis(-20, Vector3.up) * Quaternion.AngleAxis(-7, Vector3.right);
                default:
                    return originalRotation;
            }
        }

        protected virtual void RotateHandsForTwoHandedWield(Vector3 weaponPointingDir)
        {
            Vector3 desiredFrontHandForward = Vector3.Project(frontHandTransform.forward, weaponPointingDir);
            Vector3 desiredRearHandForward = Vector3.Project(rearHandTransform.forward, Quaternion.AngleAxis(10, rearHandTransform.right) * weaponPointingDir);
            frontHandConnector.rotation = Quaternion.LookRotation(desiredFrontHandForward, frontHandTransform.up);
            rearHandConnector.rotation = Quaternion.LookRotation(desiredRearHandForward, rearHandTransform.up);
        }

        // The preferred up direction used to determine the weapon's rotation around it longitudinal axis during two-handed wield.
        protected virtual Vector3 GetPreferredTwoHandedWeaponUp()
        {
            return singleHandedTransform.up;
        }

        // The preferred forward offset amount of the weapon's position from the rear hand during two-handed wield.
        protected virtual float GetPreferredOffsetFromRearHand(float handDist)
        {
            bool rearHandIsDominant = (IsPlayerLeftHanded() == (twoHandedState == TwoHandedState.LeftHandBehind));
            if (rearHandIsDominant)
            {
                return -0.1f;
            }
            else if (handDist > 0.15f)
            {
                return 0.05f;
            }
            else
            {
                // Anchor the weapon in the front/dominant hand instead.
                return handDist - 0.1f;
            }
        }

        private void WieldHandle()
        {
            // TODO: find out whether on of these two weaponForward updates can be skipped.
            weaponForward = GetWeaponPointingDir();
            UpdateTwoHandedWield();
            weaponForward = GetWeaponPointingDir();
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

        private void UpdateTwoHandedWield()
        {
            twoHandedState = GetDesiredTwoHandedState(wasTwoHanded);
            if (twoHandedState != TwoHandedState.SingleHanded)
            {
                wasTwoHanded = true;

                rearHandTransform = twoHandedState == TwoHandedState.LeftHandBehind ? GetLeftHandTransform() : GetRightHandTransform();
                frontHandTransform = twoHandedState == TwoHandedState.LeftHandBehind ? GetRightHandTransform() : GetLeftHandTransform();

                Vector3 frontHandCenter = getHandCenter(frontHandTransform);
                Vector3 rearHandCenter = getHandCenter(rearHandTransform);
                var weaponPointingDir = (frontHandCenter - rearHandCenter).normalized;

                //weapon pos&rotation
                transform.position = rearHandCenter + weaponPointingDir * (HAND_CENTER_OFFSET + GetPreferredOffsetFromRearHand(Vector3.Distance(frontHandCenter, rearHandCenter)));
                transform.rotation = Quaternion.LookRotation(weaponPointingDir, GetPreferredTwoHandedWeaponUp()) * offsetFromPointingDir;
            }
            else if (wasTwoHanded)
            {
                wasTwoHanded = false;
                ReturnToSingleHanded();
            }

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
        }

        private void ReturnToSingleHanded()
        {
            VrikCreator.ResetHandConnectors();
            transform.position = singleHandedTransform.position;
            transform.localRotation = singleHandedTransform.localRotation;
        }

        private static Vector3 getHandCenter(Transform hand)
        {
            return hand.transform.position - hand.transform.forward * HAND_CENTER_OFFSET;
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
