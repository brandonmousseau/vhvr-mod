using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts
{
    public abstract class WeaponWield : MonoBehaviour, WeaponWield.LongGripStateProvider
    {
        protected bool currentTwoHandedWieldStartedWithLongGrip { get; private set; }

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
        public Quaternion offsetFromPointingDir { get; protected set; } // The rotation offset of this transform relative to the direction the weapon is pointing at.
        protected TwoHandedGeometryProvider geometryProvider;
        protected float weaponLength { get; private set; }
        protected float distanceBetweenGripAndRearEnd { get; private set; } = 0.1f;
        private static Dictionary<string, Vector3> EstimatedWeaponLocalDirectionsAndLengths = new Dictionary<string, Vector3>();
        private static Dictionary<string, float> DistancesBehindGripAndRearEnd = new Dictionary<string, float>();
        private Transform originalTransform;
        private Vector3 longestLocalExtrusion = Vector3.forward;

        private EquipType equipType;
        private bool isLocal;
        private bool isDominantHandWeapon;

        public WeaponWield Initialize(
            ItemDrop.ItemData item, string itemName, bool isDominantHandWeapon, WeaponWieldSync.TwoHandedStateProvider twoHandedStateProvider = null)
        {
            isLocal = GetComponentInParent<Player>() == Player.m_localPlayer;
            this.isDominantHandWeapon = isDominantHandWeapon;
            this.itemName = itemName;
            equipType = (item == null ? EquipType.None : EquipScript.getEquippedItem(item));

            attackAnimation = item?.m_shared.m_attack?.m_attackAnimation ?? "";

            originalTransform = new GameObject().transform;
            originalTransform.parent = transform.parent;
            originalTransform.position = transform.position;
            originalTransform.rotation = transform.rotation;

            MeshFilter weaponMeshFilter = gameObject.GetComponentInChildren<MeshFilter>();
            if (itemName == "Hoe") {
                var meshFilters = gameObject.GetComponentsInChildren<MeshFilter>();
                foreach (var meshFilter in meshFilters)
                {
                    if (meshFilter.name == "handle")
                    {
                        weaponMeshFilter = meshFilter;
                    }
                }
            }

            if (weaponMeshFilter != null)
            {
                if (item != null &&
                    itemName != "" &&
                    EstimatedWeaponLocalDirectionsAndLengths.ContainsKey(itemName) &&
                    DistancesBehindGripAndRearEnd.ContainsKey(itemName))
                {
                    var weaponDirectionAndLength = EstimatedWeaponLocalDirectionsAndLengths[itemName];
                    longestLocalExtrusion = weaponDirectionAndLength.normalized;
                    weaponLength = weaponDirectionAndLength.magnitude;
                    distanceBetweenGripAndRearEnd = DistancesBehindGripAndRearEnd[itemName];
                }
                else
                {
                    Vector3 weaponDirectionAndLength =
                        transform.InverseTransformDirection(
                            WeaponUtils.EstimateWeaponDirectionAndLength(
                                weaponMeshFilter, handPosition: transform.parent.position, out float distanceBetweenGripAndRearEnd));
                    this.distanceBetweenGripAndRearEnd = distanceBetweenGripAndRearEnd;
                    longestLocalExtrusion = weaponDirectionAndLength.normalized;
                    weaponLength = weaponDirectionAndLength.magnitude;
                    if (item != null && itemName != "") {
                        EstimatedWeaponLocalDirectionsAndLengths.Add(itemName, weaponDirectionAndLength);
                        DistancesBehindGripAndRearEnd.Add(itemName, distanceBetweenGripAndRearEnd);
                        LogUtils.LogDebug("Registered " + itemName + " local pointing direction and length: " + weaponDirectionAndLength + " distance between rear end and grip: " + distanceBetweenGripAndRearEnd);
                    }
                    else if (!isLocal)
                    {
                        equipType = WeaponUtils.GuesstEquipTypeFromShape(weaponLength, distanceBetweenGripAndRearEnd, isDominantHandWeapon);
                    }
                }
            }

            geometryProvider = GetGeometryProvider(longestLocalExtrusion, distanceBetweenGripAndRearEnd, twoHandedStateProvider);

            var weaponPointing = GetWeaponPointingDirection();
            offsetFromPointingDir =
                Quaternion.Inverse(Quaternion.LookRotation(weaponPointing, transform.up)) * transform.rotation;

            transform.position = geometryProvider.GetDesiredSingleHandedPosition(this);
            transform.rotation = geometryProvider.GetDesiredSingleHandedRotation(this);

            return this;
        }

        public bool ShouldUseLongGrip()
        {
            return currentTwoHandedWieldStartedWithLongGrip;
        }

        public Quaternion getAimingRotation(Vector3 pointing, Vector3 upDirection)
        {
            return Quaternion.LookRotation(pointing, upDirection) * offsetFromPointingDir;
        }

        protected Vector3 GetWeaponPointingDirection()
        {
            return geometryProvider.GetWeaponPointingDirection(transform, transform.TransformDirection(longestLocalExtrusion));
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
            bool wasTwoHanded = twoHandedState != TwoHandedState.SingleHanded;

            twoHandedState = GetDesiredTwoHandedState(wasTwoHanded);

            if (twoHandedState == TwoHandedState.SingleHanded)
            {
                transform.SetPositionAndRotation(
                    geometryProvider.GetDesiredSingleHandedPosition(this), geometryProvider.GetDesiredSingleHandedRotation(this));
                return GetWeaponPointingDirection();
            }

            rearHandTransform = twoHandedState == TwoHandedState.LeftHandBehind ? GetLeftHandTransform() : GetRightHandTransform();
            frontHandTransform = twoHandedState == TwoHandedState.LeftHandBehind ? GetRightHandTransform() : GetLeftHandTransform();
            Vector3 frontHandCenter = getHandCenter(frontHandTransform);
            Vector3 rearHandCenter = getHandCenter(rearHandTransform);
            Vector3 weaponPointingDir = (frontHandCenter - rearHandCenter).normalized;

            if (!wasTwoHanded)
            {
                bool isMainHandBehind =
                    IsPlayerLeftHanded() ^ isDominantHandWeapon ?
                    twoHandedState == TwoHandedState.RightHandBehind :
                    twoHandedState == TwoHandedState.LeftHandBehind;
                Transform offHand = isMainHandBehind ? frontHandTransform : rearHandTransform;
                currentTwoHandedWieldStartedWithLongGrip =
                    isMainHandBehind ?
                    Vector3.Dot(frontHandTransform.forward, rearHandTransform.forward) > 0 && Vector3.Distance(frontHandCenter, rearHandCenter) < 0.375f :
                    Vector3.Dot(offHand.forward, weaponPointingDir) < 0 || Vector3.Distance(frontHandCenter, rearHandCenter) > 0.375f;
            }

            //weapon pos&rotation
            transform.position =
                rearHandCenter +
                weaponPointingDir * (HAND_CENTER_OFFSET + geometryProvider.GetPreferredOffsetFromRearHand(
                    Vector3.Distance(frontHandCenter, rearHandCenter), IsPlayerLeftHanded() == (twoHandedState == TwoHandedState.LeftHandBehind)));
            transform.rotation = 
                getAimingRotation(weaponPointingDir, geometryProvider.GetPreferredTwoHandedWeaponUp(this));
            return weaponPointingDir;
        }

        protected abstract bool IsPlayerLeftHanded();
        protected abstract Transform GetLeftHandTransform();
        protected abstract Transform GetRightHandTransform();
        protected abstract TwoHandedState GetDesiredTwoHandedState(bool wasTwoHanded);

        protected bool IsDundr()
        {
            return itemName == "StaffLightning";
        }

        private TwoHandedGeometryProvider GetGeometryProvider
            (Vector3 longestLocalExtrusion, float distanceBetweenGripAndRearEnd, WeaponWieldSync.TwoHandedStateProvider twoHandedStateProvider)
        {
            if (IsDundr())
            {
                return new TwoHandedGeometry.DundrGeometryProvider();
            }

            switch (equipType)
            {
                case EquipType.Axe:
                case EquipType.Pickaxe:
                    return new TwoHandedGeometry.DefaultGeometryProvider(distanceBetweenGripAndRearEnd * 0.5f);
                case EquipType.Club:
                case EquipType.Cultivator:
                case EquipType.Fishing:
                case EquipType.Hoe:
                case EquipType.Torch:
                    return new TwoHandedGeometry.DefaultGeometryProvider(distanceBetweenGripAndRearEnd: 0);
                case EquipType.BattleAxe:
                    return isLocal ? 
                        new TwoHandedGeometry.LocalBattleaxeGeometryProvider(distanceBetweenGripAndRearEnd * 0.3f, this) :
                        new TwoHandedGeometry.BattleaxeGeometryProvider(distanceBetweenGripAndRearEnd * 0.3f, this);
                case EquipType.Crossbow:
                    return isLocal ?
                        new TwoHandedGeometry.LocalCrossbowGeometryProvider() :
                        new TwoHandedGeometry.CrossbowGeometryProvider(IsPlayerLeftHanded());
                case EquipType.Knife:
                    if (isLocal)
                    {
                        return new TwoHandedGeometry.LocalKnifeGeometryProvider(distanceBetweenGripAndRearEnd);
                    }
                    break;
                case EquipType.Magic:
                    if (isDominantHandWeapon)
                    {
                        return new TwoHandedGeometry.StaffGeometryProvider(distanceBetweenGripAndRearEnd);
                    }
                    break;
                case EquipType.Polearms:
                    return isLocal ?
                        new TwoHandedGeometry.LocalAtgeirGeometryProvider(distanceBetweenGripAndRearEnd, this) :
                        new TwoHandedGeometry.AtgeirGeometryProvider(distanceBetweenGripAndRearEnd, this);
                case EquipType.Scythe:
                    return new TwoHandedGeometry.ScytheGeometryProvider(IsPlayerLeftHanded(), distanceBetweenGripAndRearEnd);
                case EquipType.Sledge:
                    return isLocal ?
                        new TwoHandedGeometry.LocalSledgeGeometryProvider(distanceBetweenGripAndRearEnd) :
                        new TwoHandedGeometry.SledgeGeometryProvider(distanceBetweenGripAndRearEnd);
                case EquipType.Sword:
                    if (isLocal)
                    {
                        return new TwoHandedGeometry.LocalSwordGeometryProvider(Mathf.Max(distanceBetweenGripAndRearEnd * 0.75f, 0.125f));
                    }
                    break;
                case EquipType.Spear:
                case EquipType.SpearChitin:
                    if (isLocal)
                    {
                        return new TwoHandedGeometry.LocalSpearGeometryProvider(this);
                    }
                    else if (twoHandedStateProvider != null)
                    {
                        return new TwoHandedGeometry.RemoteSpearGeometryProvider(twoHandedStateProvider, this);
                    }
                    break;
            }

            if (!isLocal && twoHandedStateProvider != null)
            {
                return new TwoHandedGeometry.RemoteGeometryProvider(distanceBetweenGripAndRearEnd, twoHandedStateProvider);
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
            bool InverseHoldForDominantHand();
            bool ShouldRotateHandForOneHandedWield();
        }

        public interface LongGripStateProvider {
            bool ShouldUseLongGrip();
        }

    }
}
