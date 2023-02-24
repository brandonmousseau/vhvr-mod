using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts
{
    public abstract class WeaponWield : MonoBehaviour
    {
        public enum TwoHandedState
        {
            SingleHanded = 0,
            RightHandBehind = 1,
            LeftHandBehind = 2
        }

        protected const float HAND_CENTER_OFFSET = 0.08f;

        public TwoHandedState twoHandedState { get; private set; }
        public Transform rearHandTransform { get; private set; }
        public Transform frontHandTransform { get; private set; }

        protected string attackAnimation { get; private set; }
        protected string itemName { get; private set; }

        private Transform singleHandedTransform;
        private Transform originalTransform;
        private Quaternion offsetFromPointingDir; // The rotation offset of this transform relative to the direction the weapon is pointing at.
        private Vector3 estimatedLocalWeaponPointingDir = Vector3.forward;
        private ParticleSystem particleSystem;
        private Transform particleSystemTransformUpdater;

        public WeaponWield Initialize(ItemDrop.ItemData item, string itemName)
        {
            this.itemName = itemName;

            particleSystem = gameObject.GetComponentInChildren<ParticleSystem>();
            if (particleSystem != null)
            {
                particleSystemTransformUpdater = new GameObject().transform;
                particleSystemTransformUpdater.parent = transform;
                particleSystemTransformUpdater.SetPositionAndRotation(particleSystem.transform.position, particleSystem.transform.rotation);
            }

            attackAnimation = item?.m_shared.m_attack?.m_attackAnimation?? "";

            originalTransform = new GameObject().transform;
            singleHandedTransform = new GameObject().transform;
            originalTransform.parent = singleHandedTransform.parent = transform.parent;
            originalTransform.position = singleHandedTransform.position = transform.position;
            originalTransform.rotation = transform.rotation;
            transform.rotation = singleHandedTransform.rotation = GetSingleHandedRotation(originalTransform.rotation);

            MeshFilter weaponMeshFilter = gameObject.GetComponentInChildren<MeshFilter>();
            if (weaponMeshFilter != null)
            {
                estimatedLocalWeaponPointingDir = transform.InverseTransformDirection(WeaponUtils.EstimateWeaponPointingDirection(weaponMeshFilter, transform.parent.position));
            }

            offsetFromPointingDir = Quaternion.Inverse(Quaternion.LookRotation(GetWeaponPointingDir(), transform.up)) * transform.rotation;

            return this;
        }

        protected static Vector3 getHandCenter(Transform hand)
        {
            return hand.transform.position - hand.transform.forward * HAND_CENTER_OFFSET;
        }

        protected virtual void OnDestroy()
        {
            Destroy(originalTransform.gameObject);
            Destroy(singleHandedTransform.gameObject);
            if (particleSystemTransformUpdater != null)
            {
                Destroy(particleSystemTransformUpdater.gameObject);
            }
        }

        protected virtual void OnRenderObject()
        {
            UpdateTwoHandedWield();
            if (particleSystem != null)
            {
                // The particle system on Mistwalker (as well as some modded weapons) for some reason needs it rotation updated explicitly in order to follow the sword in VR.
                particleSystem.transform.rotation = particleSystemTransformUpdater.transform.rotation;
            }
        }

        // Returns the direction the weapon is pointing.
        protected virtual Vector3 GetWeaponPointingDir()
        {
            return transform.TransformDirection(estimatedLocalWeaponPointingDir);
        }

        // Calculates the correct rotation of this game object for single-handed mode using the original rotation.
        // This should be the same as the original rotation in most cases but there are exceptions.
        protected virtual Quaternion GetSingleHandedRotation(Quaternion originalRotation)
        {
            switch (attackAnimation)
            {
                case "atgeir_attack":
                    // Atgeir wield rotation fix: the tip of the atgeir is pointing at (0.328, -0.145, 0.934) in local coordinates.
                    return originalRotation * Quaternion.AngleAxis(-20, Vector3.up) * Quaternion.AngleAxis(-7, Vector3.right);
                default:
                    return originalRotation;
            }
        }

        // The preferred up direction used to determine the weapon's rotation around it longitudinal axis during two-handed wield.
        protected virtual Vector3 GetPreferredTwoHandedWeaponUp()
        {
            return singleHandedTransform.up;
        }

        // The preferred forward offset amount of the weapon's position from the rear hand during two-handed wield.
        protected virtual float GetPreferredOffsetFromRearHand(float handDist)
        {
            bool rearHandIsDominant = (IsPlayerLeftHanded() == (twoHandedState == TwoHandedState.LeftHandBehind));
            if (rearHandIsDominant)
            {
                return -0.1f;
            }
            else if (handDist > 0.15f)
            {
                return 0.05f;
            }
            else
            {
                // Anchor the weapon in the front/dominant hand instead.
                return handDist - 0.1f;
            }
        }

        // Updates weapon position and rotation and returns the new direction that the weapon is pointing toward.
        protected virtual Vector3 UpdateTwoHandedWield()
        {
            bool wasTwoHanded = (twoHandedState != TwoHandedState.SingleHanded);
            twoHandedState = GetDesiredTwoHandedState(wasTwoHanded);
            if (twoHandedState != TwoHandedState.SingleHanded)
            {
                rearHandTransform = twoHandedState == TwoHandedState.LeftHandBehind ? GetLeftHandTransform() : GetRightHandTransform();
                frontHandTransform = twoHandedState == TwoHandedState.LeftHandBehind ? GetRightHandTransform() : GetLeftHandTransform();

                Vector3 frontHandCenter = getHandCenter(frontHandTransform);
                Vector3 rearHandCenter = getHandCenter(rearHandTransform);
                Vector3 weaponPointingDir = (frontHandCenter - rearHandCenter).normalized;

                //weapon pos&rotation
                transform.position = rearHandCenter + weaponPointingDir * (HAND_CENTER_OFFSET + GetPreferredOffsetFromRearHand(Vector3.Distance(frontHandCenter, rearHandCenter)));
                transform.rotation = Quaternion.LookRotation(weaponPointingDir, GetPreferredTwoHandedWeaponUp()) * offsetFromPointingDir;
                return weaponPointingDir;
            }
            else if (wasTwoHanded)
            {
                ReturnToSingleHanded();
            }

            return GetWeaponPointingDir();
        }

        protected Quaternion GetOriginalRotation()
        {
            return originalTransform.rotation;
        }

        protected abstract bool IsPlayerLeftHanded();
        protected abstract Transform GetLeftHandTransform();
        protected abstract Transform GetRightHandTransform();
        protected abstract TwoHandedState GetDesiredTwoHandedState(bool wasTwoHanded);

        private void ReturnToSingleHanded()
        {
            transform.position = singleHandedTransform.position;
            transform.localRotation = singleHandedTransform.localRotation;
        }
    }
}
