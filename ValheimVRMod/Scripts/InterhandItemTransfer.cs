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
        private const float HOLD_TIME_REQUIRED = 0.125f;           // seconds both must grip before transfer

        private enum TransferState
        {
            Idle,
            BothGripping,
            Transferring
        }

        private TransferState state = TransferState.Idle;
        private float bothGrippingTime = 0f;

        private static readonly EquipType[] TRANSFERABLE_TYPES =
        {
            EquipType.Pickaxe,
            EquipType.Shield,
            EquipType.Bow,
            EquipType.Lantern,
            EquipType.Tankard,
            EquipType.Knife,
            EquipType.ThrowObject
        };

        private void Update()
        {
            if (Player.m_localPlayer == null || !VRControls.mainControlsActive)
            {
                state = TransferState.Idle;
                return;
            }

            switch (state)
            {
                case TransferState.Idle:
                    CheckForGripStart();
                    break;
                case TransferState.BothGripping:
                    CheckForTransfer();
                    break;
            }
        }

        private void CheckForGripStart()
        {
            if (!SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand)
                || !SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand))
                return;

            if (!IsTransferableEquipped())
                return;

            if (!HandsCloseAndAligned())
                return;

            state = TransferState.BothGripping;
            bothGrippingTime = 0f;
        }

        private void CheckForTransfer()
        {
            // If either hand moves too far apart, cancel
            if (!HandsCloseAndAligned())
            {
                state = TransferState.Idle;
                return;
            }

            var rightGrip = SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand);
            var leftGrip = SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand);

            // Both still gripping — accumulate time
            if (rightGrip && leftGrip)
            {
                bothGrippingTime += Time.deltaTime;
                return;
            }

            // Need to have held long enough
            if (bothGrippingTime < HOLD_TIME_REQUIRED)
            {
                state = TransferState.Idle;
                return;
            }

            // One hand released — determine which
            bool rightReleased = !rightGrip && leftGrip;
            bool leftReleased = !leftGrip && rightGrip;

            if (rightReleased && leftReleased)
            {
                // Both released simultaneously — cancel
                state = TransferState.Idle;
                return;
            }

            // Check receiving hand is empty
            if (leftReleased ? 
                (VRPlayer.leftHandItem == null || VRPlayer.rightHandItem != null) :
                (VRPlayer.leftHandItem != null || VRPlayer.rightHandItem == null))
            {
                state = TransferState.Idle;
                return;
            }

            state = TransferState.Transferring;
            DoTransfer(leftReleased);
        }

        private void DoTransfer(bool toRightHand)
        {
            var player = Player.m_localPlayer;

            var ulnarsOpposite =
                Vector3.Dot(VRPlayer.leftHand.transform.forward, VRPlayer.rightHand.transform.forward) < 0;
            var newIsMainWeaponUlnar = LocalWeaponWield.IsWeaponPointingUlnar ^ ulnarsOpposite;
            var newIsSecondaryWeaponUlnar = FistCollision.ShouldSecondaryKnifeHoldInverse ^ ulnarsOpposite;
            var isTransferringMainWeapon = (EquipScript.getLeft() == EquipType.None);
            var isTransferringParryingKnife = (EquipScript.getLeft() == EquipType.Knife);

            VRPlayer.offHandWield = !VRPlayer.offHandWield;

            VRPlayer.rightHand.hapticAction.Execute(0, 0.3f, 100, 0.5f, SteamVR_Input_Sources.RightHand);
            VRPlayer.leftHand.hapticAction.Execute(0, 0.3f, 100, 0.5f, SteamVR_Input_Sources.LeftHand);

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

            if (isTransferringMainWeapon)
            {
                LocalWeaponWield.IsWeaponPointingUlnar = newIsMainWeaponUlnar;
            }
            else if (isTransferringParryingKnife)
            {
                FistCollision.ShouldSecondaryKnifeHoldInverse = newIsSecondaryWeaponUlnar;
            }

            state = TransferState.Idle;
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
            var rightType = EquipScript.getRight();
            var leftType = EquipScript.getLeft();

            foreach (var t in TRANSFERABLE_TYPES)
            {
                if (rightType == t || leftType == t)
                    return true;
            }
            return false;
        }
    }
}