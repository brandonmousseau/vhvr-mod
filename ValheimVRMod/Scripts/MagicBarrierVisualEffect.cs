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
     * active shield, each owning its own overlays, material and signature tint. Nothing here may be static or shared
     * between barriers.
     *
     * Both modes draw an inside-out sphere parented to the vanilla bubble, with the radius of the bubble's mesh and
     * raised by SPHERE_LOCAL_OFFSET, so that it moves and scales with the bubble. Its rotation is held fixed in the
     * world instead, so that its surface does not turn with the character. FullTexture mode mirrors the bubble's own
     * material onto the sphere, SimpleColor mode tints it, pulsing faster as the barrier runs out.
     *
     * Only the barrier that expires first is shown. Two pulses at different rates blended together read as neither,
     * and two nested spheres around the view are too much to look through. When the first expires the overlay
     * switches to the next one, which is itself the "one barrier left" cue.
     *
     * The vanilla bubbles, all of them and not just the one shown, are hidden from views from inside the bubble (the
     * headset, and flat screen cameras that follow the eyes), which get the sphere instead. Flat screen cameras looking
     * at the character from outside get the vanilla bubbles, as other players see them, and not the sphere.
     */
    class MagicBarrierVisualEffect : MonoBehaviour
    {
        // Signature tints, one dedicated to each type of barrier so that a tint always means the same barrier,
        // whichever barriers happen to be up and whatever size they turn out to be.
        private static readonly Color PROTECTION_TINT = new Color(0.3f, 0.125f, 0.2f);
        private static readonly Color VENGEANCE_TINT = new Color(0.125f, 0.2f, 0.375f);
        private const float OVERLAY_ALPHA = 0.2f;
        // The tinted panel in front of the camera, only used for a barrier without a dome to put a sphere on.
        private const float SIMPLE_COLOR_PANEL_DISTANCE = 0.125f;
        private const float SIMPLE_COLOR_PANEL_SIZE = 0.5f;
        // Where the spheres are centered, in the bubble's local space (and so scaled with it).
        private static readonly Vector3 SPHERE_LOCAL_OFFSET = Vector3.up * 0.5f;
        // Resolution of the spheres.
        private const int SPHERE_LONGITUDE_SEGMENTS = 32;
        private const int SPHERE_LATITUDE_SEGMENTS = 16;

        // A unit sphere facing inwards, shared by every barrier's spheres. Generated rather than taken from the
        // bubble or a primitive, whose meshes need not be readable in a player build.
        private static Mesh insideOutSphere;

        private enum View
        {
            // A camera this has no business with, e. g. the hands or world space UI camera.
            Other,
            // The headset, or a flat screen camera from the player's eyes.
            FromInside,
            // A flat screen camera looking at the character from outside.
            FromOutside
        }

        private readonly List<Barrier> barriers = new List<Barrier>();
        // What OnCameraPreCull() hid from the camera being rendered, for OnCameraPostRender() to show again.
        private readonly List<Renderer> hiddenFromCurrentCamera = new List<Renderer>();
        private Camera vrCamera;

        void Awake()
        {
            vrCamera = GetComponent<Camera>();
        }

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
            ShowHiddenRenderers();
        }

        // Culling happens per camera after onPreCull, so a renderer disabled here is skipped by this camera alone,
        // and OnCameraPostRender() turns it back on before the next camera.
        private void OnCameraPreCull(Camera camera)
        {
            View view = GetView(camera);
            if (view == View.Other)
            {
                return;
            }
            if (camera == vrCamera)
            {
                // Right before the first camera to draw the spheres, as the bubble may have turned since.
                foreach (Barrier barrier in barriers)
                {
                    barrier.HoldSphereRotation();
                }
            }
            foreach (Barrier barrier in barriers)
            {
                if (view == View.FromInside)
                {
                    Hide(barrier.vanillaDome);
                }
                else
                {
                    foreach (Renderer overlay in barrier.overlays)
                    {
                        Hide(overlay);
                    }
                }
            }
        }

        private void OnCameraPostRender(Camera camera)
        {
            ShowHiddenRenderers();
        }

        private void Hide(Renderer renderer)
        {
            if (renderer != null && renderer.enabled)
            {
                renderer.enabled = false;
                hiddenFromCurrentCamera.Add(renderer);
            }
        }

        private void ShowHiddenRenderers()
        {
            foreach (Renderer renderer in hiddenFromCurrentCamera)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }
            hiddenFromCurrentCamera.Clear();
        }

        private View GetView(Camera camera)
        {
            if (camera == vrCamera)
            {
                return View.FromInside;
            }
            if (camera.TryGetComponent(out ThirdPersonCameraUpdater thirdPersonCameraUpdater))
            {
                return thirdPersonCameraUpdater.ViewsCharacterFromOutside ? View.FromOutside : View.FromInside;
            }
            return camera.TryGetComponent(out StabilizedCameraUpdater _) ? View.FromInside : View.Other;
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
            // Optional: only a barrier drawn as a single dome mesh gets the spheres. One drawn out of particles
            // alone has no bubble surface to mirror, and gets a tinted panel in front of the camera instead. Both
            // vanilla barriers have a dome (Custom/Distortion material), so this is for modded ones.
            MeshRenderer bubbleRenderer = FindBubbleRenderer(shield);
            LogPlacement(character, effects, bubbleRenderer);

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

        // TEMP diagnostic: where vanilla put the bubble. StatusEffect.TriggerStartEffects() spawns it at
        // Character.GetCenterPoint() (the collider's center), yet the bubble ends up around the feet in VR.
        private static void LogPlacement(Character character, GameObject[] effects, MeshRenderer bubbleRenderer)
        {
            if (character == null)
            {
                return;
            }
            string log =
                "Magic barrier placement: character y=" + character.transform.position.y.ToString("F3") +
                ", center point y=" + character.GetCenterPoint().y.ToString("F3") +
                ", collider center=" + character.m_collider.center + " height=" + character.m_collider.height +
                " enabled=" + character.m_collider.enabled;
            foreach (GameObject effect in effects)
            {
                if (effect == null)
                {
                    continue;
                }
                var constraint = effect.GetComponent<UnityEngine.Animations.ParentConstraint>();
                log += "; effect " + effect.name +
                    " y=" + effect.transform.position.y.ToString("F3") +
                    " local=" + effect.transform.localPosition +
                    " scale=" + effect.transform.lossyScale +
                    " parent=" + (effect.transform.parent == null ? "<none>" : effect.transform.parent.name) +
                    " constraint=" + (constraint == null ? "none" : "offset " + constraint.GetTranslationOffset(0));
            }
            if (bubbleRenderer != null)
            {
                MeshFilter meshFilter = bubbleRenderer.GetComponent<MeshFilter>();
                log += "; dome " + bubbleRenderer.name +
                    " y=" + bubbleRenderer.transform.position.y.ToString("F3") +
                    " local=" + bubbleRenderer.transform.localPosition +
                    " world bounds=" + bubbleRenderer.bounds +
                    " mesh bounds=" +
                    (meshFilter == null || meshFilter.sharedMesh == null ? "?" : meshFilter.sharedMesh.bounds.ToString());
            }
            LogUtils.LogDebug(log);
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

        // The extent of everything the barrier draws. Every Renderer counts, including the ParticleSystemRenderers
        // of a barrier made of orbiting particles rather than a dome. Zero when the barrier draws nothing at all.
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

        // One barrier's view of itself: the vanilla bubble and the overlays that stand in for it.
        private class Barrier
        {
            public readonly StatusEffect shield;
            public float bubbleRadius { get; private set; }
            // The vanilla dome, null for a barrier without one.
            public readonly MeshRenderer vanillaDome;
            // Everything this barrier draws on its own, to be hidden from views from outside the bubble.
            public readonly List<Renderer> overlays = new List<Renderer>();

            private readonly GameObject[] effects;
            private readonly Color tint;
            private readonly Material simpleColorMaterial;
            // Null without a dome to put them on.
            private readonly GameObject fullTextureSphere;
            private readonly GameObject simpleColorSphere;
            // Null with a dome to put the spheres on.
            private readonly GameObject simpleColorPanel;
            // The world rotation the bubble had when it came up, which the spheres keep.
            private readonly Quaternion sphereRotation;
            private float phase;

            public Barrier(StatusEffect shield, MeshRenderer bubbleRenderer, GameObject[] effects, Transform camera)
            {
                this.shield = shield;
                vanillaDome = bubbleRenderer;
                this.effects = effects;
                // The Staff of Protection bubble is an SE_Shield whereas Northern Vengeance is an SE_React.
                tint = shield is SE_Shield ? PROTECTION_TINT : VENGEANCE_TINT;
                bubbleRadius = MeasureEffectRadius(effects);

                simpleColorMaterial = Object.Instantiate(VRAssetManager.GetAsset<Material>("Unlit"));

                if (bubbleRenderer != null)
                {
                    sphereRotation = bubbleRenderer.transform.rotation;
                    fullTextureSphere = CreateSphere(bubbleRenderer, bubbleRenderer.material, "VHVRMagicBarrierSphere");
                    simpleColorSphere = CreateSphere(bubbleRenderer, simpleColorMaterial, "VHVRMagicBarrierTintSphere");
                }
                else
                {
                    simpleColorPanel = CreateOverlayObject(camera, LayerUtils.WORLDSPACE_UI_LAYER);
                    simpleColorPanel.GetComponent<MeshRenderer>().material = simpleColorMaterial;
                    simpleColorPanel.transform.localPosition = Vector3.forward * SIMPLE_COLOR_PANEL_DISTANCE;
                    simpleColorPanel.transform.localScale =
                        new Vector3(SIMPLE_COLOR_PANEL_SIZE, SIMPLE_COLOR_PANEL_SIZE, 1);
                }
                foreach (GameObject overlay in new[] { fullTextureSphere, simpleColorSphere, simpleColorPanel })
                {
                    if (overlay != null)
                    {
                        overlays.Add(overlay.GetComponent<Renderer>());
                    }
                }
            }

            // A child of the bubble, so that it follows the bubble's position and scale (e. g. as it grows in)
            // without any bookkeeping, and goes away with it. Its rotation is held by HoldSphereRotation().
            private GameObject CreateSphere(MeshRenderer bubbleRenderer, Material material, string name)
            {
                GameObject sphere = CreateOverlayObject(bubbleRenderer.transform, bubbleRenderer.gameObject.layer);
                sphere.name = name;
                sphere.GetComponent<MeshFilter>().sharedMesh = GetInsideOutSphere();
                sphere.GetComponent<MeshRenderer>().material = material;
                sphere.transform.localPosition = SPHERE_LOCAL_OFFSET;
                sphere.transform.localScale = Vector3.one * GetLocalDomeRadius(bubbleRenderer);
                sphere.transform.rotation = sphereRotation;
                return sphere;
            }

            // The dome's radius in its own space. Mesh.bounds is available even when the mesh is not readable. The
            // larger of its horizontal extents, since a dome need not reach as far up and down as it does sideways.
            private static float GetLocalDomeRadius(MeshRenderer bubbleRenderer)
            {
                MeshFilter meshFilter = bubbleRenderer.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    return 1;
                }
                Vector3 extents = meshFilter.sharedMesh.bounds.extents;
                return Mathf.Max(extents.x, extents.z);
            }

            // The bubble turns with the character, which its children would follow.
            public void HoldSphereRotation()
            {
                if (fullTextureSphere != null && fullTextureSphere.activeSelf)
                {
                    fullTextureSphere.transform.rotation = sphereRotation;
                }
                if (simpleColorSphere != null && simpleColorSphere.activeSelf)
                {
                    simpleColorSphere.transform.rotation = sphereRotation;
                }
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

            public void UpdateOverlays(bool showFullTexture, bool showSimpleColor)
            {
                // A barrier with no dome to mirror still shows its tint, so that FullTexture mode does not lose
                // it entirely.
                bool showTintPanel = simpleColorPanel != null && (showSimpleColor || showFullTexture);
                if ((showSimpleColor && simpleColorSphere != null) || showTintPanel)
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
                if (simpleColorSphere != null)
                {
                    simpleColorSphere.SetActive(showSimpleColor);
                }
                if (simpleColorPanel != null)
                {
                    simpleColorPanel.SetActive(showTintPanel);
                }
            }

            public void Destroy()
            {
                foreach (GameObject overlay in new[] { fullTextureSphere, simpleColorSphere, simpleColorPanel })
                {
                    if (overlay != null)
                    {
                        Object.Destroy(overlay);
                    }
                }
                if (simpleColorMaterial != null)
                {
                    Object.Destroy(simpleColorMaterial);
                }
            }

            private static GameObject CreateOverlayObject(Transform parent, int layer)
            {
                GameObject overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.Destroy(overlay.GetComponent<Collider>());
                Renderer renderer = overlay.GetComponent<Renderer>();
                renderer.receiveShadows = false;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                overlay.layer = layer;
                overlay.transform.SetParent(parent, worldPositionStays: false);
                overlay.SetActive(false);
                return overlay;
            }
        }
    }
}
