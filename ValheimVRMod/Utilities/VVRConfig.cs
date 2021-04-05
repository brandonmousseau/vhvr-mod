using BepInEx.Configuration;
using Unity.XR.OpenVR;
using ValheimVRMod.VRCore;
using UnityEngine;

namespace ValheimVRMod.Utilities
{

    class VVRConfig
    {

        // General Settings
        private static ConfigEntry<bool> vrModEnabled;
        private static ConfigEntry<string> mirrorMode;
        private static ConfigEntry<bool> useOverlayGui;
        private static ConfigEntry<string> preferredHand;
        private static ConfigEntry<float> overlayCurvature;
        private static ConfigEntry<float> headOffsetX;
        private static ConfigEntry<float> headOffsetZ;
        private static ConfigEntry<float> headOffsetY;
        private static ConfigEntry<bool> enableHeadReposition;
        private static ConfigEntry<bool> enableHands;
        //private static ConfigEntry<bool> useLookLocomotion;

        // Graphics Settings
        private static ConfigEntry<bool> useAmplifyOcclusion;

        public static void InitializeConfiguration(ConfigFile config)
        {
            vrModEnabled = config.Bind("General",
                                       "ModEnabled",
                                       true,
                                       "Used to toggle the mod on and off.");
            mirrorMode = config.Bind("General",
                                     "MirrorMode",
                                     "Right",
                                     new ConfigDescription("The VR mirror mode.Legal values: OpenVR, Right, Left, None. Note: OpenVR is" +
                                     " required if you want to see the Overlay-type GUI in the mirror image. However, I've found that OpenVR" +
                                     " mirror mode causes some issue that requires SteamVR to be restarted after closing the game, so unless you" +
                                     " need it for some specific reason, I recommend using another mirror mode or None.",
                                     new AcceptableValueList<string>(new string[] { "Right", "Left", "OpenVR", "None" })));
            useOverlayGui = config.Bind("General",
                                        "UseOverlayGui",
                                        true,
                                        "Whether or not to use OpenVR overlay for the GUI. This produces a" +
                                        " cleaner GUI but will only be compatible with M&K or Gamepad controls.");
            preferredHand = config.Bind("General",
                                        "PreferredHand",
                                        "Right",
                                        new ConfigDescription("Which hand do you want to use for the main laser pointer input? If" +
                                        " only one hand is active, it will be used automatically regardless of this setting.",
                                        new AcceptableValueList<string>(new string[] { "Right", "Left" })));
            useAmplifyOcclusion = config.Bind("Graphics",
                                              "UseAmplifyOcclusion",
                                              true,
                                              "RECOMMENDED - Determines whether or not to use the Amplify Occlusion post processing effect." +
                                              " This implements an effect similar to SSAO but with much less performance" +
                                              " cost. While you can enable SSAO and UseAmplifyOcclusion simultaneously, it is" +
                                              " not recommended. SSAO impacts performance significantly, which is bad for VR especially. Therefore" +
                                              " you should disable SSAO in the graphics settings of the game when using this.");
            overlayCurvature = config.Bind("General",
                                           "OverlayCurvature",
                                           0.25f,
                                           new ConfigDescription("The amount of curvature to use for the GUI overlay. Only used when UseOverlayGui is true. " +
                                           "Valid values are  0.0 - 1.0. Use the -/= keys to adjust in game (setting will be remembered).",
                                           new AcceptableValueRange<float>(0f, 1f)));
            headOffsetX = config.Bind("General",
                                      "FirstPersonHeadOffsetX",
                                      0.0f,
                                      new ConfigDescription("This is an offset you can adjust, if needed, to center the camera position over the player model in first person mode. " +
                                      "I haven't found a way to programatically fully determine the exact right spot at runtime, so I need to use an offset, and based on tracking, it" +
                                      " might be different for different players. It shouldn't need to be adjusted much.",
                                      new AcceptableValueRange<float>(-2.0f, 2.0f)));
            headOffsetZ = config.Bind("General",
                          "FirstPersonHeadOffsetZ",
                          0.0f,
                          new ConfigDescription("See FirstPersonHeadOffsetX description.",
                          new AcceptableValueRange<float>(-2.0f, 2.0f)));
            headOffsetY = config.Bind("General",
                          "FirstPersonHeadOffsetY",
                          0.0f,
                          new ConfigDescription("See FirstPersonHeadOffsetX description.",
                          new AcceptableValueRange<float>(-2.0f, 2.0f)));
            enableHeadReposition = config.Bind("General",
                                                "EnableHeadRepositioning",
                                                true,
                                                "Set to this true enable using the arrow keys to position the camera when in first person mode. You can use this to set the values of FirstPersonHeadOffsetX/Z while in game " +
                                                "rather than having to edit them manually in the config file. Your settings will be remembered between gameplay sessions via this config file."
                                                );
            enableHands = config.Bind("General",
                                       "EnableHands",
                                       true,
                                       "Set this true to allow hands and laser pointers to be rendered in game. Note: motion controls are only minimally enabled, so right now this is just for fun.");

           // useLookLocomotion = config.Bind("General",
           //                                 "UseLookLocomotion",
           //                                 false,
           //                                 "Setting this to true ties the direction you are looking to the walk direction while in first person mode. Set this to false if you prefer to disconnect these so you can look" +
           //                                 " look by turning your head without affecting movement direction.");
        }

        public static bool ModEnabled()
        {
            return vrModEnabled.Value;
        }

        public static string GetPreferredHand()
        {
            return preferredHand.Value == "Left" ? VRPlayer.LEFT_HAND : VRPlayer.RIGHT_HAND;
        }

        public static OpenVRSettings.MirrorViewModes GetMirrorViewMode()
        {
            string mode = mirrorMode.Value;
            if (mode == "Right")
            {
                return OpenVRSettings.MirrorViewModes.Right;
            } else if (mode == "Left")
            {
                return OpenVRSettings.MirrorViewModes.Left;
            } else if (mode == "OpenVR")
            {
                return OpenVRSettings.MirrorViewModes.OpenVR;
            } else if (mode == "None")
            {
                return OpenVRSettings.MirrorViewModes.None;
            } else
            {
                LogUtils.LogWarning("Invalid mirror mode setting. Defaulting to None");
                return OpenVRSettings.MirrorViewModes.None;
            }
        }

        public static bool GetUseOverlayGui()
        {
            return useOverlayGui.Value;
        }

        public static float GetOverlayCurvature()
        {
            return overlayCurvature.Value;
        }

        public static void UpdateOverlayCurvature(float value)
        {
            value = Mathf.Clamp(value, 0f, 1f);
            overlayCurvature.Value = value;
        }

        public static bool UseAmplifyOcclusion()
        {
            return useAmplifyOcclusion.Value;
        }

        public static Vector3 GetHeadOffset()
        {
            return new Vector3(headOffsetX.Value, headOffsetY.Value, headOffsetZ.Value);
        }

        public static bool AllowHeadRepositioning()
        {
            return enableHeadReposition.Value;
        }

        public static void UpdateHeadOffset(Vector3 offset) {
            headOffsetX.Value = Mathf.Clamp(offset.x, -2f, 2f);
            headOffsetY.Value = Mathf.Clamp(offset.y, -2f, 2f);
            headOffsetZ.Value = Mathf.Clamp(offset.z, -2f, 2f);
        }

        public static bool HandsEnabled()
        {
            return enableHands.Value;
        }

        public static bool UseLookLocomotion()
        {
            return false;
        }

    }
}
