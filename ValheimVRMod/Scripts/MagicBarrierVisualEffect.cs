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
     * FullTexture mode mirrors each bubble's own material onto a panel in front of the camera, one panel per
     * barrier, ordered so that the larger bubble's panel sits farther away and therefore draws behind the smaller
     * one, as the bubbles themselves do.
     *
     * SimpleColor mode shows a single tinted panel for the barrier that expires first, pulsing faster as it runs
     * out. The pulse is the signal, and two pulses at different rates blended into one panel read as neither, so
     * the barriers are shown one at a time; when the first expires the tint switches to the next one, which is
     * itself the "one barrier left" cue.
     */
    class MagicBarrierVisualEffect : MonoBehaviour
    {
        // Signature tints, applied in order of bubble size: the smallest bubble (the Staff of Protection) takes
        // the first. Ranking by size rather than by status effect name keeps this working without knowing each
        // effect's asset name, and uses the same ordering as the FullTexture panel depths below.
        private static readonly Color[] SIGNATURE_TINTS = new Color[]
        {
            new Color(0.375f, 0.125f, 0.3f),
            new Color(0.125f, 0.3f, 0.5f),
        };
        private const float OVERLAY_ALPHA = 0.2f;
        // Distance of the nearest FullTexture panel, and the gap between panels of successive barriers.
        private const float FULL_TEXTURE_PANEL_DISTANCE = 1f;
        private const float FULL_TEXTURE_PANEL_SPACING = 0.25f;
        // Width and height of a FullTexture panel at FULL_TEXTURE_PANEL_DISTANCE. Panels farther away are scaled
        // up in proportion so that they all cover the same part of the view and only their depth order differs.
        private const float FULL_TEXTURE_PANEL_SIZE = 4f;
        private const float SIMPLE_COLOR_PANEL_DISTANCE = 0.125f;
        private const float SIMPLE_COLOR_PANEL_SIZE = 0.5f;

        private readonly List<Barrier> barriers = new List<Barrier>();

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
            // Optional: only a barrier drawn as a single dome mesh can be mirrored onto a FullTexture panel. One
            // drawn out of particles (Northern Vengeance's orbs) has no bubble surface to mirror, and gets the
            // SimpleColor tint only.
            MeshRenderer bubbleRenderer = FindBubbleRenderer(shield);

            barriers.Add(new Barrier(shield, bubbleRenderer, effects, transform));
            SortBarriersBySize();
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

            bool anyRadiusResolved = false;
            foreach (Barrier barrier in barriers)
            {
                barrier.CenterBubbleAtViewHeight(transform.position.y);
                // A barrier drawn out of particles has empty renderer bounds until the particles have been
                // emitted and simulated, so its size is not known when it is first shown.
                anyRadiusResolved |= barrier.RefreshRadius();
            }
            if (anyRadiusResolved)
            {
                // Its place in the size order, and with it its tint and panel depth, is only settled now.
                SortBarriersBySize();
            }

            bool fullTexture = VHVRConfig.EnableFullTextureMagicBarrierOverlay();
            Barrier soonestToExpire = fullTexture ? null : GetSoonestToExpire();
            for (int i = 0; i < barriers.Count; i++)
            {
                barriers[i].UpdateOverlays(
                    showFullTexture: fullTexture,
                    // Only one tinted panel at a time, see the class comment.
                    showSimpleColor: barriers[i] == soonestToExpire && VRPlayer.inFirstPerson,
                    tint: SIGNATURE_TINTS[Mathf.Min(i, SIGNATURE_TINTS.Length - 1)],
                    panelDistance: FULL_TEXTURE_PANEL_DISTANCE + i * FULL_TEXTURE_PANEL_SPACING);
            }
        }

        void OnDestroy()
        {
            foreach (Barrier barrier in barriers)
            {
                barrier.Destroy();
            }
            barriers.Clear();
        }

        // Smallest bubble first, so that the tints and the panel depths are assigned consistently and the larger
        // bubble's panel ends up behind the smaller one.
        private void SortBarriersBySize()
        {
            barriers.Sort((a, b) => a.bubbleRadius.CompareTo(b.bubbleRadius));
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

        // One barrier's view of itself: the bubble it mirrors and the two panels that show it.
        private class Barrier
        {
            public readonly StatusEffect shield;
            public float bubbleRadius { get; private set; }

            private readonly GameObject[] effects;
            private readonly MeshRenderer bubbleRenderer;
            private readonly GameObject simpleColorPanel;
            private readonly GameObject fullTexturePanel;
            private readonly Material simpleColorMaterial;
            private float phase;

            public Barrier(StatusEffect shield, MeshRenderer bubbleRenderer, GameObject[] effects, Transform parent)
            {
                this.shield = shield;
                this.bubbleRenderer = bubbleRenderer;
                this.effects = effects;
                bubbleRadius = MeasureEffectRadius(effects);

                simpleColorMaterial = Object.Instantiate(VRAssetManager.GetAsset<Material>("Unlit"));

                simpleColorPanel = CreatePanel(parent, LayerUtils.WORLDSPACE_UI_LAYER);
                simpleColorPanel.GetComponent<MeshRenderer>().material = simpleColorMaterial;
                simpleColorPanel.transform.localPosition = Vector3.forward * SIMPLE_COLOR_PANEL_DISTANCE;
                simpleColorPanel.transform.localScale =
                    new Vector3(SIMPLE_COLOR_PANEL_SIZE, SIMPLE_COLOR_PANEL_SIZE, 1);

                if (bubbleRenderer != null)
                {
                    fullTexturePanel = CreatePanel(parent, bubbleRenderer.gameObject.layer);
                    fullTexturePanel.GetComponent<MeshRenderer>().material = bubbleRenderer.material;
                }
            }

            // StatusEffect.IsDone() treats a non-positive ttl as never expiring, in which case GetRemainingTime()
            // counts down past zero instead of standing still.
            public bool expiresOnTime { get { return shield.m_ttl > 0; } }

            // Measures the barrier if its size is not known yet, and reports whether that just settled it. Keeps
            // retrying for as long as it stays unknown.
            public bool RefreshRadius()
            {
                if (bubbleRadius > 0)
                {
                    return false;
                }
                bubbleRadius = MeasureEffectRadius(effects);
                return bubbleRadius > 0;
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

            public void UpdateOverlays(bool showFullTexture, bool showSimpleColor, Color tint, float panelDistance)
            {
                if (showFullTexture && fullTexturePanel != null)
                {
                    fullTexturePanel.transform.localPosition = Vector3.forward * panelDistance;
                    // Keep the same coverage of the view at every distance, so only the depth order changes.
                    float size = FULL_TEXTURE_PANEL_SIZE * panelDistance / FULL_TEXTURE_PANEL_DISTANCE;
                    fullTexturePanel.transform.localScale = new Vector3(size, size, 1);
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

                if (fullTexturePanel != null)
                {
                    fullTexturePanel.SetActive(showFullTexture);
                }
                // A barrier with no dome to mirror still shows its tint, so that FullTexture mode does not lose
                // it entirely.
                simpleColorPanel.SetActive(showSimpleColor || (showFullTexture && fullTexturePanel == null));
            }

            public void Destroy()
            {
                if (simpleColorPanel != null)
                {
                    Object.Destroy(simpleColorPanel);
                }
                if (fullTexturePanel != null)
                {
                    Object.Destroy(fullTexturePanel);
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
                panel.transform.SetParent(parent);
                panel.transform.localRotation = Quaternion.identity;
                panel.SetActive(false);
                return panel;
            }
        }
    }
}
