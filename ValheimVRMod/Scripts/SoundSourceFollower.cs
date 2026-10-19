using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts
{
    // Keeps a sound effect at the creature that made it. Vanilla spawns some creature sounds (e.g. the idle sound)
    // at the creature's position without attaching them, so a creature on the move leaves its own voice behind,
    // which head-tracked spatial audio in VR makes easy to hear.
    //
    // Follows rather than being parented, so that a creature dying or despawning mid-sound does not destroy the
    // sound along with it: the sound stops following and finishes where it is, as it would in vanilla.
    class SoundSourceFollower : MonoBehaviour
    {
        private Transform target;
        private Vector3 offset;

        public static void Attach(GameObject[] effects, Transform target)
        {
            if (effects == null || target == null)
            {
                return;
            }
            foreach (GameObject effect in effects)
            {
                // Only the sounds: any particles in the same effect list stay where vanilla put them.
                if (effect == null || effect.GetComponentInChildren<AudioSource>(true) == null)
                {
                    continue;
                }
                SoundSourceFollower follower = effect.AddComponent<SoundSourceFollower>();
                follower.target = target;
                follower.offset = effect.transform.position - target.position;
            }
        }

        void LateUpdate()
        {
            if (target == null || !target.gameObject.activeInHierarchy || !VHVRConfig.FixCreatureSoundSource())
            {
                enabled = false;
                return;
            }
            transform.position = target.position + offset;
        }
    }
}
