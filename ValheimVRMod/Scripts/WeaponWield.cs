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

        public const float HAND_CENTER_OFFSET = 0.08f;

        public TwoHandedState twoHandedState { get; private set; }
        public Transform rearHandTransform { get; private set; }
        public Transform frontHandTransform { get; private set; }

        protected string attackAnimation { get; private set; }
        protected string itemName { get; private set; }

        public Vector3 originalPosition { get { return originalTransform.position; } }
        public Quaternion originalRotation { get { return originalTransform.rotation; } }
        public Quaternion offsetFromPointingDir; // The rotation offset of this transform relative to the direction the weapon is pointing at.
        protected TwoHandedGeometryProvider geometryProvider;

        private static Dictionary<string, Vector3> EstimatedWeaponLocalPointingDirections = new Dictionary<string, Vector3>();
        private static Dictionary<string, float> DistancesBehindGripAndRearEnd = new Dictionary<string, float>();
        private Transform originalTransform;
        private Vector3 longestLocalExtrusion = Vector3.forward;
        private float distanceBetweenGripAndRearEnd = 0.1f;
        private EquipType equipType;

        public WeaponWield Initialize(ItemDrop.ItemData item, string itemName, bool forceUsingCrossbowGeometry = false)
        {
            this.itemName = itemName;
            equipType = (item == null ? EquipType.None : EquipScript.getEquippedItem(item));
            if (forceUsingCrossbowGeometry)
            {
                equipType = EquipType.Crossbow;
            }

            attackAnimation = item?.m_shared.m_attack?.m_attackAnimation ?? "";

            originalTransform = new GameObject().transform;
            originalTransform.parent = transform.parent;
            originalTransform.position = transform.position;
            originalTransform.rotation = transform.rotation;

            MeshFilter weaponMeshFilter = gameObject.GetComponentInChildren<MeshFilter>();
            if (weaponMeshFilter != null)
            {
                if (item != null &&
                    itemName != "" &&
                    EstimatedWeaponLocalPointingDirections.ContainsKey(itemName) &&
                    DistancesBehindGripAndRearEnd.ContainsKey(itemName))
                {
                    longestLocalExtrusion = EstimatedWeaponLocalPointingDirections[itemName];
                    distanceBetweenGripAndRearEnd = DistancesBehindGripAndRearEnd[itemName];
                }
                else
                {
                    Vector3 handleAllowanceBehindGrip =
                        WeaponUtils.EstimateHandleAllowanceBehindGrip(weaponMeshFilter, handPosition: transform.parent.position);
                    longestLocalExtrusion = transform.InverseTransformVector(-handleAllowanceBehindGrip).normalized;
                    distanceBetweenGripAndRearEnd = handleAllowanceBehindGrip.magnitude;
                    if (item != null && itemName != "") {
                        EstimatedWeaponLocalPointingDirections.Add(itemName, longestLocalExtrusion);
                        DistancesBehindGripAndRearEnd.Add(itemName, distanceBetweenGripAndRearEnd);
                        LogUtils.LogDebug("Registered " + itemName + " local pointing direction: " + longestLocalExtrusion + " distance between rear end and grip: " + distanceBetweenGripAndRearEnd);
                    } 
                }
            }

            geometryProvider = GetGeometryProvider(longestLocalExtrusion, distanceBetweenGripAndRearEnd);

            var weaponPointing = GetWeaponPointingDirection();
            offsetFromPointingDir =
                Quaternion.Inverse(Quaternion.LookRotation(weaponPointing, transform.up)) * transform.rotation;

            transform.position = geometryProvider.GetDesiredSingleHandedPosition(this);
            transform.rotation = geometryProvider.GetDesiredSingleHandedRotation(this);

            if (IsDundr())
            {
                WeaponUtils.AlignLoadedMeshToUnloadedMesh(
                    transform.Find("Loaded").gameObject, weaponMeshFilter.gameObject);
            }

            return this;
        }

        protected Vector3 GetWeaponPointingDirection()
        {
            return geometryProvider.GetWeaponPointingDirection(transform, transform.TransformDirection(longestLocalExtrusion));
        }

        protected bool IsHandGunMode()
        {
            return IsDundr() && twoHandedState == TwoHandedState.SingleHanded;
        }

        protected static Vector3 getHandCenter(Transform hand)
        {
            return hand.transform.position - hand.transform.forward * HAND_CENTER_OFFSET;
        }

        protected virtual void OnDestroy()
        {
            Destroy(originalTransform.gameObject);
        }

        protected virtual void OnRenderObject()
        {
            UpdateTwoHandedWield();
        }

        // Updates weapon position and rotation and returns the new direction that the weapon is pointing toward.
        protected virtual Vector3 UpdateTwoHandedWield()
        {
            var wasTwoHanded = (twoHandedState != TwoHandedState.SingleHanded);
            twoHandedState = GetDesiredTwoHandedState(wasTwoHanded);

            if (twoHandedState == TwoHandedState.SingleHanded)
            {
                if (wasTwoHanded || IsDundr() || equipType == EquipType.Spear || equipType == EquipType.SpearChitin)
                {
                    transform.SetPositionAndRotation(
                        geometryProvider.GetDesiredSingleHandedPosition(this),
                        geometryProvider.GetDesiredSingleHandedRotation(this));
                }
                return GetWeaponPointingDirection();
            }

            rearHandTransform = twoHandedState == TwoHandedState.LeftHandBehind ? GetLeftHandTransform() : GetRightHandTransform();
            frontHandTransform = twoHandedState == TwoHandedState.LeftHandBehind ? GetRightHandTransform() : GetLeftHandTransform();

            Vector3 frontHandCenter = getHandCenter(frontHandTransform);
            Vector3 rearHandCenter = getHandCenter(rearHandTransform);
            Vector3 weaponPointingDir = (frontHandCenter - rearHandCenter).normalized;

            //weapon pos&rotation
            transform.position =
                rearHandCenter + 
                weaponPointingDir * (HAND_CENTER_OFFSET + geometryProvider.GetPreferredOffsetFromRearHand(
                    Vector3.Distance(frontHandCenter, rearHandCenter), IsPlayerLeftHanded() == (twoHandedState == TwoHandedState.LeftHandBehind)));
            transform.rotation =
                Quaternion.LookRotation(weaponPointingDir, geometryProvider.GetPreferredTwoHandedWeaponUp(this)) * offsetFromPointingDir;
            return weaponPointingDir;
        }

        protected abstract bool IsPlayerLeftHanded();
        protected abstract Transform GetLeftHandTransform();
        protected abstract Transform GetRightHandTransform();
        protected abstract TwoHandedState GetDesiredTwoHandedState(bool wasTwoHanded);

        private bool IsDundr()
        {
            return itemName == "StaffLightning";
        }

        private TwoHandedGeometryProvider GetGeometryProvider(Vector3 longestLocalExtrusion, float distanceBetweenGripAndRearEnd)
        {
            bool isLocal = GetComponentInParent<Player>() == Player.m_localPlayer;

            switch (equipType)
            {
                case EquipType.Spear:
                case EquipType.SpearChitin:
                    if (isLocal)
                    {
                        return new TwoHandedGeometry.LocalSpearGeometryProvider();
                    }
                    break;
                case EquipType.Polearms:
                    return new TwoHandedGeometry.AtgeirGeometryProvider(distanceBetweenGripAndRearEnd);
                case EquipType.Crossbow:
                    return isLocal ?
                        new TwoHandedGeometry.LocalCrossbowGeometryProvider() :
                        new TwoHandedGeometry.CrossbowGeometryProvider(IsPlayerLeftHanded());
            }

            if (IsDundr())
            {
                return new TwoHandedGeometry.DundrGeometryProvider();
            }

            return new TwoHandedGeometry.DefaultGeometryProvider(distanceBetweenGripAndRearEnd);
        }

        public interface TwoHandedGeometryProvider {
            // Returns the direction the weapon is pointing.
            Vector3 GetWeaponPointingDirection(Transform weaponTransform, Vector3 longestExtrusion);
            Vector3 GetDesiredSingleHandedPosition(WeaponWield weaponWield);
            Quaternion GetDesiredSingleHandedRotation(WeaponWield weaponWield);
            // The preferred up direction used to determine the weapon's rotation around it longitudinal axis during two-handed wield.
            Vector3 GetPreferredTwoHandedWeaponUp(WeaponWield weaponWield);
            // The preferred forward offset amount of the weapon's position from the rear hand during two-handed wield.
            float GetPreferredOffsetFromRearHand(float handDist, bool rearHandIsDominant);
        }
    }
}
