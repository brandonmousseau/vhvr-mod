using RootMotion.Demos;
using RootMotion.FinalIK;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts {
    public class VrikCreator {
        public const float ROOT_SCALE = 1;

        private static readonly Vector3 leftUnequippedPosition = new Vector3(-0.027f, 0.05f, -0.18f);
        private static readonly Quaternion leftUnequippedRotation = Quaternion.Euler(0, 90f, 135f);
        private static readonly Vector3 leftUnequippedEllbow = new Vector3(1, 0, 0);
        private static readonly Vector3 rightUnequippedPosition = new Vector3(0.027f, 0.05f, -0.18f);
        private static readonly Quaternion rightUnequippedRotation = Quaternion.Euler(0, -90f, -135f);
        private static readonly Vector3 rightUnequippedEllbow = new Vector3(-1, 0, 0);
        
        private static readonly Vector3 leftEquippedPosition = new Vector3(-0.02f, 0.09f, -0.1f);
        private static readonly Quaternion leftEquippedRotation = Quaternion.Euler(0, 90, 170);
        private static readonly Vector3 leftEquippedEllbow = new Vector3(1, -3f, 0);
        private static readonly Vector3 rightEquippedPosition = new Vector3(0.02f, 0.09f, -0.1f);
        private static readonly Quaternion rightEquippedRotation = Quaternion.Euler(0, -90, -170);
        private static readonly Vector3 rightEquippedEllbow = new Vector3(-1, -3f, 0);

        private static readonly Vector3 leftspearPosition = new Vector3(-0.02f, 0.06f, -0.15f);
        private static readonly Quaternion leftSpearRotation = Quaternion.Euler(0, 90, 140);
        private static readonly Vector3 leftSpearEllbow = new Vector3(1, -3f, 0);
        private static readonly Vector3 rightspearPosition = new Vector3(0.02f, 0.06f, -0.15f);
        private static readonly Quaternion rightSpearRotation = Quaternion.Euler(0, -90, -140);
        private static readonly Vector3 rightSpearEllbow = new Vector3(-1, -3f, 0);

        public static Transform localPlayerRightHandConnector = null;
        public static Transform localPlayerLeftHandConnector = null;
        public static Transform localPlayerLeftThigh = null;
        public static Transform localPlayerLeftCalf = null;
        public static Transform localPlayerLeftFoot = null;
        public static Transform localPlayerRightThigh = null;
        public static Transform localPlayerRightCalf = null;
        public static Transform localPlayerRightFoot = null;

        public static Transform camera;
        private static Transform CameraRig { get { return camera.parent; } }

        private static VRIK CreateTargets(GameObject playerObject)
        {
            VRIK vrik = playerObject.GetComponent<VRIK>() ?? playerObject.AddComponent<VRIK>();
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

        private static void InitializeTargts(VRIK vrik, Transform leftController, Transform rightController, Transform camera, Transform pelvis, bool isLocalPlayer)
        {
            vrik.AutoDetectReferences();
            if (isLocalPlayer)
            {
                localPlayerLeftThigh = vrik.references.leftThigh;
                localPlayerLeftCalf = vrik.references.leftCalf;
                localPlayerLeftFoot = vrik.references.leftFoot;
                localPlayerRightThigh = vrik.references.rightThigh;
                localPlayerRightCalf = vrik.references.rightCalf;
                localPlayerRightFoot = vrik.references.rightFoot;
            }
            if (!isLocalPlayer || !VHVRConfig.TrackFeet())
            {
                DisconnectLegs(vrik);
            }
            vrik.references.leftToes = null;
            vrik.references.rightToes = null;
            vrik.references.root.localScale = Vector3.one * ROOT_SCALE;

            Transform leftHandConnector = isLocalPlayer ? VrikCreator.localPlayerLeftHandConnector : new GameObject().transform;
            leftHandConnector.SetParent(leftController, false);
            vrik.solver.leftArm.target.SetParent(leftHandConnector, false);

            Transform rightHandConnector = isLocalPlayer ? VrikCreator.localPlayerRightHandConnector : new GameObject().transform;
            rightHandConnector.SetParent(rightController, false);
            vrik.solver.rightArm.target.SetParent(rightHandConnector, false);

            Transform head = vrik.solver.spine.headTarget;
            head.SetParent(camera);
            if (isLocalPlayer)
            {
                VrikCreator.camera = camera;
            }
            head.localPosition = new Vector3(0, -0.165f, -0.09f);
            head.localRotation = Quaternion.Euler(0, 90, 20);
            vrik.solver.spine.pelvisTarget.SetParent(pelvis, worldPositionStays: false);
            vrik.solver.spine.maxRootAngle = 180;
            vrik.solver.spine.minHeadHeight = 0.25f;

            //Avoid akward movements
            vrik.solver.spine.maintainPelvisPosition = 0f;
            vrik.solver.spine.pelvisPositionWeight = isLocalPlayer ? 0 : 1;
            vrik.solver.spine.pelvisRotationWeight = isLocalPlayer ? 0 : 1;
            vrik.solver.spine.bodyPosStiffness = 0f;
            vrik.solver.spine.bodyRotStiffness = 0f;
            //Force head to allow more vertical headlook
            vrik.solver.spine.headClampWeight = 0f;
            vrik.solver.leftLeg.positionWeight = vrik.solver.rightLeg.positionWeight = 0;
            vrik.solver.leftLeg.rotationWeight = vrik.solver.rightLeg.rotationWeight = 0;
            vrik.solver.plantFeet = false;
            vrik.solver.locomotion.weight = 0;
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
            InitializeTargts(vrik, leftController, rightController, camera, pelvis, Player.m_localPlayer != null && playerGameObject == Player.m_localPlayer.gameObject);
            return vrik;
        }

        public static void resetVrikHandTransform(Humanoid player) {
            
            VRIK vrik = player.GetComponent<VRIK>();
            var sync = player.GetComponent<VRPlayerSync>();

            if (vrik == null) {
                return;
            }
            
            if (sync.IsLeftHanded() && sync.currentLeftWeapon != null && sync.currentLeftWeapon.name.StartsWith("Spear") && !sync.InverseHold()) {
                vrik.solver.leftArm.target.localPosition = leftspearPosition;
                vrik.solver.leftArm.target.localRotation = leftSpearRotation;
                vrik.solver.leftArm.palmToThumbAxis = leftSpearEllbow;
            } else if (sync?.currentLeftWeapon != null || sync?.currentDualWieldWeapon != null) {
                vrik.solver.leftArm.target.localPosition = leftEquippedPosition;
                vrik.solver.leftArm.target.localRotation = leftEquippedRotation;
                vrik.solver.leftArm.palmToThumbAxis = leftEquippedEllbow;
            } else {
                vrik.solver.leftArm.target.localPosition = leftUnequippedPosition;
                vrik.solver.leftArm.target.localRotation = leftUnequippedRotation;
                vrik.solver.leftArm.palmToThumbAxis = leftUnequippedEllbow;
            }
            
            if (!sync.IsLeftHanded() && sync.currentRightWeapon != null && sync.currentRightWeapon.name.StartsWith("Spear") && !sync.InverseHold()) {
                vrik.solver.rightArm.target.localPosition = rightspearPosition;
                vrik.solver.rightArm.target.localRotation = rightSpearRotation;
                vrik.solver.rightArm.palmToThumbAxis = rightSpearEllbow;
            } else if (sync?.currentRightWeapon != null || sync?.currentDualWieldWeapon != null) {
                vrik.solver.rightArm.target.localPosition = rightEquippedPosition;
                vrik.solver.rightArm.target.localRotation = rightEquippedRotation;
                vrik.solver.rightArm.palmToThumbAxis = rightEquippedEllbow;
            } else {
                vrik.solver.rightArm.target.localPosition = rightUnequippedPosition;
                vrik.solver.rightArm.target.localRotation = rightUnequippedRotation;
                vrik.solver.rightArm.palmToThumbAxis = rightUnequippedEllbow;
            }

            if (player == Player.m_localPlayer)
            {
                vrik.solver.spine.pelvisTarget.localPosition = Vector3.zero;
                vrik.solver.spine.pelvisTarget.localRotation = Quaternion.identity;
            }
        }

        public static void DisconnectLegs(VRIK vrik)
        {
            vrik.references.leftThigh = null;
            vrik.references.leftCalf = null;
            vrik.references.leftFoot = null;
            vrik.references.rightThigh = null;
            vrik.references.rightCalf = null;
            vrik.references.rightFoot = null;
        }

        public static void ReconnectLocalPlayerLegs()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }
            VRIK vrik = player.GetComponentInChildren<VRIK>();
            if (vrik == null)
            {
                return;
            }
            vrik.references.leftThigh = localPlayerLeftThigh;
            vrik.references.leftCalf = localPlayerLeftCalf;
            vrik.references.leftFoot = localPlayerLeftFoot;
            vrik.references.rightThigh = localPlayerRightThigh;
            vrik.references.rightCalf = localPlayerRightCalf;
            vrik.references.rightFoot = localPlayerRightFoot;
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

            vrik.solver.leftArm.target.SetParent(camera.parent, true);
            vrik.solver.rightArm.target.SetParent(camera.parent, true);
            vrik.solver.spine.headTarget.SetParent(camera.parent, true);
            vrik.solver.spine.pelvisTarget.SetParent(camera.parent, true);
            vrik.solver.leftLeg.target.SetParent(camera.parent, true);
            vrik.solver.rightLeg.target.SetParent(camera.parent, true);
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

            InitializeTargts(vrik, localPlayerLeftHandConnector.parent, localPlayerRightHandConnector.parent, camera, VRPlayer.pelvis, isLocalPlayer: true);
            resetVrikHandTransform(Player.m_localPlayer);
        }
    }
}
