using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts {
    // Fix for updating the position and rotation (e. g. Mistwalker and some modded weapons) of particle system in weapons.
    public class ParticleFix : MonoBehaviour {

        private Transform origin;
        private bool shouldHide;
        
        private void Awake() {
            origin = new GameObject().transform;
            origin.parent = transform.parent;
            origin.localPosition = transform.localPosition;
            origin.localRotation = transform.localRotation;
            if (VHVRConfig.UseVrControls())
            {
                transform.SetParent(null);
            }
        }
        
        private void OnRenderObject() {

            if (origin == null) {
                Destroy(gameObject);
                return;
            }

            if (shouldHide)
            {
                gameObject.SetActive(false);
            }

            if (VHVRConfig.UseVrControls())
            {
                transform.SetPositionAndRotation(origin.position, origin.rotation);
            }
        }

        void OnDestroy()
        {
            if (origin)
            {
                Destroy(origin.gameObject);
            }
        }

        public static void maybeFix(GameObject target, EquipType equipType) {
            var isTorch = equipType == EquipType.Torch;
            var isRangedWeapon = equipType == EquipType.Bow || equipType == EquipType.Crossbow || equipType == EquipType.Magic;
            var shouldHideParticles = isTorch ? false : (isRangedWeapon ? !VHVRConfig.EnableRangedWeaponGlowParticle() : !VHVRConfig.EnableMeleeWeaponGlowParticle());

            var particleSystems = target.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            var skinnedMeshParticleSystems = new List<ParticleSystem>();
            foreach (ParticleSystem particleSystem in particleSystems) {
                if (SkinnedParticleFix.EmitsFromSkinnedMesh(particleSystem))
                {
                    // Moving these particle systems does not move their emission, which follows the bones instead.
                    if (shouldHideParticles)
                    {
                        particleSystem.gameObject.SetActive(false);
                    }
                    else
                    {
                        skinnedMeshParticleSystems.Add(particleSystem);
                    }
                    continue;
                }
                particleSystem.gameObject.AddComponent<ParticleFix>().shouldHide = shouldHideParticles;
            }

            if (skinnedMeshParticleSystems.Count > 0 && VHVRConfig.UseVrControls())
            {
                SkinnedParticleFix.Create(target, skinnedMeshParticleSystems);
            }

            if (isTorch)
            {
                return;
            }

            if (isRangedWeapon && VHVRConfig.EnableRangedWeaponGlowLight())
            {
                return;
            }

            if (!isRangedWeapon && VHVRConfig.EnableMeleeWeaponGlowLight())
            {
                return;
            }

            var lights = target.GetComponentsInChildren<Light>(includeInactive: true);
            foreach (var light in lights)
            {
                light.gameObject.SetActive(false);
            }
        }
    }
}
