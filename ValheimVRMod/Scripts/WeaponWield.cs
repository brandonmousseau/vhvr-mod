using System.Collections.Generic;
using System.ComponentModel;
using HarmonyLib;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts {
    public class WeaponWield : MonoBehaviour {
        private Attack attack;
        public bool itemIsTool;
        private bool weaponSubPos ;

        private Vector3 weaponForward;

        public string _name;
        private Transform selectedWeapon;
        private ItemDrop.ItemData item;
        private GameObject rotSave;
        private static isTwoHanded _isTwoHanded;
        private SteamVR_Input_Sources mainHandInputSource;

        private enum isTwoHanded
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
            Destroy(rotSave);
        }

        private void OnRenderObject() {
            WieldHandle();
            
        }

        private void WieldHandle()
        {
            switch (_name)
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
            if (_isTwoHanded == isTwoHanded.SingleHanded)
            {
                if (SteamVR_Actions.valheim_Grab.GetState(mainHandInputSource))
                {
                    ResetOffset();
                    transform.localRotation = transform.localRotation * Quaternion.AngleAxis(180, Vector3.right);
                    weaponForward = transform.forward;
                    weaponSubPos = true;
                }else if (weaponSubPos)
                {
                    ResetOffset();
                    weaponSubPos = false;
                }
            }
            
        }
        private void UpdateTwoHandedWield()
        {
            if (VHVRConfig.TwoHandedWield()) {
                if (SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand) && SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand)) {
                    var mainHand = VRPlayer.rightHand;
                    var offHand = VRPlayer.leftHand;
                    float handAngleDiff = Vector3.Dot(GetHandWieldDirection(), VRPlayer.rightHand.transform.InverseTransformPoint(VRPlayer.leftHand.transform.position).normalized);
                    if (_isTwoHanded == isTwoHanded.SingleHanded) {
                        if (handAngleDiff > 0.6f) {
                            _isTwoHanded = isTwoHanded.MainRight;
                            if (isSpear()) {
                                _isTwoHanded = isTwoHanded.MainLeft;
                            }
                        }
                        else if (handAngleDiff < -0.6f) {
                            _isTwoHanded = isTwoHanded.MainLeft;
                            if (isSpear()) {
                                _isTwoHanded = isTwoHanded.MainRight;
                            }
                        }
                        else {
                            return;
                        }
                    }
                    if (_isTwoHanded == isTwoHanded.MainLeft) {
                        mainHand = VRPlayer.leftHand;
                        offHand = VRPlayer.rightHand;
                    }
                    var offsetPos = Vector3.Distance(mainHand.transform.position, rotSave.transform.position);
                    var handDist = Vector3.Distance(mainHand.transform.position, offHand.transform.position);
                    var inversePosition = mainHand.transform.position - offHand.transform.position;
                    var distLimit = 0f;
                    var distMultiplier = 0f;
                    var originMultiplier = -0.1f;
                    var rotOffset = 180;
                    var handForward = new Vector3(0, -0.45f, 0.55f);
                    var handAvgVector = ((offHand.transform.TransformDirection(handForward) + mainHand.transform.TransformDirection(handForward)) / 2).normalized;
                    switch (attack.m_attackAnimation) {
                        case "spear_poke":
                            distMultiplier = 0.1f;
                            distLimit = 0.1f;
                            originMultiplier = 0.2f;
                            break;
                    }
                    var CalculateDistance = (inversePosition.normalized * distMultiplier / Mathf.Max(handDist, distLimit)) - inversePosition.normalized * originMultiplier;
                    ResetOffset();
                    transform.position = mainHand.transform.position + (CalculateDistance);
                    transform.LookAt(mainHand.transform.position + inversePosition.normalized * 5, transform.up);
                    transform.localRotation = transform.localRotation * (rotSave.transform.localRotation) * Quaternion.AngleAxis(180, Vector3.right) * Quaternion.AngleAxis(rotOffset, transform.InverseTransformDirection(inversePosition));

                    //Atgeir Rotation fix
                    if (attack.m_attackAnimation == "atgeir_attack")
                    {
                        transform.localRotation = (transform.localRotation * Quaternion.AngleAxis(-20, Vector3.up)) * Quaternion.AngleAxis(-5, Vector3.right);
                    }

                    weaponForward = transform.forward;
                    return;
                }
                else if (SteamVR_Actions.valheim_Grab.GetStateUp(SteamVR_Input_Sources.LeftHand) || SteamVR_Actions.valheim_Grab.GetStateUp(SteamVR_Input_Sources.RightHand)) {
                    _isTwoHanded = isTwoHanded.SingleHanded;
                    ResetOffset();
                }
            }
        }
        private bool isSpear()
        {
            return EquipScript.getRight() == EquipType.Spear;
        }
        private void ResetOffset()
        {
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

        public bool isCurrentlyTwoHanded()
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
        public Vector3 getWeaponForward()
        {
            return weaponForward;
        }

        
    }
}