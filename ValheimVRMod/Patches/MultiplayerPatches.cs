using HarmonyLib;
using UnityEngine;
using ValheimVRMod.Scripts;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Patches {

    [HarmonyPatch(typeof(Player), "Start")]
    class PatchPlayerAwake {
        public static void Postfix(Player __instance) {
            if (VHVRConfig.NonVrPlayer() && __instance == Player.m_localPlayer) {
                // __instance.gameObject.AddComponent<ActionLogger>();
                return;
            }
            __instance.gameObject.AddComponent<VRPlayerSync>();
        }
    }

    class ActionLogger : MonoBehaviour {
        float t = 0;
        bool wasAttacking;
        void FixedUpdate()
        {
            if (Player.m_localPlayer.InAttack())
            {
                if (!wasAttacking)
                {
                    t = 0;
                }
                else
                {
                    t += Time.fixedDeltaTime;
                }
                wasAttacking = true;
            } else
            {
                t += Time.fixedDeltaTime;
                wasAttacking = false;
            }
        }

        void OnRenderObject()
        {
            if (Player.m_localPlayer == null) return;

            Attack attack = Player.m_localPlayer.GetRightItem()?.m_shared.m_attack;
            Attack secondaryAttack = Player.m_localPlayer.GetRightItem()?.m_shared.m_secondaryAttack;
            LogUtils.LogWarning("Player action: " + (attack?.m_attackAnimation ?? "") + (secondaryAttack?.m_attackAnimation ?? "") + " " + t + " " + (-Player.m_localPlayer.m_queuedAttackTimer - Player.m_localPlayer.GetTimeSinceLastAttack()));
            // LogUtils.LogWarning("Player action: " + attack.m_hitPointtype + " " + attack.m_attackType + secondaryAttack.m_hitPointtype + " " + secondaryAttack.m_attackType);
            //foreach (var animation in Player.m_localPlayer.m_animator.runtimeAnimatorController.animationClips)
            //{
            //    LogUtils.LogWarning("Player action: " + animation.name + animation.length + " " + animation.apparentSpeed + " " + animation.averageSpeed);
            //}   
        }
    }
}