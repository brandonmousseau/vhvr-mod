using BepInEx.Configuration;
using Unity.XR.OpenVR;
using System;
using ValheimVRMod.VRCore;
using UnityEngine;

using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.Utilities
{

    class VHVRConfig
    {

        // General Settings
        private static ConfigEntry<bool> vrModEnabled;
        private static ConfigEntry<string> mirrorMode;
        private static ConfigEntry<float> headOffsetX;
        private static ConfigEntry<float> headOffsetZ;
        private static ConfigEntry<float> headOffsetY;
        private static ConfigEntry<float> headOffsetThirdPersonX;
        private static ConfigEntry<float> headOffsetThirdPersonZ;
        private static ConfigEntry<float> headOffsetThirdPersonY;
        private static ConfigEntry<bool> enableHeadReposition;
        private static ConfigEntry<bool> recenterOnStart;

        // UI Settings
        private static ConfigEntry<bool> useOverlayGui;
        private static ConfigEntry<float> overlayCurvature;
        private static ConfigEntry<float> overlayWidth;
        private static ConfigEntry<float> overlayVerticalPosition;
        private static ConfigEntry<float> overlayDistance;
        private static ConfigEntry<float> uiPanelSize;
        private static ConfigEntry<float> uiPanelVerticalOffset;
        private static ConfigEntry<float> uiPanelDistance;
        private static ConfigEntry<bool> showStaticCrosshair;
        private static ConfigEntry<float> crosshairScale;
        private static ConfigEntry<bool> showRepairHammer;
        private static ConfigEntry<bool> showEnemyHuds;
        private static ConfigEntry<float> enemyHudScale;
        private static ConfigEntry<float> stationaryGuiRecenterAngle;
        private static ConfigEntry<float> mobileGuiRecenterAngle;
        private static ConfigEntry<bool> recenterGuiOnMove;

        // Controls Settings
        private static ConfigEntry<bool> enableHands;
        private static ConfigEntry<bool> useVrControls;
        private static ConfigEntry<bool> useLookLocomotion;
        private static ConfigEntry<string> preferredHand;
        private static ConfigEntry<string> headReposFowardKey;
        private static ConfigEntry<string> headReposBackwardKey;
        private static ConfigEntry<string> headReposLeftKey;
        private static ConfigEntry<string> headReposRightKey;
        private static ConfigEntry<string> headReposUpKey;
        private static ConfigEntry<string> headReposDownKey;
        private static ConfigEntry<string> hmdRecenterKey;

        // Graphics Settings
        private static ConfigEntry<bool> useAmplifyOcclusion;

        public static void InitializeConfiguration(ConfigFile config)
        {
            InitializeGeneralSettings(config);
            InitializeUISettings(config);
            InitializeControlsSettings(config);
            InitializeGraphicsSettings(config);
        }

        private static void InitializeGeneralSettings(ConfigFile config)
        {
            vrModEnabled = config.Bind("General",
                                       "ModEnabled",
                                       true,
                                       "Used to toggle the mod on and off.");
            recenterOnStart = config.Bind("General",
                                          "RecenterOnStart",
                                          true,
                                          "Set this to true if you want tracking to be automatically re-centered when the game first starts up.");
            mirrorMode = config.Bind("General",
                                     "MirrorMode",
                                     "Right",
                                     new ConfigDescription("The VR mirror mode.Legal values: OpenVR, Right, Left, None. Note: OpenVR is" +
                                     " required if you want to see the Overlay-type GUI in the mirror image. However, I've found that OpenVR" +
                                     " mirror mode causes some issue that requires SteamVR to be restarted after closing the game, so unless you" +
                                     " need it for some specific reason, I recommend using another mirror mode or None.",
                                     new AcceptableValueList<string>(new string[] { "Right", "Left", "OpenVR", "None" })));
            headOffsetX = config.Bind("General",
                                      "FirstPersonHeadOffsetX",
                                      0.0f,
                                      new ConfigDescription("**OBSOLETE**: Due to some changes made to recentering tracking, the first person positional values are" +
                                      "no longer saved/used between play sessions. You can still manually adjust position in game, but it will be reset each time you restart" +
                                      " the game or recenter HMD tracking.",
                                      new AcceptableValueRange<float>(-2.0f, 2.0f)));
            headOffsetZ = config.Bind("General",
                          "FirstPersonHeadOffsetZ",
                          0.0f,
                          new ConfigDescription("**OBSOLETE**: See FirstPersonHeadOffsetX description.",
                          new AcceptableValueRange<float>(-2.0f, 2.0f)));
            headOffsetY = config.Bind("General",
                          "FirstPersonHeadOffsetY",
                          0.0f,
                          new ConfigDescription("**OBSOLETE**: See FirstPersonHeadOffsetX description.",
                          new AcceptableValueRange<float>(-2.0f, 2.0f)));
            headOffsetThirdPersonX = config.Bind("General",
                          "ThirdPersonHeadOffsetX",
                          0.0f,
                          new ConfigDescription("Adjusts X position in third person cam. All third person zoom levels all share same offset.",
                          new AcceptableValueRange<float>(-2.0f, 2.0f)));
            headOffsetThirdPersonZ = config.Bind("General",
                          "ThirdPersonHeadOffsetZ",
                          0.0f,
                          new ConfigDescription("Adjusts Z position in third person cam. All third person zoom levels all share same offset.",
                          new AcceptableValueRange<float>(-2.0f, 2.0f)));
            headOffsetThirdPersonY = config.Bind("General",
                          "ThirdPersonHeadOffsetY",
                          0.0f,
                          new ConfigDescription("Adjusts Y position in third person cam. All third person zoom levels all share same offset.",
                          new AcceptableValueRange<float>(-2.0f, 2.0f)));
            enableHeadReposition = config.Bind("General",
                                                "EnableHeadRepositioning",
                                                true,
                                                "Set to this true enable using the arrow keys to position the camera when in first or third person mode. You can use this to" +
                                                " set the values of First/ThirdPersonHeadOffsetX/Y/Z while in game rather than having to edit them manually in the config file. " +
                                                "Your settings will be remembered between gameplay sessions via this config file.");

        }

        private static void InitializeUISettings(ConfigFile config)
        {
            useOverlayGui = config.Bind("UI",
                            "UseOverlayGui",
                            true,
                            "Whether or not to use OpenVR overlay for the GUI. This produces a" +
                            " cleaner GUI but will only be compatible with M&K or Gamepad controls.");
            overlayWidth = config.Bind("UI",
                                       "OverlayWidth",
                                       4f,
                                       new ConfigDescription("Width, in meters, that you want the Overlay GUI to be.",
                                       new AcceptableValueRange<float>(0.5f, 15f)));
            overlayDistance = config.Bind("UI",
                                          "OverlayDistance",
                                          2f,
                                           new ConfigDescription("The distance from you that you want the Overlay GUI to be rendered at.",
                                           new AcceptableValueRange<float>(0.5f, 15f)));
            overlayVerticalPosition = config.Bind("UI",
                                                   "OverlayVerticalPosition",
                                                   1f,
                                                   new ConfigDescription("Vertical offset for the Overlay GUI to make it higher or lower",
                                                   new AcceptableValueRange<float>(-0.5f, 3f)));
            overlayCurvature = config.Bind("UI",
                                           "OverlayCurvature",
                                           0.25f,
                                           new ConfigDescription("The amount of curvature to use for the GUI overlay. Only used when UseOverlayGui is true. " +
                                           "Valid values are  0.0 - 1.0. Use the -/= keys to adjust in game (setting will be remembered).",
                                           new AcceptableValueRange<float>(0f, 1f)));
            uiPanelSize = config.Bind("UI",
                                      "UIPanelSize",
                                      3f,
                                      new ConfigDescription("Size for the UI panel display (non-Overlay GUI).",
                                      new AcceptableValueRange<float>(0.5f, 15f)));
            uiPanelDistance = config.Bind("UI",
                                      "UIPanelDistance",
                                      3f,
                                      new ConfigDescription("Distance to draw the UI panel at.",
                                      new AcceptableValueRange<float>(0.5f, 15f)));
            uiPanelVerticalOffset  = config.Bind("UI",
                                      "UIPanelVerticalOffset",
                                      1f,
                                      new ConfigDescription("Height the UI Panel will be drawn.",
                                      new AcceptableValueRange<float>(-0.5f, 3f)));
            showStaticCrosshair = config.Bind("UI",
                                   "ShowStaticCrosshair",
                                   true,
                                   "This determines whether or not the normal crosshair that is visible by default is visible in VR.");
            crosshairScale = config.Bind("UI",
                                         "CrosshairScale",
                                         1.0f,
                                         new ConfigDescription("Scalar multiplier to adjust the size of the crosshair to your preference. 1.0 is probably fine.",
                                         new AcceptableValueRange<float>(0.8f, 2.5f)));
            showRepairHammer = config.Bind("UI",
                                           "ShowRepairHammer",
                                           true,
                                           "This adds an indicator on screen when in repair mode that shows where the repair cursor currently is.");
            showEnemyHuds = config.Bind("UI",
                                        "ShowEnemyHuds",
                                        true,
                                        "Enable or disable displaying enemy names and stats above them in game.");
            enemyHudScale = config.Bind("UI",
                                        "EnemyHudScale",
                                         1.0f,
                                         new ConfigDescription("Scalar multiplier to adjust the size of enemy huds to your preference. 1.0 is probably fine.",
                                         new AcceptableValueRange<float>(0.5f, 3f)));
            stationaryGuiRecenterAngle = config.Bind("UI",
                                                     "StationaryGuiRecenterAngle",
                                                     75f,
                                                     new ConfigDescription("Only used when UseLookLocomotion is true. This is the angle away from the center of the GUI that will trigger the GUI to" +
                                                     " recenter on your current look direction while stationary.",
                                                     new AcceptableValueRange<float>(25f, 100f)));
            mobileGuiRecenterAngle = config.Bind("UI",
                                     "MobileGuiRecenterAngle",
                                     50f,
                                     new ConfigDescription("Only used when UseLookLocomotion is true. This is the angle away from the center of the GUI that will trigger the GUI to" +
                                     " recenter on your current look direction while moving.",
                                     new AcceptableValueRange<float>(25f, 100f)));
            recenterGuiOnMove = config.Bind("UI",
                                            "RecenterGuiOnMove",
                                            true,
                                            "Only used when UseLookLocomotion is true. This will cause the GUI to recenter to your current look direction when you first start moving.");
        }

        private static void InitializeControlsSettings(ConfigFile config)
        {
            useLookLocomotion = config.Bind("Controls",
                                            "UseLookLocomotion",
                                            true,
                                            "Setting this to true ties the direction you are looking to the walk direction while in first person mode. " +
                                            "Set this to false if you prefer to disconnect these so you can look" +
                                            " look by turning your head without affecting movement direction.");
            enableHands = config.Bind("Controls",
                                       "EnableHands",
                                       true,
                                       "Set this true to allow hands and laser pointers to be rendered in game. Note: motion controls are only" +
                                       " minimally enabled, so right now this is just for fun.");
            useVrControls = config.Bind("Controls",
                                        "UseVRControls",
                                        false,
                                        "This setting enables the use of the VR motion controllers as input (only Valve Knuckles supported currently. " +
                                        "This setting, if true, will force EnableHands to be true and UseOverlayGui to be false as both of these settings " +
                                        "must be set this way for VR controllers to work.");
            preferredHand = config.Bind("Controls",
                                        "PreferredHand",
                                        "Right",
                                        new ConfigDescription("Which hand do you want to use for the main laser pointer input? If" +
                                        " only one hand is active, it will be used automatically regardless of this setting.",
                                        new AcceptableValueList<string>(new string[] { "Right", "Left" })));
            InitializeConfigurableKeyBindings(config);
        }

        private static void InitializeConfigurableKeyBindings(ConfigFile config)
        {
            headReposFowardKey = config.Bind("Controls",
                                             "HeadRepositionForwardKey",
                                             "UpArrow",
                                             "Key used to move head camera forwards. Must be matching key from https://docs.unity3d.com/2019.4/Documentation/ScriptReference/KeyCode.html");
            headReposBackwardKey = config.Bind("Controls",
                                               "HeadRepositionBackwardKey",
                                               "DownArrow",
                                               "Key used to move head camera backwards. Must be matching key from https://docs.unity3d.com/2019.4/Documentation/ScriptReference/KeyCode.html");
            headReposLeftKey = config.Bind("Controls",
                                           "HeadRepositionLeftKey",
                                           "LeftArrow",
                                           "Key used to move head camera left. Must be matching key from https://docs.unity3d.com/2019.4/Documentation/ScriptReference/KeyCode.html");
            headReposRightKey = config.Bind("Controls",
                                           "HeadRepositionRightKey",
                                           "RightArrow",
                                           "Key used to move head camera right. Must be matching key from https://docs.unity3d.com/2019.4/Documentation/ScriptReference/KeyCode.html");
            headReposUpKey = config.Bind("Controls",
                                           "HeadRepositionUpKey",
                                           "PageUp",
                                           "Key used to move head camera up. Must be matching key from https://docs.unity3d.com/2019.4/Documentation/ScriptReference/KeyCode.html");
            headReposDownKey = config.Bind("Controls",
                                           "HeadRepositionDownKey",
                                           "PageDown",
                                           "Key used to move head camera down. Must be matching key from https://docs.unity3d.com/2019.4/Documentation/ScriptReference/KeyCode.html");
            hmdRecenterKey = config.Bind("Controls",
                                           "HMDRecenterKey",
                                           "Home",
                                           "Used to recenter HMD tracking. Must be matching key from https://docs.unity3d.com/2019.4/Documentation/ScriptReference/KeyCode.html");
        }

        private static void InitializeGraphicsSettings(ConfigFile config)
        {
            useAmplifyOcclusion = config.Bind("Graphics",
                                              "UseAmplifyOcclusion",
                                              true,
                                              "RECOMMENDED - Determines whether or not to use the Amplify Occlusion post processing effect." +
                                              " This implements an effect similar to SSAO but with much less performance" +
                                              " cost. While you can enable SSAO and UseAmplifyOcclusion simultaneously, it is" +
                                              " not recommended. SSAO impacts performance significantly, which is bad for VR especially. Therefore" +
                                              " you should disable SSAO in the graphics settings of the game when using this.");
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
            // Force this to be off if UseVrControls is on
            return useOverlayGui.Value && !UseVrControls();
        }

        public static float GetOverlayWidth()
        {
            return overlayWidth.Value;
        }

        public static float GetOverlayDistance()
        {
            return overlayDistance.Value;
        }

        public static float GetOverlayVerticalOffset()
        {
            return overlayVerticalPosition.Value;
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

        public static float GetUiPanelSize()
        {
            return uiPanelSize.Value;
        }

        public static float GetUiPanelVerticalOffset()
        {
            return uiPanelVerticalOffset.Value;
        }

        public static float GetUiPanelDistance()
        {
            return uiPanelDistance.Value;
        }

        public static bool UseAmplifyOcclusion()
        {
            return useAmplifyOcclusion.Value;
        }

        public static Vector3 GetFirstPersonHeadOffset()
        {
            return new Vector3(headOffsetX.Value, headOffsetY.Value, headOffsetZ.Value);
        }

        public static Vector3 GetThirdPersonHeadOffset()
        {
            return new Vector3(headOffsetThirdPersonX.Value, headOffsetThirdPersonY.Value, headOffsetThirdPersonZ.Value);
        }

        public static bool AllowHeadRepositioning()
        {
            return enableHeadReposition.Value;
        }

        public static void UpdateFirstPersonHeadOffset(Vector3 offset) {
            headOffsetX.Value = Mathf.Clamp(offset.x, -2f, 2f);
            headOffsetY.Value = Mathf.Clamp(offset.y, -2f, 2f);
            headOffsetZ.Value = Mathf.Clamp(offset.z, -2f, 2f);
        }

        public static void UpdateThirdPersonHeadOffset(Vector3 offset)
        {
            headOffsetThirdPersonX.Value = Mathf.Clamp(offset.x, -2f, 2f);
            headOffsetThirdPersonY.Value = Mathf.Clamp(offset.y, -2f, 2f);
            headOffsetThirdPersonZ.Value = Mathf.Clamp(offset.z, -2f, 2f);
        }

        public static bool HandsEnabled()
        {
            // Force hands to be on if UseVrControls is on
            return enableHands.Value || UseVrControls();
        }

        public static bool UseLookLocomotion()
        {
            return useLookLocomotion.Value;
        }

        public static bool ShowStaticCrosshair()
        {
            return showStaticCrosshair.Value;
        }

        public static float CrosshairScalar()
        {
            return crosshairScale.Value;
        }

        public static bool RecenterOnStart()
        {
            return recenterOnStart.Value;
        }

        public static KeyCode GetRecenterKey()
        {
            return tryAndParseConfiguredKeycode(hmdRecenterKey.Value, KeyCode.Home);
        }

        public static KeyCode GetHeadForwardKey()
        {
            return tryAndParseConfiguredKeycode(headReposFowardKey.Value, KeyCode.UpArrow);
        }

        public static KeyCode GetHeadBackwardKey()
        {
            return tryAndParseConfiguredKeycode(headReposBackwardKey.Value, KeyCode.DownArrow);
        }

        public static KeyCode GetHeadLeftKey()
        {
            return tryAndParseConfiguredKeycode(headReposLeftKey.Value, KeyCode.LeftArrow);
        }

        public static KeyCode GetHeadRightKey()
        {
            return tryAndParseConfiguredKeycode(headReposRightKey.Value, KeyCode.RightArrow);
        }

        public static KeyCode GetHeadUpKey()
        {
            return tryAndParseConfiguredKeycode(headReposUpKey.Value, KeyCode.PageUp);
        }

        public static KeyCode GetHeadDownKey()
        {
            return tryAndParseConfiguredKeycode(headReposDownKey.Value, KeyCode.PageDown);
        }

        private static KeyCode tryAndParseConfiguredKeycode(string configuredKey, KeyCode defaultValue)
        {
            try
            {
                return (KeyCode)Enum.Parse(typeof(KeyCode), configuredKey, true);
            }
            catch (ArgumentNullException)
            {
                LogError("Invalid configured key: " + configuredKey + " Using Default Key: " + defaultValue);
            }
            catch (ArgumentException)
            {
                LogError("Invalid configured key: " + configuredKey + " Using Default Key: " + defaultValue);
            }
            catch (OverflowException)
            {
                LogError("Invalid configured key: " + configuredKey + " Using Default Key: " + defaultValue);
            }
            return defaultValue;
        }

        public static bool ShowRepairHammer()
        {
            return showRepairHammer.Value;
        }

        public static bool ShowEnemyHuds()
        {
            return showEnemyHuds.Value;
        }

        public static float EnemyHudScale()
        {
            return enemyHudScale.Value;
        }

        public static float MobileGuiRecenterAngle()
        {
            return mobileGuiRecenterAngle.Value;
        }

        public static float StationaryGuiRecenterAngle()
        {
            return stationaryGuiRecenterAngle.Value;
        }

        public static bool RecenterGuiOnMove()
        {
            return recenterGuiOnMove.Value;
        }

        public static bool UseVrControls()
        {
            return useVrControls.Value;
        }

    }
}
