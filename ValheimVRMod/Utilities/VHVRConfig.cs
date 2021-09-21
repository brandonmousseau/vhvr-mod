using BepInEx.Configuration;
using Unity.XR.OpenVR;
using ValheimVRMod.VRCore;
using UnityEngine;

namespace ValheimVRMod.Utilities
{
    
    static class VHVRConfig {

        public static ConfigFile config;
        
        // Immutable Settings
        private static ConfigEntry<bool> vrModEnabled;
        private static ConfigEntry<bool> nonVrPlayer;
        private static ConfigEntry<bool> useVrControls;
        private static ConfigEntry<bool> useOverlayGui;
        
        // General Settings
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
        private static ConfigEntry<int> guiRecenterSpeed;
        private static ConfigEntry<bool> unlockDesktopCursor;
        private static ConfigEntry<bool> QuickMenuFollowCam;
        private static ConfigEntry<int> QuickMenuAngle;

        // VR Hud Settings
        private static ConfigEntry<bool> useLegacyHud;
        private static ConfigEntry<float> cameraHudX;
        private static ConfigEntry<float> cameraHudY;
        private static ConfigEntry<float> cameraHudScale;
        private static ConfigEntry<Vector3> leftWristPos;
        private static ConfigEntry<Quaternion> leftWristRot;
        private static ConfigEntry<Vector3> rightWristPos;
        private static ConfigEntry<Quaternion> rightWristRot;
        private static ConfigEntry<string> healthPanelPlacement;
        private static ConfigEntry<string> staminaPanelPlacement;
        private static ConfigEntry<string> minimapPanelPlacement;
        private static ConfigEntry<bool> allowHudFade;
        private static ConfigEntry<bool> hideHotbar;

        // Controls Settings
        private static ConfigEntry<bool> useLookLocomotion;
        private static ConfigEntry<string> preferredHand;
        private static ConfigEntry<KeyCode> headReposFowardKey;
        private static ConfigEntry<KeyCode> headReposBackwardKey;
        private static ConfigEntry<KeyCode> headReposLeftKey;
        private static ConfigEntry<KeyCode> headReposRightKey;
        private static ConfigEntry<KeyCode> headReposUpKey;
        private static ConfigEntry<KeyCode> headReposDownKey;
        private static ConfigEntry<KeyCode> hmdRecenterKey;
        private static ConfigEntry<bool> snapTurnEnabled;
        private static ConfigEntry<int> snapTurnAngle;
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
        
        // Motion Control Settings
        private static ConfigEntry<bool> useArrowPredictionGraphic;
        private static ConfigEntry<float> arrowParticleSize;
        private static ConfigEntry<string> spearThrowingType;
        private static ConfigEntry<bool> useSpearDirectionGraphic;
        private static ConfigEntry<bool> spearThrowSpeedDynamic;
        private static ConfigEntry<bool> spearTwoHanded;

#if DEBUG
        private static ConfigEntry<float> DebugPosX;
        private static ConfigEntry<float> DebugPosY;
        private static ConfigEntry<float> DebugPosZ;
        private static ConfigEntry<float> DebugRotX;
        private static ConfigEntry<float> DebugRotY;
        private static ConfigEntry<float> DebugRotZ;
        private static ConfigEntry<float> DebugScale;
#endif

        // Common values
        private static readonly string[] k_HudAlignmentValues = { "LeftWrist", "RightWrist", "CameraLocked", "Legacy" };

        public static void InitializeConfiguration(ConfigFile mConfig) {
            
            config = mConfig;
            InitializeImmutableSettings();
            InitializeGeneralSettings();
            InitializeUISettings();
            InitializeVrHudSettings();
            InitializeControlsSettings();
            InitializeGraphicsSettings();
            InitializeMotionControlSettings();
        }

        private static void InitializeImmutableSettings() 
        {
            vrModEnabled = config.Bind("Immutable",
                "ModEnabled",
                true,
                "Used to toggle the mod on and off.");
            nonVrPlayer = config.Bind("Immutable",
                "nonVrPlayer",
                false,
                "Disables VR completely. This is for Non-Vr Players that want to see their Multiplayer companions in VR Bodys");
            useVrControls = config.Bind("Immutable",
                "UseVRControls",
                true,
                "This setting enables the use of the VR motion controllers as input (Only Oculus Touch and Valve Index supported)." +
                "This setting, if true, will also force UseOverlayGui to be false as this setting Overlay GUI is not compatible with VR laser pointer inputs.");
            useOverlayGui = config.Bind("Immutable",
                "UseOverlayGui",
                true,
                "Whether or not to use OpenVR overlay for the GUI. This produces a" +
                " cleaner GUI but will only be compatible with M&K or Gamepad controls.");
        }
        
        private static void InitializeGeneralSettings()
        {
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

        private static void InitializeUISettings()
        {
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
            guiRecenterSpeed = config.Bind("UI",
                                            "GuiRecenterSpeed",
                                            180,
                                            new ConfigDescription("Speed in degrees per second of the Gui recentering algorithm",
                                                new AcceptableValueRange<int>(0, 360)));
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
                new ConfigDescription("Set the quickmenu vertical angle ",
                    new AcceptableValueRange<int>(0, 360)));
        }

        private static void InitializeVrHudSettings()
        {
            useLegacyHud = config.Bind("VRHUD",
                                            "UseLegacyHud",
                                            false,
                                            "Disables custom VR HUD features and moves HUD elements to main UI panel.");
            cameraHudX = config.Bind("VRHUD",
                                            "CameraHudX",
                                            0f,
                                            new ConfigDescription("Offset to reposition VR health panel for Camera Position.",
                                                new AcceptableValueRange<float>(-0.001f, 0.001f)));
            cameraHudY = config.Bind("VRHUD",
                                            "CameraHudY",
                                            0f,
                                            new ConfigDescription("Offset to reposition VR health panel for Camera Position.",
                                                new AcceptableValueRange<float>(-0.001f, 0.001f)));
            cameraHudScale = config.Bind("VRHUD",
                                            "CameraHudScale",
                                            1f,
                                            new ConfigDescription("Scalar multiple to determine VR Camera Hud scale.",
                                                new AcceptableValueRange<float>(.25f, 3f)));
            leftWristPos = config.Bind("VRHUD",
                                            "LeftWrist",
                                            Vector3.zero,
                                            "Position of VR Hud on Left Wrist.");
            leftWristRot = config.Bind("VRHUD",
                                            "LeftWristRot",
                                            Quaternion.identity,
                                            "Rotation for reposition VR Hud on Left Wrist.");
            rightWristPos = config.Bind("VRHUD",
                                            "RightWrist",
                                            Vector3.zero,
                                            "Position of VR Hud on Right Wrist.");
            rightWristRot = config.Bind("VRHUD",
                                            "RightWristRot",
                                            Quaternion.identity,
                                            "Rotation for reposition VR Hud on Right Wrist");
            healthPanelPlacement = config.Bind("VRHUD",
                                              "HealthPanelPlacement",
                                              "LeftWrist",
                                              new ConfigDescription("Where should the health panel be placed?",
                                              new AcceptableValueList<string>(k_HudAlignmentValues)));
            staminaPanelPlacement = config.Bind("VRHUD",
                                            "StaminaPanelPlacement",
                                            "CameraLocked",
                                            new ConfigDescription("Where should the stamina panel be placed?",
                                                new AcceptableValueList<string>(k_HudAlignmentValues)));
            minimapPanelPlacement = config.Bind("VRHUD",
                                            "MinimapPanelPlacement",
                                            "RightWrist",
                                            new ConfigDescription("Where should the minimap panel be placed?",
                                                new AcceptableValueList<string>(k_HudAlignmentValues)));
            allowHudFade = config.Bind("VRHUD",
                                        "AllowHudFade",
                                        true,
                                        "When the HUD is attached to a wrist, allow it to fade away unless you are actively looking at it.");
            hideHotbar = config.Bind("VRHUD",
                                        "HideHotbar",
                                        true,
                                        "Hide the hotbar, as it is generally unused when playing in VR. Ignored if not playing with VR controls.");
        }

        private static void InitializeControlsSettings()
        {
            useLookLocomotion = config.Bind("Controls",
                                            "UseLookLocomotion",
                                            true,
                                            "Setting this to true ties the direction you are looking to the walk direction while in first person mode. " +
                                            "Set this to false if you prefer to disconnect these so you can look" +
                                            " look by turning your head without affecting movement direction.");
            snapTurnEnabled = config.Bind("Controls",
                                          "SnapTurnEnabled",
                                          false,
                                          "Enable Snap Turning");
            snapTurnAngle = config.Bind("Controls",
                                        "SnapTurnAngle",
                                        45,
                                        new ConfigDescription("Angle to snap to when snap turning is used.",
                                            new AcceptableValueRange<int>(0, 90)));
            smoothSnapTurn = config.Bind("Controls",
                                         "SmoothSnapTurn",
                                         true,
                                         "While snap turn is enabled, this will cause the snap to be a very quick turn rather than an immediate change to the new snapped angle.");
            smoothSnapSpeed = config.Bind("Controls",
                                          "SmoothSnapSpeed",
                                          10f,
                                          new ConfigDescription("This will affect the speed that the smooth snap turns occur at.",
                                              new AcceptableValueRange<float>(5, 30)));
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
                                             KeyCode.UpArrow,
                                             "Key used to move head camera forwards. Must be matching key from https://docs.unity3d.com/2019.4/Documentation/ScriptReference/KeyCode.html");
            headReposBackwardKey = config.Bind("Controls",
                                               "HeadRepositionBackwardKey",
                                               KeyCode.DownArrow,
                                               "Key used to move head camera backwards. Must be matching key from https://docs.unity3d.com/2019.4/Documentation/ScriptReference/KeyCode.html");
            headReposLeftKey = config.Bind("Controls",
                                           "HeadRepositionLeftKey",
                                           KeyCode.LeftArrow,
                                           "Key used to move head camera left. Must be matching key from https://docs.unity3d.com/2019.4/Documentation/ScriptReference/KeyCode.html");
            headReposRightKey = config.Bind("Controls",
                                           "HeadRepositionRightKey",
                                           KeyCode.RightArrow,
                                           "Key used to move head camera right. Must be matching key from https://docs.unity3d.com/2019.4/Documentation/ScriptReference/KeyCode.html");
            headReposUpKey = config.Bind("Controls",
                                           "HeadRepositionUpKey",
                                           KeyCode.PageUp,
                                           "Key used to move head camera up. Must be matching key from https://docs.unity3d.com/2019.4/Documentation/ScriptReference/KeyCode.html");
            headReposDownKey = config.Bind("Controls",
                                           "HeadRepositionDownKey",
                                           KeyCode.PageDown,
                                           "Key used to move head camera down. Must be matching key from https://docs.unity3d.com/2019.4/Documentation/ScriptReference/KeyCode.html");
            hmdRecenterKey = config.Bind("Controls",
                                           "HMDRecenterKey",
                                           KeyCode.Home,
                                           "Key used to recenter HMD tracking. Must be matching key from https://docs.unity3d.com/2019.4/Documentation/ScriptReference/KeyCode.html");
        }

        private static void InitializeGraphicsSettings()
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
                                              0.3f,
                                              new ConfigDescription("Ammount of Sharpen applied after the TAA filter, values should be in the range [0.0,3.0]." +
                                                                    " Values outside this range will be ignored and the default game settings will be used instead.",
                                                  new AcceptableValueRange<float>(0, 3)));
            nearClipPlane = config.Bind("Graphics",
                                        "NearClipPlane",
                                        .05f,
                                        new ConfigDescription("This can be used to adjust the distance where where anything inside will be clipped out and not rendered. You can try adjusting this if you experience" +
                                                              " problems where you see the nose of the player character for example.",
                                        new AcceptableValueRange<float>(0, 0.5f)));
        }

        private static void InitializeMotionControlSettings() {

            useArrowPredictionGraphic = config.Bind("Motion Control",
                "UseArrowPredictionGraphic",
                true,
                "Use this to toggle the path predictor when using the bow and arrow with VR controls.");
            arrowParticleSize = config.Bind("Motion Control",
                "ArrowParticleSize",
                0.5f,
                new ConfigDescription("set size of the particles on drawing arrows (fire,poison, etc.)",
                    new AcceptableValueRange<float>(0, 1)));
            spearThrowingType = config.Bind("Motion Control",
                                            "SpearThrowingMode",
                                            "DartType",
                                            new ConfigDescription("Change the throwing mode." +
                                            "DartType - Throw by holding grab and trigger and then releasing trigger, Throw aim is based on first trigger pressed to release in a straight line, and throwing power is based on how fast you swing. " +
                                            "TwoStagedThrowing - Throw aim is based on first grab and then aim is locked after pressing trigger, throw by releasing trigger while swinging, throw speed based on how fast you swing. " +
                                            "SecondHandAiming - Throw by holding grab and trigger and then releasing trigger, Throw aim is based from your head to your left hand in a straight line, throw by releasing trigger while swinging, throw speed based on how fast you swing.",
                                            new AcceptableValueList<string>(new string[] { "DartType", "TwoStagedThrowing", "SecondHandAiming" })));
            spearThrowSpeedDynamic = config.Bind("Motion Control",
                                                "SpearThrowSpeedDynamic",
                                                true,
                                                "Determine whether or not your throw power depends on swing speed, setting to false make the throw always on fixed speed.");
            useSpearDirectionGraphic = config.Bind("Motion Control",
                                                    "UseSpearDirectionGraphic",
                                                    true,
                                                    "Use this to toggle the direction line of throwing when using the spear with VR controls.");
            spearTwoHanded = config.Bind("Motion Control",
                                                    "TwoHandedSpear",
                                                    false,
                                                    "Use this to toggle controls of two handed spear (left hand grab while having spear) (experimental)");
// #if DEBUG
//             DebugPosX = config.Bind("Motion Control",
//                 "DebugPosX",
//                 0.0f,
//                 "DebugPosX");
//             DebugPosY = config.Bind("Motion Control",
//                 "DebugPosY",
//                 0.0f,
//                 "DebugPosY");
//             DebugPosZ = config.Bind("Motion Control",
//                 "DebugPosZ",
//                 0.0f,
//                 "DebugPosZ");
//             DebugRotX = config.Bind("Motion Control",
//                 "DebugRotX",
//                 0.0f,
//                 "DebugRotX");
//             DebugRotY = config.Bind("Motion Control",
//                 "DebugRotY",
//                 0.0f,
//                 "DebugRotY");
//             DebugRotZ = config.Bind("Motion Control",
//                 "DebugRotZ",
//                 0.0f,
//                 "DebugRotZ");
//             DebugScale = config.Bind("Motion Control",
//                 "DebugScale",
//                 1.0f,
//                 "DebugScale");
// #endif
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
            return hmdRecenterKey.Value;
        }

        public static KeyCode GetHeadForwardKey()
        {
            return headReposFowardKey.Value;
        }

        public static KeyCode GetHeadBackwardKey()
        {
            return headReposBackwardKey.Value;
        }

        public static KeyCode GetHeadLeftKey()
        {
            return headReposLeftKey.Value;
        }

        public static KeyCode GetHeadRightKey()
        {
            return headReposRightKey.Value;
        }

        public static KeyCode GetHeadUpKey()
        {
            return headReposUpKey.Value;
        }

        public static KeyCode GetHeadDownKey()
        {
            return headReposDownKey.Value;
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

        public static float GuiRecenterSpeed()
        {
            return guiRecenterSpeed.Value;
        }

        public static bool UseVrControls()
        {
            return useVrControls.Value && !NonVrPlayer();
        }

        public static bool UseArrowPredictionGraphic()
        {
            return useArrowPredictionGraphic.Value;
        }

        public static bool NonVrPlayer()
        {
#if NONVRMODE
            return true;
#else
            return nonVrPlayer.Value;
#endif
        }
        
#if DEBUG
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
#endif
        
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
            return snapTurnAngle.Value;
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
        
        public static float ArrowParticleSize()
        {
            return arrowParticleSize.Value;
        }
        public static bool SpearThrowSpeedDynamic()
        {
            return spearThrowSpeedDynamic.Value;
        }

        public static string SpearThrowType()
        {
            return spearThrowingType.Value;
        }
        public static bool SpearTwoHanded()
        {
            return spearTwoHanded.Value;
        }
        public static bool UseSpearDirectionGraphic()
        {
            return useSpearDirectionGraphic.Value;
        }

        public static bool UseLegacyHud()
        {
            return useLegacyHud.Value;
        }
        
        public static float CameraHudX()
        {
            return cameraHudX.Value;
        }
        
        public static float CameraHudY()
        {
            return cameraHudY.Value;
        }
        
        public static float CameraHudScale()
        {
            return cameraHudScale.Value;
        }

        public static Vector3 LeftWristPos()
        {
            return leftWristPos.Value;
        }

        public static Quaternion LeftWristRot()
        {
            return leftWristRot.Value;
        }

        public static Vector3 RightWristPos()
        {
            return rightWristPos.Value;
        }

        public static Quaternion RightWristRot()
        {
            return rightWristRot.Value;
        }

        public static string HealthPanelPlacement()
        {
            return healthPanelPlacement.Value;
        }

        public static string StaminaPanelPlacement()
        {
            return staminaPanelPlacement.Value;
        }

        public static string MinimapPanelPlacement()
        {
            return minimapPanelPlacement.Value;
        }

        public static bool AllowHudFade()
        {
            return allowHudFade.Value;
        }

        public static bool HideHotbar()
        {
            return hideHotbar.Value && UseVrControls();
        }
    }
}
