using UnityEngine;
using ValheimVRMod.Scripts.Block;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;
using Valve.VR.InteractionSystem;

namespace ValheimVRMod.Scripts
{
    public class WeaponWield : MonoBehaviour
    {
        private const float HAND_CENTER_OFFSET = 0.08f;

        private Attack attack;
        private bool weaponSubPos;

        public static Vector3 weaponForward;
        public string itemName;
        public Hand rearHand { get; private set; }
        public Hand frontHand { get; private set; }
        public Hand mainHand { get { return isCurrentlyTwoHanded() ? rearHand : VRPlayer.dominantHand; } } 

        private ItemDrop.ItemData item;
        private Transform singleHandedTransform;
        private Transform originalTransform;
        public static isTwoHanded _isTwoHanded;
        private float shieldSize = 1f;
        private bool isOtherHandWeapon = false;
        private Transform frontHandConnector { get { return _isTwoHanded == isTwoHanded.LeftHandBehind ? VrikCreator.rightHandConnector : VrikCreator.leftHandConnector; } }
        private Transform rearHandConnector { get { return _isTwoHanded == isTwoHanded.LeftHandBehind ? VrikCreator.leftHandConnector : VrikCreator.rightHandConnector; } }

        ParticleSystem particleSystem;
        Transform particleSystemTransformUpdater;

        public enum isTwoHanded
        {
            SingleHanded,
            RightHandBehind,
            LeftHandBehind
        }

        public WeaponWield Initialize(bool isLeftHandEquip)
        {
            isOtherHandWeapon = isLeftHandEquip;
            if (isLeftHandEquip)
            {
                item = Player.m_localPlayer.GetLeftItem();
            }
            else
            {
                item = Player.m_localPlayer.GetRightItem();
            }

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

            _isTwoHanded = isTwoHanded.SingleHanded;

            return this;
        }
        private void OnDestroy()
        {
            ReturnToSingleHanded();
            Destroy(originalTransform.gameObject);
            Destroy(singleHandedTransform.gameObject);
            if (particleSystemTransformUpdater != null)
            {
                Destroy(particleSystemTransformUpdater.gameObject);
            }
        }

        protected virtual void OnRenderObject()
        {
            WieldHandle();
            if (particleSystem != null)
            {
                // The particle system on Mistwalker (as well as some modded weapons) for some reason needs it rotation updated explicitly in order to follow the sword in VR.
                particleSystem.transform.rotation = particleSystemTransformUpdater.transform.rotation;
            }
        }

        protected virtual bool TemporaryDisableTwoHandedWield()
        {
            // TODO: implement a subclass ThrowableWeaponWield and move this impl to the override method there.
            return EquipScript.isSpearEquipped() && (SpearManager.IsAiming() || SpearManager.isThrowing);
        }
        
        // Calculates the correct rotation of this game object for single-handed mode using the original rotation.
        // This should be the same as the original rotation in most cases but there are exceptions.
        protected virtual Quaternion GetSingleHandedRotation(Quaternion originalRotation)
        {
            switch (attack.m_attackAnimation)
            {
                case "atgeir_attack":
                    // Atgeir wield rotation fix
                    return originalRotation * Quaternion.AngleAxis(-20, Vector3.up) * Quaternion.AngleAxis(-7, Vector3.right);
                default:
                    return originalRotation;
            }
        }        

        protected virtual void RotateHandsForTwoHandedWield(Vector3 weaponHoldVector)
        {
            frontHandConnector.LookAt(frontHandConnector.position - weaponHoldVector, frontHand.transform.up);
            rearHandConnector.LookAt(rearHandConnector.position + weaponHoldVector, rearHand.transform.up);
            
            if (GetHandAngleDiff(frontHand.transform, rearHand.transform) <= 0)
            {
                frontHandConnector.Rotate(Vector3.up, 180);
            }
            if (GetHandAngleDiff(rearHand.transform, frontHand.transform) < 0)
            {
                rearHandConnector.Rotate(Vector3.up, 180);
            }
            rearHandConnector.Rotate(Vector3.right, 10);
        }
   
        private void WieldHandle()
        {
            weaponForward = transform.forward;
            switch (itemName)
            {
                case "Hoe":
                case "Hammer":
                case "Cultivator":
                    return;
                case "FishingRod":
                    if (FishingManager.instance && FishingManager.instance.reelGrabbed)
                        return;
                    break;
            }
            switch (attack.m_attackAnimation)
            {
                case "knife_stab":
                    KnifeWield();
                    break;
                default:
                    if (isLeftHandWeapon() && EquipScript.getLeft() != EquipType.Crossbow)
                    {
                        break;
                    }
                    UpdateTwoHandedWield();
                    if (!EquipScript.isSpearEquipped() && VHVRConfig.TwoHandedWithShield())
                    {
                        ShieldBlock.instance?.ScaleShieldSize(shieldSize);
                    }
                    break;
            }
            weaponForward = transform.forward;
        }
        private void KnifeWield()
        {
            if (_isTwoHanded != isTwoHanded.SingleHanded)
            {
                return;
            }

            if (SteamVR_Actions.valheim_Grab.GetState(VRPlayer.dominantHandInputSource))
            {
                ReturnToSingleHanded();
                // Reverse grip
                transform.localRotation *= Quaternion.AngleAxis(180, Vector3.right);
                weaponSubPos = true;
            }
            else if (weaponSubPos)
            {
                ReturnToSingleHanded();
                weaponSubPos = false;
            }
        }
        
        private void UpdateTwoHandedWield()
        {
            if (!VHVRConfig.TwoHandedWield())
            {
                return;
            }

            if (!VHVRConfig.TwoHandedWithShield() && EquipScript.getLeft() == EquipType.Shield)
            {
                if (weaponSubPos)
                {
                    _isTwoHanded = isTwoHanded.SingleHanded;
                    weaponSubPos = false;
                    ReturnToSingleHanded();
                }
                return;
            }

            if (SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand) && 
                SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand) &&
                !TemporaryDisableTwoHandedWield())
            {
                float handAngleDiff = GetHandAngleDiff(VRPlayer.rightHand.transform, VRPlayer.leftHand.transform);
                if (_isTwoHanded == isTwoHanded.SingleHanded)
                {
                    if (handAngleDiff > 0.6f)
                    {
                        _isTwoHanded = isTwoHanded.RightHandBehind;
                    }
                    else if (handAngleDiff < -0.6f)
                    {
                        _isTwoHanded = isTwoHanded.LeftHandBehind;
                    }
                    else
                    {
                        return;
                    }
                }

                rearHand = _isTwoHanded == isTwoHanded.LeftHandBehind ? VRPlayer.leftHand : VRPlayer.rightHand;
                frontHand = rearHand.otherHand;

                Vector3 frontHandCenter = getHandCenter(frontHand.transform);
                Vector3 rearHandCenter = getHandCenter(rearHand.transform);
                var handDist = Vector3.Distance(frontHandCenter, rearHandCenter);
                var weaponHoldVector = frontHandCenter - rearHandCenter;
                var distLimit = 0f;
                var distMultiplier = 0f;
                var originMultiplier = -0.1f;
                bool rearHandIsDominant = (VHVRConfig.LeftHanded() == (_isTwoHanded == isTwoHanded.LeftHandBehind));

                //debug check animation
                //LogUtils.LogDebug("animation = " + attack.m_attackAnimation);
                switch (attack.m_attackAnimation)
                {
                    case "spear_poke":
                        distMultiplier = -0.09f;
                        distLimit = 0.09f;
                        originMultiplier = 0.2f;
                        break;
                    case "crossbow_fire":
                        originMultiplier = 0.35f;
                        break;
                    default:
                        if (!rearHandIsDominant && !EquipScript.isSpearEquipped()) {
                            // Anchor the weapon on the dominant hand.
                            originMultiplier = Mathf.Min(handDist, 0.15f) - 0.1f;
                        }
                        break;
                }
                var weaponOffset = weaponHoldVector.normalized * (HAND_CENTER_OFFSET + originMultiplier - distMultiplier / Mathf.Max(handDist, distLimit));
                ReturnToSingleHanded();

                //VRIK Hand rotation
                RotateHandsForTwoHandedWield(weaponHoldVector);
                // Adjust the positions so that they are rotated around the hand centers which are slightly off from their local origins.
                frontHandConnector.position = frontHandConnector.parent.position + frontHandConnector.forward * HAND_CENTER_OFFSET + (frontHandCenter - frontHand.transform.position);
                rearHandConnector.position = rearHandConnector.parent.position + rearHandConnector.forward * HAND_CENTER_OFFSET + (rearHandCenter - rearHand.transform.position);

                //weapon pos&rotation
                transform.position = rearHandCenter + weaponOffset;
                transform.rotation = Quaternion.LookRotation(weaponHoldVector, originalTransform.up) * singleHandedTransform.localRotation;

                if (EquipScript.isSpearEquippedUlnarForward())
                {
                    transform.rotation *= Quaternion.Euler(180, 0, 0);
                }
                else if (EquipScript.getLeft() == EquipType.Crossbow && VHVRConfig.CrossbowSaggitalRotationSource() == "BothHands")
                {
                    Vector3 frontHandPalmar = _isTwoHanded == isTwoHanded.LeftHandBehind ? -frontHand.transform.right : frontHand.transform.right;
                    Vector3 frontHandRadial = frontHand.transform.up;
                    Vector3 rearHandRadial = rearHand.transform.up;
                    Vector3 weaponUp = (frontHandPalmar * 1.73f + frontHandRadial).normalized + rearHandRadial;
                    transform.rotation = Quaternion.LookRotation(weaponHoldVector, weaponUp) * originalTransform.localRotation;
                }

                weaponSubPos = true;
                shieldSize = 0.4f;
            }
            else if (SteamVR_Actions.valheim_Grab.GetStateUp(SteamVR_Input_Sources.LeftHand) || 
                     SteamVR_Actions.valheim_Grab.GetStateUp(SteamVR_Input_Sources.RightHand)||
                     TemporaryDisableTwoHandedWield())
            {
                _isTwoHanded = isTwoHanded.SingleHanded;
                weaponSubPos = false;
                ReturnToSingleHanded();
            }
        }

        private void ReturnToSingleHanded()
        {
            VrikCreator.ResetHandConnectors();
            shieldSize = 1f;
            transform.position = singleHandedTransform.position;
            transform.localRotation = singleHandedTransform.localRotation;
        }

        private float GetHandAngleDiff(Transform mainHand, Transform refHand)
        {
            Vector3 localHandWieldDirection = attack.m_attackAnimation == "spear_poke" ? new Vector3(0, 0.45f, 0.55f) : Vector3.forward;
            return Vector3.Dot(localHandWieldDirection, mainHand.InverseTransformVector(getHandCenter(refHand) - getHandCenter(mainHand)).normalized);
        }

        private static Vector3 getHandCenter(Transform hand) {
            return hand.transform.position - hand.transform.forward * HAND_CENTER_OFFSET;
        }

        public static bool isCurrentlyTwoHanded()
        {
            return _isTwoHanded != isTwoHanded.SingleHanded;
        }

        public bool allowBlocking()
        {
            switch (attack.m_attackAnimation)
            {
                case "knife_stab":
                    if (EquipScript.getLeft() == EquipType.Shield)
                        return false;
                    else
                        return weaponSubPos;
                default:
                    return isCurrentlyTwoHanded();
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
