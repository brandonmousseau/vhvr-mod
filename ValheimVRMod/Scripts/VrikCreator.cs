using RootMotion.FinalIK;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts {
    public class VrikCreator {
        // Valheim characters are 2 meters tall. Scale it down to make tracking less awkward.
        public const float ROOT_SCALE = 0.9f;

        public static readonly Vector3 leftUnequippedPosition = new Vector3(-0.027f, 0.05f, -0.18f);
        public static readonly Quaternion leftUnequippedRotation = Quaternion.Euler(0, 90f, 135f);
        private static readonly Vector3 leftUnequippedElbow = new Vector3(1, 0, 0);
        public static readonly Vector3 rightUnequippedPosition = new Vector3(0.027f, 0.05f, -0.18f);
        public static readonly Quaternion rightUnequippedRotation = Quaternion.Euler(0, -90f, -135f);
        private static readonly Vector3 rightUnequippedElbow = new Vector3(-1, 0, 0);

        private static readonly Vector3 leftEquippedPosition = new Vector3(-0.02f, 0.09f, -0.1f);
        private static readonly Quaternion leftEquippedRotation = Quaternion.Euler(0, 90, 170);
        private static readonly Vector3 leftEquippedElbow = new Vector3(1, -3f, 0);
        private static readonly Vector3 rightEquippedPosition = new Vector3(0.02f, 0.09f, -0.1f);
        private static readonly Quaternion rightEquippedRotation = Quaternion.Euler(0, -90, -170);
        private static readonly Vector3 rightEquippedElbow = new Vector3(-1, -3f, 0);

        private static Transform localPlayerCamera;
        private static Transform CameraRig { get { return localPlayerCamera.parent; } }

        public static Transform localPlayerRightHandConnector = null;
        public static Transform localPlayerLeftHandConnector = null;

        private static VRIK CreateTargets(GameObject playerObject)
        {
            VRIK vrik = playerObject.GetOrAddComponent<VRIK>();
            vrik.solver.leftArm.target = new GameObject().transform;
            vrik.solver.rightArm.target = new GameObject().transform;
            vrik.solver.leftLeg.target = new GameObject().transform;
            vrik.solver.rightLeg.target = new GameObject().transform;
            vrik.solver.spine.headTarget = new GameObject().transform;
            vrik.solver.spine.pelvisTarget = new GameObject().transform;
            if (playerObject == Player.m_localPlayer.gameObject)
            {
                localPlayerLeftHandConnector = new GameObject().transform;
                localPlayerRightHandConnector = new GameObject().transform;
            }
            return vrik;
        }

        private static bool InitializeTargts(VRIK vrik, Transform leftController, Transform rightController, Transform camera, Transform pelvis, bool isLocalPlayer)
        {
            vrik.AutoDetectReferences();

            if (vrik == null || vrik.references.head == null || vrik.references.leftHand == null || vrik.references.rightHand == null)
            {
                return false;
            }

            if (!isLocalPlayer)
            {
                vrik.references.leftThigh = null;
                vrik.references.leftCalf = null;
                vrik.references.leftFoot = null;
                vrik.references.rightThigh = null;
                vrik.references.rightCalf = null;
                vrik.references.rightFoot = null;
            }
            vrik.references.leftToes = null;
            vrik.references.rightToes = null;
            vrik.references.root.localScale = Vector3.one * ROOT_SCALE;

            Transform leftHandConnector = isLocalPlayer ? localPlayerLeftHandConnector : new GameObject().transform;
            leftHandConnector.SetParent(leftController, false);
            vrik.solver.leftArm.target.SetParent(leftHandConnector, false);

            Transform rightHandConnector = isLocalPlayer ? localPlayerRightHandConnector : new GameObject().transform;
            rightHandConnector.SetParent(rightController, false);
            vrik.solver.rightArm.target.SetParent(rightHandConnector, false);

            Transform head = vrik.solver.spine.headTarget;
            head.SetParent(camera);
            if (isLocalPlayer)
            {
                VrikCreator.localPlayerCamera = camera;
            }
            head.localPosition = new Vector3(0, -0.165f, -0.09f) * ROOT_SCALE;
            head.localRotation = Quaternion.Euler(0, 90, 20);

            vrik.solver.spine.pelvisTarget.SetParent(pelvis, worldPositionStays: false);
            vrik.solver.spine.pelvisTarget.localPosition = Vector3.zero;
            vrik.solver.spine.pelvisTarget.localRotation = Quaternion.identity;
            if (isLocalPlayer)
            {
                vrik.solver.leftLeg.target.parent = VRPlayer.leftFoot;
                vrik.solver.leftLeg.bendToTargetWeight = 0.5f;
                vrik.solver.rightLeg.target.parent = VRPlayer.rightFoot;
                vrik.solver.rightLeg.bendToTargetWeight = 0.5f;
                vrik.solver.rightLeg.swivelOffset = -30;
            }
            ResetPelvisAndFootTransform(vrik);

            // Avoid akward movements
            vrik.solver.spine.maintainPelvisPosition = 0f;
            vrik.solver.spine.pelvisPositionWeight = isLocalPlayer ? 0 : 1;
            vrik.solver.spine.pelvisRotationWeight = isLocalPlayer ? 0 : 1;
            vrik.solver.spine.bodyPosStiffness = 0f;
            vrik.solver.spine.bodyRotStiffness = 0f;
            // Force head to allow more vertical headlook
            vrik.solver.spine.headClampWeight = 0f;
            vrik.solver.leftLeg.positionWeight = vrik.solver.rightLeg.positionWeight = 0;
            vrik.solver.leftLeg.rotationWeight = vrik.solver.rightLeg.rotationWeight = 0;
            vrik.solver.plantFeet = false;
            vrik.solver.locomotion.weight = 0;
            vrik.solver.spine.maxRootAngle = 180;
            vrik.solver.spine.minHeadHeight = 0;

            return true;
        }

        private static bool IsPaused(VRIK vrik)
        {
            return
                vrik.solver.leftArm.target.parent == CameraRig &&
                vrik.solver.rightArm.target.parent == CameraRig &&
                vrik.solver.spine.headTarget.parent == CameraRig;
        }

        public static VRIK initialize(GameObject playerGameObject, Transform leftController, Transform rightController, Transform camera, Transform pelvis) {
            VRIK vrik = CreateTargets(playerGameObject);
            bool success = InitializeTargts(vrik, leftController, rightController, camera, pelvis, Player.m_localPlayer != null && playerGameObject == Player.m_localPlayer.gameObject);
            if (success)
            {
                return vrik;
            }
            GameObject.Destroy(vrik);
            return null;
        }

        public static void resetVrikHandTransform(Humanoid player) {
            
            VRIK vrik = player.GetComponent<VRIK>();
            var sync = player.GetComponent<VRPlayerSync>();

            if (vrik == null) {
                return;
            }

            if ((sync?.currentLeftWeapon != null && !IsHoldingBowInLeftHandAsLocalPlayer(player.gameObject)) || sync?.currentDualWieldWeapon != null)
            {
                vrik.solver.leftArm.target.localPosition = leftEquippedPosition;
                vrik.solver.leftArm.target.localRotation = leftEquippedRotation;
                vrik.solver.leftArm.palmToThumbAxis = leftEquippedElbow;
            }
            else
            {
                vrik.solver.leftArm.target.localPosition = leftUnequippedPosition;
                vrik.solver.leftArm.target.localRotation = leftUnequippedRotation;
                vrik.solver.leftArm.palmToThumbAxis = leftUnequippedElbow;
            }
            
            if ((sync?.currentRightWeapon != null && !IsHoldingBowInRightHandAsLocalPlayer(player.gameObject)) || sync?.currentDualWieldWeapon != null)
            {
                vrik.solver.rightArm.target.localPosition = rightEquippedPosition;
                vrik.solver.rightArm.target.localRotation = rightEquippedRotation;
                vrik.solver.rightArm.palmToThumbAxis = rightEquippedElbow;
            }
            else
            {
                vrik.solver.rightArm.target.localPosition = rightUnequippedPosition;
                vrik.solver.rightArm.target.localRotation = rightUnequippedRotation;
                vrik.solver.rightArm.palmToThumbAxis = rightUnequippedElbow;
            }

            if (player == Player.m_localPlayer)
            {
                vrik.solver.spine.pelvisTarget.localPosition = Vector3.zero;
                vrik.solver.spine.pelvisTarget.localRotation = Quaternion.identity;
                VRPlayer.leftHandBone.localPosition = vrik.solver.leftArm.target.localPosition;
                VRPlayer.leftHandBone.localRotation = vrik.solver.leftArm.target.localRotation;
                VRPlayer.rightHandBone.localPosition = vrik.solver.rightArm.target.localPosition;
                VRPlayer.rightHandBone.localRotation = vrik.solver.rightArm.target.localRotation;
            }
        }

        public static void ResetPelvisAndFootTransform(VRIK vrik)
        {
            vrik.solver.spine.pelvisTarget.localPosition = Vector3.zero;
            vrik.solver.spine.pelvisTarget.localRotation = Quaternion.identity;
            if (vrik.references.rightFoot == null)
            {
                return;
            }
            vrik.solver.leftLeg.target.localPosition = new Vector3(0, 0, -0.1f);
            vrik.solver.leftLeg.target.localRotation = Quaternion.Euler(315, 0, 180);
            vrik.solver.rightLeg.target.localPosition = new Vector3(0, 0, -0.1f);
            vrik.solver.rightLeg.target.localRotation = Quaternion.Euler(315, 0, 180);
        }

        public static Transform GetLocalPlayerDominantHandConnector()
        {
            return VHVRConfig.LeftHanded() ? VrikCreator.localPlayerLeftHandConnector : VrikCreator.localPlayerRightHandConnector;
        }

        public static Transform GetLocalPlayerNonDominantHandConnector()
        {
            return VHVRConfig.LeftHanded() ? VrikCreator.localPlayerRightHandConnector : VrikCreator.localPlayerLeftHandConnector;
        }
        public static void ResetHandConnectors()
        {
            localPlayerLeftHandConnector.localPosition = Vector3.zero;
            localPlayerLeftHandConnector.localRotation = Quaternion.identity;
            localPlayerRightHandConnector.localPosition = Vector3.zero;
            localPlayerRightHandConnector.localRotation = Quaternion.identity;
        }

        public static void PauseLocalPlayerVrik() {
            VRIK vrik = Player.m_localPlayer?.GetComponent<VRIK>();

            if (vrik == null)
            {
                return;
            }

            if (IsPaused(vrik))
            {
                LogUtils.LogWarning("Trying to pause VRIK while it is already paused.");
                return;
            }

            vrik.solver.leftArm.target.SetParent(localPlayerCamera.parent, true);
            vrik.solver.rightArm.target.SetParent(localPlayerCamera.parent, true);
            vrik.solver.spine.headTarget.SetParent(localPlayerCamera.parent, true);
            vrik.solver.spine.pelvisTarget.SetParent(localPlayerCamera.parent, true);
            vrik.solver.leftLeg.target.SetParent(localPlayerCamera.parent, true);
            vrik.solver.rightLeg.target.SetParent(localPlayerCamera.parent, true);
        }

        public static void UnpauseLocalPlayerVrik()
        {
            VRIK vrik = Player.m_localPlayer?.GetComponent<VRIK>();

            if (vrik == null)
            {
                return;
            }

            if (!IsPaused(vrik))
            {
                LogUtils.LogWarning("Trying to unpause VRIK while it is not yet paused.");
                return;
            }

            InitializeTargts(vrik, localPlayerLeftHandConnector.parent, localPlayerRightHandConnector.parent, localPlayerCamera, VRPlayer.pelvis, isLocalPlayer: true);
            resetVrikHandTransform(Player.m_localPlayer);
        }

        private static bool IsHoldingBowInLeftHandAsLocalPlayer(GameObject player)
        {
            return !VHVRConfig.LeftHanded() && player == Player.m_localPlayer.gameObject && EquipScript.getLeft() == EquipType.Bow;
        }

        private static bool IsHoldingBowInRightHandAsLocalPlayer(GameObject player)
        {
            return VHVRConfig.LeftHanded() && player == Player.m_localPlayer.gameObject && EquipScript.getLeft() == EquipType.Bow;
        }
    }
}
