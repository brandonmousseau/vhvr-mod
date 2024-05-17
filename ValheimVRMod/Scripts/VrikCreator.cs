using RootMotion.FinalIK;
using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts {
    public class VrikCreator {

        private static readonly Vector3 leftUnequippedPosition = new Vector3(-0.027f, 0.05f, -0.18f);
        private static readonly Quaternion leftUnequippedRotation = Quaternion.Euler(0, 90f, 135f);
        private static readonly Vector3 leftUnequippedEllbow = new Vector3(1, 0, 0);
        private static readonly Vector3 rightUnequippedPosition = new Vector3(0.027f, 0.05f, -0.18f);
        private static readonly Quaternion rightUnequippedRotation = Quaternion.Euler(0, -90f, -135f);
        private static readonly Vector3 rightUnequippedEllbow = new Vector3(-1, 0, 0);
        
        private static readonly Vector3 leftEquippedPosition = new Vector3(-0.02f, 0.09f, -0.1f);
        private static readonly Quaternion leftEquippedRotation = Quaternion.Euler(0, 90, -170);
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
        public static Transform camera;
        private static Transform CameraRig { get { return camera.parent; } }


        private static VRIK CreateTargets(GameObject playerObject)
        {
            VRIK vrik = playerObject.GetComponent<VRIK>() ?? playerObject.AddComponent<VRIK>();
            vrik.solver.leftArm.target = new GameObject().transform;
            vrik.solver.rightArm.target = new GameObject().transform;
            vrik.solver.spine.headTarget = new GameObject().transform;
            localPlayerLeftHandConnector = new GameObject().transform;
            localPlayerRightHandConnector = new GameObject().transform;
            return vrik;
        }

        private static void InitializeTargts(VRIK vrik, Transform leftController, Transform rightController, Transform camera, bool isLocalPlayer)
        {
            vrik.AutoDetectReferences();
            vrik.references.leftThigh = null;
            vrik.references.leftCalf = null;
            vrik.references.leftFoot = null;
            vrik.references.leftToes = null;
            vrik.references.rightThigh = null;
            vrik.references.rightCalf = null;
            vrik.references.rightFoot = null;
            vrik.references.rightToes = null;

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
            vrik.solver.spine.maxRootAngle = 180;

            //Avoid akward movements
            vrik.solver.spine.maintainPelvisPosition = 0f;
            vrik.solver.spine.pelvisPositionWeight = 0f;
            vrik.solver.spine.pelvisRotationWeight = 0f;
            vrik.solver.spine.bodyPosStiffness = 0f;
            vrik.solver.spine.bodyRotStiffness = 0f;
            //Force head to allow more vertical headlook
            vrik.solver.spine.headClampWeight = 0f;
        }


        private static bool IsPaused(VRIK vrik)
        {
            return
                vrik.solver.leftArm.target.parent == CameraRig &&
                vrik.solver.rightArm.target.parent == CameraRig &&
                vrik.solver.spine.headTarget.parent == CameraRig;
        }

        public static VRIK initialize(GameObject playerGameObject, Transform leftController, Transform rightController, Transform camera) {
            VRIK vrik = CreateTargets(playerGameObject);
            InitializeTargts(vrik, leftController, rightController, camera, Player.m_localPlayer != null && playerGameObject == Player.m_localPlayer.gameObject);
            return vrik;
        }

        public static void resetVrikHandTransform(Humanoid player) {
            
            VRIK vrik = player.GetComponent<VRIK>();   
            
            if (vrik == null) {
                return;
            }

            if (player.GetComponent<VRPlayerSync>()?.currentLeftWeapon != null) {
                if (VHVRConfig.LeftHanded() && player.GetComponent<VRPlayerSync>().currentLeftWeapon.name.StartsWith("Spear") && !VHVRConfig.SpearInverseWield()) {
                    vrik.solver.leftArm.target.localPosition = leftspearPosition;
                    vrik.solver.leftArm.target.localRotation = leftSpearRotation;
                    vrik.solver.leftArm.palmToThumbAxis = leftSpearEllbow;
                    return;
                }
                vrik.solver.leftArm.target.localPosition = leftEquippedPosition;
                vrik.solver.leftArm.target.localRotation = leftEquippedRotation;
                vrik.solver.leftArm.palmToThumbAxis = leftEquippedEllbow;
            }
            else {
                vrik.solver.leftArm.target.localPosition = leftUnequippedPosition;
                vrik.solver.leftArm.target.localRotation = leftUnequippedRotation;
                vrik.solver.leftArm.palmToThumbAxis = leftUnequippedEllbow;
            }
            
            if (player.GetComponent<VRPlayerSync>()?.currentRightWeapon != null) {
                if (! VHVRConfig.LeftHanded() && player.GetComponent<VRPlayerSync>().currentRightWeapon.name.StartsWith("Spear") && !VHVRConfig.SpearInverseWield()) {
                    vrik.solver.rightArm.target.localPosition = rightspearPosition;
                    vrik.solver.rightArm.target.localRotation = rightSpearRotation;
                    vrik.solver.rightArm.palmToThumbAxis = rightSpearEllbow;
                    return;
                }
                vrik.solver.rightArm.target.localPosition = rightEquippedPosition;
                vrik.solver.rightArm.target.localRotation = rightEquippedRotation;
                vrik.solver.rightArm.palmToThumbAxis = rightEquippedEllbow;
                return;
            }
            vrik.solver.rightArm.target.localPosition = rightUnequippedPosition;
            vrik.solver.rightArm.target.localRotation = rightUnequippedRotation;
            vrik.solver.rightArm.palmToThumbAxis = rightUnequippedEllbow;
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

            InitializeTargts(vrik, localPlayerLeftHandConnector.parent, localPlayerRightHandConnector.parent, camera, isLocalPlayer: true);
            resetVrikHandTransform(Player.m_localPlayer);
        }
    }
}
