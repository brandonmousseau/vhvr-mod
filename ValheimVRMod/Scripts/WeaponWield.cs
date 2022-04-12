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
            MainRight,
            MainLeft
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
                var mainHand = VRPlayer.rightHand;
                var offHand = VRPlayer.leftHand;
                float handAngleDiff = Vector3.Dot(GetHandWieldDirection(), VRPlayer.rightHand.transform.InverseTransformPoint(VRPlayer.leftHand.transform.position).normalized);
                if (_isTwoHanded == isTwoHanded.SingleHanded)
                {
                    if (handAngleDiff > 0.6f)
                    {
                        _isTwoHanded = isTwoHanded.MainRight;
                    }
                    else if (handAngleDiff < -0.6f)
                    {
                        _isTwoHanded = isTwoHanded.MainLeft;
                    }
                    else
                    {
                        return;
                    }
                }
                if (_isTwoHanded == isTwoHanded.MainLeft)
                {
                    mainHand = VRPlayer.leftHand;
                    offHand = VRPlayer.rightHand;
                }
                var handDist = Vector3.Distance(mainHand.transform.position, offHand.transform.position);
                var inversePosition = mainHand.transform.position - offHand.transform.position;
                var distLimit = 0f;
                var distMultiplier = 0f;
                var originMultiplier = -0.1f;
                var rotOffset = 180;
                bool isPoleArmOrSpear = false;
                switch (attack.m_attackAnimation)
                {
                    case "spear_poke":
                        distMultiplier = -0.09f;
                        distLimit = 0.09f;
                        originMultiplier = 0.2f;
                        isPoleArmOrSpear = true;
                        break;
                    case "atgeir_attack":
                        distMultiplier = -0.18f;
                        distLimit = 0.18f;
                        originMultiplier = -0.7f;
                        isPoleArmOrSpear = true;
                        break;
                }
                var CalculateDistance = inversePosition.normalized * distMultiplier / Mathf.Max(handDist, distLimit) - inversePosition.normalized * originMultiplier;
                ResetOffset();

                //VRIK Hand rotation
                if (_isTwoHanded == isTwoHanded.MainLeft)
                {
                    VrikCreator.mainHandConnector.LookAt(VrikCreator.offHandConnector, offHand.transform.up);
                    VrikCreator.offHandConnector.LookAt(VrikCreator.mainHandConnector, mainHand.transform.up);
                    VrikCreator.mainHandConnector.Rotate(Vector3.up, 180);
                    if (GetHandAngleDiff(offHand.transform, mainHand.transform) > 0)
                    {
                        VrikCreator.mainHandConnector.Rotate(Vector3.up, 180);
                    }
                    if (GetHandAngleDiff(mainHand.transform, offHand.transform) < 0)
                    {
                        VrikCreator.offHandConnector.Rotate(Vector3.up, 180);
                    }
                }
                else
                {
                    VrikCreator.mainHandConnector.LookAt(VrikCreator.offHandConnector, mainHand.transform.up);
                    VrikCreator.offHandConnector.LookAt(VrikCreator.mainHandConnector, offHand.transform.up);
                    VrikCreator.offHandConnector.Rotate(Vector3.up, 180);
                    if (GetHandAngleDiff(offHand.transform, mainHand.transform) > 0)
                    {
                        VrikCreator.offHandConnector.Rotate(Vector3.up, 180);
                    }
                    if (GetHandAngleDiff(mainHand.transform, offHand.transform) < 0)
                    {
                        VrikCreator.mainHandConnector.Rotate(Vector3.up, 180);
                    }
                }
                VrikCreator.mainHandConnector.Rotate(Vector3.right, 10);

                //weapon pos&rotation
                if (isSpear() && !VHVRConfig.SpearInverseWield())
                {
                    transform.position = mainHand.transform.position + CalculateDistance;
                    transform.LookAt(offHand.transform.position + inversePosition.normalized * 5, transform.up);
                    transform.localRotation = transform.localRotation * (rotSave.transform.localRotation) * Quaternion.AngleAxis(180, Vector3.right) * Quaternion.AngleAxis(rotOffset, transform.InverseTransformDirection(inversePosition));
                    transform.localRotation = transform.localRotation * (rotSave.transform.localRotation) * Quaternion.AngleAxis(180, Vector3.right);
                }
                else
                {
                    bool mainHandIsDominant = (VHVRConfig.LeftHanded() == (_isTwoHanded == isTwoHanded.MainLeft));
                    transform.position = mainHand.transform.position + CalculateDistance;
                    if (!mainHandIsDominant && !isPoleArmOrSpear) {
                        // Anchor the weapon on the dominant hand.
                        transform.position = transform.position - inversePosition.normalized * Mathf.Min(handDist, 0.15f);
                    }
                    transform.LookAt(mainHand.transform.position + inversePosition.normalized * 5, transform.up);
                    transform.localRotation = transform.localRotation * (rotSave.transform.localRotation) * Quaternion.AngleAxis(180, Vector3.right) * Quaternion.AngleAxis(rotOffset, transform.InverseTransformDirection(inversePosition));
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
            VrikCreator.mainHandConnector.localRotation = Quaternion.identity;
            VrikCreator.offHandConnector.localRotation = Quaternion.identity;
            transform.position = rotSave.transform.position;
            transform.localRotation = rotSave.transform.localRotation;
        }
        private Vector3 GetHandWieldDirection()
        {
            if (attack.m_attackAnimation == "spear_poke")
            {
                return new Vector3(0, 0.45f, 0.55f);
            }
            return new Vector3(0, 0, 1);
        }

        private float GetHandAngleDiff(Transform mainHand, Transform refHand)
        {
            return Vector3.Dot(GetHandWieldDirection(), mainHand.InverseTransformPoint(refHand.position).normalized);
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
