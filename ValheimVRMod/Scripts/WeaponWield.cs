using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts
{
    public class WeaponWield : MonoBehaviour
    {
        private Attack attack;
        private bool weaponSubPos;

        public Vector3 weaponForward;
        public string itemName;
        private ItemDrop.ItemData item;
        private GameObject rotSave;
        public static isTwoHanded _isTwoHanded;
        private SteamVR_Input_Sources mainHandInputSource;

        public enum isTwoHanded
        {
            SingleHanded,
            RightHandBehind,
            LeftHandBehind
        }

        private void Awake()
        {
            rotSave = new GameObject();
            rotSave.transform.SetParent(transform.parent);
            rotSave.transform.position = transform.position;
            rotSave.transform.localRotation = transform.localRotation;

            item = Player.m_localPlayer.GetRightItem();
            attack = item.m_shared.m_attack.Clone();
            _isTwoHanded = isTwoHanded.SingleHanded;

            if (VHVRConfig.LeftHanded())
            {
                mainHandInputSource = SteamVR_Input_Sources.LeftHand;
            }
            else
            {
                mainHandInputSource = SteamVR_Input_Sources.RightHand;
            }

        }
        private void OnDestroy()
        {
            ResetOffset();
            Destroy(rotSave);
        }

        private void OnRenderObject()
        {
            WieldHandle();

        }
        private void WieldHandle()
        {
            switch (itemName)
            {
                case "Hoe":
                case "Hammer":
                case "FishingRod":
                case "Cultivator":
                    return;
            }
            switch (attack.m_attackAnimation)
            {
                case "knife_stab":
                    KnifeWield();
                    break;
                default:
                    UpdateTwoHandedWield();
                    break;
            }
        }
        private void KnifeWield()
        {
            if (_isTwoHanded != isTwoHanded.SingleHanded)
            {
                return;
            }

            if (SteamVR_Actions.valheim_Grab.GetState(mainHandInputSource))
            {
                ResetOffset();
                transform.localRotation *= Quaternion.AngleAxis(180, Vector3.right);
                weaponForward = transform.forward;
                weaponSubPos = true;
            }
            else if (weaponSubPos)
            {
                ResetOffset();
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
                    ResetOffset();
                }
                return;
            }

            if (SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand) && 
                SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand) &&
                !(isSpear() && SpearManager.IsAiming()) )
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

                var rearHand = _isTwoHanded == isTwoHanded.LeftHandBehind ? VRPlayer.leftHand : VRPlayer.rightHand;
                var frontHand = rearHand.otherHand;

                var handDist = Vector3.Distance(frontHand.transform.position, rearHand.transform.position);
                var weaponHoldVector = frontHand.transform.position - rearHand.transform.position;
                var distLimit = 0f;
                var distMultiplier = 0f;
                var originMultiplier = -0.1f;
                var rotOffset = 180;
                bool rearHandIsDominant = (VHVRConfig.LeftHanded() == (_isTwoHanded == isTwoHanded.LeftHandBehind));
                switch (attack.m_attackAnimation)
                {
                    case "spear_poke":
                        distMultiplier = -0.09f;
                        distLimit = 0.09f;
                        originMultiplier = 0.2f;
                        break;
                    case "atgeir_attack":
                        distMultiplier = -0.18f;
                        distLimit = 0.18f;
                        originMultiplier = -0.7f;
                        break;
                    default:
                        if (!rearHandIsDominant && !isSpear()) {
                            // Anchor the weapon on the dominant hand.
                            originMultiplier = Mathf.Min(handDist, 0.15f) - 0.1f;
                        }
                        break;
                }
                var weaponOffset = weaponHoldVector.normalized * (originMultiplier - distMultiplier / Mathf.Max(handDist, distLimit));
                ResetOffset();

                //VRIK Hand rotation
                var frontHandConnector = _isTwoHanded == isTwoHanded.LeftHandBehind ? VrikCreator.rightHandConnector : VrikCreator.leftHandConnector;
                var rearHandConnector = _isTwoHanded == isTwoHanded.LeftHandBehind ? VrikCreator.leftHandConnector : VrikCreator.rightHandConnector;
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

                //weapon pos&rotation
                transform.position = rearHand.transform.position + weaponOffset;
                if (isSpear() && !VHVRConfig.SpearInverseWield())
                {
                    transform.LookAt(frontHand.transform.position - weaponHoldVector.normalized * 5, transform.up);
                    transform.localRotation = transform.localRotation * (rotSave.transform.localRotation) * Quaternion.AngleAxis(180, Vector3.right) * Quaternion.AngleAxis(rotOffset, transform.InverseTransformDirection(-weaponHoldVector));
                    transform.localRotation = transform.localRotation * (rotSave.transform.localRotation) * Quaternion.AngleAxis(180, Vector3.right);
                }
                else
                {
                    transform.LookAt(rearHand.transform.position - weaponHoldVector.normalized * 5, transform.up);
                    transform.localRotation = transform.localRotation * (rotSave.transform.localRotation) * Quaternion.AngleAxis(180, Vector3.right) * Quaternion.AngleAxis(rotOffset, transform.InverseTransformDirection(-weaponHoldVector));
                }

                //Atgeir Rotation fix
                switch (attack.m_attackAnimation)
                {
                    case "atgeir_attack":
                        transform.localRotation = transform.localRotation * Quaternion.AngleAxis(-20, Vector3.up) * Quaternion.AngleAxis(-7, Vector3.right);
                        break;
                }
                weaponForward = transform.forward;
                weaponSubPos = true;
            }
            else if (SteamVR_Actions.valheim_Grab.GetStateUp(SteamVR_Input_Sources.LeftHand) || 
                     SteamVR_Actions.valheim_Grab.GetStateUp(SteamVR_Input_Sources.RightHand)||
                     (isSpear() && (SpearManager.IsAiming() || SpearManager.isThrowing)))
            {
                _isTwoHanded = isTwoHanded.SingleHanded;
                weaponSubPos = false;
                ResetOffset();
            }
        }
        private bool isSpear()
        {
            return EquipScript.getRight() == EquipType.Spear || EquipScript.getRight() == EquipType.SpearChitin;
        }
        private void ResetOffset()
        {
            VrikCreator.rightHandConnector.localRotation = Quaternion.identity;
            VrikCreator.leftHandConnector.localRotation = Quaternion.identity;
            transform.position = rotSave.transform.position;
            transform.localRotation = rotSave.transform.localRotation;
        }

        private float GetHandAngleDiff(Transform mainHand, Transform refHand)
        {
            Vector3 localHandWieldDirection = (attack.m_attackAnimation == "spear_poke") ? new Vector3(0, 0.45f, 0.55f) : Vector3.forward;
            return Vector3.Dot(localHandWieldDirection, mainHand.InverseTransformPoint(refHand.position).normalized);
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
                    return weaponSubPos;
                default:
                    return isCurrentlyTwoHanded();
            }
        }
    }
}
