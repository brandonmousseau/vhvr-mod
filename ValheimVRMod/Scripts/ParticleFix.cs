using UnityEngine;

namespace ValheimVRMod.Scripts {
    public class ParticleFix : MonoBehaviour {

        private Transform origin;
        
        private void Awake() {
            origin = new GameObject().transform;
            origin.parent = transform.parent;
            origin.localPosition = transform.localPosition;
            transform.SetParent(null);
        }
        
        private void OnRenderObject() {

            if (origin == null) {
                Destroy(gameObject);
                return;
            }
            
            transform.position = origin.position;
        }

        public static void maybeFix(GameObject target) {

            var particleSystem = target.GetComponentInChildren<ParticleSystem>();

            if (particleSystem != null) {
                particleSystem.gameObject.AddComponent<ParticleFix>();
            }
        }
    }
}