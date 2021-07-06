using BepInEx.Configuration;
using Unity.XR.OpenVR;
using System;
using ValheimVRMod.VRCore;
using UnityEngine;

using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.Utilities
{

    static class VHVRConfig
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
        private static ConfigEntry<bool> roomscaleFadeToBlack;

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
        private static ConfigEntry<bool> useArrowPredictionGraphic;
        private static ConfigEntry<float> DebugPosX;
        private static ConfigEntry<float> DebugPosY;
        private static ConfigEntry<float> DebugPosZ;
        private static ConfigEntry<float> DebugRotX;
        private static ConfigEntry<float> DebugRotY;
        private static ConfigEntry<float> DebugRotZ;
        private static ConfigEntry<float> DebugScale;
        private static ConfigEntry<bool> unlockDesktopCursor;
        private static ConfigEntry<bool> QuickMenuFollowCam;
        private static ConfigEntry<int> QuickMenuAngle;

        // Controls Settings
        private static ConfigEntry<bool> nonVrPlayer;
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
        private static ConfigEntry<bool> snapTurnEnabled;
        private static ConfigEntry<float> snapTurnAngle;
        private static ConfigEntry<bool> smoothSnapTurn;
        private static ConfigEntry<float> smoothSnapSpeed;
        private static ConfigEntry<bool> roomScaleSneaking;
        private static ConfigEntry<float> roomScaleSneakHeight;
        private static ConfigEntry<bool> weaponNeedsSpeed;
        private static ConfigEntry<float> altPieceRotationDelay;
        private static ConfigEntry<bool> runIsToggled;

        // Graphics Settings
        private static ConfigEntry<bool> useAmplifyOcclusion;
        private static ConfigEntry<float> taaSharpenAmmount;
        private static ConfigEntry<float> nearClipPlane;

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
            roomscaleFadeToBlack = config.Bind("General",
                                          "RoomscaleFadeToBlack",
                                          false,
                                          "Set this to true if you want the game to fade to black when roomscale movement causes the player to being pushed back.");
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
            useArrowPredictionGraphic = config.Bind("UI",
                                                     "UseArrowPredictionGraphic",
                                                     true,
                                                     "Use this to toggle the path predictor when using the bow and arrow with VR controls.");
            DebugPosX = config.Bind("UI",
                "DebugPosX",
                0.0f,
                "DebugPosX");
            DebugPosY = config.Bind("UI",
                "DebugPosY",
                0.0f,
                "DebugPosY");
            DebugPosZ = config.Bind("UI",
                "DebugPosZ",
                0.0f,
                "DebugPosZ");
            DebugRotX = config.Bind("UI",
                "DebugRotX",
                0.0f,
                "DebugRotX");
            DebugRotY = config.Bind("UI",
                "DebugRotY",
                0.0f,
                "DebugRotY");
            DebugRotZ = config.Bind("UI",
                "DebugRotZ",
                0.0f,
                "DebugRotZ");
            DebugScale = config.Bind("UI",
                "DebugScale",
                1.0f,
                "DebugScale");
            unlockDesktopCursor = config.Bind("UI",
                "UnlockDesktopCursor",
                false,
                "Normally the desktop cursor is locked to the center of the screen to avoid having the player accidentally lose focus when playing. This option can be used to free the mouse " +
                "cursor which some users may want, especially if exclusively using motion controls where window focus is not needed.");
            QuickMenuFollowCam = config.Bind("UI",
                "QuickMenuFollowCam",
                true,
                "If Set to true, the quickmenu will rotate towards your head (ignoring Quickmenu angle option),if set to false, it will rotate towards your hands at start");
            QuickMenuAngle = config.Bind("UI",
                "QuickMenuAngle",
                60,
                "Set the quickmenu vertical angle ");
        }

        private static void InitializeControlsSettings(ConfigFile config)
        {
            nonVrPlayer = config.Bind("Controls",
                "nonVrPlayer",
                false,
                "Disables VR completely. This is for Non-Vr Players that want to see their Multiplayer companions in VR Bodys");
            useLookLocomotion = config.Bind("Controls",
                                            "UseLookLocomotion",
                                            true,
                                            "Setting this to true ties the direction you are looking to the walk direction while in first person mode. " +
                                            "Set this to false if you prefer to disconnect these so you can look" +
                                            " look by turning your head without affecting movement direction.");
            useVrControls = config.Bind("Controls",
                                        "UseVRControls",
                                        true,
                                        "This setting enables the use of the VR motion controllers as input (Only Oculus Touch and Valve Index supported)." +
                                        "This setting, if true, will also force UseOverlayGui to be false as this setting Overlay GUI is not compatible with VR laser pointer inputs.");
            snapTurnEnabled = config.Bind("Controls",
                                          "SnapTurnEnabled",
                                          false,
                                          "Enable Snap Turning");
            snapTurnAngle = config.Bind("Controls",
                                        "SnapTurnAngle",
                                        45f,
                                        "Angle to snap to when snap turning is used.");
            smoothSnapTurn = config.Bind("Controls",
                                         "SmoothSnapTurn",
                                         true,
                                         "While snap turn is enabled, this will cause the snap to be a very quick turn rather than an immediate change to the new snapped angle.");
            smoothSnapSpeed = config.Bind("Controls",
                                          "SmoothSnapSpeed",
                                          10f,
                                          "This will affect the speed that the smooth snap turns occur at.");
            roomScaleSneaking = config.Bind("Controls",
                                          "RoomScaleSneaking",
                                          false,
                                          "Enable RoomScale Sneaking.");
            roomScaleSneakHeight = config.Bind("Controls",
                                          "RoomScaleSneakHeight",
                                          0.7f,
                                          new ConfigDescription("This will affect the eye height that the roomscale sneak occur at.  (e.g. 0.7 means if your headset lower than 70% of your height, it will do sneak)  " +
                                           "Valid values are  0.0 - 0.95.",
                                           new AcceptableValueRange<float>(0f, 0.95f)));
            preferredHand = config.Bind("Controls",
                                        "PreferredHand",
                                        "Right",
                                        new ConfigDescription("Which hand do you want to use for the main laser pointer input? If" +
                                        " only one hand is active, it will be used automatically regardless of this setting.",
                                        new AcceptableValueList<string>(new string[] { "Right", "Left" })));
            weaponNeedsSpeed = config.Bind("Controls",
                "SwingWeapons",
                true,
                "Defines if Swinging a Weapon needs certain speed. if set to false, single touch will already trigger hit");
            altPieceRotationDelay = config.Bind("Controls",
                                                "AltPieceRotationDelay",
                                                1f,
                                                new ConfigDescription("Affects speed of piece rotation when using 'Grab' + 'Joystick' method of rotating build objects. Legal values 0.1 - 3. Higher is longer delay.",
                                                new AcceptableValueRange<float>(0.1f, 3f)));
            runIsToggled = config.Bind("Controls",
                                       "RunIsToggled",
                                       true,
                                       "Determine whether or not you need to hold run or it is a toggle. Keep it as toggle (true) to have your thumb free when sprinting.");
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
            taaSharpenAmmount = config.Bind("Graphics",
                                              "TAASharpenAmmount",
                                              -1.0f,
                                              "Ammount of Sharpen applied after the TAA filter, values should be in the range [0.0,3.0]." +
                                              " Values outside this range will be ignored and the default game settings will be used instead.");
            nearClipPlane = config.Bind("Graphics",
                                        "NearClipPlane",
                                        .05f,
                                        "This can be used to adjust the distance where where anything inside will be clipped out and not rendered. You can try adjusting this if you experience" +
                                        " problems where you see the nose of the player character for example.");
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

        public static float GetTaaSharpenAmmount()
        {
            return taaSharpenAmmount.Value;
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

        public static bool RoomscaleFadeToBlack()
        {
            return roomscaleFadeToBlack.Value;
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
            return useVrControls.Value && ! nonVrPlayer.Value;
        }

        public static bool UseArrowPredictionGraphic()
        {
            return useArrowPredictionGraphic.Value;
        }

        public static bool NonVrPlayer()
        {
            return nonVrPlayer.Value;
        }

        public static Vector3 getDebugPos()
        {
            return new Vector3(DebugPosX.Value, DebugPosY.Value, DebugPosZ.Value);
        }
        
        public static Vector3 getDebugRot()
        {
            return new Vector3(DebugRotX.Value, DebugRotY.Value, DebugRotZ.Value);
        }
        
        public static float getDebugScale() {
            return DebugScale.Value;
        }

        public static bool UnlockDesktopCursor()
        {
            return unlockDesktopCursor.Value;
        }

        public static bool getQuickMenuFollowCam() {
            return QuickMenuFollowCam.Value;
        }
        public static int getQuickMenuAngle()
        {
            return QuickMenuAngle.Value;
        }

        public static float GetSnapTurnAngle()
        {
            return Mathf.Abs(snapTurnAngle.Value);
        }

        public static bool SnapTurnEnabled()
        {
            return snapTurnEnabled.Value;
        }

        public static bool SmoothSnapTurn()
        {
            return smoothSnapTurn.Value;
        }

        public static float SmoothSnapSpeed()
        {
            return Mathf.Abs(smoothSnapSpeed.Value);
        }
        
        public static bool WeaponNeedsSpeed()
        {
            return weaponNeedsSpeed.Value;
        }

        public static bool RoomScaleSneakEnabled() {
            return roomScaleSneaking.Value;
        }

        public static float RoomScaleSneakHeight() {
            return roomScaleSneakHeight.Value;
        }

        public static float GetNearClipPlane()
        {
            return nearClipPlane.Value;
        }

        public static float AltPieceRotationDelay()
        {
            return altPieceRotationDelay.Value;
        }

        public static bool ToggleRun()
        {
            return runIsToggled.Value;
        }
    }
}
