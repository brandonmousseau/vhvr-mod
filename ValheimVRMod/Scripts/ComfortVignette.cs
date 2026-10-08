using UnityEngine;
using UnityEngine.Rendering;
using ValheimVRMod.Utilities;

using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.Scripts
{
    // Darkens the edge of the view to reduce motion sickness. VHVRConfig.VignetteStrength() always applies; on top of
    // it the view closes in further while the game moves or turns the player, as far as the setting for that kind
    // of movement says. The strongest one wins, they do not add up.
    //
    // Movement is read from the camera rig, which only the game moves: the user moving their head moves the camera
    // inside the rig and must not bring up the vignette.
    //
    // Goes on the VR camera. The vignette is drawn by the world space UI camera, i. e. over the game world but
    // under the hands and the VR GUI.
    class ComfortVignette : MonoBehaviour
    {
        private const float MIN_VISIBLE_STRENGTH = 0.001f;
        // In metres, larger than any near clip plane in use so that the mesh is never clipped.
        private const float MESH_RADIUS = 2f;
        // The half angle of the view left clear, in degrees, at the weakest and at full strength.
        private const float WEAKEST_CLEAR_ANGLE = 50f;
        private const float STRONGEST_CLEAR_ANGLE = 20f;
        private const float EDGE_SOFTNESS_ANGLE = 12f;
        // In metres per second: no vignette below the first, full locomotion vignette from the second on.
        private const float MIN_LOCOMOTION_SPEED = 0.5f;
        private const float FULL_LOCOMOTION_SPEED = 4f;
        // In degrees per second, likewise.
        private const float MIN_TURN_SPEED = 10f;
        private const float FULL_TURN_SPEED = 90f;
        // An immediate snap turn lasts a single frame, so the vignette is held for a moment to be seen at all.
        private const float SNAP_TURN_MIN_ANGLE = 5f;
        private const float SNAP_TURN_HOLD_TIME = 0.2f;
        // Moving farther than this in one frame is a teleport or a respawn and not locomotion.
        private const float MAX_STEP_DISTANCE = 5f;
        private const float CLOSE_TIME = 0.1f;
        private const float OPEN_TIME = 0.4f;

        private static readonly int INNER_ID = Shader.PropertyToID("_Inner");
        private static readonly int OUTER_ID = Shader.PropertyToID("_Outer");

        private Transform rig;
        private FadingManager fadingManager;
        private GameObject vignette;
        private MeshRenderer vignetteRenderer;
        private Material material;
        private Vector3 lastRigPosition;
        private float lastRigYaw;
        private float snapTurnHoldTimer;
        private float strength;

        void Start()
        {
            Shader shader = null;
            try
            {
                shader = VRAssetManager.GetAsset<Shader>("VHVRVignette");
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
            }
            if (shader == null)
            {
                LogWarning("VHVRVignette shader is not in the asset bundle, the comfort vignette is unavailable.");
                enabled = false;
                return;
            }

            rig = transform.parent != null ? transform.parent : transform;
            fadingManager = GetComponent<FadingManager>();
            lastRigPosition = rig.position;
            lastRigYaw = rig.eulerAngles.y;

            vignette = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            vignette.name = "ComfortVignette";
            vignette.layer = LayerUtils.WORLDSPACE_UI_LAYER;
            Destroy(vignette.GetComponent<Collider>());
            vignette.transform.SetParent(transform, false);
            vignetteRenderer = vignette.GetComponent<MeshRenderer>();
            material = new Material(shader);
            vignetteRenderer.material = material;
            vignetteRenderer.receiveShadows = false;
            vignetteRenderer.shadowCastingMode = ShadowCastingMode.Off;
            vignetteRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            vignetteRenderer.enabled = false;

            Camera.onPreCull += OnCameraPreCull;
        }

        void OnDestroy()
        {
            Camera.onPreCull -= OnCameraPreCull;
            if (vignette != null)
            {
                Destroy(vignette);
            }
            if (material != null)
            {
                Destroy(material);
            }
        }

        void LateUpdate()
        {
            float deltaTime = Time.unscaledDeltaTime;
            if (deltaTime <= 0)
            {
                return;
            }

            float target = Mathf.Max(VHVRConfig.VignetteStrength(), GetMovementStrength(deltaTime));
            strength = Mathf.MoveTowards(strength, target, deltaTime / (target > strength ? CLOSE_TIME : OPEN_TIME));
            if (strength < MIN_VISIBLE_STRENGTH)
            {
                return;
            }

            // The primitive sphere has a diameter of 1, and the rig may be scaled.
            float parentScale = Mathf.Max(transform.lossyScale.x, 0.001f);
            vignette.transform.localScale = Vector3.one * (MESH_RADIUS * 2 / parentScale);
            float clearAngle = Mathf.Lerp(WEAKEST_CLEAR_ANGLE, STRONGEST_CLEAR_ANGLE, strength);
            material.SetFloat(INNER_ID, clearAngle * Mathf.Deg2Rad);
            material.SetFloat(OUTER_ID, (clearAngle + EDGE_SOFTNESS_ANGLE) * Mathf.Deg2Rad);
        }

        private float GetMovementStrength(float deltaTime)
        {
            Vector3 rigPosition = rig.position;
            float rigYaw = rig.eulerAngles.y;
            float stepDistance = Vector3.Distance(rigPosition, lastRigPosition);
            float turnAngle = Mathf.Abs(Mathf.DeltaAngle(lastRigYaw, rigYaw));
            lastRigPosition = rigPosition;
            lastRigYaw = rigYaw;

            // Teleporting, sleeping and dying fade the whole view already.
            if (Player.m_localPlayer == null || (fadingManager != null && fadingManager.IsFadingToBlack))
            {
                snapTurnHoldTimer = 0;
                return 0;
            }

            float locomotion = 0;
            if (stepDistance < MAX_STEP_DISTANCE)
            {
                locomotion =
                    Mathf.InverseLerp(MIN_LOCOMOTION_SPEED, FULL_LOCOMOTION_SPEED, stepDistance / deltaTime) *
                    VHVRConfig.VignetteOnLocomotion();
            }

            float turn = Mathf.InverseLerp(MIN_TURN_SPEED, FULL_TURN_SPEED, turnAngle / deltaTime);
            if (VHVRConfig.SnapTurnEnabled())
            {
                if (turnAngle >= SNAP_TURN_MIN_ANGLE)
                {
                    snapTurnHoldTimer = SNAP_TURN_HOLD_TIME;
                }
                else if (snapTurnHoldTimer > 0)
                {
                    snapTurnHoldTimer -= deltaTime;
                    turn = 1;
                }
                turn *= VHVRConfig.VignetteOnSnapTurn();
            }
            else
            {
                snapTurnHoldTimer = 0;
                turn *= VHVRConfig.VignetteOnSmoothTurn();
            }

            return Mathf.Max(locomotion, turn);
        }

        // Keeps the vignette out of the cameras that render for the flat screen.
        private void OnCameraPreCull(Camera camera)
        {
            if (vignetteRenderer != null)
            {
                vignetteRenderer.enabled = strength >= MIN_VISIBLE_STRENGTH && camera.stereoEnabled;
            }
        }
    }
}
