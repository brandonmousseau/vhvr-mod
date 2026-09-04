using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Rendering;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Utilities
{
    public static class WeaponUtils
    {
        private static readonly Vector3[] BASE = new Vector3[] { Vector3.right, Vector3.up, Vector3.forward };

        // TODO: consider use different default weapon collider lenght proportion for different weapon types.
        private const float DEFAULT_WEAPON_COLLIDER_LENGTH_PROPORTION = 0.75f;

        private static readonly Dictionary<string, WeaponColData> COLLIDERS = new Dictionary<string, WeaponColData>
        {
            {   // Axes
                "$item_axe_jotunbane", WeaponColData.create(
                    -0.0048f,  0.6406f, 0.001f,
                    0, 0, 0,
                    0.3860153f,  0.3521031f, 0.02278163f
                )}, {
                // Swords
                "$item_sword_bronze", WeaponColData.create(
                    0,  1.523f, 0,
                    0,  0, 0,
                    0.1934575f,  2.34697f, 0.05382534f
                )}, {
                "$item_sword_iron", WeaponColData.create(
                    0,  2.102f, 0,
                    0,  0, 0,
                    0.1934575f,  3.425369f, 0.05382534f
                )}, {
                "$item_sword_silver", WeaponColData.create(
                    0,  2.158f, 0,
                    0,  0, 0,
                    0.1101757f,  3.519603f, 0.05382534f
                )}, {
                // Clubs
                "$item_club", WeaponColData.create(
                    -0.013f,  -0.022f, 0.603f,
                    0,  -5.241f, 45,
                    0.1044289f,  0.09270307f, 0.5340722f
                )}, {
                "$item_mace_needle", WeaponColData.create(
                    0,  1.063f, 0,
                    0,  0, 0,
                    0.4f,  0.4f, 0.4f
                )}, {
                // Spears
                "$item_spear_wolffang", WeaponColData.create(
                    0,  -6.06f, 0,
                    0,  0, 0,
                    0.3996784f,  7.445521f, 0.4638378f
                )}, {
                "$item_spear_flint", WeaponColData.create(
                    0,  0, 0.738f,
                    0,  0, 0,
                    0.08946446f,  0.05617056f, 1.1811694f
                )}, {
                "$item_spear_chitin", WeaponColData.create( 
                     0,  1.12f, 0.008f,
                     0,  0, 0,
                     0.01591795f,  0.8536723f, 0.09076092f
                )}, {
                "$item_spear_ancientbark", WeaponColData.create(
                    0,  1.7915f, 0,
                    0,  0, 0,
                    0.07673188f,  0.9863854f, 0.02554126f
                )}, {
                "$item_spear_bronze", WeaponColData.create(
                    0,  1.882f, 0,
                    0,  0, 0,
                    0.07756059f,  1.025059f, 0.02554126f
                )}, {
                "$item_spear_carapace", WeaponColData.create(
                    0, 1.9915f, 0,
                    0, 0, 0,
                    0.07673188f,  0.8555415f, 0.02554126f
                )}, {
                // Sledges
                "$item_stagbreaker", WeaponColData.create(
                    0,  2.064f, 0,
                    0,  0, 0,
                    0.5530369f,  0.5530369f, 1.284601f
                )}, {
                // Knives
                "$item_knife_flint", WeaponColData.create(
                    -0.142f,  0.602f, 0,
                    0,  0, 28.779f,
                    0.2098247f,  0.6237586f, 0.03287503f
                )}, {
                "$item_knife_chitin", WeaponColData.create(
                    0,  0.208f, 0.027f,
                    15.689f,  0, 0,
                    0.0150678f,  0.3227402f, 0.086121f
                )}, {
                "$item_knife_butcher", WeaponColData.create(
                    -0.3549f,  0.968f, 0.472f,
                    15.689f,  20.2f, -1.5f,
                    0.4f,  0.01f, 0.06f
                )}, {
                // Tools
                "$item_hammer", WeaponColData.create(
                    0,  0.956f, 0,
                    0,  0, 0,
                    0.9030886f,  0.3803992f, 0.3803992f
                )}, {
                "$item_torch", WeaponColData.create(
                    0,  0.385f, 0,
                    0,  0, 0,
                    0.1f,  0.3f, 0.1f
                )}, {
                "$item_pickaxe_stone", WeaponColData.create(
                    -0.608f,  2.267f, 0,
                    0,  0, 0,
                    1.000489f,  0.177166f, 0.2626824f
                )}, {
                "$item_scythe", WeaponColData.create(
                    -0.3f,  1.4f, 0,
                    0,  0, 0,
                    0.71f,  0.71f, 0.1f
                )}, {
                "$item_tankard_anniversary", WeaponColData.create(
                    0,  0.48f, 0,
                    0, 0, 0,
                    0.18f,  0.03f, 0.18f
                )}, {
                "$item_tankard_odin", WeaponColData.create(
                    0,  -0.01f, 0.115f,
                    0, 0, 0,
                    0.025f, 0.025f, 0.005f
                )}
        };

        private static readonly Dictionary<EquipType, WeaponColData> DUAL_WIELD_COLLIDERS =
            new Dictionary<EquipType, WeaponColData>
            {
                {
                    EquipType.Claws, WeaponColData.create(
                        0, 0.25f, 0.016f,
                        0, 0, 0,
                        0.45f, 0.5f, 0.45f
                )},
                {
                    EquipType.DualAxes, WeaponColData.create(
                        0.45f, 0.2f, 0.05f,
                        0,  0, 0,
                        0.45f, 0.45f, 0.45f
                )},
                {
                    EquipType.DualKnives, WeaponColData.create(
                        0.225f, 0.15f, 0.05f,
                        0,  0, 0,
                        0.45f, 0.45f, 0.45f
                )},
                {
                    EquipType.None, WeaponColData.create( // fists
                        0, 0.2f, 0.016f,
                        0, 0, 0,
                        0.45f, 0.45f, 0.45f
                )},
                {
                    EquipType.Torch, WeaponColData.create(
                        0.55f, 0.15f, 0.05f,
                        0, 0, 0,
                        0.45f, 0.45f, 0.45f
                )},
                {
                    EquipType.Knife, WeaponColData.create(
                        0.33f, 0.2f, 0.01f,
                        0, 0, 0,
                        0.45f,  0.45f, 0.45f
                )},
                {
                    EquipType.Shield, WeaponColData.create(
                        0f, 0.125f, -0.0625f,
                        22.5f, 0, 0,
                        0.625f, 0.625f, 0.25f
                )},
            };

        private static readonly Dictionary<EquipType, WeaponColData> DUAL_WIELD_BLOCKING_COLLIDERS =
            new Dictionary<EquipType, WeaponColData>
            {
                {
                    EquipType.Claws, WeaponColData.create(
                        0,  0.05f, 0.016f,
                        0,  0, 0,
                        0.3f,  0.9f, 0.3f
                )},
                {
                    EquipType.DualAxes, WeaponColData.create(
                        0.4f,  0.2f, 0.016f,
                        0,  0, 0,
                        0.75f, 0.35f, 0.3f
                )},
                {
                    EquipType.DualKnives, WeaponColData.create(
                        0.15f,  0.15f, 0.016f,
                        0,  0, 0,
                        0.5f, 0.5f, 0.3f
                )},
                {
                    EquipType.Knife, WeaponColData.create(
                        0.4f,  0.2f, 0.016f,
                        0,  0, 0,
                        0.6f, 0.35f, 0.35f
                )},
                {
                    EquipType.Torch, WeaponColData.create(
                        0.4f,  0.2f, 0.016f,
                        0,  0, 0,
                        0.75f, 0.35f, 0.3f
                )},
                {
                    EquipType.None, WeaponColData.create( // fists
                        0,  0, 0.016f,
                        0,  0, 0,
                        0.3f,  0.85f, 0.3f
                )},
            };

        private static readonly Dictionary<string, float> ATTACK_DURATIONS = new Dictionary<string, float>
        {
            { "atgeir_attack", 0.81f },
            { "battleaxe_attack", 0.87f },
            { "dualaxes", 0.4f }, // TODO: Find an accurate value for dual axes
            { "dualaxes_secondary", 1.9f },
            { "dual_knives", 0.43f },
            { "greatsword", 1.13f },
            { "knife_stab", 0.49f },
            { "scything", 1.5f },
            { "swing_longsword", 0.63f },
            { "spear_poke", 0.63f },
            { "swing_pickaxe", 1.3f },
            { "swing_sledge", 2.15f},
            { "swing_axe", 0.64f },
            { "unarmed_attack", 0.54f },
            { "atgeir_secondary", 1.54f },
            { "battleaxe_secondary", 0.7f },
            { "dual_knives_secondary", 1.54f },
            { "greatsword_secondary", 2.04f },
            { "knife_secondary", 1.52f },
            { "sword_secondary", 1.84f },
            { "mace_secondary", 1.72f },
            { "axe_secondary", 2f },
            { "unarmed_kick", 1.34f }
        };
        private const float DEFAULT_ATTACK_DURATION = 0.63f;

        private static readonly HashSet<string> TWO_HANDED_MULTITARGET_SWIPE_NAMES =
            new HashSet<string>(new string[] { "battleaxe_attack", "atgeir_secondary" });

        private static readonly Dictionary<int, WeaponColData> estimatedColliders = new Dictionary<int, WeaponColData>();

        public static float GetAttackDuration(Attack attack)
        {
            return ATTACK_DURATIONS.ContainsKey(attack.m_attackAnimation) ? ATTACK_DURATIONS[attack.m_attackAnimation] : DEFAULT_ATTACK_DURATION;
        }

        public static bool IsTwoHandedMultitargetSwipe(Attack attack)
        {
            return TWO_HANDED_MULTITARGET_SWIPE_NAMES.Contains(attack.m_attackAnimation);
        }

        public static WeaponColData GetColliderData(int itemHash, ItemDrop.ItemData item, MeshFilter meshFilter, Vector3? handPosition)
        {
            if (COLLIDERS.TryGetValue(item.m_shared.m_name, out var weaponColData))
            {
                return weaponColData;
            }

            if (estimatedColliders.TryGetValue(itemHash, out var estimated))
            {
                return estimated;
            }

            if (meshFilter != null && handPosition != null)
            {
                var estimatedCollider = EstimateWeaponCollider(meshFilter, (Vector3)handPosition, EquipScript.GetEquipType(item));
                if (itemHash != 0)
                {
                    estimatedColliders[itemHash] = estimatedCollider;
                }
                LogUtils.LogDebug(
                    "Estimated and registered collider for weapon " + itemHash + " " + item.m_shared.m_name + ": position " + estimatedCollider.pos + " scale " + estimatedCollider.scale);
                return estimatedCollider;
            }

            throw new InvalidEnumArgumentException();
        }

        public static WeaponColData GetDualWieldLeftHandColliderData(ItemDrop.ItemData item)
        {
            return GetDualWieldLeftHandColliderData(EquipScript.GetEquipType(item));
        }

        public static WeaponColData GetDualWieldLeftHandColliderData(EquipType equipType)
        {
            if (!DUAL_WIELD_COLLIDERS.ContainsKey(equipType))
            {
                equipType = EquipType.None;
            }
            return DUAL_WIELD_COLLIDERS[equipType];
        }

        public static WeaponColData GetDualWieldLeftHandBlockingColliderData(ItemDrop.ItemData item)
        {
            var equipType = EquipScript.GetEquipType(item);
            if (!DUAL_WIELD_BLOCKING_COLLIDERS.ContainsKey(equipType))
            {
                equipType = EquipType.None;
            }
            return DUAL_WIELD_BLOCKING_COLLIDERS[equipType];
        }

        // Estimates the direction and length of a weapon by identifying the dimension on which its mesh bounds is offset the farthest.
        // This estimation therefore assumes:
        //   1. The weapon pointing direction is parallel to the x, y, or z axis of the mesh; and
        //   2. The offset of tip of the weapon is larger than its lateral, dorsal, and ventral expanse.
        public static Vector3 EstimateWeaponDirectionAndLength(
            MeshFilter weaponMeshFilter, Vector3 handPosition, out float handleAllowanceBehindGrip)
        {
            Bounds weaponLocalBounds = weaponMeshFilter.sharedMesh.bounds;
            Vector3 centerOffset = weaponLocalBounds.center - weaponMeshFilter.transform.InverseTransformPoint(handPosition);
            Vector3[] corners = new Vector3[] {
                centerOffset - weaponLocalBounds.extents,
                centerOffset + weaponLocalBounds.extents
            };

            float longestExtrusion = 0;
            Vector3 weaponPointingDirection = Vector3.zero;
            float weaponLength = 0;
            for (int i = 0; i < 3; i++)
            {
                foreach (Vector3 corner in corners)
                {
                    float extrusion = corner[i];
                    if (Mathf.Abs(extrusion) > longestExtrusion)
                    {
                        longestExtrusion = Mathf.Abs(extrusion);
                        weaponPointingDirection = BASE[i] * Mathf.Sign(extrusion);
                        weaponLength = weaponLocalBounds.size[i];
                    }
                }
            }

            if (weaponLength < longestExtrusion)
            {
                weaponLength = longestExtrusion;
                LogUtils.LogWarning("Weapon mesh is off hand, weapon direction and length estimation might be inaccurate.");
            }
            var result = weaponMeshFilter.transform.TransformVector(weaponPointingDirection * weaponLength);
            handleAllowanceBehindGrip = result.magnitude * (1 - longestExtrusion / weaponLength);
            return result;
        }

        public static EquipType GuesstEquipTypeFromShape(float weaponLength, float distanceBetweenGripAndRearEnd, bool isDominantHandWeapon)
        {
            if (!isDominantHandWeapon)
            {
                return weaponLength > 0.5F && distanceBetweenGripAndRearEnd > 0.5f ? EquipType.Crossbow : EquipType.None;
            }

            if (weaponLength > 2f && distanceBetweenGripAndRearEnd > 0.95f)
            {
                return EquipType.Spear;
            }

            if (weaponLength > 2.5f && distanceBetweenGripAndRearEnd > 0.7f)
            {
                return EquipType.Polearms;
            }

            if (weaponLength > 3 && distanceBetweenGripAndRearEnd > 0.3f)
            {
                return EquipType.Fishing;
            }

            if (weaponLength > 1.8f && distanceBetweenGripAndRearEnd > 0.85f)
            {
                return EquipType.Magic;
            }

            if (weaponLength > 1.5f && distanceBetweenGripAndRearEnd > 0.8f)
            {
                return EquipType.Scythe;
            }

            if (weaponLength > 1.9f && distanceBetweenGripAndRearEnd > 0.28f)
            {
                return EquipType.Sword;
            }

            if (weaponLength > 1.69f && distanceBetweenGripAndRearEnd > 0.25f && distanceBetweenGripAndRearEnd < 0.35f)
            {
                return EquipType.BattleAxe;
            }

            if (weaponLength > 1 && distanceBetweenGripAndRearEnd > 0.45f)
            {
                return EquipType.Magic;
            }

            if (weaponLength < 0.7f && distanceBetweenGripAndRearEnd < 0.1f)
            {
                return EquipType.Knife;
            }

            return EquipType.Club;
        }

        // Whether the straight line (t -> p + t * v) intersects with the given bounds.
        public static bool LineIntersectsWithBounds(Bounds bounds, Vector3 p, Vector3 v)
        {
            // TODO: Consider removing this method since it is not used anywhere.

            // Center the bound and the line around the original bounds center to simplify calculation.
            Bounds centeredBounds = new Bounds(Vector3.zero, bounds.size);
            Vector3 p0 = p - bounds.center;

            // Where the line intersects with the right plane of the bounds.
            Vector3 rightIntersection = p0 + v * ((bounds.extents.x - p0.x) / v.x);
            if (centeredBounds.Contains(Vector3.ProjectOnPlane(rightIntersection, Vector3.right)))
            {
                // The line intersects with the right face of the bounds.
                return true;
            }

            // Where the line intersects with the left plane of the bounds.
            Vector3 leftIntersection = p0 + v * ((-bounds.extents.x - p0.x) / v.x);
            if (centeredBounds.Contains(Vector3.ProjectOnPlane(leftIntersection, Vector3.right)))
            {
                // The line intersects with the left face of the bounds.
                return true;
            }

            // Where the line intersects with the top plane of the bounds.
            Vector3 topIntersection = p0 + v * ((bounds.extents.y - p0.y) / v.y);
            if (centeredBounds.Contains(Vector3.ProjectOnPlane(topIntersection, Vector3.up)))
            {
                // The line intersects with the top face of the bounds.
                return true;
            }

            // Where the line intersects with the bottom plane of the bounds.
            Vector3 bottomIntersection = p0 + v * ((-bounds.extents.y - p0.y) / v.y);
            if (centeredBounds.Contains(Vector3.ProjectOnPlane(bottomIntersection, Vector3.up)))
            {
                // The line intersects with the bottom face of the bounds.
                return true;
            }

            // Where the line intersects with the front plane of the bounds.
            Vector3 frontIntersection = p0 + v * ((bounds.extents.z - p0.z) / v.z);
            if (centeredBounds.Contains(Vector3.ProjectOnPlane(frontIntersection, Vector3.forward)))
            {
                // The line intersects with the front face of the bounds.
                return true;
            }

            // Where the line intersects with the rear plane of the bounds.
            Vector3 rearIntersection = p0 + v * ((-bounds.extents.z - p0.z) / v.z);
            if (centeredBounds.Contains(Vector3.ProjectOnPlane(rearIntersection, Vector3.forward)))
            {
                // The line intersects with the rear face of the bounds.
                return true;
            }

            return false;
        }

        // Find the mesh renderer of the glow on a bow or crossbow if that mesh renderer should be hidden
        // when it is bending. We can only bend the bow/crossbow not its glow, so it looks better with the
        // glow removed.
        public static MeshRenderer GetHideableBowGlowMeshRenderer(Transform bowTransform, string bowName)
        {
            // TODO: consider explicitly adding a list of the names of the bows and crossbows here instead
            // of calling string#Contains().
            if (!bowName.Contains("blood") && !bowName.Contains("storm") && !bowName.Contains("root") &&
                !bowName.Contains("lightning") && !bowName.Contains("nature"))
            {
                return null;
            }
            for (int i = 0; i < bowTransform.childCount; i++)
            {
                var meshRenderer = bowTransform.GetChild(i).GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    return meshRenderer;
                }
            }
            return null;
        }

        private const float MAX_STAB_ANGLE_TWO_HAND = 40;
        private const float MAX_STAB_ANGLE = 30;
        public static bool IsStab(Vector3 velocity, Vector3 weaponPointing, bool isTwoHanded)
        {
            return Vector3.Angle(velocity, weaponPointing) < (isTwoHanded ? MAX_STAB_ANGLE_TWO_HAND : MAX_STAB_ANGLE);
        }

        public static void AlignLoadedMeshToUnloadedMesh(GameObject loaded, GameObject unloaded)
        {
            var loadedMeshFilter = loaded.GetComponentInChildren<MeshFilter>();
            var unloadedMeshFilter = unloaded.GetComponentInChildren<MeshFilter>();
            var loadedMeshCenter = loadedMeshFilter.transform.TransformPoint(loadedMeshFilter.sharedMesh.bounds.center);
            var unloadedMeshCenter = unloadedMeshFilter.transform.TransformPoint(unloadedMeshFilter.sharedMesh.bounds.center);

            loaded.transform.position += (unloadedMeshCenter - loadedMeshCenter);
        }

        public static GameObject CreateDebugSphere(Transform parent)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            PrepareDebugGameObject(sphere, parent);
            return sphere;
        }

        public static GameObject CreateDebugBox(Transform parent)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            PrepareDebugGameObject(box, parent);
            return box;
        }

        private static void PrepareDebugGameObject(GameObject gameObject, Transform parent)
        {
            gameObject.transform.parent = parent;
            gameObject.transform.localScale = Vector3.one;
            gameObject.transform.localPosition = Vector3.zero;
            gameObject.transform.localRotation = Quaternion.identity;
            gameObject.GetComponent<MeshRenderer>().material = Object.Instantiate(VRAssetManager.GetAsset<Material>("Unlit"));
            gameObject.GetComponent<MeshRenderer>().material.color = new Vector4(0.5f, 0, 0, 0.5f);
            gameObject.GetComponent<MeshRenderer>().receiveShadows = false;
            gameObject.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            gameObject.GetComponent<MeshRenderer>().reflectionProbeUsage = ReflectionProbeUsage.Off;
            Object.Destroy(gameObject.GetComponent<Collider>());
        }

        private static WeaponColData EstimateWeaponCollider(MeshFilter meshFilter, Vector3 handPosition, EquipType type)
        {
            var weaponPointing =
                meshFilter.transform.InverseTransformDirection(
                    EstimateWeaponDirectionAndLength(meshFilter, handPosition, out float handleAllowanceBehindGrip)).normalized;
            var handLocalPosition = meshFilter.transform.InverseTransformPoint(handPosition);
            var bounds = meshFilter.mesh.bounds;
            var weaponTip = bounds.center + weaponPointing * Mathf.Abs(Vector3.Dot(bounds.extents, weaponPointing));
            var colliderLength = EstimateColliderLength(Vector3.Distance(weaponTip, handLocalPosition), type);
            var colliderCenter = (type == EquipType.Pickaxe ? weaponTip : weaponTip - weaponPointing * (colliderLength * 0.5f));
            var colliderOffset = colliderCenter - bounds.center;
            var colliderSize =
                bounds.size - (new Vector3(Mathf.Abs(colliderOffset.x), Mathf.Abs(colliderOffset.y), Mathf.Abs(colliderOffset.z))) * 2;
            // In case the collider length exceeds the mesh bound, extend the collider size to respect the collider length
            colliderSize = Vector3.Max(colliderSize, colliderLength * weaponPointing);
            return new WeaponColData(colliderCenter, Vector3.zero, colliderSize);
        }

        private static float EstimateColliderLength(float weaponTipDistanceFromHand, EquipType type)
        {
            switch (type)
            {
                case EquipType.Axe:
                case EquipType.BattleAxe:
                case EquipType.Club:
                case EquipType.Sledge:
                    return weaponTipDistanceFromHand * 0.375f;
                case EquipType.Pickaxe:
                case EquipType.Spear:
                    return weaponTipDistanceFromHand * 0.875f;
                case EquipType.Sword:
                    return Mathf.Max(0.125f, weaponTipDistanceFromHand - 0.15f);
                default:
                    return weaponTipDistanceFromHand * 0.75f;
            }
        }

        public static Vector3 GetWeaponVelocity(Vector3 handVelocity, Vector3 handAngularVelocity, Vector3 weaponOffset)
        {
            return handVelocity + Vector3.Cross(handAngularVelocity, weaponOffset);
        }

        // Update the holding direction of the knife based button press and hand angular momentum.
        public static bool MaybeFlipKnife(bool isKnifeCurrentlyUlnarPointing, bool isLeftHand)
        {
            var inputSource = isLeftHand ? SteamVR_Input_Sources.LeftHand : SteamVR_Input_Sources.RightHand;
            var isReleasing = SteamVR_Actions.valheim_Grab.GetStateUp(inputSource);
            if (!isReleasing) {
                var isCatching = SteamVR_Actions.valheim_Grab.GetStateDown(inputSource);
                if (!isCatching)
                {
                    // Neither releasing or catching the knife, do not change current orientation.
                    return isKnifeCurrentlyUlnarPointing;
                }
            }

            var physicsEstimator = isLeftHand ? VRPlayer.leftHandPhysicsEstimator : VRPlayer.rightHandPhysicsEstimator;
            var rotationSpeed = Vector3.Dot(physicsEstimator.GetAngularVelocity(), physicsEstimator.transform.up);

            if (-8 < rotationSpeed && rotationSpeed < 8)
            {
                // Hand rotating too slow, do not change current orientation.
                return isKnifeCurrentlyUlnarPointing;
            }

            // Update orientation based on hand rotation direction, handedness, and catching/releasing.
            return rotationSpeed > 0 ^ (isLeftHand ^ isReleasing);
        }
    }
}
