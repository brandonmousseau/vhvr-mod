using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using ValheimVRMod.VRCore.UI;
using Valve.VR;

namespace ValheimVRMod.Scripts
{
    public class InterhandItemTransfer : MonoBehaviour
    {
        // Tuning constants
        private const float HANDS_CLOSE_DISTANCE = 0.2f;
        private const float FORWARD_ALIGNMENT_TOLERANCE = 0.6f;  // dot product threshold (same or opposite)
        private const float POSITION_ALIGNMENT_TOLERANCE = 0.25f; // how far off-axis the hands can be

        private bool leftHandPreparingToTransfer;
        private bool rightHandPreparingToTransfer;

        private static readonly EquipType[] TRANSFERABLE_TYPES =
        {
            EquipType.Axe,
            EquipType.Bow,
            EquipType.Club,
            EquipType.Cultivator,
            EquipType.Fishing,
            EquipType.Hammer,
            EquipType.Hoe,
            EquipType.Knife,
            EquipType.Lantern,
            EquipType.Magic,
            EquipType.Pickaxe,
            EquipType.Scythe,
            EquipType.Shield,
            EquipType.Torch,
            EquipType.Tankard,
            EquipType.ThrowObject
        };

        private void Update()
        {
            if (Player.m_localPlayer == null ||
                !VRControls.mainControlsActive ||
                !IsTransferableEquipped() ||
                !HandsCloseAndAligned())
            {
                leftHandPreparingToTransfer = rightHandPreparingToTransfer = false;
                return;
            }

            bool leftReleased = SteamVR_Actions.valheim_Grab.GetStateUp(SteamVR_Input_Sources.LeftHand);
            bool rightReleased = SteamVR_Actions.valheim_Grab.GetStateUp(SteamVR_Input_Sources.RightHand);

            if (leftReleased || rightReleased)
            {
                if (leftHandPreparingToTransfer && rightHandPreparingToTransfer)
                {
                    if (leftReleased && !rightReleased)
                    {
                        DoTransfer(toRightHand: true);
                    }
                    else if (!leftReleased && rightReleased)
                    {
                        DoTransfer(toRightHand: false);
                    }
                }
                leftHandPreparingToTransfer = rightHandPreparingToTransfer = false;
                return;
            }

            if (SteamVR_Actions.valheim_Grab.GetStateDown(SteamVR_Input_Sources.LeftHand))
            {
                leftHandPreparingToTransfer = true;
            }

            if (SteamVR_Actions.valheim_Grab.GetStateDown(SteamVR_Input_Sources.RightHand))
            {
                rightHandPreparingToTransfer = true;
            }
        }

        private void DoTransfer(bool toRightHand)
        {
            if (toRightHand ?
                (VRPlayer.leftHandItem == null || VRPlayer.rightHandItem != null) :
                (VRPlayer.leftHandItem != null || VRPlayer.rightHandItem == null))
            {
                return;
            }

            var player = Player.m_localPlayer;

            var ulnarsOpposite =
                Vector3.Dot(VRPlayer.leftHand.transform.forward, VRPlayer.rightHand.transform.forward) < 0;
            var newIsSecondaryWeaponUlnar = FistCollision.ShouldSecondaryKnifeHoldInverse ^ ulnarsOpposite;
            var isTransferringMainWeapon = (EquipScript.CurrentOffHandEquipType() == EquipType.None);
            var isTransferringParryingKnife = (EquipScript.CurrentOffHandEquipType() == EquipType.Knife);

            VRPlayer.offHandWield = !VRPlayer.offHandWield;

            VRPlayer.rightHand.hapticAction.Execute(0, 0.3f, 100, 0.5f, SteamVR_Input_Sources.RightHand);
            VRPlayer.leftHand.hapticAction.Execute(0, 0.3f, 100, 0.5f, SteamVR_Input_Sources.LeftHand);

            if (isTransferringMainWeapon)
            {
                LocalWeaponWield.NextWeaponHoldShouldStartPointingUlnar =
                    LocalWeaponWield.IsWeaponPointingUlnar ^ ulnarsOpposite;
            }

            // Force re-equip to apply new handedness
            var rightHash = player.m_visEquipment.m_currentRightItemHash;
            if (rightHash != 0)
            {
                player.m_visEquipment.SetRightHandEquipped(0);
                player.m_visEquipment.SetRightHandEquipped(rightHash);
            }

            var leftHash = player.m_visEquipment.m_currentLeftItemHash;
            var leftVariant = player.m_visEquipment.m_currentLeftItemVariant;
            if (leftHash != 0)
            {
                player.m_visEquipment.SetLeftHandEquipped(0, 0);
                player.m_visEquipment.SetLeftHandEquipped(leftHash, leftVariant);
            }

            if (isTransferringParryingKnife)
            {
                FistCollision.ShouldSecondaryKnifeHoldInverse = newIsSecondaryWeaponUlnar;
            }
        }

        private bool HandsCloseAndAligned()
        {
            var rh = VRPlayer.rightHand.transform;
            var lh = VRPlayer.leftHand.transform;

            if (Vector3.Distance(rh.position, lh.position) > HANDS_CLOSE_DISTANCE)
                return false;

            // Check forward alignment - same direction or opposite
            float forwardDot = Vector3.Dot(rh.forward, lh.forward);
            if (Mathf.Abs(forwardDot) < FORWARD_ALIGNMENT_TOLERANCE)
                return false;

            // Check hands are positioned along the forward axis (not side by side)
            Vector3 handAxis = (lh.position - rh.position);
            Vector3 avgForward =
                forwardDot >= 0?
                (rh.forward + lh.forward).normalized :
                (rh.forward - lh.forward).normalized;
            float axisAlignment = Mathf.Abs(Vector3.Dot(handAxis.normalized, avgForward));
            if (axisAlignment < (1f - POSITION_ALIGNMENT_TOLERANCE))
                return false;

            return true;
        }

        private bool IsTransferableEquipped()
        {
            var rightType = EquipScript.CurrentMainHandEquipType();
            var leftType = EquipScript.CurrentOffHandEquipType();

            foreach (var t in TRANSFERABLE_TYPES)
            {
                if (rightType == t || leftType == t)
                    return true;
            }
            return false;
        }
    }
}