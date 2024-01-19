using UnityEngine;

namespace ValheimVRMod.Scripts {
    // Fix for updating the position and rotation (e. g. Mistwalker and some modded weapons) of particle system in weapons.
    public class ParticleFix : MonoBehaviour {

        private Transform origin;
        
        private void Awake() {
            origin = new GameObject().transform;
            origin.parent = transform.parent;
            origin.localPosition = transform.localPosition;
            origin.localRotation = transform.localRotation;
            transform.SetParent(null);
        }
        
        private void OnRenderObject() {

            if (origin == null) {
                Destroy(gameObject);
                return;
            }

            transform.SetPositionAndRotation(origin.position, origin.rotation);
        }

        void OnDestroy()
        {
            if (origin)
            {
                Destroy(origin.gameObject);
            }
        }

        public static void maybeFix(GameObject target) {

            var particleSystem = target.GetComponentInChildren<ParticleSystem>();

            if (particleSystem != null) {
                particleSystem.gameObject.AddComponent<ParticleFix>();
            }
        }
    }
}
