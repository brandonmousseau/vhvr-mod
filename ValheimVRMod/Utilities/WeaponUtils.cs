using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

namespace ValheimVRMod.Utilities
{
    public static class WeaponUtils
    {
        
        private static readonly Dictionary<string, float> ATTACK_DURATIONS = new Dictionary<string, float>
        {
            { "atgeir_attack", 0.81f },
            { "battleaxe_attack", 0.87f },
            { "dual_knives", 0.43f },
            { "greatsword", 1.13f },
            { "knife_stab", 0.49f },
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

        public static float GetAttackDuration(Attack attack)
        {
            return ATTACK_DURATIONS.ContainsKey(attack.m_attackAnimation) ? ATTACK_DURATIONS[attack.m_attackAnimation] : DEFAULT_ATTACK_DURATION;
        }

        public static bool IsTwoHandedMultitargetSwipe(Attack attack)
        {
            return TWO_HANDED_MULTITARGET_SWIPE_NAMES.Contains(attack.m_attackAnimation);
        }

        // Estimates the direction that the weapon is pointing by identifying the dimension on which its mesh bounds is offset the farthest.
        // This estimation therefore assumes:
        //   1. The weapon pointing direction is parallel to the x, y, or z axis of the mesh; and
        //   2. The offset of tip of the weapon is larger than its lateral, dorsal, and ventral expanse.
        public static Vector3 EstimateWeaponPointingDirection(MeshFilter weaponMeshFilter, Vector3 handPosition)
        {
            Bounds weaponLocalBounds = weaponMeshFilter.sharedMesh.bounds;
            Vector3 centerOffset = weaponLocalBounds.center - weaponMeshFilter.transform.InverseTransformPoint(handPosition);
            float maxX = Mathf.Abs(centerOffset.x) + weaponLocalBounds.extents.x;
            float maxY = Mathf.Abs(centerOffset.y) + weaponLocalBounds.extents.y;
            float maxZ = Mathf.Abs(centerOffset.z) + weaponLocalBounds.extents.z;

            Vector3 longestDimension = weaponMeshFilter.transform.forward;
            if (maxX > maxY && maxX > maxZ)
            {
                longestDimension = weaponMeshFilter.transform.right;
            }
            else if (maxY > maxZ && maxY > maxX)
            {
                longestDimension = weaponMeshFilter.transform.up;
            }

            Vector3 roughDirection = weaponMeshFilter.transform.TransformPoint(weaponLocalBounds.center) - handPosition;
            return Vector3.Project(roughDirection, longestDimension).normalized;
        }

        // Whether the straight line (t -> p + t * v) intersects with the given bounds.
        public static bool LineIntersectsWithBounds(Bounds bounds, Vector3 p, Vector3 v)
        {
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
    }
}
