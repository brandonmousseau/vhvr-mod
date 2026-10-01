using RootMotion.FinalIK;
using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts
{
    // Turns the barber station into a mirror: the VR camera rig is placed in front of the character, facing it, and the
    // character's VRIK follows the headset and the controllers reflected in a vertical plane halfway between the two.
    // Like in a real mirror, the user's right hand moves the character's left hand, which is the one on the same side
    // of the view. The plane being vertical keeps heights as they are, so the character's head stays level with the
    // camera wherever the user moves it.
    static class BarberMirror
    {
        // From the VR camera to the character's eyes, when the mirror is entered.
        private const float MIRROR_DISTANCE = 1f;
        private static VRIK vrik;
        private static Vector3 planePoint;
        private static Vector3 planeNormal;
        private static Transform headSource;
        private static Transform leftHandSource;
        private static Transform rightHandSource;
        private static Transform headProxy;
        private static Transform leftHandProxy;
        private static Transform rightHandProxy;

        public static bool IsActive { get { return vrik != null; } }
        // Where the camera was and which way it faced the character when the mirror was entered.
        public static Vector3 ViewPoint { get; private set; }
        public static Vector3 ViewDirection { get { return -planeNormal; } }

        public static bool ShouldBeActive(VRIK localPlayerVrik)
        {
            return localPlayerVrik != null &&
                Player.m_localPlayer != null &&
                PlayerCustomizaton.IsBarberGuiVisible() &&
                VHVRConfig.UseVrControls();
        }

        // Expects the rig to be parented to the character already. Places it once so that the camera faces the
        // character horizontally from in front of its eyes; it stays put afterwards, leaving the view to the head
        // tracking. Pauses the local player's VRIK (see VrikCreator.PauseLocalPlayerVrik), which Exit leaves paused.
        public static void Enter(VRIK localPlayerVrik, Player player, Transform rig, Transform camera)
        {
            if (IsActive)
            {
                return;
            }
            Vector3 up = player.transform.up;
            Vector3 eye = player.m_eye.position;
            placeRig(player, rig, camera, up, eye);
            if (!VrikCreator.IsLocalPlayerVrikPaused())
            {
                VrikCreator.PauseLocalPlayerVrik();
            }

            planeNormal = Vector3.ProjectOnPlane(camera.position - eye, up);
            if (planeNormal.sqrMagnitude < 1e-6f)
            {
                planeNormal = Vector3.ProjectOnPlane(player.transform.forward, up);
            }
            planeNormal.Normalize();
            planePoint = (camera.position + eye) / 2;
            ViewPoint = camera.position;

            headSource = camera;
            leftHandSource = VrikCreator.localPlayerLeftHandConnector;
            rightHandSource = VrikCreator.localPlayerRightHandConnector;
            // Under the camera rig so that they have the scale of the camera and the controllers, which the local
            // poses of the VRIK targets are meant for.
            headProxy = createProxy("VHVRBarberMirrorHead", camera.parent);
            leftHandProxy = createProxy("VHVRBarberMirrorLeftHand", camera.parent);
            rightHandProxy = createProxy("VHVRBarberMirrorRightHand", camera.parent);
            updateProxies();

            vrik = localPlayerVrik;
            VrikCreator.BindLocalPlayerVrik(headProxy, leftHandProxy, rightHandProxy);
            vrik.solver.OnPreUpdate += updateProxies;
        }

        // Leaves the local player's VRIK paused, for the usual unpausing to restore its targets.
        public static void Exit()
        {
            if (vrik != null)
            {
                vrik.solver.OnPreUpdate -= updateProxies;
                VrikCreator.ReturnLocalPlayerVrikToPause();
            }
            vrik = null;
            destroyProxy(ref headProxy);
            destroyProxy(ref leftHandProxy);
            destroyProxy(ref rightHandProxy);
            headSource = leftHandSource = rightHandSource = null;
        }

        // Places the panel straight ahead of the point the camera viewed the character from, facing it.
        public static void GetGuiPose(Vector3 offset, Vector3 floorPoint, Vector3 up, out Vector3 position, out Quaternion rotation)
        {
            rotation = Quaternion.LookRotation(ViewDirection, up);
            position = floorPoint + Vector3.ProjectOnPlane(ViewPoint - floorPoint, up) + rotation * offset;
        }

        private static void placeRig(Player player, Transform rig, Transform camera, Vector3 up, Vector3 eye)
        {
            rig.localScale = Vector3.one / VrikCreator.ROOT_SCALE;
            rig.localRotation = Quaternion.identity;
            Vector3 facing = Vector3.ProjectOnPlane(player.transform.forward, up).normalized;
            Vector3 cameraForward = Vector3.ProjectOnPlane(camera.forward, up);
            if (cameraForward.sqrMagnitude > 1e-6f)
            {
                rig.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(cameraForward, -facing, up), up) * rig.rotation;
            }
            rig.position += eye + facing * MIRROR_DISTANCE - camera.position;
        }

        private static Transform createProxy(string name, Transform parent)
        {
            Transform proxy = new GameObject(name).transform;
            proxy.SetParent(parent, false);
            return proxy;
        }

        private static void destroyProxy(ref Transform proxy)
        {
            if (proxy != null)
            {
                Object.Destroy(proxy.gameObject);
            }
            proxy = null;
        }

        private static void updateProxies()
        {
            if (headSource == null || leftHandSource == null || rightHandSource == null ||
                headProxy == null || leftHandProxy == null || rightHandProxy == null)
            {
                return;
            }
            reflect(headSource, headProxy);
            // The reflection of a hand is the opposite hand of the reflected body.
            reflect(rightHandSource, leftHandProxy);
            reflect(leftHandSource, rightHandProxy);
        }

        // Reflecting the forward and up directions and rebuilding a rotation from them flips the local x axis as well,
        // which keeps the rotation proper. The result is the pose that the opposite device would have in the mirrored
        // motion, since a left and a right controller, as well as the camera, are mirror images in their local x.
        private static void reflect(Transform source, Transform proxy)
        {
            proxy.SetPositionAndRotation(
                source.position - 2 * Vector3.Dot(source.position - planePoint, planeNormal) * planeNormal,
                Quaternion.LookRotation(reflectDirection(source.forward), reflectDirection(source.up)));
        }

        private static Vector3 reflectDirection(Vector3 direction)
        {
            return direction - 2 * Vector3.Dot(direction, planeNormal) * planeNormal;
        }
    }
}
