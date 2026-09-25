using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts
{
    /**
     * Shows a full-view overlay while the player is protected by a magic barrier.
     *
     * More than one barrier can be up at a time (the Staff of Protection bubble and Northern Vengeance, which has
     * the larger bubble), so this component is a container: it lives on the VR camera and keeps one Barrier per
     * active shield, each owning its own panels, material and signature tint. Nothing here may be static or shared
     * between barriers.
     *
     * FullTexture mode mirrors the bubble's own material onto an inside-out sphere with the radius of the bubble's
     * dome in the world. The sphere is centered on the player character's hips, but keeps a
     * fixed world rotation, so the pattern stays put in the world like the bubble itself instead of turning with
     * the head.
     *
     * SimpleColor mode shows a tinted panel, pulsing faster as the barrier runs out.
     *
     * Either way only the barrier that expires first is shown. Two pulses at different rates blended into one
     * panel read as neither, and two nested spheres around the view are too much to look through. When the first
     * expires the overlay switches to the next one, which is itself the "one barrier left" cue.
     */
    class MagicBarrierVisualEffect : MonoBehaviour
    {
        // Signature tints, one dedicated to each type of barrier so that a tint always means the same barrier,
        // whichever barriers happen to be up and whatever size they turn out to be.
        private static readonly Color PROTECTION_TINT = new Color(0.375f, 0.125f, 0.3f);
        private static readonly Color VENGEANCE_TINT = new Color(0.125f, 0.3f, 0.5f);
        private const float OVERLAY_ALPHA = 0.2f;
        private const float SIMPLE_COLOR_PANEL_DISTANCE = 0.125f;
        private const float SIMPLE_COLOR_PANEL_SIZE = 0.5f;
        // Height of the FullTexture sphere's center above the player character's feet when the character has no
        // hip bone to center it on: about hip height when standing straight.
        private const float FALLBACK_SPHERE_CENTER_HEIGHT = 0.9f;
        // Resolution of the FullTexture sphere.
        private const int SPHERE_LONGITUDE_SEGMENTS = 32;
        private const int SPHERE_LATITUDE_SEGMENTS = 16;

        // A unit sphere facing inwards, shared by every barrier's FullTexture sphere. Generated rather than taken
        // from the bubble or a primitive, whose meshes need not be readable in a player build.
        private static Mesh insideOutSphere;

        private readonly List<Barrier> barriers = new List<Barrier>();
        private Camera vrCamera;

        void Awake()
        {
            vrCamera = GetComponent<Camera>();
        }

        // The character's hip bone, looked up once per local player.
        private Player hipsOwner;
        private Transform hips;
        // Set while the FullTexture spheres are hidden from the camera being rendered.
        private bool spheresHiddenFromCurrentCamera;

        void OnEnable()
        {
            Camera.onPreCull += OnCameraPreCull;
            Camera.onPostRender += OnCameraPostRender;
        }

        void OnDisable()
        {
            UnsubscribeFromCameras();
        }

        private void UnsubscribeFromCameras()
        {
            Camera.onPreCull -= OnCameraPreCull;
            Camera.onPostRender -= OnCameraPostRender;
            if (spheresHiddenFromCurrentCamera)
            {
                SetFullTextureSpheresRendered(true);
            }
        }

        private void OnCameraPreCull(Camera camera)
        {
            if (camera == vrCamera)
            {
                // Moves the FullTexture sphere to the character's hips right before the VR camera renders, i. e.
                // where they are drawn this frame. Done in FixedUpdate it would visibly trail behind.
                Player player = Player.m_localPlayer;
                if (player == null)
                {
                    return;
                }
                Vector3 center = GetSphereCenter(player);
                foreach (Barrier barrier in barriers)
                {
                    barrier.CenterFullTextureSphere(center);
                }
            }
            else if (ViewsCharacterFromOutside(camera))
            {
                // A flat screen camera looking at the character from outside shows the vanilla bubble, as other
                // players see it. Seen from outside, the sphere would only add its inward facing far half on top of
                // the bubble. Culling happens per camera after onPreCull, so a renderer disabled here is skipped by
                // this camera alone, and OnCameraPostRender() turns it back on for the VR camera's next frame.
                SetFullTextureSpheresRendered(false);
                spheresHiddenFromCurrentCamera = true;
            }
        }

        private void OnCameraPostRender(Camera camera)
        {
            if (spheresHiddenFromCurrentCamera)
            {
                SetFullTextureSpheresRendered(true);
                spheresHiddenFromCurrentCamera = false;
            }
        }

        // The character's hips as last posed, whether by the animation or by VRIK, which solves in LateUpdate,
        // before any camera culls. The tracked pelvis is not used: it follows a waist tracker, not the character.
        private Vector3 GetSphereCenter(Player player)
        {
            if (hipsOwner != player)
            {
                hipsOwner = player;
                hips = player.m_animator != null && player.m_animator.isHuman ?
                    player.m_animator.GetBoneTransform(HumanBodyBones.Hips) :
                    null;
            }
            return hips != null ?
                hips.position :
                player.transform.position + Vector3.up * FALLBACK_SPHERE_CENTER_HEIGHT;
        }

        private static bool ViewsCharacterFromOutside(Camera camera)
        {
            // The stabilized camera has a different updater, and is a first person view like the headset's.
            return camera.TryGetComponent(out ThirdPersonCameraUpdater updater) && updater.ViewsCharacterFromOutside;
        }

        private void SetFullTextureSpheresRendered(bool rendered)
        {
            foreach (Barrier barrier in barriers)
            {
                barrier.SetFullTextureRendered(rendered);
            }
        }

        public void Show(StatusEffect shield, Character character)
        {
            if (shield == null)
            {
                return;
            }
            if (barriers.Exists(barrier => barrier.shield == shield))
            {
                // Already shown, e.g. because the shield was set up again to change its level.
                return;
            }

            // The effect instances of this shield in particular: the character can be carrying the vfx of
            // another barrier too, and looking it up by name on the character would find either.
            GameObject[] effects = shield.m_startEffectInstances;
            if (effects == null || effects.Length == 0)
            {
                LogUtils.LogError("Magic barrier effect not found");
                return;
            }
            // Optional: only a barrier drawn as a single dome mesh can be mirrored onto a FullTexture sphere. One
            // drawn out of particles alone has no bubble surface to mirror, and gets the SimpleColor tint only. Both
            // vanilla barriers have a dome (Custom/Distortion material), so this is for modded ones.
            MeshRenderer bubbleRenderer = FindBubbleRenderer(shield);

            barriers.Add(new Barrier(shield, bubbleRenderer, effects, transform));
            enabled = true;
        }

        void FixedUpdate()
        {
            for (int i = barriers.Count - 1; i >= 0; i--)
            {
                if (barriers[i].IsDone())
                {
                    barriers[i].Destroy();
                    barriers.RemoveAt(i);
                }
            }
            if (barriers.Count == 0)
            {
                enabled = false;
                return;
            }

            foreach (Barrier barrier in barriers)
            {
                barrier.CenterBubbleAtViewHeight(transform.position.y);
                // A barrier drawn out of particles has empty renderer bounds until the particles have been
                // emitted and simulated, so its size is not known when it is first shown.
                barrier.RefreshRadius();
            }

            bool fullTexture = VHVRConfig.EnableFullTextureMagicBarrierOverlay();
            // Only one barrier at a time, see the class comment.
            Barrier soonestToExpire = GetSoonestToExpire();
            foreach (Barrier barrier in barriers)
            {
                bool shown = barrier == soonestToExpire;
                barrier.UpdateOverlays(
                    showFullTexture: shown && fullTexture,
                    showSimpleColor: shown && !fullTexture && VRPlayer.inFirstPerson);
            }
        }

        void OnDestroy()
        {
            UnsubscribeFromCameras();
            foreach (Barrier barrier in barriers)
            {
                barrier.Destroy();
            }
            barriers.Clear();
        }

        private static Mesh GetInsideOutSphere()
        {
            if (insideOutSphere != null)
            {
                return insideOutSphere;
            }

            int columns = SPHERE_LONGITUDE_SEGMENTS + 1;
            var vertices = new Vector3[columns * (SPHERE_LATITUDE_SEGMENTS + 1)];
            var normals = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            for (int row = 0; row <= SPHERE_LATITUDE_SEGMENTS; row++)
            {
                float polar = Mathf.PI * row / SPHERE_LATITUDE_SEGMENTS;
                for (int column = 0; column <= SPHERE_LONGITUDE_SEGMENTS; column++)
                {
                    float azimuth = 2 * Mathf.PI * column / SPHERE_LONGITUDE_SEGMENTS;
                    var direction = new Vector3(
                        Mathf.Sin(polar) * Mathf.Cos(azimuth), Mathf.Cos(polar), Mathf.Sin(polar) * Mathf.Sin(azimuth));
                    int index = row * columns + column;
                    vertices[index] = direction;
                    normals[index] = -direction;
                    uvs[index] = new Vector2((float)column / SPHERE_LONGITUDE_SEGMENTS, 1 - (float)row / SPHERE_LATITUDE_SEGMENTS);
                }
            }

            // Wound so that the front faces are on the inside: going down a row and along a column, (a, b, c) and
            // (c, b, d) face the center.
            var triangles = new int[SPHERE_LATITUDE_SEGMENTS * SPHERE_LONGITUDE_SEGMENTS * 6];
            int t = 0;
            for (int row = 0; row < SPHERE_LATITUDE_SEGMENTS; row++)
            {
                for (int column = 0; column < SPHERE_LONGITUDE_SEGMENTS; column++)
                {
                    int a = row * columns + column;
                    int b = a + columns;
                    int c = a + 1;
                    int d = b + 1;
                    triangles[t++] = a;
                    triangles[t++] = b;
                    triangles[t++] = c;
                    triangles[t++] = c;
                    triangles[t++] = b;
                    triangles[t++] = d;
                }
            }

            insideOutSphere = new Mesh { name = "VHVRInsideOutSphere" };
            insideOutSphere.vertices = vertices;
            insideOutSphere.normals = normals;
            insideOutSphere.uv = uvs;
            insideOutSphere.triangles = triangles;
            insideOutSphere.RecalculateBounds();
            return insideOutSphere;
        }

        // A barrier that never times out is the least urgent, whatever its remaining time says, so it is only
        // picked when there is nothing that does time out. Note that a barrier can also break from absorbing
        // enough damage at any moment, which no amount of remaining time predicts.
        private Barrier GetSoonestToExpire()
        {
            Barrier soonest = null;
            foreach (Barrier barrier in barriers)
            {
                if (soonest == null ||
                    (barrier.expiresOnTime && !soonest.expiresOnTime) ||
                    (barrier.expiresOnTime == soonest.expiresOnTime &&
                        barrier.GetRemainingTime() < soonest.GetRemainingTime()))
                {
                    soonest = barrier;
                }
            }
            return soonest;
        }

        // The extent of everything the barrier draws, so that barriers can be ordered by size without knowing
        // which effect is which. Every Renderer counts, including the ParticleSystemRenderers of a barrier made
        // of orbiting particles rather than a dome. Zero when the barrier draws nothing at all.
        private static float MeasureEffectRadius(GameObject[] effects)
        {
            float radius = 0;
            foreach (GameObject effect in effects)
            {
                if (effect == null)
                {
                    continue;
                }
                foreach (Renderer renderer in effect.GetComponentsInChildren<Renderer>())
                {
                    radius = Mathf.Max(radius, renderer.bounds.extents.magnitude);
                }
            }
            return radius;
        }

        private static MeshRenderer FindBubbleRenderer(StatusEffect shield)
        {
            if (shield.m_startEffectInstances == null)
            {
                return null;
            }
            foreach (GameObject effect in shield.m_startEffectInstances)
            {
                if (effect == null)
                {
                    continue;
                }
                MeshRenderer renderer = effect.GetComponentInChildren<MeshRenderer>();
                if (renderer != null)
                {
                    return renderer;
                }
            }
            return null;
        }

        // One barrier's view of itself: the bubble it mirrors, the sphere that shows it in FullTexture mode and the
        // panel that shows its tint in SimpleColor mode.
        private class Barrier
        {
            public readonly StatusEffect shield;
            public float bubbleRadius { get; private set; }
            // The radius of the dome itself, which the FullTexture sphere copies. bubbleRadius is the half diagonal
            // of the bounds of everything the barrier draws, i. e. about √3 times that for a dome.
            public float domeRadius { get; private set; }

            private readonly GameObject[] effects;
            private readonly MeshRenderer bubbleRenderer;
            private readonly Color tint;
            private readonly GameObject simpleColorPanel;
            private readonly GameObject fullTextureSphere;
            // The world rotation the bubble had when it came up, kept by the sphere so that its pattern stays put.
            private readonly Quaternion fullTextureSphereRotation;
            private readonly Material simpleColorMaterial;
            private float phase;

            public Barrier(StatusEffect shield, MeshRenderer bubbleRenderer, GameObject[] effects, Transform parent)
            {
                this.shield = shield;
                this.bubbleRenderer = bubbleRenderer;
                this.effects = effects;
                // The Staff of Protection bubble is an SE_Shield whereas Northern Vengeance is an SE_React.
                tint = shield is SE_Shield ? PROTECTION_TINT : VENGEANCE_TINT;
                bubbleRadius = MeasureEffectRadius(effects);

                simpleColorMaterial = Object.Instantiate(VRAssetManager.GetAsset<Material>("Unlit"));

                simpleColorPanel = CreatePanel(parent, LayerUtils.WORLDSPACE_UI_LAYER);
                simpleColorPanel.GetComponent<MeshRenderer>().material = simpleColorMaterial;
                simpleColorPanel.transform.localPosition = Vector3.forward * SIMPLE_COLOR_PANEL_DISTANCE;
                simpleColorPanel.transform.localScale =
                    new Vector3(SIMPLE_COLOR_PANEL_SIZE, SIMPLE_COLOR_PANEL_SIZE, 1);

                if (bubbleRenderer != null)
                {
                    Material bubbleMaterial = bubbleRenderer.material;
                    fullTextureSphereRotation = bubbleRenderer.transform.rotation;
                    // Not parented to the character, whose rotation it must not take. OnCameraPreCull() keeps it
                    // centered on the character's hips.
                    fullTextureSphere = CreatePanel(null, bubbleRenderer.gameObject.layer);
                    fullTextureSphere.name = "VHVRMagicBarrierSphere";
                    fullTextureSphere.GetComponent<MeshFilter>().sharedMesh = GetInsideOutSphere();
                    fullTextureSphere.GetComponent<MeshRenderer>().material = bubbleMaterial;
                    fullTextureSphere.transform.rotation = fullTextureSphereRotation;
                    RefreshDomeRadius();
                }
            }

            // The dome can be scaled in as it comes up, so it is measured all along rather than once. The larger of
            // its horizontal extents, since a dome need not reach as far up and down as it does sideways.
            private void RefreshDomeRadius()
            {
                if (bubbleRenderer == null)
                {
                    return;
                }
                Vector3 extents = bubbleRenderer.bounds.extents;
                domeRadius = Mathf.Max(extents.x, extents.z);
            }

            // Only the renderer, so that UpdateOverlays() keeps owning whether the sphere is shown at all.
            public void SetFullTextureRendered(bool rendered)
            {
                if (fullTextureSphere != null)
                {
                    fullTextureSphere.GetComponent<MeshRenderer>().enabled = rendered;
                }
            }

            public void CenterFullTextureSphere(Vector3 center)
            {
                if (fullTextureSphere == null || !fullTextureSphere.activeSelf)
                {
                    return;
                }
                fullTextureSphere.transform.SetPositionAndRotation(center, fullTextureSphereRotation);
            }


            // StatusEffect.IsDone() treats a non-positive ttl as never expiring, in which case GetRemainingTime()
            // counts down past zero instead of standing still.
            public bool expiresOnTime { get { return shield.m_ttl > 0; } }

            // Measures the barrier if its size is not known yet, and keeps retrying for as long as it stays unknown.
            public void RefreshRadius()
            {
                if (bubbleRadius > 0)
                {
                    return;
                }
                bubbleRadius = MeasureEffectRadius(effects);
            }

            public bool IsDone()
            {
                return shield == null || shield.IsDone();
            }

            public float GetRemainingTime()
            {
                return shield.GetRemaningTime();
            }

            // Without this the bubble would be centered around the player's feet.
            public void CenterBubbleAtViewHeight(float viewHeight)
            {
                if (bubbleRenderer == null)
                {
                    return;
                }
                Vector3 p = bubbleRenderer.transform.position;
                bubbleRenderer.transform.position = new Vector3(p.x, viewHeight, p.z);
            }

            public void UpdateOverlays(bool showFullTexture, bool showSimpleColor)
            {
                if (showFullTexture && fullTextureSphere != null)
                {
                    RefreshDomeRadius();
                    fullTextureSphere.transform.localScale = Vector3.one * domeRadius;
                }
                else if (showSimpleColor)
                {
                    // Pulse faster as the barrier runs out; one that never times out keeps the calm rate.
                    float remainingTime = expiresOnTime ? GetRemainingTime() : float.MaxValue;
                    phase += Time.fixedDeltaTime * (remainingTime > 16 ? 2 : (6f - remainingTime / 4f));
                    // The pulse rides on brightness instead of a single color channel, so that it reads the same
                    // on every signature tint.
                    Color pulsed = tint * (Mathf.Abs(Mathf.Sin(phase)) + 1) * 0.75f;
                    pulsed.a = OVERLAY_ALPHA;
                    simpleColorMaterial.color = pulsed;
                }

                if (fullTextureSphere != null)
                {
                    fullTextureSphere.SetActive(showFullTexture);
                }
                // A barrier with no dome to mirror still shows its tint, so that FullTexture mode does not lose
                // it entirely.
                simpleColorPanel.SetActive(showSimpleColor || (showFullTexture && fullTextureSphere == null));
            }

            public void Destroy()
            {
                if (simpleColorPanel != null)
                {
                    Object.Destroy(simpleColorPanel);
                }
                if (fullTextureSphere != null)
                {
                    Object.Destroy(fullTextureSphere);
                }
                if (simpleColorMaterial != null)
                {
                    Object.Destroy(simpleColorMaterial);
                }
            }

            private static GameObject CreatePanel(Transform parent, int layer)
            {
                GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.Destroy(panel.GetComponent<Collider>());
                Renderer renderer = panel.GetComponent<Renderer>();
                renderer.receiveShadows = false;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                panel.layer = layer;
                if (parent != null)
                {
                    panel.transform.SetParent(parent);
                    panel.transform.localRotation = Quaternion.identity;
                }
                panel.SetActive(false);
                return panel;
            }
        }
    }
}
