using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using ValheimVRMod.VRCore;
using ValheimVRMod.VRCore.UI;
using ValheimVRMod.Utilities;
using Valve.VR.InteractionSystem;
using Valheim.SettingsGui;

namespace ValheimVRMod.Patches {
    [HarmonyPatch(typeof(Hand), "FixedUpdate")]
    class PatchDebug {

        static bool Prefix(Hand __instance, ref List<Hand.AttachedObject> ___attachedObjects) {
            if (VHVRConfig.NonVrPlayer())
            {
                return true;
            }
            if (__instance.currentAttachedObject == null) {
                return false;
            }
            
            if (__instance.currentAttachedObjectInfo.Value.interactable == null) {
                ___attachedObjects.RemoveAt(___attachedObjects.Count - 1);
                return false;   
            }
            
            return true;
        }
    }
    
    [HarmonyPatch(typeof(Character), "SetVisible")]
    class PatchFixVanishing {

        static bool Prefix(Player __instance) {
            if (VHVRConfig.NonVrPlayer()) {
                return true;
            }
            return __instance != Player.m_localPlayer;
        }
    }

    [HarmonyPatch(typeof(UpscaledFrameBuffer), nameof(UpscaledFrameBuffer.UpdateCurrentRenderScale))]
    class PatchUpdateCurrentRenderScale
    {
        static bool Prefix()
        {
            if (VHVRConfig.NonVrPlayer())
            {
                return true;
            }
            // Force-disable frame scaling since it would cause the game world to disappear in VR.
            UpscaledFrameBuffer.m_targetResolutionVertical = int.MaxValue;
            return false;
        }
    }

    // Holds the startup intro until the VR GUI can show it, so that it plays on the UI panel like every
    // other cinematic (see CinematicsManager_Play_Patch) instead of on CinematicsManager's own flat camera.
    // FejdStartup.Start() starts this coroutine as its last statement, well before VR has finished coming
    // up, and its very first statement hides the main menu and plays the video. Wrapping the returned
    // iterator therefore also defers hiding the menu, so the player looks at the menu on the VR panel while
    // VR initializes rather than at nothing.
    // assembly_valheim is publicized at build time, so nameof() here turns a rename by Iron Gate into a build
    // failure instead of a patch that silently stops applying.
    [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.PlayIntroCinematic))]
    class IntroCinematicVrDelayPatch
    {
        // A backstop against a SteamVR that never finishes starting, not the normal path. Giving up here
        // costs only the VR presentation of the intro: the video still plays, on the flat screen, and
        // ValheimVRMod.InitializeVRAndCreateRig() then holds the VR rig back until it is over.
        private const float TIMEOUT_SECONDS = 10f;

        static void Postfix(ref IEnumerator __result)
        {
            if (VHVRConfig.NonVrPlayer() || __result == null)
            {
                return;
            }
            __result = PlayOnceVrIsReady(__result);
        }

        private static IEnumerator PlayOnceVrIsReady(IEnumerator playIntroCinematic)
        {
            float deadline = Time.realtimeSinceStartup + TIMEOUT_SECONDS;
            // NonVrPlayer() covers both flat screen mode and VR failing to initialize, in which case it
            // starts returning true and there is nothing left to wait for.
            while (!VHVRConfig.NonVrPlayer() &&
                !VRGUI.isReadyToShowCinematic &&
                Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (!VHVRConfig.NonVrPlayer() && !VRGUI.isReadyToShowCinematic)
            {
                LogUtils.LogWarning(
                    "VR GUI was not ready after " + TIMEOUT_SECONDS +
                    "s, playing the startup cinematic on the flat screen.");
            }

            while (playIntroCinematic.MoveNext())
            {
                yield return playIntroCinematic.Current;
            }
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.TeleportTo))]
    class WaterLevelFixPatch
    {
        public static void Postfix(Player __instance, bool __result)
        {
            if (Player.m_localPlayer != __instance || VHVRConfig.NonVrPlayer() || !__result)
            {
                return;
            }

            __instance.m_liquids[(int)LiquidType.Water] = 0;
            __instance.SetLiquidLevel(-10000, LiquidType.Water, null);
        }
    }


    [HarmonyPatch(typeof(Player), nameof(Player.Start))]
    class PlayerDriftFixPatch
    {
        public static void Postfix(Player __instance)
        {
            if (Player.m_localPlayer != __instance || VHVRConfig.NonVrPlayer())
            {
                return;
            }

            __instance.gameObject.GetOrAddComponent<PlayerDriftFix>();
        }
    }

    [HarmonyPatch(typeof(GraphicsSettings), nameof(GraphicsSettings.UpdateSettingAvailability))]
    class GraphicsSettingsUpdateSettingAvailabilityPatch
    {
        public static bool Prefix(GraphicsSettings __instance)
        {
            // TODO: investigate why clicking OK on vanilla settings after opening and closing VHVR settings will result in a null instance here.
            //Stack trace:
            // UnityEngine.Bindings.ThrowHelper.ThrowNullReferenceException(System.Object obj)(at < 89f741081c874c65b780dbd6a0d8d33e >:0)
            // UnityEngine.Component.get_gameObject()(at < 89f741081c874c65b780dbd6a0d8d33e >:0)
            // Valheim.SettingsGui.GraphicsSettings.SetChangeable(System.Boolean isChangeable, UnityEngine.UI.Selectable ui)(at<f02a2207632846a3a0012c7f73401843>:0)
            // (wrapper dynamic - method) Valheim.SettingsGui.GraphicsSettings.DMD<Valheim.SettingsGui.GraphicsSettings::UpdateSettingAvailability>(Valheim.SettingsGui.GraphicsSettings, GraphicsModeConfiguration)
            // Valheim.SettingsGui.GraphicsSettings.UpdateUI()(at<f02a2207632846a3a0012c7f73401843>:0)
            // GraphicsSettingsManager.ApplyGraphicsSettingsToCurrentSession()(at<f02a2207632846a3a0012c7f73401843>:0)
            // GraphicsSettingsManager.SaveAndApplyGraphicsSettingsCustom(GraphicsSettingsState & settings)(at<f02a2207632846a3a0012c7f73401843>:0)
            // Valheim.SettingsGui.GraphicsSettings.OnOkAsync(Valheim.SettingsGui.OkActionCompletedHandler okActionCompletedCallback)(at<f02a2207632846a3a0012c7f73401843>:0)
            // (wrapper dynamic - method) Settings.DMD<Settings::OnOk>(Settings)
            return __instance != null;
        }
    }


    [HarmonyPatch(typeof(Character), nameof(Character.Decrement))]
    class CharacterDecrementPatch
    {
        public static void Postfix(Character __instance, ref int __result, LiquidType type)
        {
            if ((Character) Player.m_localPlayer != __instance || VHVRConfig.NonVrPlayer())
            {
                return;
            }

            if (__result < 0 && type == LiquidType.Water)
            {
                __instance.m_liquids[(int)LiquidType.Water] = 0;
                __result = 0;
            }
        }
    }

    /**
     * Remove attack animation by speeding it up. It only applies to attack moves,
     * because the original method switches it back to normal for other animations
     */
    [HarmonyPatch(typeof(CharacterAnimEvent), nameof(CharacterAnimEvent.CustomFixedUpdate))]
    class PatchFixedUpdate
    {

        public static float lastSpeedUp = 1f;
        static void Prefix(Character ___m_character, ref Animator ___m_animator)
        {
            if (___m_character != Player.m_localPlayer || !VHVRConfig.UseVrControls())
            {
                return;
            }

            if (!EquipScript.ShouldSkipAttackAnimation() || ___m_character.IsStaggering() || !VRPlayer.attachedToPlayer)
            {
                ___m_animator.speed = 1f;
                return;
            }

            if (___m_character.IsSitting() && !___m_character.m_attack && !___m_character.m_attackHold)
            {
                ___m_animator.speed = 1f;
                return;
            }

            if (___m_animator.speed != 1 && ___m_animator.speed != 1000)
            {
                lastSpeedUp = ___m_animator.speed;
            }
            ___m_animator.speed = 1000f;
        }
    }
}
