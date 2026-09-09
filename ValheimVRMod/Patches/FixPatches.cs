using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using ValheimVRMod.VRCore;
using ValheimVRMod.Utilities;
using Valve.VR.InteractionSystem;
using Valheim.SettingsGui;

namespace ValheimVRMod.Patches {

    // Valheim 1.0 added CinematicsManager, whose camera renders straight to the screen with every
    // layer in its culling mask, including the UI layer. In VR that camera drew the world space GUI
    // canvas a second time from its own transform, which is why the main menu appeared twice while
    // the startup cinematic was still active. The intro video itself also does not composite into a
    // headset, so it is not started in VR.
    [HarmonyPatch(typeof(CinematicsManager), "Awake")]
    class CinematicsManager_Awake_Patch
    {
        static void Postfix(CinematicsManager __instance)
        {
            if (VHVRConfig.NonVrPlayer())
            {
                return;
            }

            __instance.m_introOnStartup = false;
            __instance.m_introOnNewWorld = false;

            if (__instance.m_camera == null)
            {
                return;
            }

            // Anything VHVR renders through its own dedicated cameras must stay off this one.
            var mask = __instance.m_camera.cullingMask;
            mask &= ~(1 << LayerMask.NameToLayer("UI"));
            mask &= ~(1 << LayerUtils.getUiPanelLayer());
            mask &= ~(1 << LayerUtils.getHandsLayer());
            mask &= ~(1 << LayerUtils.getWorldspaceUiLayer());
            __instance.m_camera.cullingMask = mask;
        }
    }

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

    // The intro cinematic that plays on first startup swaps the game over to its own camera:
    // CinematicsManager.Play() disables Utils.GetMainCamera() (which resolves to the VR camera,
    // since VHVR keeps the vanilla "Main Camera" disabled) and CinematicsManager.Stop() enables it
    // again, leaving the start menu fighting VRPlayer.enableCameras() over who owns the camera.
    // The video itself is not rendered in stereo either and only shows up as a magenta block, so
    // suppress the automatic intro and let FejdStartup go straight to the main menu.
    // Cinematics started from the menu, dreams and the outro are left alone.
    // TODO: m_introOnNewWorld plays the same intro video via Game when a new world is created and
    // breaks the VR camera the same way. Consider clearing it here too.
    [HarmonyPatch(typeof(CinematicsManager), "Awake")]
    class DisableStartupCinematicPatch
    {
        static void Postfix(CinematicsManager __instance)
        {
            if (VHVRConfig.NonVrPlayer())
            {
                return;
            }
            __instance.m_introOnStartup = false;
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
