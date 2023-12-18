using System.Collections.Generic;
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

        private static Dictionary<string, Vector3> EstimatedWeaponLocalPointingDirections = new Dictionary<string, Vector3>();
        private static Dictionary<string, float> DistancesBehindGripAndRearEnd = new Dictionary<string, float>();
        private Transform singleHandedTransform;
        private Transform originalTransform;
        private Quaternion offsetFromPointingDir; // The rotation offset of this transform relative to the direction the weapon is pointing at.
        private Vector3 estimatedLocalWeaponPointingDir = Vector3.forward;
        private float distanceBetweenGripAndRearEnd = 0.1f;

        public WeaponWield Initialize(ItemDrop.ItemData item, string itemName)
        {
            this.itemName = itemName;

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
                if (EstimatedWeaponLocalPointingDirections.ContainsKey(itemName) &&
                    DistancesBehindGripAndRearEnd.ContainsKey(itemName))
                {
                    estimatedLocalWeaponPointingDir = EstimatedWeaponLocalPointingDirections[itemName];
                    distanceBetweenGripAndRearEnd = DistancesBehindGripAndRearEnd[itemName];
                }
                else
                {
                    Vector3 handleAllowanceBehindGrip =
                        WeaponUtils.EstimateHandleAllowanceBehindGrip(weaponMeshFilter, handPosition: transform.parent.position);
                    EstimatedWeaponLocalPointingDirections.Add(
                        itemName,
                        estimatedLocalWeaponPointingDir =
                            transform.InverseTransformVector(-handleAllowanceBehindGrip).normalized);
                    DistancesBehindGripAndRearEnd.Add(
                        itemName,
                        distanceBetweenGripAndRearEnd = handleAllowanceBehindGrip.magnitude);
                    LogUtils.LogDebug("Registered " + itemName + " local pointing direction: " + estimatedLocalWeaponPointingDir + " distance between rear end and grip: " + distanceBetweenGripAndRearEnd);
                }
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
        }

        protected virtual void OnRenderObject()
        {
            UpdateTwoHandedWield();
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
                // Anchor the grip of the weapon in the rear/dominant hand.
                return -HAND_CENTER_OFFSET;
            }
            else if (handDist > distanceBetweenGripAndRearEnd)
            {
                // Anchor the rear end of the weapon in the rear/non-dominant hand.
                return distanceBetweenGripAndRearEnd - HAND_CENTER_OFFSET;
            }
            else
            {
                // Anchor the grip of the weapon in the front/dominant hand instead.
                return handDist - HAND_CENTER_OFFSET;
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
