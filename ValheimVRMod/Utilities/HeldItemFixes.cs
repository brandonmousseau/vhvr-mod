using System.Collections.Generic;
using UnityEngine;

namespace ValheimVRMod.Utilities
{
    // Fixes of how the vanilla prefabs of items are held in the hand.
    public static class HeldItemFixes
    {
        // The items that have nothing to show in the hand: what is to be attached is named "model" in the vanilla
        // prefab rather than "attach".
        private static readonly HashSet<int> ITEMS_WITHOUT_ATTACH_OBJECT = new HashSet<int>
        {
            "Honey".GetStableHashCode()
        };

        // The items whose mesh is inactive in what is attached of the vanilla prefab.
        private static readonly HashSet<int> ITEMS_WITH_INACTIVE_MESH = new HashSet<int>
        {
            "MeadStaminaLingering".GetStableHashCode()
        };

        // The items that the vanilla prefab sticks through the right hand, with their top out of the back of it. They
        // are to be held with their top towards the thumb instead.
        private static readonly HashSet<int> ITEMS_STUCK_THROUGH_HAND = new HashSet<int>
        {
            "Carrot".GetStableHashCode(),
            "MushroomJotunPuffs".GetStableHashCode(),
            "MushroomMagecap".GetStableHashCode(),
            "MushroomSmokePuff".GetStableHashCode(),
            "Mushroom".GetStableHashCode()
        };

        // The items that the vanilla prefab puts on the back of the right hand rather than in its palm.
        private static readonly HashSet<int> ITEMS_HELD_ON_BACK_OF_HAND = new HashSet<int>
        {
            "OvenPancake".GetStableHashCode(),
            "SparklingShroomshake".GetStableHashCode(),
            "ShocklateSmoothie".GetStableHashCode(),
            "QueensJam".GetStableHashCode(),
            "BakedPoteitr".GetStableHashCode(),
            "Kale".GetStableHashCode(),
            "SpicyMarmalade".GetStableHashCode()
        };

        // The items whose big end the vanilla prefab points to the little finger. They are to be held with it towards
        // the thumb instead.
        private static readonly HashSet<int> ITEMS_POINTING_TO_PINKY = new HashSet<int>
        {
            "CookedWolfMeat".GetStableHashCode(),
            "CookedMeat".GetStableHashCode(),
            "NeckTailGrilled".GetStableHashCode(),
            "CookedLoxMeat".GetStableHashCode(),
            "CookedAsksvinMeat".GetStableHashCode()
        };

        // The items whose equip offset is off in another way than FixEquipOffset() fixes, and what is to be added to
        // it on top of that:
        // - Cooked boar meat has the equip offset of the other cooked meats, whose prefabs are 0.4 meters to the side
        //   as well as 50 meters up.
        // - Cooked lox meat has an equip offset that is off on every axis, and is put where the cooked wolf meat is,
        //   which is held the same way.
        // - Cooked moose meat is 13 centimeters off the palm where the hand is, a few times as far as the others.
        private static readonly Dictionary<int, Vector3> EQUIP_OFFSET_CORRECTIONS = new Dictionary<int, Vector3>
        {
            { "CookedMeat".GetStableHashCode(), new Vector3(-0.4f, 0, 0) },
            { "CookedLoxMeat".GetStableHashCode(), new Vector3(0.13f, 0.63f, -0.17f) },
            { "CookedMooseMeat".GetStableHashCode(), new Vector3(0, 0.1f, 0) }
        };

        private static readonly HashSet<int> ITEMS_MADE_FOR_LEFT_HAND = new HashSet<int>
        {
            "StaffFrostOrbs".GetStableHashCode(),
            "StaffSkeleton".GetStableHashCode(),
            "StaffSpiritCaller".GetStableHashCode()
        };

        // The items other than consumables, shields and throwables that are turned when held in the hand that their
        // vanilla prefab is not made for, so that they are in it like in the other one (see ShouldTurnForOtherHand()).
        // That takes a plane that the item is symmetric about. The horns have none, but are turned by the plane of
        // their cup and their handle, which keeps the handle in the hand and the cup on the side of the fingers, and
        // only leaves their curved tail pointing another way than in the other hand. Any other tankard that is not
        // listed is mirrored instead.
        private static readonly HashSet<int> ITEMS_TURNED_FOR_OTHER_HAND = new HashSet<int>
        {
            "Tankard".GetStableHashCode(),
            "Tankard_dvergr".GetStableHashCode(),
            "TankardAnniversary".GetStableHashCode(),
            "TankardOdin".GetStableHashCode(),
            "Feaster".GetStableHashCode(),
            "StaffFrostOrbs".GetStableHashCode(),
            "StaffSkeleton".GetStableHashCode(),
            "StaffSpiritCaller".GetStableHashCode(),
        };

        // The normal of the plane that an item is symmetric about, for the items whose model does not tell (see
        // GetSymmetryPlaneNormal()):
        // - The horns have their handle along the thumb at the attach point and their cup beside it like that of a
        //   mug, but their tail curves out of the plane that those are in and takes the center with it.
        // - The skulls are right over the attach point with their top away from the palm, and their left and right
        //   sides are towards the thumb and the little finger.
        private static readonly Dictionary<int, Vector3> SYMMETRY_PLANE_NORMALS = new Dictionary<int, Vector3>
        {
            { "TankardAnniversary".GetStableHashCode(), new Vector3(0.515f, 0.857f, 0) },
            { "TankardOdin".GetStableHashCode(), new Vector3(0.454f, 0.891f, 0) },
            { "StaffFrostOrbs".GetStableHashCode(), Vector3.forward },
            { "StaffSkeleton".GetStableHashCode(), Vector3.forward },
            { "StaffSpiritCaller".GetStableHashCode(), Vector3.forward }
        };

        private const float MIN_SYMMETRY_PLANE_OFFSET = 0.02f;

        // The height that the vanilla prefabs of the consumables that do show up in the hand are at.
        private const float EQUIP_OFFSET_PREFAB_HEIGHT = 50f;

        // Attaches an item that VisEquipment.AttachItem() has nothing to attach of, like it would have.
        public static GameObject AttachModel(Transform joint, int itemHash)
        {
            if (joint == null || !ITEMS_WITHOUT_ATTACH_OBJECT.Contains(itemHash))
            {
                return null;
            }
            GameObject prefab = ObjectDB.instance.GetItemPrefab(itemHash);
            Transform model = prefab == null ? null : prefab.transform.Find("model");
            if (model == null)
            {
                return null;
            }

            GameObject attached = Object.Instantiate(model.gameObject);
            attached.SetActive(true);
            VisEquipment.CleanupInstance(attached);
            attached.transform.SetParent(joint);
            attached.transform.localPosition = Vector3.zero;
            attached.transform.localRotation = Quaternion.identity;
            Transform equipOffset = prefab.transform.Find("equipoffset");
            if (equipOffset != null)
            {
                attached.transform.localPosition += equipOffset.position;
                attached.transform.localRotation *= equipOffset.rotation;
            }
            return attached;
        }

        // Applies the fixes that an item is listed for to what is attached of it to a hand.
        public static void Apply(GameObject attached, Transform joint, int itemHash, bool isInLeftHand)
        {
            Transform transform = attached.transform;
            FixEquipOffset(transform, itemHash);
            if (EQUIP_OFFSET_CORRECTIONS.TryGetValue(itemHash, out Vector3 correction))
            {
                transform.localPosition += correction;
            }
            if (ITEMS_WITH_INACTIVE_MESH.Contains(itemHash))
            {
                foreach (var renderer in attached.GetComponentsInChildren<MeshRenderer>(true))
                {
                    renderer.gameObject.SetActive(true);
                }
            }
            if (ITEMS_STUCK_THROUGH_HAND.Contains(itemHash))
            {
                // A quarter turn around the fingers, about the attach point.
                Quaternion turn = Quaternion.AngleAxis(90, Vector3.right);
                transform.localPosition = turn * transform.localPosition;
                transform.localRotation = turn * transform.localRotation;
            }
            if (IsHeldTilted(transform, itemHash))
            {
                LayOnPalm(attached, joint, itemHash);
            }
            if (ITEMS_HELD_ON_BACK_OF_HAND.Contains(itemHash))
            {
                TurnToOtherSideOfHand(attached, joint, itemHash);
            }
            if (ITEMS_POINTING_TO_PINKY.Contains(itemHash))
            {
                // Half a turn around the fingers, about the attach point.
                Quaternion turn = Quaternion.AngleAxis(180, Vector3.right);
                transform.localPosition = turn * transform.localPosition;
                transform.localRotation = turn * transform.localRotation;
            }

            bool isMadeForLeftHand = ITEMS_MADE_FOR_LEFT_HAND.Contains(itemHash) || IsShield(itemHash);
            if (isInLeftHand == isMadeForLeftHand)
            {
                return;
            }
            if (!ShouldTurnForOtherHand(itemHash))
            {
                if (EquipScript.GetEquipTypeFromHash(itemHash) == EquipType.Tankard)
                {
                    // Mirrored through the palm, as it is not known to be fine with a turn.
                    transform.localScale = Vector3.Scale(transform.localScale, new Vector3(1, -1, 1));
                }
                return;
            }
            Vector3 symmetryPlaneNormal = GetSymmetryPlaneNormal(attached, joint, itemHash);
            // The plane goes through the attach point, except for a shield, whose grip may be to the side of its
            // middle.
            float symmetryPlaneOffset = IsShield(itemHash) ? Vector3.Dot(symmetryPlaneNormal, GetCenter(attached, joint)) : 0;
            RotateForOtherHand(transform, symmetryPlaneNormal, symmetryPlaneOffset);
        }

        // Any shield and anything that is thrown like a bomb is turned for the other hand, and so is any consumable,
        // except for those that are stuck through the hand, which are in the plane of the palm and therefore the same
        // in both hands.
        private static bool ShouldTurnForOtherHand(int itemHash)
        {
            return ITEMS_TURNED_FOR_OTHER_HAND.Contains(itemHash) ||
                IsShield(itemHash) ||
                EquipScript.GetEquipTypeFromHash(itemHash) == EquipType.ThrowObject ||
                (IsConsumable(itemHash) && !ITEMS_STUCK_THROUGH_HAND.Contains(itemHash));
        }

        private static bool IsShield(int itemHash)
        {
            return EquipScript.GetEquipTypeFromHash(itemHash) == EquipType.Shield;
        }

        private static bool IsConsumable(int itemHash)
        {
            GameObject prefab = ObjectDB.instance.GetItemPrefab(itemHash);
            ItemDrop itemDrop = prefab == null ? null : prefab.GetComponent<ItemDrop>();
            return itemDrop != null && itemDrop.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable;
        }

        // Brings a consumable into sight whose equip offset is about 50 meters off (e. g. blueberries and most meads):
        // it was made for a prefab that is 50 meters up, as the prefabs of the consumables that do show up are, but the
        // prefab is not. Some are a meter or more off on another axis on top of that.
        private static void FixEquipOffset(Transform attached, int itemHash)
        {
            if (!IsConsumable(itemHash))
            {
                return;
            }
            Transform equipOffset = ObjectDB.instance.GetItemPrefab(itemHash).transform.Find("equipoffset");
            if (equipOffset == null)
            {
                return;
            }
            // What VisEquipment.AttachItem() has added to the local position.
            Vector3 appliedOffset = equipOffset.position;
            if (appliedOffset.magnitude < EQUIP_OFFSET_PREFAB_HEIGHT / 2)
            {
                return;
            }
            Vector3 offset = new Vector3(
                FixEquipOffset(appliedOffset.x), FixEquipOffset(appliedOffset.y), FixEquipOffset(appliedOffset.z));
            attached.localPosition += offset - appliedOffset;
        }

        private static float FixEquipOffset(float offset)
        {
            offset -= EQUIP_OFFSET_PREFAB_HEIGHT * Mathf.Round(offset / EQUIP_OFFSET_PREFAB_HEIGHT);
            // The equip offsets of the prefabs that are right are within 0.45 meters.
            return Mathf.Abs(offset) > 0.6f ? 0 : offset;
        }

        // Whether the vanilla prefab of a consumable has it tilted in the right hand, with its top in between away
        // from the hand and towards the thumb, like most bowls and plates (a bottle has its top towards the thumb, a
        // piece of meat or a jar has it away from the hand). Those are to lie on the palm instead (see LayOnPalm()),
        // unless they are on the back of the hand, which there is another fix for.
        private static bool IsHeldTilted(Transform attached, int itemHash)
        {
            if (!IsConsumable(itemHash) || ITEMS_HELD_ON_BACK_OF_HAND.Contains(itemHash))
            {
                return false;
            }
            Vector3 up = attached.localRotation * GetUpDirection(itemHash);
            return Mathf.Abs(up.y) > 0.3f && Mathf.Abs(up.y) < 0.9f && up.z > 0.5f && Mathf.Abs(up.x) < 0.3f;
        }

        // Turns a held item so that its top is away from the palm of the right hand, and then moves it so that it
        // rests on the palm with its center over the attach point, as far along the fingers as it was.
        private static void LayOnPalm(GameObject attached, Transform joint, int itemHash)
        {
            Transform transform = attached.transform;
            // The palm faces -Y.
            Quaternion turn = Quaternion.FromToRotation(transform.localRotation * GetUpDirection(itemHash), Vector3.down);
            transform.localRotation = turn * transform.localRotation;
            if (!TryGetBounds(attached, joint, out Bounds bounds))
            {
                return;
            }
            transform.localPosition += new Vector3(0, -bounds.max.y, -bounds.center.z);
        }

        // Takes a held item from the back of the hand to the palm or the other way around, by half a turn about an axis
        // in the plane of the palm that goes through the point of that plane that the item is in front of or behind.
        //
        // The axis is the upward direction of the item as far as that is in the plane of the palm (e. g. of a cup or
        // a bottle, which is held with its top towards the thumb), so that the item stays upright. An item whose top
        // is away from the hand (e. g. a mushroom) is turned around the fingers instead and has its top away from
        // the hand again.
        private static void TurnToOtherSideOfHand(GameObject attached, Transform joint, int itemHash)
        {
            Transform transform = attached.transform;
            Vector3 axis = Vector3.ProjectOnPlane(transform.localRotation * GetUpDirection(itemHash), Vector3.up);
            axis = axis.magnitude > 0.3f ? axis.normalized : Vector3.right;

            Vector3 pivot = GetCenter(attached, joint);
            pivot.y = 0;

            Quaternion turn = Quaternion.AngleAxis(180, axis);
            transform.localPosition = pivot + turn * (transform.localPosition - pivot);
            transform.localRotation = turn * transform.localRotation;
        }

        // Makes a held item be in a hand like it is in the other one, which its vanilla prefab is made for, given the
        // unit normal of a plane through the attach point that the item is symmetric about.
        //
        // The attach points of the two hands are mirror images of each other through the palm: in both the fingers
        // are along X and the thumb is towards Z, but the palm faces -Y in the right hand and Y in the left hand. An
        // item therefore has to end up where its mirror image through the palm would be. Its scale is not to be
        // mirrored, though, and a rotation alone only gets it there if the item is symmetric. A reflection through
        // the plane that it is symmetric about followed by one through the palm is a rotation about the line that
        // the two planes share, and none at all for an item whose plane is that of the palm. Both the position and
        // the rotation of the item are rotated by it, nothing else changes.
        //
        // That line goes through the attach point if the plane does. A plane that is beside the attach point by an
        // offset along its normal moves the line with it, which takes a translation on top of the rotation.
        private static void RotateForOtherHand(Transform transform, Vector3 symmetryPlaneNormal, float symmetryPlaneOffset)
        {
            Vector3 axis = Vector3.Cross(Vector3.up, symmetryPlaneNormal);
            Quaternion rotation = new Quaternion(axis.x, axis.y, axis.z, -Vector3.Dot(Vector3.up, symmetryPlaneNormal));
            transform.localPosition =
                rotation * transform.localPosition + 2 * symmetryPlaneOffset * Vector3.Reflect(symmetryPlaneNormal, Vector3.up);
            transform.localRotation = rotation * transform.localRotation;
        }

        // The unit normal of the plane that a held item is symmetric about, in the space of the attach point.
        //
        // Unless listed, that is taken from the model:
        // - A shield faces its upward direction and is symmetric about the plane that this and its longer side are
        //   in: its height if it is a tall one, its width if it is a wide one (ShieldKnight), either if it is round.
        // - Anything else is taken to be symmetric about the plane that its upward direction, its center and the
        //   attach point are in, which holds for anything round (e. g. a bowl or a bottle) and for a mug that is
        //   held by its handle.
        private static Vector3 GetSymmetryPlaneNormal(GameObject attached, Transform joint, int itemHash)
        {
            if (SYMMETRY_PLANE_NORMALS.TryGetValue(itemHash, out Vector3 normal))
            {
                return normal;
            }

            Vector3 up = attached.transform.localRotation * GetUpDirection(itemHash);
            if (IsShield(itemHash) && TryGetBounds(attached, joint, out Bounds bounds))
            {
                // The vanilla prefabs have the height of a tall shield along the thumb, which is upward as it is held.
                bool isWide = bounds.size.x > 1.2f * bounds.size.z;
                normal = Vector3.Cross(up, isWide ? Vector3.right : Vector3.forward);
                if (normal.magnitude > MIN_SYMMETRY_PLANE_OFFSET)
                {
                    return normal.normalized;
                }
            }

            normal = Vector3.Cross(up, GetCenter(attached, joint));
            if (normal.magnitude < MIN_SYMMETRY_PLANE_OFFSET)
            {
                // The item is right at the attach point or straight above or below it, so any plane that its upward
                // direction is in will do: the closest one to the palm.
                normal = Vector3.ProjectOnPlane(Vector3.up, up);
            }
            return normal.magnitude < MIN_SYMMETRY_PLANE_OFFSET ? Vector3.forward : normal.normalized;
        }

        // The center of what is to be seen of a held item, in the space of the attach point of the hand.
        private static Vector3 GetCenter(GameObject attached, Transform joint)
        {
            return TryGetBounds(attached, joint, out Bounds bounds) ? bounds.center : attached.transform.localPosition;
        }

        // The bounds of what is to be seen of a held item, in the space of the attach point of the hand.
        private static bool TryGetBounds(GameObject attached, Transform joint, out Bounds bounds)
        {
            bounds = new Bounds();
            bool hasBounds = false;
            foreach (var meshFilter in attached.GetComponentsInChildren<MeshFilter>())
            {
                if (meshFilter.sharedMesh == null || meshFilter.GetComponent<MeshRenderer>() == null)
                {
                    continue;
                }
                Bounds meshBounds = meshFilter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = meshBounds.center + Vector3.Scale(
                        meshBounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 point = joint.InverseTransformPoint(meshFilter.transform.TransformPoint(corner));
                    if (hasBounds)
                    {
                        bounds.Encapsulate(point);
                    }
                    else
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        hasBounds = true;
                    }
                }
            }
            return hasBounds;
        }

        // The direction that is upward when the item lies in the world, in the space of what is attached of it.
        private static Vector3 GetUpDirection(int itemHash)
        {
            GameObject prefab = ObjectDB.instance.GetItemPrefab(itemHash);
            Transform visual = prefab.transform.Find("attach");
            if (visual == null)
            {
                visual = prefab.transform.Find("model");
            }
            return visual == null ? Vector3.up : Quaternion.Inverse(visual.rotation) * Vector3.up;
        }
    }
}
