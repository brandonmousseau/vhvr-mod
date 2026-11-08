using UnityEngine;
using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.VRCore.Backends
{
    // A hand of the OpenXR rig, standing in for SteamVR's Hand and SteamVR_Behaviour_Pose on the prefab's hand
    // objects: it puts the hand at the controller's SteamVR raw pose (the default Pose action), in the space of its
    // parent, which is the tracking space as the head's. It moves the hand in Update() and again before rendering,
    // as SteamVR_Behaviour_Pose does. No controller or hand model is drawn.
    [DefaultExecutionOrder(-29000)]
    public sealed class OpenXRHand : MonoBehaviour
    {
        internal XRHand Hand;
        internal VRInputSource Source => Hand == XRHand.Left ? VRInputSource.LeftHand : VRInputSource.RightHand;

        internal bool PoseIsValid { get; private set; }

        // Worked out from the pose, for a runtime that gives no velocities.
        private Vector3 lastPosition;
        private Quaternion lastRotation = Quaternion.identity;
        private bool hasLast;
        private bool loggedOnce;
        private Vector3 estimatedVelocity;
        private Vector3 estimatedAngularVelocity;

        private static OpenXRInputBackend Backend => VRBackend.Active.Input as OpenXRInputBackend;

        private void OnEnable()
        {
            Application.onBeforeRender += updatePose;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= updatePose;
        }

        private void Update()
        {
            updatePose();
            estimateVelocity();
        }

        private void updatePose()
        {
            OpenXRInputBackend input = Backend;
            if (this == null || input == null)
            {
                return;
            }
            // An untracked controller leaves the hand where it was last seen, as SteamVR does.
            bool wasValid = PoseIsValid;
            PoseIsValid = input.TryGetRawPose(Hand, out Vector3 position, out Quaternion rotation);
            if (PoseIsValid != wasValid || !loggedOnce)
            {
                loggedOnce = true;
                LogInfo("OpenXR " + Hand + " hand: " + input.PoseStatus(Hand) + ".");
            }
            if (PoseIsValid)
            {
                transform.localPosition = position;
                transform.localRotation = rotation;
            }
        }

        private void estimateVelocity()
        {
            float dt = Time.unscaledDeltaTime;
            if (!PoseIsValid || dt <= 0)
            {
                hasLast = false;
                estimatedVelocity = estimatedAngularVelocity = Vector3.zero;
                return;
            }
            Vector3 position = transform.localPosition;
            Quaternion rotation = transform.localRotation;
            if (hasLast)
            {
                estimatedVelocity = (position - lastPosition) / dt;
                (rotation * Quaternion.Inverse(lastRotation)).ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180)
                {
                    angle -= 360;
                }
                estimatedAngularVelocity = float.IsInfinity(axis.x) ? Vector3.zero : axis * (angle * Mathf.Deg2Rad / dt);
            }
            lastPosition = position;
            lastRotation = rotation;
            hasLast = true;
        }

        // In world space, as Hand.GetTrackedObjectVelocity() gives them.
        internal Vector3 GetVelocity() => toWorld(velocity(out _), false);

        internal Vector3 GetAngularVelocity()
        {
            velocity(out Vector3 angular);
            return toWorld(angular, true);
        }

        // The runtime's velocities of the raw pose in tracking space, or the estimated ones when it reports none.
        private Vector3 velocity(out Vector3 angular)
        {
            Vector3 v = Vector3.zero;
            angular = Vector3.zero;
            OpenXRInputBackend input = Backend;
            if (input != null)
            {
                input.GetRawVelocity(Hand, out v, out angular);
            }
            if (v == Vector3.zero && angular == Vector3.zero)
            {
                angular = estimatedAngularVelocity;
                return estimatedVelocity;
            }
            return v;
        }

        private Vector3 toWorld(Vector3 v, bool direction)
        {
            Transform origin = transform.parent;
            if (origin == null)
            {
                return v;
            }
            return direction ? origin.TransformDirection(v) : origin.TransformVector(v);
        }
    }

    // SteamVR_LaserPointer (Unity/ValheimVR/Assets/SteamVR/Extras) for the OpenXR rig, on the same hand object and
    // with the same ray, holder offset, colors and click feedback, so that everything aiming with it (the GUI, the
    // bow, building, blocking) points the same way under both runtimes.
    public sealed class OpenXRLaserPointer : MonoBehaviour
    {
        public Color color = new Color(0.8773585f, 0.43454075f, 0.4780619f, 0f);
        public Color clickColor = new Color(1f, 0f, 0.08039522f, 1f);
        public float thickness = 0.002f;
        public float maxRaycastDistance = Mathf.Infinity;
        public int raycastLayerMask = Physics.DefaultRaycastLayers;
        public Vector3 rayStartingPosition = Vector3.zero;
        public Quaternion rayDirection = Quaternion.identity;
        public GameObject holder;
        public GameObject pointer;
        public event VRPointerEventHandler PointerTracking;

        internal VRInputSource Source;
        internal object Sender;

        private bool usePointer = true;
        private bool mouseButtonsLocked;
        private Transform previousContact;
        private MeshRenderer pointerRenderer;

        public void setVisible(bool visible)
        {
            if (pointerRenderer != null)
            {
                pointerRenderer.enabled = visible;
            }
        }

        public void setUsePointer(bool active) { usePointer = active; }
        public bool pointerIsActive() => usePointer;

        private void Start()
        {
            holder = new GameObject("LaserPointerHolder");
            holder.transform.parent = transform;
            holder.transform.localPosition = new Vector3(0.04f, -0.05f, -0.01f);
            holder.transform.localRotation = Quaternion.Euler(40f, 3f, 0f);

            pointer = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pointer.layer = 23; // Hands layer
            pointer.transform.parent = holder.transform;
            pointer.transform.localScale = new Vector3(thickness, thickness, 100f);
            pointer.transform.localPosition = new Vector3(0f, 0f, 50f);
            pointer.transform.localRotation = Quaternion.identity;
            Collider collider = pointer.GetComponent<Collider>();
            if (collider)
            {
                Destroy(collider);
            }
            pointerRenderer = pointer.GetComponent<MeshRenderer>();
            Shader shader = VRBackend.Active.GetShader("Custom/SteamVR_ClearAll");
            LogInfo("OpenXR laser pointer on " + name + (shader != null ? "." : ": no Custom/SteamVR_ClearAll shader."));
            var material = shader != null ? new Material(shader) : new Material(pointerRenderer.sharedMaterial);
            material.SetColor("_Color", color);
            pointerRenderer.material = material;
        }

        private void OnEnable()
        {
            mouseButtonsLocked = true;
        }

        private void Update()
        {
            if (holder == null)
            {
                return;
            }
            rayStartingPosition = holder.transform.position;
            rayDirection = holder.transform.rotation;
            if (!usePointer)
            {
                return;
            }
            VRBooleanAction leftClick = VRInputActions.valheim_LeftClick;
            VRBooleanAction rightClick = VRInputActions.valheim_RightClick;
            bool left = leftClick != null && leftClick.GetState(Source);
            bool right = rightClick != null && rightClick.GetState(Source);
            if (!left && !right)
            {
                // As in SteamVR_LaserPointer: buttons held when the pointer comes up count as released until both
                // are let go, so that switching action sets does not click.
                mouseButtonsLocked = false;
            }

            float dist = 100f;
            var ray = new Ray(rayStartingPosition, rayDirection * Vector3.forward);
            bool bHit = Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, raycastLayerMask);
            if (bHit && previousContact == hit.transform)
            {
                PointerTracking?.Invoke(Sender ?? this, new VRPointerEventArgs
                {
                    fromInputSource = Source,
                    distance = hit.distance,
                    target = hit.transform,
                    position = hit.point,
                    buttonStateLeft = !mouseButtonsLocked && left,
                    buttonStateRight = !mouseButtonsLocked && right,
                });
            }
            previousContact = bHit ? hit.transform : null;
            if (bHit && hit.distance < 100f)
            {
                dist = hit.distance;
            }

            if (left)
            {
                pointer.transform.localScale = new Vector3(thickness * 5f, thickness * 5f, dist);
                pointerRenderer.material.color = clickColor;
            }
            else
            {
                pointer.transform.localScale = new Vector3(thickness, thickness, dist);
                pointerRenderer.material.color = color;
            }
            pointer.transform.localPosition = new Vector3(0f, 0f, dist / 2f);
        }
    }
}
