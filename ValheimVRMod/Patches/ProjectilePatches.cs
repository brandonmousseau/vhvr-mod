using HarmonyLib;
using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Patches
{
    // The carapace spear and the Deep North spears are all thrown as projectile_wolffang and therefore fly looking
    // like a wolf fang spear. Replace the model of such a projectile with the model of the spear actually thrown.
    // Projectile#Setup only runs on the client that throws, so only the local player's own throws are fixed.
    [HarmonyPatch(typeof(Projectile), nameof(Projectile.Setup))]
    class PatchProjectileSetup
    {
        private const string WOLF_FANG_PROJECTILE_NAME = "projectile_wolffang";
        private const string WOLF_FANG_SPEAR_NAME = "$item_spear_wolffang";
        private const string WOLF_FANG_SPEAR_PREFAB_NAME = "SpearWolfFang";
        private const string REPLACEMENT_MODEL_NAME = "vhvrProjectileModel";

        static void Postfix(Projectile __instance, Character owner, ItemDrop.ItemData item)
        {
            if (VHVRConfig.NonVrPlayer() || owner != Player.m_localPlayer || item?.m_shared == null)
            {
                return;
            }

            if (item.m_shared.m_name == WOLF_FANG_SPEAR_NAME ||
                Utils.GetPrefabName(__instance.gameObject) != WOLF_FANG_PROJECTILE_NAME)
            {
                return;
            }

            ReplaceModel(__instance.gameObject, item);
        }

        private static void ReplaceModel(GameObject projectile, ItemDrop.ItemData item)
        {
            MeshFilter projectileModel = projectile.GetComponentInChildren<MeshFilter>();
            MeshFilter itemModel = GetItemModel(item);
            if (projectileModel == null || itemModel == null)
            {
                LogUtils.LogDebug("Cannot find the model to fix the thrown " + item.m_shared.m_name);
                return;
            }

            Transform parent = projectileModel.transform.parent;
            if (parent == null || parent.Find(REPLACEMENT_MODEL_NAME) != null)
            {
                // Already fixed, e. g. if the projectile instance is reused.
                return;
            }

            Quaternion? replacementRotation = GetReplacementRotation(projectile, projectileModel, item);
            if (replacementRotation == null)
            {
                return;
            }

            GameObject replacement = Object.Instantiate(itemModel.gameObject);
            replacement.name = REPLACEMENT_MODEL_NAME;
            // The replacement keeps its own scale but takes the placement of the model it replaces.
            replacement.transform.SetParent(parent, worldPositionStays: false);
            replacement.transform.localPosition = projectileModel.transform.localPosition;
            replacement.transform.rotation = projectile.transform.rotation * replacementRotation.Value;
            foreach (Transform child in replacement.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                child.gameObject.layer = projectileModel.gameObject.layer;
            }
            foreach (Collider collider in replacement.GetComponentsInChildren<Collider>(includeInactive: true))
            {
                // The replacement is only a visual, the projectile keeps its own collision.
                Object.Destroy(collider);
            }
            // The two models have different pivots and lengths, therefore line them up by their centers.
            WeaponUtils.AlignLoadedMeshToUnloadedMesh(loaded: replacement, unloaded: projectileModel.gameObject);

            MeshRenderer replacedRenderer = projectileModel.GetComponent<MeshRenderer>();
            if (replacedRenderer != null)
            {
                replacedRenderer.enabled = false;
            }
        }

        // Spear models are not all built pointing the same way: the Deep North ones point opposite to the wolf fang
        // one, so simply reusing the rotation of the model being replaced would make them fly backwards. Instead,
        // take how the replaced model is rotated relative to the spear it belongs to when held, and rotate the
        // replacement the same way relative to its own spear.
        private static Quaternion? GetReplacementRotation(
            GameObject projectile, MeshFilter projectileModel, ItemDrop.ItemData item)
        {
            GameObject wolfFangSpear = ObjectDB.instance?.GetItemPrefab(WOLF_FANG_SPEAR_PREFAB_NAME);
            MeshFilter wolfFangModel = wolfFangSpear == null ? null : GetModel(wolfFangSpear.transform);
            MeshFilter itemModel = item.m_dropPrefab == null ? null : GetModel(item.m_dropPrefab.transform);
            if (wolfFangModel == null || itemModel == null)
            {
                LogUtils.LogDebug("Cannot find the model rotation to fix the thrown " + item.m_shared.m_name);
                return null;
            }

            return GetModelRotation(projectile.transform, projectileModel.transform) *
                Quaternion.Inverse(GetModelRotation(wolfFangSpear.transform, wolfFangModel.transform)) *
                GetModelRotation(item.m_dropPrefab.transform, itemModel.transform);
        }

        private static Quaternion GetModelRotation(Transform root, Transform model)
        {
            return Quaternion.Inverse(root.rotation) * model.rotation;
        }

        // The model of an item is in its drop prefab, which is also what the item looks like when equipped.
        private static MeshFilter GetItemModel(ItemDrop.ItemData item)
        {
            return item.m_dropPrefab == null ? null : GetModel(item.m_dropPrefab.transform);
        }

        private static MeshFilter GetModel(Transform itemPrefab)
        {
            Transform attach = itemPrefab.Find("attach");
            return attach == null ? null : attach.GetComponentInChildren<MeshFilter>();
        }
    }
}
