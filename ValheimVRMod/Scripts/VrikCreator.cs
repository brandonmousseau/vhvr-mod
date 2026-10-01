using HarmonyLib;
using RootMotion.FinalIK;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts {
    public class VrikCreator {
        // Valheim characters are 2 meters tall. Scale it down to make tracking less awkward.
        public const float ROOT_SCALE = 0.9f;

        // A foot target's rotation relative to its calibrated foot, which faces forward.
        private static readonly Quaternion footTargetRotation = Quaternion.Euler(315, 0, 180);
        // VRIK takes the plane each knee bends in from the leg's pose when it starts, stored relative to the foot. A
        // leg that is nearly straight in that pose makes it arbitrary, so it is replaced with the plane of a knee
        // bending the way the calibrated foot points: thigh (down and forward) cross calf (down and back) points to
        // the foot's right, for either leg.
        private static readonly AccessTools.FieldRef<IKSolverVR.Leg, Vector3> bendNormalRelToTarget =
            AccessTools.FieldRefAccess<IKSolverVR.Leg, Vector3>("bendNormalRelToTarget");
        private static readonly Vector3 forwardKneeBendNormalRelToTarget =
            Quaternion.Inverse(footTargetRotation) * Vector3.right;

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

        // The head target's pose relative to the camera.
        private static readonly Vector3 headTargetLocalPosition = new Vector3(0, -0.165f, -0.09f) * ROOT_SCALE;
        private static readonly Quaternion headTargetLocalRotation = Quaternion.Euler(0, 90, 20);

        private static Transform localPlayerCamera;
        private static Transform CameraRig { get { return localPlayerCamera.parent; } }

        public static Transform localPlayerRightHandConnector = null;
        public static Transform localPlayerLeftHandConnector = null;

        public static void EnableFootTracking(VRIK vrik)
        {
            vrik.solver.leftLeg.rotationWeight = vrik.solver.rightLeg.rotationWeight = 1;
            vrik.solver.leftLeg.positionWeight = vrik.solver.rightLeg.positionWeight = 1;
            // The feet are taken to point forward when calibrated, so a tracked knee bends the way its foot points.
            // Blending in the root's facing would turn both knees the same way whenever the body faces elsewhere.
            vrik.solver.leftLeg.bendToTargetWeight = vrik.solver.rightLeg.bendToTargetWeight = 1;
            vrik.solver.rightLeg.swivelOffset = 0;
            // Before VRIK starts, it would overwrite these from its starting pose. This is called on every update
            // while the feet are tracked, so they are set once it has started.
            if (vrik.solver.initiated)
            {
                bendNormalRelToTarget(vrik.solver.leftLeg) = forwardKneeBendNormalRelToTarget;
                bendNormalRelToTarget(vrik.solver.rightLeg) = forwardKneeBendNormalRelToTarget;
            }
        }

        public static void DisableFootTracking(VRIK vrik)
        {
            vrik.solver.leftLeg.rotationWeight = vrik.solver.rightLeg.rotationWeight = 0;
            vrik.solver.leftLeg.positionWeight = vrik.solver.rightLeg.positionWeight = 0;
            vrik.solver.leftLeg.bendToTargetWeight = vrik.solver.rightLeg.bendToTargetWeight = 0.5f;
            // Untracked legs keep the knee directions VRIK records from its starting pose, where the right one is off.
            vrik.solver.rightLeg.swivelOffset = -30;
        }

        private static VRIK CreateTargets(GameObject playerObject)
        {
            VRIK vrik = playerObject.GetOrAddComponent<VRIK>();
            vrik.solver.leftArm.target = new GameObject().transform;
            vrik.solver.rightArm.target = new GameObject().transform;
            vrik.solver.leftLeg.target = new GameObject().transform;
            vrik.solver.rightLeg.target = new GameObject().transform;
            vrik.solver.spine.headTarget = new GameObject().transform;
            vrik.solver.spine.pelvisTarget = new GameObject().transform;
            if (Player.m_localPlayer != null && Player.m_localPlayer.gameObject == playerObject)
            {
                localPlayerLeftHandConnector = new GameObject().transform;
                localPlayerRightHandConnector = new GameObject().transform;
            }
            return vrik;
        }

        private static bool InitializeTargts(
            VRIK vrik, Transform leftController, Transform rightController, Transform camera, Transform pelvis, Transform leftFoot, Transform rightFoot, bool isLocalPlayer)
        {
            vrik.AutoDetectReferences();

            if (vrik == null || vrik.references.head == null || vrik.references.leftHand == null || vrik.references.rightHand == null)
            {
                return false;
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
            head.localPosition = headTargetLocalPosition;
            head.localRotation = headTargetLocalRotation;
            vrik.solver.spine.pelvisTarget.SetParent(pelvis, worldPositionStays: false);
            vrik.solver.spine.pelvisTarget.localPosition = Vector3.zero;
            vrik.solver.spine.pelvisTarget.localRotation = Quaternion.identity;
            vrik.solver.leftLeg.target.SetParent(leftFoot, worldPositionStays: true);
            vrik.solver.rightLeg.target.SetParent(rightFoot, worldPositionStays: true);
            if (isLocalPlayer && VRPlayer.vrPlayerInstance != null && VRPlayer.vrPlayerInstance.shouldTrackFeet())
            {
                EnableFootTracking(vrik);
            }
            else
            {
                DisableFootTracking(vrik);
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

        public static VRIK initialize(
            GameObject playerGameObject, Transform leftController, Transform rightController, Transform camera, Transform pelvis, Transform leftFoot, Transform rightFoot) {
            VRIK vrik = CreateTargets(playerGameObject);
            bool success =
                InitializeTargts(
                    vrik,
                    leftController, 
                    rightController, 
                    camera, 
                    pelvis,
                    leftFoot,
                    rightFoot,
                    Player.m_localPlayer != null && playerGameObject == Player.m_localPlayer.gameObject);
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

            if (sync != null && (UseEquippedHandRotation(sync.leftHandEquipType) || UseEquippedDualWeaponHandRotation(sync.mainHandEquipType)))
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

            if (sync != null && (UseEquippedHandRotation(sync.rightHandEquipType) || UseEquippedDualWeaponHandRotation(sync.mainHandEquipType)))
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
            vrik.solver.leftLeg.target.localPosition = new Vector3(0, 0, -0.1f);
            vrik.solver.leftLeg.target.localRotation = footTargetRotation;
            vrik.solver.rightLeg.target.localPosition = new Vector3(0, 0, -0.1f);
            vrik.solver.rightLeg.target.localRotation = footTargetRotation;
        }

        public static Transform GetLocalPlayerArrowHandConnector()
        {
            return VRPlayer.isRightHandMainWeaponHand ? VrikCreator.localPlayerRightHandConnector : VrikCreator.localPlayerLeftHandConnector;
        }

        public static Transform GetLocalPlayerBowHandConnector()
        {
            return VRPlayer.isRightHandMainWeaponHand ? VrikCreator.localPlayerLeftHandConnector : VrikCreator.localPlayerRightHandConnector;
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

        public static bool IsLocalPlayerVrikPaused()
        {
            VRIK vrik = Player.m_localPlayer?.GetComponent<VRIK>();
            return vrik != null && IsPaused(vrik);
        }

        // Drives the head and the hands of the paused local player VRIK from stand-ins for the camera and the hand
        // connectors, e. g. the reflections of BarberMirror. The pelvis and the feet are left untracked meanwhile.
        public static void BindLocalPlayerVrik(Transform camera, Transform leftHandConnector, Transform rightHandConnector)
        {
            VRIK vrik = Player.m_localPlayer?.GetComponent<VRIK>();
            if (vrik == null)
            {
                return;
            }
            vrik.solver.spine.headTarget.SetParent(camera, false);
            vrik.solver.spine.headTarget.localPosition = headTargetLocalPosition;
            vrik.solver.spine.headTarget.localRotation = headTargetLocalRotation;
            vrik.solver.leftArm.target.SetParent(leftHandConnector, false);
            vrik.solver.rightArm.target.SetParent(rightHandConnector, false);
            resetVrikHandTransform(Player.m_localPlayer);
            vrik.solver.spine.pelvisPositionWeight = 0;
            vrik.solver.spine.pelvisRotationWeight = 0;
            DisableFootTracking(vrik);
        }

        // Undoes BindLocalPlayerVrik, leaving the VRIK paused as PauseLocalPlayerVrik does.
        public static void ReturnLocalPlayerVrikToPause()
        {
            VRIK vrik = Player.m_localPlayer?.GetComponent<VRIK>();
            if (vrik == null || localPlayerCamera == null)
            {
                return;
            }
            vrik.solver.leftArm.target.SetParent(CameraRig, true);
            vrik.solver.rightArm.target.SetParent(CameraRig, true);
            vrik.solver.spine.headTarget.SetParent(CameraRig, true);
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

            InitializeTargts(
                vrik, localPlayerLeftHandConnector.parent, localPlayerRightHandConnector.parent, localPlayerCamera, VRPlayer.pelvis, VRPlayer.leftFoot, VRPlayer.rightFoot, isLocalPlayer: true);
            resetVrikHandTransform(Player.m_localPlayer);
        }

        private static bool UseEquippedHandRotation(EquipType equipType)
        {
            switch (equipType)
            {
                case EquipType.None:
                case EquipType.Claws:
                case EquipType.Bow:
                    return false;
                default:
                    return true;
            }
        }

        private static bool UseEquippedDualWeaponHandRotation(EquipType equipType)
        {
            return equipType == EquipType.DualAxes || equipType == EquipType.DualKnives;
        }
    }
}
