using System.Collections.Generic;
using UnityEngine;

namespace ValheimVRMod.Scripts
{
    // Fix for particle systems which emit from the shape of a skinned mesh (e. g. Thunderblood and Frostfire knuckles).
    // Such particle systems sample their emission positions from the bones before VRIK moves the bones in LateUpdate,
    // so the particles would follow the vanilla animation pose instead of the VR hands. They cannot simply be reparented
    // to the hands either, since each particle system emits from one mesh covering both hands.
    // Instead, we let them emit from a hidden copy of the skinned mesh renderer whose skeleton copies the final pose of
    // the real skeleton after rendering and is not touched by the animator before the particles update in the next frame.
    public class SkinnedParticleFix : MonoBehaviour
    {
        private readonly Dictionary<Transform, Transform> proxies = new Dictionary<Transform, Transform>();
        private readonly Dictionary<SkinnedMeshRenderer, SkinnedMeshRenderer> proxyRenderers = new Dictionary<SkinnedMeshRenderer, SkinnedMeshRenderer>();
        private Transform root;
        private Transform proxyRoot;
        private int lastSyncFrame = -1;

        public static void Create(GameObject target, List<ParticleSystem> particleSystems)
        {
            // VisEquipment#AttachItem parents skinned items to the same transform that contains the player skeleton.
            Transform root = target.transform.parent;
            if (root == null)
            {
                return;
            }

            SkinnedParticleFix fix = target.AddComponent<SkinnedParticleFix>();
            fix.root = root;
            fix.proxyRoot = new GameObject("SkinnedParticleFixProxy").transform;
            fix.proxyRoot.SetParent(root, false);

            foreach (ParticleSystem particleSystem in particleSystems)
            {
                var shape = particleSystem.shape;
                SkinnedMeshRenderer proxyRenderer = fix.GetOrCreateProxyRenderer(shape.skinnedMeshRenderer);
                if (proxyRenderer != null)
                {
                    shape.skinnedMeshRenderer = proxyRenderer;
                }
            }
        }

        public static bool EmitsFromSkinnedMesh(ParticleSystem particleSystem)
        {
            var shape = particleSystem.shape;
            return shape.enabled && shape.shapeType == ParticleSystemShapeType.SkinnedMeshRenderer && shape.skinnedMeshRenderer != null;
        }

        private void OnRenderObject()
        {
            if (proxyRoot == null || lastSyncFrame == Time.frameCount)
            {
                return;
            }
            lastSyncFrame = Time.frameCount;

            foreach (var entry in proxies)
            {
                Transform source = entry.Key;
                if (source == null)
                {
                    continue;
                }
                Transform proxy = entry.Value;
                proxy.localPosition = source.localPosition;
                proxy.localRotation = source.localRotation;
                proxy.localScale = source.localScale;
            }
        }

        private void OnDestroy()
        {
            if (proxyRoot)
            {
                Destroy(proxyRoot.gameObject);
            }
        }

        private SkinnedMeshRenderer GetOrCreateProxyRenderer(SkinnedMeshRenderer renderer)
        {
            if (proxyRenderers.TryGetValue(renderer, out SkinnedMeshRenderer existingProxyRenderer))
            {
                return existingProxyRenderer;
            }

            Transform[] bones = renderer.bones;
            Transform[] proxyBones = new Transform[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null)
                {
                    continue;
                }
                proxyBones[i] = GetOrCreateProxy(bones[i]);
                if (proxyBones[i] == null)
                {
                    return null;
                }
            }

            Transform proxyRendererTransform = GetOrCreateProxy(renderer.transform);
            if (proxyRendererTransform == null)
            {
                return null;
            }

            SkinnedMeshRenderer proxyRenderer = proxyRendererTransform.gameObject.AddComponent<SkinnedMeshRenderer>();
            proxyRenderer.sharedMesh = renderer.sharedMesh;
            proxyRenderer.bones = proxyBones;
            proxyRenderer.rootBone = renderer.rootBone == null ? null : GetOrCreateProxy(renderer.rootBone);
            // The proxy only serves as the emission shape; the original renderer is still the visible one.
            proxyRenderer.forceRenderingOff = true;
            proxyRenderers[renderer] = proxyRenderer;
            return proxyRenderer;
        }

        // Mirrors the hierarchy from root down to the source transform so that copying local poses reproduces its world pose.
        private Transform GetOrCreateProxy(Transform source)
        {
            if (source == null)
            {
                // The source is not under root.
                return null;
            }
            if (source == root)
            {
                return proxyRoot;
            }
            if (proxies.TryGetValue(source, out Transform existingProxy))
            {
                return existingProxy;
            }

            Transform proxyParent = GetOrCreateProxy(source.parent);
            if (proxyParent == null)
            {
                return null;
            }

            Transform proxy = new GameObject(source.name).transform;
            proxy.SetParent(proxyParent, false);
            proxy.localPosition = source.localPosition;
            proxy.localRotation = source.localRotation;
            proxy.localScale = source.localScale;
            proxies[source] = proxy;
            return proxy;
        }
    }
}
