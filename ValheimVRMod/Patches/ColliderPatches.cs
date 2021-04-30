using HarmonyLib;
using UnityEngine;
using ValheimVRMod.Scripts;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Patches
{
    public class ColliderPatches
    {
        [HarmonyPatch(typeof(VisEquipment), "SetRightHandEquiped")]
        class PatchSetRightHandEquiped
        {

            static void Postfix(bool __result, string ___m_rightItem, ref GameObject ___m_rightItemInstance)
            {
                if (!__result || ___m_rightItemInstance == null)
                {
                    return;
                }

                MeshFilter meshFilter = ___m_rightItemInstance.GetComponentInChildren<MeshFilter>();
            
                if (meshFilter == null)
                {
                    return;
                }

                Transform item = meshFilter.transform;
                VRPlayer.colliderCube().GetComponent<CollisionDetection>().setColliderParent(item, ___m_rightItem);

            }
        }
    }
}